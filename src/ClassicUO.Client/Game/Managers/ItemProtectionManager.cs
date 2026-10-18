using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal static class ItemProtectionManager
    {
        private static Profile _profile;
        private static object _session;
        private static volatile bool _active;
        private static readonly HashSet<uint> Marked = new HashSet<uint>();
        private static uint _nextRefresh, _saveAt, _noticeAt;
        private static bool _dirty;
        [ThreadStatic] private static SwitcherScope _switcher;
        internal static bool Active => _active;
        internal static IEnumerable<ProtectedItemRecord> Records => ProfileManager.CurrentProfile?.ProtectedItems
            ?? Enumerable.Empty<ProtectedItemRecord>();

        internal static List<uint> Ancestors(Item item, Func<uint, Item> lookup)
        {
            var result = new List<uint>();
            var seen = new HashSet<uint> { item.Serial };
            uint parent = item.Container;
            while (parent != 0 && parent != uint.MaxValue && seen.Add(parent))
            {
                Item container = lookup(parent);
                if (container == null)
                { if ((parent & 0x40000000) != 0) result.Add(parent); break; }
                result.Add(parent); parent = container.Container;
            }
            return result;
        }

        internal static uint BlockingSerial(uint serial, IEnumerable<ProtectedItemRecord> records, Func<uint, Item> lookup)
        {
            foreach (var record in records)
            {
                if (record.Serial == serial) return record.Serial;
                Item item = lookup(record.Serial);
                var parents = item == null || item.IsDestroyed ? record.Ancestors : Ancestors(item, lookup);
                if (parents?.Contains(serial) == true) return record.Serial;
            }
            return 0;
        }

        private static uint Blocker(uint serial) => BlockingSerial(serial, Records, World.Items.Get);
        internal static bool CanMove(uint serial, bool notify = false)
        {
            uint blocked = Blocker(serial);
            if (blocked == 0) return true;
            if (notify) Notify(blocked);
            return false;
        }

        internal static bool CanDrop(uint serial, uint destination, bool notify = false) =>
            _switcher != null && _switcher.Serial == serial && _switcher.Destination == destination || CanMove(serial, notify);

        internal static bool Owned(Item item)
        {
            if (item == null || item.IsDestroyed || World.Player == null) return false;
            Item backpack = World.Player.FindItemByLayer(Layer.Backpack);
            return item.Container == World.Player.Serial && item.Layer != Layer.Bank
                || backpack != null && Ancestors(item, World.Items.Get).Contains(backpack.Serial);
        }

        internal static void Target(bool protect)
        {
            if (!World.InGame) return;
            TargetManager.SetTargeting(protect ? CursorTarget.ProtectItem : CursorTarget.UnprotectItem, CursorType.Target, TargetType.Neutral);
            GameActions.Print(protect ? "Target an item on your character or in your backpack to protect." : "Target an item to remove its protection.", 0x35);
        }

        internal static void TargetItem(Entity entity, bool protect)
        {
            if (!(entity is Item item)) { GameActions.Print("Target an item.", 0x35); return; }
            if (protect && (!Owned(item) || Client.Game.GameCursor.ItemHold.Enabled))
            { GameActions.Print("Put down any held item, then target your equipment or an item in your backpack.", 0x35); return; }
            if (!protect) { Remove(item.Serial); return; }
            Update();
            var profile = ProfileManager.CurrentProfile;
            profile.ProtectedItems ??= new List<ProtectedItemRecord>();
            if (profile.ProtectedItems.Any(r => r.Serial == item.Serial))
            { GameActions.Print("This item is already protected.", 0x35); return; }
            profile.ProtectedItems.Add(new ProtectedItemRecord { Serial = item.Serial, Name = item.Name ?? item.ItemData.Name,
                Ancestors = Ancestors(item, World.Items.Get) });
            _active = true; _nextRefresh = 0;
            Save(); Update();
            GameActions.Print("Item protected. Ordinary moves are blocked; the weapon switcher can still equip it.", 0x35);
            ItemProtectionGump.RefreshOpen();
        }

        internal static void Remove(uint serial)
        {
            if (!World.InGame || ProfileManager.CurrentProfile?.ProtectedItems == null) return;
            if (ProfileManager.CurrentProfile.ProtectedItems.RemoveAll(r => r.Serial == serial) == 0)
            { GameActions.Print("This item is not individually protected.", 0x35); return; }
            Save(); _nextRefresh = 0; Update();
            GameActions.Print("Item protection removed.", 0x35);
            ItemProtectionGump.RefreshOpen();
        }

        internal static void Update()
        {
            var profile = ProfileManager.CurrentProfile;
            if (_profile != profile)
            { _profile = profile; Volatile.Write(ref _session, new object()); _dirty = false; _nextRefresh = 0; Marked.Clear(); }
            _active = profile?.ProtectedItems?.Count > 0;
            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = Time.Ticks + 250;
                Marked.Clear();
                foreach (var record in Records)
                {
                    Item item = World.Items.Get(record.Serial);
                    if (item != null && !item.IsDestroyed)
                    {
                        var parents = Ancestors(item, World.Items.Get);
                        if (record.Ancestors == null || !record.Ancestors.SequenceEqual(parents))
                        { record.Ancestors = parents; _dirty = true; _saveAt = Time.Ticks + 2000; }
                    }
                    Marked.Add(record.Serial);
                    if (record.Ancestors != null) foreach (uint parent in record.Ancestors) Marked.Add(parent);
                }
            }
            if (_dirty && Time.Ticks >= _saveAt) Save();
        }

        private static void Save()
        {
            if (_profile != ProfileManager.CurrentProfile || _profile == null || !World.InGame) return;
            _profile.Save(ProfileManager.ProfilePath, false, true); _dirty = false;
        }

        internal static void Reset()
        {
            if (_dirty) Save();
            _active = false; _profile = null; Volatile.Write(ref _session, null);
            Marked.Clear(); _dirty = false; _switcher = null; _nextRefresh = _noticeAt = 0;
        }

        private static void Notify(uint serial)
        {
            if (Time.Ticks < _noticeAt) return;
            _noticeAt = Time.Ticks + 1500;
            string name = Records.FirstOrDefault(r => r.Serial == serial)?.Name;
            GameActions.Print("Protected item—move blocked" + (string.IsNullOrEmpty(name) ? "." : ": " + name), 0x35);
        }

        private static bool TwoHandedWeapon(Item item) => item != null && item.ItemData.IsWeapon
            && (Layer)item.ItemData.Layer == Layer.TwoHanded
            && (item.ItemData.Name?.IndexOf("shield", StringComparison.OrdinalIgnoreCase) ?? -1) < 0
            && (item.ItemData.Name?.IndexOf("buckler", StringComparison.OrdinalIgnoreCase) ?? -1) < 0;

        private static uint EquipBlocker(uint serial, Layer layer, uint owner)
        {
            uint blocked = Blocker(serial);
            if (blocked != 0 || World.Player == null || owner != World.Player.Serial) return blocked;
            Item same = World.Player.FindItemByLayer(layer);
            if (same != null && same.Serial != serial && (blocked = Blocker(same.Serial)) != 0) return blocked;
            Item source = World.Items.Get(serial);
            Item other = World.Player.FindItemByLayer(layer == Layer.OneHanded ? Layer.TwoHanded : Layer.OneHanded);
            if (other != null && (layer == Layer.OneHanded && TwoHandedWeapon(other)
                || layer == Layer.TwoHanded && TwoHandedWeapon(source))) return Blocker(other.Serial);
            return 0;
        }

        internal static bool CanEquip(uint serial, Layer layer, uint owner, bool notify = false)
        {
            uint blocked = EquipBlocker(serial, layer, owner);
            if (blocked == 0) return true;
            if (notify) Notify(blocked);
            return false;
        }

        internal static bool CanSwitch(uint serial, uint destination, Layer layer)
        {
            Item item = World.Items.Get(serial);
            if (!Owned(item)) return false;
            Item backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) return false;
            Layer actual = SpellbookOpenerManager.IsSpellbook(item) ? Layer.OneHanded : (Layer)item.ItemData.Layer;
            if (actual != Layer.OneHanded && actual != Layer.TwoHanded && actual != Layer.Talisman) return false;
            return destination == uint.MaxValue ? layer == actual
                : destination == backpack.Serial && item.Container == World.Player.Serial;
        }

        internal static IDisposable AuthorizeSwitcher(uint serial, uint destination, Layer layer)
            => CanSwitch(serial, destination, layer) ? new SwitcherScope(serial, destination, layer) : null;

        private sealed class SwitcherScope : IDisposable
        {
            internal readonly uint Serial, Destination, Player;
            internal readonly Layer Layer;
            private readonly SwitcherScope _previous;
            internal SwitcherScope(uint serial, uint destination, Layer layer)
            { Serial = serial; Destination = destination; Layer = layer; Player = World.Player.Serial; _previous = _switcher; _switcher = this; }
            public void Dispose() => _switcher = _previous;
        }

        internal static bool SwitcherPacket(ReadOnlySpan<byte> packet)
        {
            var scope = _switcher;
            if (!Active || scope == null || packet.Length < 5 || U32(packet, 1) != scope.Serial) return false;
            if (packet[0] == 7) return packet.Length == 7 && U16(packet, 5) == 1;
            if (packet[0] == 8) return scope.Destination != uint.MaxValue && (packet.Length == 14 || packet.Length == 15)
                && U32(packet, packet.Length - 4) == scope.Destination;
            return packet[0] == 0x13 && packet.Length == 10 && scope.Destination == uint.MaxValue
                && packet[5] == (byte)scope.Layer && U32(packet, 6) == scope.Player;
        }

        internal static bool Sensitive(byte id) => id == 7 || id == 8 || id == 0x13 || id == 6
            || id == 0x9F || id == 0xEC || id == 0xED || id == 0xD7;

        // Assistant movement requests are deferred without waiting: World is only inspected on the game thread.
        internal static bool Defer(ReadOnlySpan<byte> packet, Action<byte[]> send)
        {
            if (!Active || packet.IsEmpty || !Sensitive(packet[0]) || Thread.CurrentThread == CUOEnviroment.GameThread) return false;
            byte[] copy = packet.ToArray(); object session = Volatile.Read(ref _session);
            MainThreadQueue.EnqueueAction(() =>
            { if (session != null && ReferenceEquals(session, Volatile.Read(ref _session)) && World.InGame) send(copy); });
            return true;
        }

        // Keep both halves of a switch in order: an assistant must not delay just the unprotected pickup.
        internal static bool NeedsSwitcherBypass(ReadOnlySpan<byte> packet) => SwitcherPacket(packet);
        internal static bool AllowPacket(ReadOnlySpan<byte> packet, bool notify = true)
        {
            if (!Active || packet.IsEmpty || !Sensitive(packet[0]) || SwitcherPacket(packet)) return true;
            uint blocked = PacketBlocker(packet);
            if (blocked == 0) return true;
            if (notify) Notify(blocked);
            return false;
        }

        private static uint PacketBlocker(ReadOnlySpan<byte> p)
        {
            if (p[0] == 7 || p[0] == 8 || p[0] == 0x13)
            {
                if (p.Length < 5) return uint.MaxValue;
                uint serial = U32(p, 1);
                if (p[0] != 0x13) return Blocker(serial);
                return p.Length == 10 ? EquipBlocker(serial, (Layer)p[5], U32(p, 6)) : uint.MaxValue;
            }
            if (p[0] == 6 && p.Length >= 5)
            {
                Item item = World.Items.Get(U32(p, 1) & 0x7FFFFFFF);
                if (item != null && !item.ItemData.IsContainer && !SpellbookOpenerManager.IsSpellbook(item)
                    && (item.ItemData.IsWearable || item.ItemData.IsWeapon))
                    return EquipBlocker(item.Serial, (Layer)item.ItemData.Layer, World.Player?.Serial ?? 0);
            }
            if (p[0] == 0x9F || p[0] == 0xEC || p[0] == 0xED)
            {
                int header = p[0] == 0x9F ? 9 : 4, stride = p[0] == 0x9F ? 6 : p[0] == 0xEC ? 4 : 2;
                if (p.Length < header) return uint.MaxValue;
                int count = p[0] == 0x9F ? U16(p, 7) : p[3];
                if (p.Length < header + count * stride) return uint.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    int offset = header + i * stride;
                    if (p[0] == 0xED && U16(p, offset) > byte.MaxValue) return uint.MaxValue;
                    Item item = p[0] == 0xED ? World.Player?.FindItemByLayer((Layer)U16(p, offset)) : World.Items.Get(U32(p, offset));
                    uint serial = p[0] == 0xED ? item?.Serial ?? 0 : U32(p, offset);
                    uint blocked = p[0] == 0xEC && item != null
                        ? EquipBlocker(serial, (Layer)item.ItemData.Layer, World.Player?.Serial ?? 0) : Blocker(serial);
                    if (blocked != 0) return blocked;
                }
            }
            if (p[0] == 0xD7 && p.Length >= 9 && U16(p, 7) == 0x1E)
                foreach (var record in Records)
                {
                    Item item = World.Items.Get(record.Serial);
                    if (Owned(item) && ((Layer)item.ItemData.Layer == Layer.OneHanded || (Layer)item.ItemData.Layer == Layer.TwoHanded)) return record.Serial;
                }
            return 0;
        }

        private static uint U32(ReadOnlySpan<byte> p, int offset) => (uint)p[offset] << 24 | (uint)p[offset + 1] << 16 | (uint)p[offset + 2] << 8 | p[offset + 3];
        private static int U16(ReadOnlySpan<byte> p, int offset) => p[offset] * 256 + p[offset + 1];

        internal static void DrawMarker(UltimaBatcher2D batcher, uint serial, int x, int y)
        {
            if (!Marked.Contains(serial)) return;
            ushort markerHue = ProfileManager.CurrentProfile?.ProtectedItemHue ?? 0;
            var texture = SolidColorTextureCache.GetTexture(markerHue == 0 ? Color.Gold : Color.White);
            var hue = ShaderHueTranslator.GetHueVector(markerHue, false, 0.95f);
            batcher.Draw(SolidColorTextureCache.GetTexture(Color.Black), new Rectangle(x - 1, y - 1, 10, 12),
                ShaderHueTranslator.GetHueVector(0, false, 0.7f));
            batcher.DrawRectangle(texture, x, y + 4, 8, 6, hue);
            batcher.DrawRectangle(texture, x + 2, y, 4, 5, hue);
        }
    }
}
