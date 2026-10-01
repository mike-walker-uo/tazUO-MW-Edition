using System;
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
        Party
    }

    internal static class GlobalChatChannels
    {
        internal static readonly string[] Names = { "All", "Global", "Trade", "Events", "Help", "LFG", "Guild", "Party" };
        internal static readonly string[] SendNames = { "Global", "Trade", "Events", "Help", "LFG", "Guild", "Party" };

        internal static Color TagColor(GlobalChatChannel channel)
        {
            switch (channel)
            {
                case GlobalChatChannel.Global: return new Color(40, 156, 184);
                case GlobalChatChannel.Trade: return new Color(205, 157, 63);
                case GlobalChatChannel.Events: return new Color(200, 74, 135);
                case GlobalChatChannel.Help: return new Color(130, 167, 187);
                case GlobalChatChannel.LFG: return new Color(137, 173, 112);
                case GlobalChatChannel.Guild: return new Color(67, 173, 97);
                case GlobalChatChannel.Party: return new Color(86, 145, 190);
                default: throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        internal static GlobalChatChannel? GetDisplayParts(ChatHistoryRecord record, out string name, out string text)
        {
            name = record.Name;
            text = record.Text ?? string.Empty;
            GlobalChatChannel? channel = GetChannel(name, text, record.MsgType);
            if (!channel.HasValue) return null;

            string channelName = Names[(int)channel.Value];
            string tag = "[" + channelName + "]";
            if (name?.TrimStart().StartsWith(tag, StringComparison.OrdinalIgnoreCase) == true)
                name = name.TrimStart().Substring(tag.Length).Trim();
            else if (text.TrimStart().StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                text = text.TrimStart().Substring(tag.Length).TrimStart();
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
            for (int i = 1; i < Names.Length; i++)
            {
                string tag = "[" + Names[i] + "]";
                if (text?.TrimStart().StartsWith(tag, StringComparison.OrdinalIgnoreCase) == true
                    || name?.TrimStart().StartsWith(tag, StringComparison.OrdinalIgnoreCase) == true)
                    return (GlobalChatChannel)i;
            }
            return type == MessageType.ChatSystem ? GlobalChatChannel.Global : (GlobalChatChannel?)null;
        }

        internal static bool IsChatMessage(MessageEventArgs message) =>
            message.Type == MessageType.ChatSystem || message.Type == MessageType.Guild || message.Type == MessageType.Party
            || ((message.Type == MessageType.System || message.Type == MessageType.Regular) && message.Parent == null
                && GetChannel(message.Name, message.Text, message.Type).HasValue);

        internal static bool Matches(ChatHistoryRecord record, GlobalChatChannel selected)
        {
            GlobalChatChannel? channel = GetChannel(record.Name, record.Text, record.MsgType);
            return selected == GlobalChatChannel.All
                ? channel.HasValue && channel != GlobalChatChannel.Guild && channel != GlobalChatChannel.Party
                : channel == selected;
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
                case GlobalChatChannel.Guild: return "\\";
                case GlobalChatChannel.Party: return "/";
                default: return null;
            }
        }
    }
}
