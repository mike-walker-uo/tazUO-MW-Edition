using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal enum GlobalChatChannel
    {
        All,
        Global,
        Trade,
        Events,
        Help,
        LFG,
        Guild,
        Party,
        Pariah
    }

    internal static class GlobalChatChannels
    {
        // Keep numeric IDs stable for saved gumps; All is a legacy alias for Global.
        internal static readonly string[] Names = { "All", "Global", "Trade", "Events", "Help", "LFG", "Guild", "Party", "Pariah" };
        internal static readonly GlobalChatChannel[] Selectable =
        {
            GlobalChatChannel.Global, GlobalChatChannel.Trade, GlobalChatChannel.Events,
            GlobalChatChannel.Help, GlobalChatChannel.LFG, GlobalChatChannel.Pariah,
            GlobalChatChannel.Guild, GlobalChatChannel.Party
        };
        internal static readonly string[] SelectionNames = { "Global", "Trade", "Events", "Help", "LFG", "Pariah", "Guild", "Party" };

        internal static Color TagColor(GlobalChatChannel channel, bool lightMode = false)
        {
            switch (channel)
            {
                case GlobalChatChannel.Global: return lightMode ? new Color(30, 96, 115) : new Color(133, 200, 214);
                case GlobalChatChannel.Trade: return lightMode ? new Color(88, 103, 46) : new Color(194, 209, 141);
                case GlobalChatChannel.Events: return lightMode ? new Color(14, 110, 93) : new Color(83, 210, 184);
                case GlobalChatChannel.Help: return lightMode ? new Color(105, 63, 148) : new Color(199, 177, 238);
                case GlobalChatChannel.LFG: return lightMode ? new Color(73, 97, 126) : new Color(143, 177, 237);
                case GlobalChatChannel.Guild: return lightMode ? new Color(49, 108, 55) : new Color(111, 194, 139);
                case GlobalChatChannel.Party: return lightMode ? new Color(41, 100, 157) : new Color(111, 215, 239);
                case GlobalChatChannel.Pariah: return lightMode ? new Color(151, 40, 55) : new Color(244, 100, 128);
                default: throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        internal static string ColorKey(GlobalChatChannel channel, bool lightMode) =>
            (lightMode ? "Light:" : "Dark:") + Names[(int)channel];

        internal static Color DisplayColor(GlobalChatChannel channel, Profile profile, bool? lightMode = null)
        {
            bool light = lightMode ?? (profile?.GlobalChatLightMode == true);
            if (profile?.GlobalChatChannelColors != null && profile.GlobalChatChannelColors.TryGetValue(ColorKey(channel, light), out uint packed))
                return new Color { PackedValue = packed, A = 255 };
            return TagColor(channel, light);
        }

        internal static int SelectionIndex(int channelId) =>
            Math.Max(0, Array.IndexOf(Selectable, (GlobalChatChannel)channelId));

        internal static int RestoreSendIndex(int saved, bool legacy) => SelectionIndex(legacy ? saved + 1 : saved);

        internal static GlobalChatChannel? GetDisplayParts(ChatHistoryRecord record, out string name, out string text)
        {
            name = record.Name;
            text = record.Text ?? string.Empty;
            GlobalChatChannel? channel = GetChannel(name, text, record.MsgType);
            if (!channel.HasValue) return null;

            string channelName = Names[(int)channel.Value];
            if (TaggedChannel(name, out int namePrefix) == channel)
                name = name.Substring(namePrefix).Trim();
            else if (TaggedChannel(text, out int textPrefix) == channel)
            {
                text = text.Substring(textPrefix).TrimStart();
                if (string.IsNullOrEmpty(name) || name.Equals("System", StringComparison.OrdinalIgnoreCase)
                    || name.Equals(channelName, StringComparison.OrdinalIgnoreCase))
                {
                    name = null;
                    int colon = text.IndexOf(':');
                    if (colon > 0)
                    {
                        name = text.Substring(0, colon).Trim();
                        text = text.Substring(colon + 1).TrimStart();
                    }
                }
            }
            return channel;
        }

        internal static GlobalChatChannel? GetChannel(string name, string text, MessageType type)
        {
            if (type == MessageType.Guild) return GlobalChatChannel.Guild;
            if (type == MessageType.Party) return GlobalChatChannel.Party;
            return TaggedChannel(name, out _) ?? TaggedChannel(text, out _)
                ?? (type == MessageType.ChatSystem ? GlobalChatChannel.Global : (GlobalChatChannel?)null);
        }

        private static GlobalChatChannel? TaggedChannel(string value, out int prefixLength)
        {
            prefixLength = 0;
            if (string.IsNullOrEmpty(value)) return null;
            int start = 0;
            while (start < value.Length && char.IsWhiteSpace(value[start])) start++;
            if (start == value.Length || value[start] != '[') return null;
            int end = value.IndexOf(']', start + 1);
            if (end < 0) return null;
            string tag = value.Substring(start + 1, end - start - 1).Trim();
            for (int i = 1; i < Names.Length; i++)
            {
                if (tag.Equals(Names[i], StringComparison.OrdinalIgnoreCase))
                {
                    prefixLength = end + 1;
                    return (GlobalChatChannel)i;
                }
            }
            return null;
        }

        internal static bool IsChatMessage(MessageEventArgs message) =>
            message.Type == MessageType.ChatSystem || message.Type == MessageType.Guild || message.Type == MessageType.Party
            || ((message.Type == MessageType.System || message.Type == MessageType.Regular) && message.Parent == null
                && GetChannel(message.Name, message.Text, message.Type).HasValue);

        internal static bool Matches(ChatHistoryRecord record, GlobalChatChannel selected,
            bool includeGuild = false, bool includeParty = false)
        {
            GlobalChatChannel? channel = GetChannel(record.Name, record.Text, record.MsgType);
            return selected == GlobalChatChannel.All || selected == GlobalChatChannel.Global
                ? channel.HasValue && (channel != GlobalChatChannel.Guild || includeGuild)
                    && (channel != GlobalChatChannel.Party || includeParty)
                : channel == selected;
        }

        internal static object RepeatKey(ChatHistoryRecord record)
        {
            GlobalChatChannel? channel = GetDisplayParts(record, out string name, out string text);
            return channel.HasValue ? Tuple.Create(channel.Value, name ?? string.Empty, text) : null;
        }

        internal static bool TryReadCommand(string text, out GlobalChatChannel channel, out string message)
        {
            channel = GlobalChatChannel.Global;
            message = text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.TrimStart();
            int split = 0;
            while (split < text.Length && !char.IsWhiteSpace(text[split])) split++;
            string command = text.Substring(0, split);
            foreach (GlobalChatChannel candidate in Selectable)
            {
                if (command.Equals(Command(candidate), StringComparison.OrdinalIgnoreCase)
                    || command.Equals(Alias(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    channel = candidate;
                    message = text.Substring(split).TrimStart();
                    return true;
                }
            }
            return false;
        }

        internal static string Alias(GlobalChatChannel channel)
        {
            switch (channel)
            {
                case GlobalChatChannel.Global: return "[chat";
                case GlobalChatChannel.Trade: return "[trade";
                case GlobalChatChannel.Events: return "[event";
                case GlobalChatChannel.Help: return "[help";
                case GlobalChatChannel.Pariah: return "[pariah";
                default: return null;
            }
        }

        internal static string Command(GlobalChatChannel channel)
        {
            switch (channel)
            {
                case GlobalChatChannel.Global: return "[c";
                case GlobalChatChannel.Trade: return "[tc";
                case GlobalChatChannel.Events: return "[ec";
                case GlobalChatChannel.Help: return "[hc";
                case GlobalChatChannel.LFG: return "[lfg";
                case GlobalChatChannel.Pariah: return "[cp";
                case GlobalChatChannel.Guild: return "\\";
                case GlobalChatChannel.Party: return "/";
                default: return null;
            }
        }
    }
}
