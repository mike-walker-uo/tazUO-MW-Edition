using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // Optional standalone textures only. Atlases, fonts and render targets have separate owners.
    public static class OptionalTextureCache
    {
        private sealed class Entry { public Texture2D Texture; public Action Evict; public long Bytes; public int Used; }
        private static readonly Dictionary<Texture2D, Entry> Entries = new Dictionary<Texture2D, Entry>();
        private static int _frame;
        public static long ResidentBytes { get; private set; }
        public static void BeginFrame() => _frame++;

        public static void Register(Texture2D texture, Action evict = null)
        {
            if (texture == null || Entries.ContainsKey(texture)) return;
            // Include a small minimum allocation cost for tiny standalone GPU resources.
            long bytes = Math.Max(4096, (long)texture.Width * texture.Height * 4);
            Entries.Add(texture, new Entry { Texture = texture, Bytes = bytes, Evict = evict, Used = _frame });
            ResidentBytes += bytes;
        }

        public static void Touch(Texture2D texture)
        {
            if (texture != null && Entries.TryGetValue(texture, out Entry entry)) entry.Used = _frame;
        }

        // Called only after scene, UI, cursor and plugin batches have finished.
        public static void EndFrame(int budgetMB)
        {
            long budget = Math.Max(32, Math.Min(512, budgetMB)) * 1024L * 1024L;
            if (ResidentBytes <= budget) return;
            var candidates = new List<Entry>();
            foreach (Entry entry in Entries.Values)
                if (_frame - entry.Used > 2) candidates.Add(entry);
            candidates.Sort((a, b) => a.Used.CompareTo(b.Used));
            foreach (Entry entry in candidates)
            {
                if (ResidentBytes <= budget) break;
                Entries.Remove(entry.Texture);
                ResidentBytes -= entry.Bytes;
                entry.Evict?.Invoke();
                if (!entry.Texture.IsDisposed) entry.Texture.Dispose();
            }
        }
    }
}
