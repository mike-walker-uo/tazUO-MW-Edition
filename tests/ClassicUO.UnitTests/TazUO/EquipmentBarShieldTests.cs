using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class EquipmentBarShieldTests : IDisposable
    {
        private readonly PlayerMobile _originalPlayer = World.Player;
        private readonly StaticTiles[] _originalTiles = TileDataLoader._staticData;
        private readonly MoveItemQueue _originalQueue = MoveItemQueue.Instance;
        private readonly uint _originalOneHand = AutoRearmManager.OneHandedSerial;
        private readonly uint _originalTwoHand = AutoRearmManager.TwoHandedSerial;
        private readonly List<Item> _items = new List<Item>();
        private readonly MoveItemQueue _queue = new MoveItemQueue();
        private readonly PaperDollBackpackEquipmentGump _bar = Empty<PaperDollBackpackEquipmentGump>();
        private readonly Item _backpack, _sword, _shield, _bow, _newSword;

        public EquipmentBarShieldTests()
        {
            SetPlayer(Empty<PlayerMobile>());
            World.Player.Serial = 1;
            TileDataLoader._staticData = new StaticTiles[5];
            TileDataLoader._staticData[1] = new StaticTiles { Name = "sword", Flags = TileFlag.Weapon, Layer = (byte)Layer.OneHanded };
            TileDataLoader._staticData[2] = new StaticTiles { Name = "shield", Flags = TileFlag.Wearable, Layer = (byte)Layer.TwoHanded };
            TileDataLoader._staticData[3] = new StaticTiles { Name = "bow", Flags = TileFlag.Weapon, Layer = (byte)Layer.TwoHanded };
            _backpack = Add(0x40008801, 0, Layer.Backpack, 1);
            _sword = Add(0x40008802, 1, Layer.OneHanded, 1);
            _shield = Add(0x40008803, 2, Layer.TwoHanded, 1);
            _bow = Add(0x40008804, 3, Layer.Invalid, _backpack.Serial);
            _newSword = Add(0x40008805, 1, Layer.Invalid, _backpack.Serial);
            World.Player.Items = _backpack;
            _backpack.Next = _sword;
            _sword.Next = _shield;
        }

        [Fact]
        public void One_handed_weapon_swap_leaves_equipped_shield_untouched()
        {
            Equip(_newSword);
            Assert.Equal(new[] { _sword.Serial, _newSword.Serial }, Requests().Select(r => r.Serial));
        }

        [Fact]
        public void Two_handed_weapon_then_one_handed_weapon_restores_displaced_shield_in_order()
        {
            Equip(_bow);
            Assert.Equal(new[] { _sword.Serial, _shield.Serial, _bow.Serial }, Requests().Select(r => r.Serial));
            _queue.Clear();
            // Apply the server-observed equipment state before the next click.
            _sword.Container = _shield.Container = _backpack.Serial;
            _bow.Container = 1;
            _bow.Layer = Layer.TwoHanded;
            _backpack.Next = _bow;

            Equip(_sword);

            var requests = Requests();
            Assert.Equal(new[] { _bow.Serial, _sword.Serial, _shield.Serial }, requests.Select(r => r.Serial));
            Assert.Equal(_backpack.Serial, requests[0].Destination);
            Assert.Equal(uint.MaxValue, requests[1].Destination);
            Assert.Equal(Layer.OneHanded, requests[1].Layer);
            Assert.Equal(uint.MaxValue, requests[2].Destination);
            Assert.Equal(Layer.TwoHanded, requests[2].Layer);
        }

        [Fact]
        public void Spellbook_swap_also_restores_displaced_shield()
        {
            Equip(_bow);
            _queue.Clear();
            _sword.Container = _shield.Container = _backpack.Serial;
            _bow.Container = 1;
            _bow.Layer = Layer.TwoHanded;
            _backpack.Next = _bow;
            Item book = Add(0x40008806, 4, Layer.Invalid, _backpack.Serial);

            Equip(book, "Spellbook");

            Assert.Equal(new[] { _bow.Serial, book.Serial, _shield.Serial }, Requests().Select(r => r.Serial));
        }

        [Fact]
        public void Shield_equipped_by_another_tool_is_preserved()
        {
            Equip(_bow);
            _queue.Clear();
            _shield.Container = _backpack.Serial;
            Item otherShield = Add(0x40008806, 2, Layer.TwoHanded, 1);
            _sword.Next = otherShield;

            Equip(_newSword);

            Assert.Equal(new[] { _sword.Serial, _newSword.Serial }, Requests().Select(r => r.Serial));
        }

        [Fact]
        public void Shield_no_longer_owned_is_not_reequipped()
        {
            Equip(_bow);
            _queue.Clear();
            _sword.Container = _backpack.Serial;
            _shield.Container = 2; // transferred to another mobile
            _bow.Container = 1;
            _bow.Layer = Layer.TwoHanded;
            _backpack.Next = _bow;

            Equip(_sword);

            Assert.Equal(new[] { _bow.Serial, _sword.Serial }, Requests().Select(r => r.Serial));
        }

        public void Dispose()
        {
            _queue.Clear();
            foreach (Item item in _items) World.Items.Remove(item.Serial);
            SetPlayer(_originalPlayer);
            TileDataLoader._staticData = _originalTiles;
            typeof(MoveItemQueue).GetProperty(nameof(MoveItemQueue.Instance)).SetValue(null, _originalQueue);
            AutoRearmManager.OneHandedSerial = _originalOneHand;
            AutoRearmManager.TwoHandedSerial = _originalTwoHand;
        }

        private Item Add(uint serial, ushort graphic, Layer layer, uint container)
        {
            var item = new Item { Serial = serial, Graphic = graphic, Layer = layer, Container = container, Amount = 1 };
            World.Items.Add(serial, item);
            _items.Add(item);
            return item;
        }

        private void Equip(Item item, string kind = "Weapon")
        {
            Type cellType = typeof(PaperDollBackpackEquipmentGump).GetNestedType("QuickEquipmentItemControl", BindingFlags.NonPublic);
            Type kindType = typeof(PaperDollBackpackEquipmentGump).GetNestedType("EquipmentKind", BindingFlags.NonPublic);
            object cell = FormatterServices.GetUninitializedObject(cellType);
            cellType.GetField("_equipmentBar", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cell, _bar);
            cellType.GetMethod("Equip", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cell, new[] { (object)item, Enum.Parse(kindType, kind) });
        }

        private (uint Serial, uint Destination, Layer Layer)[] Requests() =>
            ((IEnumerable)typeof(MoveItemQueue).GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_queue)).Cast<object>().Select(r =>
                ((uint)r.GetType().GetProperty("Serial").GetValue(r),
                 (uint)r.GetType().GetProperty("Destination").GetValue(r),
                 (Layer)r.GetType().GetProperty("Layer").GetValue(r))).ToArray();

        private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void SetPlayer(PlayerMobile player) => typeof(World).GetProperty(nameof(World.Player)).SetValue(null, player);
    }
}
