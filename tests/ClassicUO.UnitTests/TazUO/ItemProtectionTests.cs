using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ItemProtectionTests : IDisposable
    {
        private readonly Profile _oldProfile = ProfileManager.CurrentProfile;
        private readonly PlayerMobile _oldPlayer = World.Player;
        private readonly StaticTiles[] _oldTiles = TileDataLoader._staticData;
        private readonly Thread _oldThread = CUOEnviroment.GameThread;
        private readonly Action[] _actions = MainThreadQueue.QueuedActions.ToArray();
        private readonly MoveItemQueue _oldQueue = MoveItemQueue.Instance;
        private readonly Dictionary<uint, Item> _oldItems = new();
        private readonly Profile _profile = new Profile();
        private const uint PACK = 0x4FAA5000, SWORD = 0x4FAA5001, SHIELD = 0x4FAA5002, BAG = 0x4FAA5003, OTHER = 0x4FAA5004;

        public ItemProtectionTests()
        {
            TestLogging.EnsureInitialized(); MainThreadQueue.Reset();
            CUOEnviroment.GameThread = Thread.CurrentThread;
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, FormatterServices.GetUninitializedObject(typeof(PlayerMobile)));
            World.Player.Serial = 1;
            TileDataLoader._staticData = new StaticTiles[0x1000];
            TileDataLoader._staticData[1] = new StaticTiles { Name = "Backpack", Layer = (byte)Layer.Backpack, Flags = TileFlag.Container };
            TileDataLoader._staticData[2] = new StaticTiles { Name = "Sword", Layer = (byte)Layer.OneHanded, Flags = TileFlag.Weapon | TileFlag.Wearable };
            TileDataLoader._staticData[3] = new StaticTiles { Name = "Shield", Layer = (byte)Layer.TwoHanded, Flags = TileFlag.Wearable };
            TileDataLoader._staticData[4] = new StaticTiles { Name = "Axe", Layer = (byte)Layer.TwoHanded, Flags = TileFlag.Weapon | TileFlag.Wearable };
            TileDataLoader._staticData[5] = new StaticTiles { Name = "Talisman", Layer = (byte)Layer.Talisman, Flags = TileFlag.Wearable };
            TileDataLoader._staticData[0x0EFA] = new StaticTiles { Name = "Spellbook", Layer = (byte)Layer.OneHanded, Flags = TileFlag.Container };
            var pack = Make(PACK, 1, 1); pack.Layer = Layer.Backpack; World.Player.PushToBack(pack);
            ItemProtectionManager.Update();
        }

        private Item Make(uint serial, ushort graphic, uint parent)
        {
            if (!_oldItems.ContainsKey(serial)) { World.Items.TryGetValue(serial, out var old); _oldItems[serial] = old; }
            var item = new Item { Serial = serial, Graphic = graphic, Container = parent, Amount = 1 };
            World.Items[serial] = item; return item;
        }
        private void Protect(Item item)
        {
            _profile.ProtectedItems.Add(new ProtectedItemRecord { Serial = item.Serial, Name = item.ItemData.Name,
                Ancestors = ItemProtectionManager.Ancestors(item, World.Items.Get) });
            ItemProtectionManager.Update();
            typeof(ItemProtectionManager).GetField("_noticeAt", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Time.Ticks + 100000);
        }
        private static byte[] Request(byte id, uint serial)
        {
            var p = new byte[id == 7 ? 7 : id == 8 ? 15 : id == 0x13 ? 10 : 5]; p[0] = id; Put(p, 1, serial);
            if (id == 7) p[6] = 1;
            return p;
        }
        private static void Put(byte[] p, int offset, uint value)
        { p[offset] = (byte)(value >> 24); p[offset + 1] = (byte)(value >> 16); p[offset + 2] = (byte)(value >> 8); p[offset + 3] = (byte)value; }

        [Theory]
        [InlineData(7)] [InlineData(8)] [InlineData(0x13)]
        public void Movement_packets_block_protected_items_and_allow_other_items(int id)
        {
            var sword = Make(SWORD, 2, PACK); Protect(sword);
            Make(OTHER, 2, PACK);
            var protectedPacket = Request((byte)id, SWORD);
            var otherPacket = Request((byte)id, OTHER);
            if (id == 0x13) { protectedPacket[5] = otherPacket[5] = (byte)Layer.OneHanded; Put(protectedPacket, 6, 1); Put(otherPacket, 6, 1); }
            Assert.False(ItemProtectionManager.AllowPacket(protectedPacket, false));
            Assert.True(ItemProtectionManager.AllowPacket(otherPacket, false));
        }

        [Fact]
        public void Empty_protection_list_does_not_change_normal_packet_handling()
        {
            var packet = Request(7, SWORD); var original = packet.ToArray();
            Assert.False(ItemProtectionManager.Active);
            Assert.True(ItemProtectionManager.AllowPacket(packet, false));
            Assert.False(ItemProtectionManager.Defer(packet, _ => throw new Exception("Unexpected deferral")));
            Assert.Equal(original, packet); Assert.Empty(MainThreadQueue.QueuedActions);
        }

        [Fact]
        public void Containers_cannot_bypass_protection_and_missing_items_keep_saved_ancestors()
        {
            var bag = Make(BAG, 1, PACK); var sword = Make(SWORD, 2, BAG); Protect(sword);
            Assert.False(ItemProtectionManager.CanMove(SWORD)); Assert.False(ItemProtectionManager.CanMove(BAG));
            Assert.False(ItemProtectionManager.CanMove(PACK));
            World.Items.Remove(SWORD);
            Assert.False(ItemProtectionManager.CanMove(BAG));
            World.Items[SWORD] = sword; sword.Container = PACK;
            Assert.True(ItemProtectionManager.CanMove(BAG));
            Assert.False(ItemProtectionManager.CanMove(PACK));
            bag.Container = SWORD; sword.Container = BAG;
            Assert.InRange(ItemProtectionManager.Ancestors(sword, World.Items.Get).Count, 0, 2);
        }

        [Fact]
        public void Protection_applies_to_individual_serial_not_graphic_or_hue()
        {
            var first = Make(SWORD, 2, PACK); var other = Make(OTHER, 2, PACK); Protect(first);
            Assert.False(ItemProtectionManager.CanMove(first.Serial)); Assert.True(ItemProtectionManager.CanMove(other.Serial));
        }

        [Theory]
        [InlineData(2, 1)] [InlineData(4, 2)]
        public void Equipment_requests_cannot_displace_protected_hand_items(int graphic, int layer)
        {
            var sword = Make(SWORD, 2, 1); sword.Layer = Layer.OneHanded; World.Player.PushToBack(sword); Protect(sword);
            Make(OTHER, (ushort)graphic, PACK);
            Assert.False(ItemProtectionManager.CanEquip(OTHER, (Layer)layer, 1));
        }

        [Fact]
        public void One_handed_weapon_can_coexist_with_a_protected_shield()
        {
            var shield = Make(SHIELD, 3, 1); shield.Layer = Layer.TwoHanded; World.Player.PushToBack(shield); Protect(shield);
            Make(SWORD, 2, PACK);
            Assert.True(ItemProtectionManager.CanEquip(SWORD, Layer.OneHanded, 1));
        }

        [Fact]
        public void Switcher_permission_is_exact_scoped_and_cannot_move_gear_to_another_bag()
        {
            var sword = Make(SWORD, 2, 1); sword.Layer = Layer.OneHanded; World.Player.PushToBack(sword); Protect(sword);
            var pickup = Request(7, SWORD); var drop = Request(8, SWORD); Put(drop, 11, PACK);
            Assert.False(ItemProtectionManager.AllowPacket(pickup, false));
            using (ItemProtectionManager.AuthorizeSwitcher(SWORD, PACK, Layer.Invalid))
            {
                Assert.True(ItemProtectionManager.AllowPacket(pickup, false));
                Assert.True(ItemProtectionManager.NeedsSwitcherBypass(pickup));
                Assert.True(ItemProtectionManager.AllowPacket(drop, false));
                Put(drop, 11, BAG); Assert.False(ItemProtectionManager.AllowPacket(drop, false));
                pickup[6] = 2; Assert.False(ItemProtectionManager.AllowPacket(pickup, false));
            }
            Assert.False(ItemProtectionManager.AllowPacket(Request(7, SWORD), false));
            Assert.Null(ItemProtectionManager.AuthorizeSwitcher(SWORD, BAG, Layer.Invalid));
        }

        [Fact]
        public void Switcher_can_equip_protected_gear_and_restore_a_protected_shield()
        {
            var sword = Make(SWORD, 2, PACK); var shield = Make(SHIELD, 3, PACK); Protect(sword); Protect(shield);
            var equip = Request(0x13, SHIELD); equip[5] = (byte)Layer.TwoHanded; Put(equip, 6, 1);
            using (ItemProtectionManager.AuthorizeSwitcher(SHIELD, uint.MaxValue, Layer.TwoHanded))
            {
                Assert.True(ItemProtectionManager.AllowPacket(equip, false));
                equip[5] = (byte)Layer.OneHanded; Assert.False(ItemProtectionManager.AllowPacket(equip, false));
                equip[5] = (byte)Layer.TwoHanded; Put(equip, 6, 2); Assert.False(ItemProtectionManager.AllowPacket(equip, false));
            }
            Assert.True(ItemProtectionManager.CanSwitch(SWORD, uint.MaxValue, Layer.OneHanded));
            Assert.False(ItemProtectionManager.CanSwitch(PACK, uint.MaxValue, Layer.Backpack));
            Assert.False(ItemProtectionManager.CanSwitch(SWORD, PACK, Layer.Invalid));
        }

        [Fact]
        public void Switcher_keeps_pickup_and_equip_in_order_even_for_an_unprotected_replacement()
        {
            var shield = Make(SHIELD, 3, 1); shield.Layer = Layer.TwoHanded; World.Player.PushToBack(shield); Protect(shield);
            Make(SWORD, 4, PACK);
            var pickup = Request(7, SWORD); var equip = Request(0x13, SWORD); equip[5] = (byte)Layer.TwoHanded; Put(equip, 6, 1);
            using (ItemProtectionManager.AuthorizeSwitcher(SWORD, uint.MaxValue, Layer.TwoHanded))
            {
                Assert.True(ItemProtectionManager.NeedsSwitcherBypass(pickup));
                Assert.True(ItemProtectionManager.NeedsSwitcherBypass(equip));
                Assert.True(ItemProtectionManager.AllowPacket(pickup, false));
                Assert.True(ItemProtectionManager.AllowPacket(equip, false));
            }
            Assert.False(ItemProtectionManager.NeedsSwitcherBypass(pickup));
            Assert.False(ItemProtectionManager.AllowPacket(equip, false));
        }

        [Theory]
        [InlineData(2, 1)] [InlineData(3, 2)] [InlineData(4, 2)] [InlineData(5, 9)] [InlineData(0x0EFA, 1)]
        public void Switcher_supports_all_protected_equipment_categories(int graphic, int layer)
        {
            var item = Make(SWORD, (ushort)graphic, PACK); Protect(item);
            var packet = Request(0x13, SWORD); packet[5] = (byte)layer; Put(packet, 6, 1);
            Assert.False(ItemProtectionManager.AllowPacket(packet, false));
            using (ItemProtectionManager.AuthorizeSwitcher(SWORD, uint.MaxValue, (Layer)layer))
                Assert.True(ItemProtectionManager.AllowPacket(packet, false));
            Assert.False(ItemProtectionManager.AllowPacket(packet, false));
        }

        [Fact]
        public void Protected_spellbooks_and_bags_can_still_be_opened()
        {
            var book = Make(SWORD, 0x0EFA, PACK); Protect(book);
            Assert.True(ItemProtectionManager.AllowPacket(Request(6, SWORD), false));
            Assert.True(ItemProtectionManager.AllowPacket(Request(6, PACK), false));
            Assert.True(ItemProtectionManager.CanSwitch(SWORD, uint.MaxValue, Layer.OneHanded));
        }

        [Fact]
        public void Sell_and_equipment_macros_obey_protection()
        {
            var sword = Make(SWORD, 2, 1); sword.Layer = Layer.OneHanded; World.Player.PushToBack(sword); Protect(sword);
            Make(OTHER, 2, PACK);
            byte[] sell = new byte[15]; sell[0] = 0x9F; sell[8] = 1; Put(sell, 9, SWORD); sell[14] = 1;
            Assert.False(ItemProtectionManager.AllowPacket(sell, false));
            Put(sell, 9, OTHER); Assert.True(ItemProtectionManager.AllowPacket(sell, false));
            byte[] dress = new byte[8]; dress[0] = 0xEC; dress[3] = 1; Put(dress, 4, OTHER);
            Assert.False(ItemProtectionManager.AllowPacket(dress, false));
            byte[] undress = { 0xED, 0, 6, 1, 0, (byte)Layer.OneHanded };
            Assert.False(ItemProtectionManager.AllowPacket(undress, false));
            byte[] lastWeapon = new byte[10]; lastWeapon[0] = 0xD7; lastWeapon[8] = 0x1E;
            Assert.False(ItemProtectionManager.AllowPacket(lastWeapon, false));
        }

        [Fact]
        public void Queued_moves_skip_protected_items_with_operation_accounting()
        {
            var sword = Make(SWORD, 2, PACK); Protect(sword);
            var queue = new MoveItemQueue(); var operation = new QueuedOperation("Protection test");
            queue.EnqueueTracked(SWORD, BAG, 1, 0xFFFF, 0xFFFF, 0, operation);
            Assert.True(queue.IsEmpty); Assert.Equal(1, operation.Total); Assert.Equal(1, operation.Skipped);
            queue.EnqueueEquipSingle(SWORD, Layer.OneHanded); Assert.True(queue.IsEmpty);
            queue.EnqueueSwitcherEquip(SWORD, Layer.OneHanded); Assert.False(queue.IsEmpty);
        }

        [Fact]
        public void Worker_requests_do_not_block_and_pending_requests_are_cancelled_on_logout()
        {
            Protect(Make(SWORD, 2, PACK)); int sent = 0;
            var task = Task.Run(() => ItemProtectionManager.Defer(Request(7, SWORD), _ => sent++));
            Assert.True(task.Wait(TimeSpan.FromSeconds(5))); Assert.True(task.Result);
            Assert.Equal(0, sent); Assert.Single(MainThreadQueue.QueuedActions);
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _oldProfile);
            ItemProtectionManager.Reset();
            Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out var pending)); pending(); Assert.Equal(0, sent);
        }

        [Fact]
        public void Protection_round_trips_but_settings_transfer_preserves_destination_items()
        {
            Protect(Make(SWORD, 2, BAG));
            Assert.Equal((ushort)0, _profile.ProtectedItemHue);
            _profile.ProtectedItemHue = 1150;
            string json = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(SWORD, Assert.Single(restored.ProtectedItems).Serial);
            Assert.Contains(BAG, restored.ProtectedItems[0].Ancestors);
            Assert.Equal((ushort)1150, restored.ProtectedItemHue);
            Assert.Equal(ProfileTransferCategory.Interface, ProfileTransferCatalog.JsonCategories["protected_item_hue"]);
            Assert.False(ProfileTransferCatalog.JsonCategories.ContainsKey("protected_items"));
            using var merged = JsonDocument.Parse(ProfileTransfer.Merge(json, "{\"protected_items\":[{\"serial\":17}]}",
                new HashSet<ProfileTransferCategory>(ProfileTransferCatalog.All)));
            Assert.Equal(17u, merged.RootElement.GetProperty("protected_items")[0].GetProperty("serial").GetUInt32());
            Assert.Equal(1150, merged.RootElement.GetProperty("protected_item_hue").GetInt32());
            using var unselected = JsonDocument.Parse(ProfileTransfer.Merge(json, "{\"protected_item_hue\":42}",
                new HashSet<ProfileTransferCategory> { ProfileTransferCategory.Containers }));
            Assert.Equal(42, unselected.RootElement.GetProperty("protected_item_hue").GetInt32());
        }

        public void Dispose()
        {
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _oldProfile);
            ItemProtectionManager.Reset(); MainThreadQueue.Reset();
            foreach (var action in _actions) MainThreadQueue.EnqueueAction(action);
            foreach (var entry in _oldItems) { if (entry.Value == null) World.Items.Remove(entry.Key); else World.Items[entry.Key] = entry.Value; }
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _oldPlayer);
            typeof(MoveItemQueue).GetProperty(nameof(MoveItemQueue.Instance)).SetValue(null, _oldQueue);
            TileDataLoader._staticData = _oldTiles; CUOEnviroment.GameThread = _oldThread;
        }
    }
}
