// TazUO addition: backpack weapon, shield, talisman and spellbook picker attached to the paperdoll.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PaperDollBackpackEquipmentGump : Gump
    {
        private const int CELL = 30;
        private const int HEADER_HEIGHT = 18;
        private const int MAX_ROWS = 9;
        private const int PADDING = 5;
        private const int CATEGORY_GAP = 4;
        private const int OWNER_GAP = 3;
        private const uint SCAN_INTERVAL = 500;

        private readonly PaperDollGump _owner;
        private uint _nextScan;
        private int _contentSignature = int.MinValue;

        public PaperDollBackpackEquipmentGump(PaperDollGump owner) : base(0, 0)
        {
            _owner = owner;
            CanMove = false;
            CanCloseWithRightClick = false;
            CanBeLocked = false;
            AcceptMouseInput = false;
            BuildContents();
            UpdatePosition();
        }

        public override bool ShouldBeSaved => false;

        public override void Update()
        {
            if (_owner == null || _owner.IsDisposed || World.Player == null)
            {
                Dispose();
                return;
            }

            IsVisible = _owner.IsVisible && !_owner.IsMinimized;

            if (!IsVisible)
            {
                return;
            }

            if (Time.Ticks >= _nextScan)
            {
                _nextScan = Time.Ticks + SCAN_INTERVAL;
                int signature = GetContentSignature();

                if (signature != _contentSignature)
                {
                    BuildContents();
                }
            }

            UpdatePosition();
            base.Update();
        }

        private void UpdatePosition()
        {
            X = _owner.X - Width - OWNER_GAP;
            Y = _owner.Y + Math.Max(0, (_owner.Height - Height) / 2);
        }

        private void BuildContents()
        {
            List<Item> weapons = new List<Item>();
            List<Item> shields = new List<Item>();
            List<Item> talismans = new List<Item>();
            List<Item> spellbooks = new List<Item>();
            CollectBackpackItems(weapons, shields, talismans, spellbooks);

            int signature = CalculateSignature(weapons, shields, talismans, spellbooks);
            int categoryCount = (weapons.Count > 0 ? 1 : 0)
                + (shields.Count > 0 ? 1 : 0)
                + (talismans.Count > 0 ? 1 : 0)
                + (spellbooks.Count > 0 ? 1 : 0);

            if (categoryCount == 0)
            {
                Clear();
                Width = 0;
                Height = 0;
                _contentSignature = signature;
                return;
            }

            int weaponColumns = (weapons.Count + MAX_ROWS - 1) / MAX_ROWS;
            int shieldColumns = (shields.Count + MAX_ROWS - 1) / MAX_ROWS;
            int talismanColumns = (talismans.Count + MAX_ROWS - 1) / MAX_ROWS;
            int spellbookColumns = (spellbooks.Count + MAX_ROWS - 1) / MAX_ROWS;
            int totalColumns = weaponColumns + shieldColumns + talismanColumns + spellbookColumns;
            int visibleRows = Math.Min(
                MAX_ROWS,
                Math.Max(weapons.Count, Math.Max(shields.Count, Math.Max(talismans.Count, spellbooks.Count)))
            );

            Width = PADDING * 2 + totalColumns * CELL + CATEGORY_GAP * (categoryCount - 1);
            Height = PADDING * 2 + HEADER_HEIGHT + visibleRows * CELL;

            Clear();
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.88f));

            int column = 0;
            int category = 0;

            if (weapons.Count > 0)
            {
                AddCategory("W", "Weapons", weapons, EquipmentKind.Weapon, column, weaponColumns, category++);
                column += weaponColumns;
            }

            if (shields.Count > 0)
            {
                AddCategory("S", "Shields", shields, EquipmentKind.Shield, column, shieldColumns, category++);
                column += shieldColumns;
            }

            if (talismans.Count > 0)
            {
                AddCategory("T", "Talismans", talismans, EquipmentKind.Talisman, column, talismanColumns, category++);
                column += talismanColumns;
            }

            if (spellbooks.Count > 0)
            {
                AddCategory("B", "Spellbooks", spellbooks, EquipmentKind.Spellbook, column, spellbookColumns, category);
            }

            _contentSignature = signature;
            UpdatePosition();
        }

        private void AddCategory(
            string caption,
            string tooltip,
            List<Item> items,
            EquipmentKind kind,
            int firstColumn,
            int columnCount,
            int categoryIndex)
        {
            int categoryOffset = categoryIndex * CATEGORY_GAP;
            int startX = PADDING + firstColumn * CELL + categoryOffset;
            int categoryWidth = columnCount * CELL;
            Label label = new Label(caption, true, CustomGumpThemeManager.TitleHue, font: 1);
            label.X = startX + (categoryWidth - label.Width) / 2;
            label.Y = PADDING;
            Add(label);

            HitBox headerTooltip = new HitBox(startX, PADDING, categoryWidth, HEADER_HEIGHT);
            headerTooltip.SetTooltip(tooltip);
            Add(headerTooltip);

            for (int i = 0; i < items.Count; i++)
            {
                int localColumn = i / MAX_ROWS;
                int row = i % MAX_ROWS;
                Add(new QuickEquipmentItemControl(items[i], kind)
                {
                    X = startX + localColumn * CELL + 2,
                    Y = PADDING + HEADER_HEIGHT + row * CELL + 2
                });
            }
        }

        private static void CollectBackpackItems(
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (backpack != null)
            {
                CollectContainer(backpack, weapons, shields, talismans, spellbooks);
            }
        }

        private static void CollectContainer(
            Item container,
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks)
        {
            for (var node = container.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed)
                {
                    continue;
                }

                Layer layer = (Layer)item.ItemData.Layer;

                if (SpellbookOpenerManager.IsSpellbook(item))
                {
                    spellbooks.Add(item);
                }
                else if (IsShield(item))
                {
                    shields.Add(item);
                }
                else if (item.ItemData.IsWeapon && (layer == Layer.OneHanded || layer == Layer.TwoHanded))
                {
                    weapons.Add(item);
                }
                else if (layer == Layer.Talisman)
                {
                    talismans.Add(item);
                }

                if (item.ItemData.IsContainer && !SpellbookOpenerManager.IsSpellbook(item))
                {
                    CollectContainer(item, weapons, shields, talismans, spellbooks);
                }
            }
        }

        private static int GetContentSignature()
        {
            List<Item> weapons = new List<Item>();
            List<Item> shields = new List<Item>();
            List<Item> talismans = new List<Item>();
            List<Item> spellbooks = new List<Item>();
            CollectBackpackItems(weapons, shields, talismans, spellbooks);
            return CalculateSignature(weapons, shields, talismans, spellbooks);
        }

        private static int CalculateSignature(
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks)
        {
            unchecked
            {
                int hash = 17;
                hash = AddItemsToSignature(hash, weapons, 1);
                hash = AddItemsToSignature(hash, shields, 2);
                hash = AddItemsToSignature(hash, talismans, 3);
                hash = AddItemsToSignature(hash, spellbooks, 4);
                hash = hash * 31 + (int)CustomGumpThemeManager.Current;
                return hash;
            }
        }

        private static int AddItemsToSignature(int hash, List<Item> items, int category)
        {
            unchecked
            {
                hash = hash * 31 + category;
                hash = hash * 31 + items.Count;

                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    hash = hash * 31 + (int)item.Serial;
                    hash = hash * 31 + item.DisplayedGraphic;
                    hash = hash * 31 + item.Hue;
                }

                return hash;
            }
        }

        private static bool IsShield(Item item)
        {
            if (
                item == null
                || (Layer)item.ItemData.Layer != Layer.TwoHanded
                || !item.ItemData.IsWearable
                || item.ItemData.IsLight
            )
            {
                return false;
            }

            if (!item.ItemData.IsWeapon)
            {
                return true;
            }

            string tileName = item.ItemData.Name;
            return !string.IsNullOrEmpty(tileName)
                && (
                    tileName.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0
                    || tileName.IndexOf("buckler", StringComparison.OrdinalIgnoreCase) >= 0
                );
        }

        private enum EquipmentKind : byte
        {
            Weapon,
            Shield,
            Talisman,
            Spellbook
        }

        private sealed class QuickEquipmentItemControl : Control
        {
            private readonly EquipmentKind _kind;

            public QuickEquipmentItemControl(Item item, EquipmentKind kind)
            {
                LocalSerial = item.Serial;
                _kind = kind;
                Width = CELL - 4;
                Height = CELL - 4;
                AcceptMouseInput = true;
                CanMove = false;

                AlphaBlendControl background = new AlphaBlendControl(0.60f)
                {
                    Width = Width,
                    Height = Height
                };
                CustomGumpThemeManager.ApplyDataSurface(background, 0.60f);
                Add(background);
                SetTooltip(item.Serial);
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                base.OnMouseUp(x, y, button);

                if (button != MouseButtonType.Left)
                {
                    return;
                }

                Item item = World.Items.Get(LocalSerial);

                if (item == null || item.IsDestroyed)
                {
                    Dispose();
                    return;
                }

                if (TargetManager.IsTargeting)
                {
                    TargetManager.Target(item.Serial);
                    return;
                }

                Equip(item, _kind);
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                Item item = World.Items.Get(LocalSerial);

                if (item == null || item.IsDestroyed)
                {
                    Dispose();
                    return false;
                }

                base.Draw(batcher, x, y);

                bool equipped = World.Player != null && item.Container == World.Player.Serial;
                Color borderColor = equipped ? Color.LimeGreen : MouseIsOver ? Color.Gold : Color.Gray;
                Vector3 borderHue = ShaderHueTranslator.GetHueVector(0, false, equipped || MouseIsOver ? 0.9f : 0.45f);
                batcher.DrawRectangle(
                    SolidColorTextureCache.GetTexture(borderColor),
                    x,
                    y,
                    Width,
                    Height,
                    borderHue
                );

                ref readonly var texture = ref Client.Game.Arts.GetArt((uint)item.DisplayedGraphic);
                Rectangle bounds = Client.Game.Arts.GetRealArtBounds((uint)item.DisplayedGraphic);

                if (texture.Texture == null || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return true;
                }

                int available = Width - 4;
                float scale = Math.Min(1f, Math.Min((float)available / bounds.Width, (float)available / bounds.Height));
                int drawWidth = Math.Max(1, (int)(bounds.Width * scale));
                int drawHeight = Math.Max(1, (int)(bounds.Height * scale));
                int drawX = x + (Width - drawWidth) / 2;
                int drawY = y + (Height - drawHeight) / 2;
                Vector3 hue = ShaderHueTranslator.GetHueVector(
                    MouseIsOver ? (ushort)0x0035 : item.Hue,
                    item.ItemData.IsPartialHue,
                    1,
                    true
                );

                batcher.Draw(
                    texture.Texture,
                    new Rectangle(drawX, drawY, drawWidth, drawHeight),
                    new Rectangle(texture.UV.X + bounds.X, texture.UV.Y + bounds.Y, bounds.Width, bounds.Height),
                    hue
                );

                return true;
            }

            private static void Equip(Item item, EquipmentKind kind)
            {
                if (World.Player == null || World.Player.IsDead || MoveItemQueue.Instance == null)
                {
                    return;
                }

                Item backpack = World.Player.FindItemByLayer(Layer.Backpack);

                if (backpack == null)
                {
                    GameActions.Print("No backpack.", 0x21);
                    return;
                }

                Layer targetLayer = kind == EquipmentKind.Spellbook
                    ? Layer.OneHanded
                    : kind == EquipmentKind.Shield
                        ? Layer.TwoHanded
                        : (Layer)item.ItemData.Layer;

                if (kind == EquipmentKind.Spellbook)
                {
                    QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);
                    AutoRearmManager.OneHandedSerial = item.Serial;
                }
                else if (kind == EquipmentKind.Weapon)
                {
                    if (targetLayer == Layer.OneHanded)
                    {
                        QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);

                        Item offHand = World.Player.FindItemByLayer(Layer.TwoHanded);

                        if (offHand != null && offHand.ItemData.IsWeapon && !IsShield(offHand))
                        {
                            QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                            AutoRearmManager.TwoHandedSerial = 0;
                        }

                        AutoRearmManager.OneHandedSerial = item.Serial;
                    }
                    else
                    {
                        QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);
                        QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                        AutoRearmManager.OneHandedSerial = 0;
                        AutoRearmManager.TwoHandedSerial = item.Serial;
                    }
                }
                else if (kind == EquipmentKind.Shield)
                {
                    QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                    AutoRearmManager.TwoHandedSerial = item.Serial;
                }
                else
                {
                    QueueUnequip(Layer.Talisman, item.Serial, backpack.Serial);
                }

                if (item.Container != World.Player.Serial)
                {
                    MoveItemQueue.Instance.EnqueueEquipSingle(item.Serial, targetLayer);
                }
            }

            private static void QueueUnequip(Layer layer, uint selectedSerial, uint backpackSerial)
            {
                Item equipped = World.Player.FindItemByLayer(layer);

                if (equipped != null && equipped.Serial != selectedSerial)
                {
                    MoveItemQueue.Instance.Enqueue(equipped.Serial, backpackSerial);
                }
            }
        }
    }
}
