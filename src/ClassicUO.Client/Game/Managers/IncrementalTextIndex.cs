using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.Managers
{
    // Candidate pruning only. The existing query evaluator remains authoritative.
    internal sealed class IncrementalTextIndex
    {
        private readonly Dictionary<uint, string> _texts = new();
        private readonly Dictionary<string, HashSet<uint>> _grams = new(StringComparer.Ordinal);

        internal void Clear() { _texts.Clear(); _grams.Clear(); }

        internal void Update(uint serial, string text)
        {
            text = (text ?? string.Empty).ToUpperInvariant();
            if (_texts.TryGetValue(serial, out string previous) && previous == text) return;
            Remove(serial);
            _texts[serial] = text;
            foreach (string gram in Grams(text))
            {
                if (!_grams.TryGetValue(gram, out var set)) _grams[gram] = set = new();
                set.Add(serial);
            }
        }

        internal void Remove(uint serial)
        {
            if (!_texts.TryGetValue(serial, out string previous)) return;
            foreach (string gram in Grams(previous))
                if (_grams.TryGetValue(gram, out var set))
                {
                    set.Remove(serial);
                    if (set.Count == 0) _grams.Remove(gram);
                }
            _texts.Remove(serial);
        }

        // null means no safe pruning (short, negative-only, numeric or Unicode query).
        internal HashSet<uint> Candidates(IEnumerable<string> requiredTerms)
        {
            var sets = new List<HashSet<uint>>();
            foreach (string term in requiredTerms)
            {
                if (term.Length < 3 || term.Any(c => c > 127)) continue;
                foreach (string gram in Grams(term.ToUpperInvariant()))
                {
                    if (!_grams.TryGetValue(gram, out var set)) return new();
                    sets.Add(set);
                }
            }
            if (sets.Count == 0) return null;
            sets.Sort((a, b) => a.Count.CompareTo(b.Count));
            var candidates = new HashSet<uint>(sets[0]);
            foreach (var set in sets.Skip(1)) candidates.IntersectWith(set);
            return candidates;
        }

        private static HashSet<string> Grams(string text)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i + 2 < text.Length; i++) result.Add(text.Substring(i, 3));
            return result;
        }
    }
}
