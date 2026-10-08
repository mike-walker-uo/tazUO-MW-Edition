using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    // Frame-thread cache of observed contents only; no requests or persisted/live-bank claims.
    internal static class InventoryCounts
    {
        private static readonly Dictionary<uint, Snapshot> _roots = new();
        private static PlayerMobile _owner;
        private static string _profile;
        private static long _revision;
        static InventoryCounts()
        {
            EventSink.OnItemUpdated += OnChanged;
            EventSink.OnItemCreated += OnChanged;
        }

        private static void OnChanged(object sender, EventArgs _) { if (sender is Item item) NotifyChanged(item); }

        internal static void Reset() { _roots.Clear(); _owner = null; _profile = null; }

        internal static void NotifyChanged(Item item)
        {
            if (item == null) return;
            foreach (Snapshot snapshot in _roots.Values)
                if (snapshot.Members.Contains(item.Serial) || snapshot.Members.Contains(item.Container))
                    snapshot.Dirty = true;
        }

        internal static Snapshot Get(Item root)
        {
            if (_owner != World.Player || _profile != ProfileManager.ProfilePath)
            { Reset(); _owner = World.Player; _profile = ProfileManager.ProfilePath; }
            if (root == null || root.IsDestroyed) return Snapshot.Empty;
            if (!_roots.TryGetValue(root.Serial, out Snapshot snapshot) || snapshot.Root != root)
            {
                if (_roots.Count >= 64) _roots.Clear();
                _roots[root.Serial] = snapshot = new Snapshot(root);
            }
            if (snapshot.Dirty) snapshot.Rebuild(++_revision);
            return snapshot;
        }

        internal sealed class Snapshot
        {
            internal static readonly Snapshot Empty = new Snapshot(null) { Dirty = false };
            internal readonly Item Root;
            internal readonly HashSet<uint> Members = new();
            internal readonly List<Entry> Items = new();
            private readonly Dictionary<(ushort, ushort), Total> _totals = new();
            internal bool Dirty = true;
            internal long Revision { get; private set; }
            internal Snapshot(Item root) { Root = root; }
            internal int ItemCount => Items.Count;
            internal int Count(ushort graphic, ushort hue, bool anyHue = false, bool rawAmounts = false)
            {
                if (!anyHue && _totals.TryGetValue((graphic, hue), out Total total))
                    return rawAmounts ? total.RawAmount : total.Units;
                if (!anyHue) return 0;
                int count = 0;
                foreach (var pair in _totals)
                    if (pair.Key.Item1 == graphic) count += rawAmounts ? pair.Value.RawAmount : pair.Value.Units;
                return count;
            }
            internal Item Sample(ushort graphic, ushort hue) =>
                _totals.TryGetValue((graphic, hue), out Total total) ? total.Sample : null;

            internal void Rebuild(long revision)
            {
                Items.Clear(); Members.Clear(); _totals.Clear();
                var pending = new Stack<Item>(); pending.Push(Root); Members.Add(Root.Serial);
                while (pending.Count > 0)
                {
                    Item parent = pending.Pop();
                    for (LinkedObject node = parent.Items; node != null; node = node.Next)
                    {
                        if (!(node is Item item)) continue;
                        if (!Members.Add(item.Serial)) break; // Also bounds corrupt sibling cycles.
                        if (item.IsDestroyed) continue;
                        var entry = new Entry(item); Items.Add(entry);
                        var key = (item.Graphic, item.Hue);
                        _totals.TryGetValue(key, out Total total);
                        total.Units += entry.Units; total.RawAmount += entry.RawAmount; total.Sample = item;
                        _totals[key] = total;
                        if (!item.IsEmpty) pending.Push(item);
                    }
                }
                Revision = revision; Dirty = false;
            }
        }

        internal readonly struct Entry
        {
            internal readonly Item Item;
            internal readonly ushort Graphic, Hue;
            internal readonly int Units, RawAmount;
            internal Entry(Item item)
            {
                Item = item; Graphic = item.Graphic; Hue = item.Hue; RawAmount = item.Amount;
                Units = item.ItemData.IsStackable ? Math.Max(1, (int)item.Amount) : 1;
            }
        }
        private struct Total { internal int Units, RawAmount; internal Item Sample; }
    }
}
