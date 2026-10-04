using System;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal static class ChatMentions
    {
        internal static bool Matches(string text, string playerName, string guildTag, string words)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (ContainsWord(text, playerName) || ContainsWord(text, guildTag)) return true;
            foreach (string word in (words ?? string.Empty).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (ContainsWord(text, word.Trim())) return true;
            return false;
        }

        private static string _player, _guild, _words;
        private static string[] _keywords = Array.Empty<string>();
        private static int _version;
        internal static int Version
        {
            get
            {
                Profile profile = ProfileManager.CurrentProfile;
                string player = World.Player?.Name, guild = profile?.ChatMentionGuildTag, words = profile?.ChatMentionWords;
                if (player != _player || guild != _guild || words != _words)
                {
                    _player = player; _guild = guild; _words = words;
                    _keywords = (words ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < _keywords.Length; i++) _keywords[i] = _keywords[i].Trim();
                    _version++;
                }
                return _version;
            }
        }
        internal static bool IsMention(string text)
        {
            _ = Version;
            if (string.IsNullOrEmpty(text)) return false;
            if (ContainsWord(text, _player) || ContainsWord(text, _guild)) return true;
            foreach (string word in _keywords) if (ContainsWord(text, word)) return true;
            return false;
        }

        private static bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            int start = 0;
            while ((start = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = start + word.Length;
                if ((start == 0 || !char.IsLetterOrDigit(text[start - 1]))
                    && (end == text.Length || !char.IsLetterOrDigit(text[end]))) return true;
                start++;
            }
            return false;
        }
    }
}
