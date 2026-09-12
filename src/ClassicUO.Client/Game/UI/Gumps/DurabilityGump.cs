using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class DurabilityGumpMinimized : Gump
    {
        public uint Graphic { get; set; } = 5587;

        public DurabilityGumpMinimized() : base(0, 0)
        {
            SetTooltip("Open Equipment Durability Tracker");

            WantUpdateSize = true;
            AcceptMouseInput = true;
            Width = 30;
            Height = 30;
        }

        public override bool AcceptMouseInput => DurabilityManager.HasDurabilityData;

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            ref readonly var texture = ref Client.Game.Gumps.GetGump(Graphic);

            if (texture.Texture != null && DurabilityManager.HasDurabilityData)
            {
                Rectangle rect = new Rectangle(x, y, Width, Height);
                batcher.Draw(texture.Texture, rect, texture.UV, ShaderHueTranslator.GetHueVector(0));
            }

            return base.Draw(batcher, x, y);
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left && DurabilityManager.HasDurabilityData)
            {
                UIManager.GetGump<DurabilitysGump>()?.Dispose();
                UIManager.Add(new DurabilitysGump());
            }
        }
    }

    internal class DurabilitysGump : NineSliceGump
    {
        private static int lastWidth = 300, lastHeight = 400;
        private static int lastX, lastY;

        private enum DurabilityColors
        {
            RED = 0x0805,
            BLUE = 0x0806,
            GREEN = 0x0808,
            YELLOW = 0x0809
        }

        private readonly Dictionary<string, ContextMenuItemEntry> _menuItems = new Dictionary<string, ContextMenuItemEntry>();
        private VBoxContainer _dataBox;
        private ThemedGumpBackground _bgOverlay;

        public enum RepairCraft
        {
            Auto = 0,
            Tinkering,    // jewelry
            Blacksmithy,  // metal armor/weapons
            Carpentry,    // furniture, staves
            Tailoring,    // cloth, leather, bone
            Masonry,      // stone
            Glassblowing, // glass
            Fletching     // bows
        }
        public override GumpType GumpType => GumpType.DurabilityGump;

        public DurabilitysGump() : base(lastX, lastY, lastWidth, lastHeight, ModernUIConstants.ModernUIPanel, ModernUIConstants.ModernUIPanel_BoderSize, true, 200, 200)
        {
            DrawNineSliceBackground = false;
            LayerOrder = UILayer.Default;
            CanCloseWithRightClick = true;
            CanMove = true;

            Width = lastWidth;
            Height = lastHeight;

            X = lastX;
            Y = lastY;

            if (lastX == 0 || lastY == 0)
            {
                X = lastX = (Client.Game.Scene.Camera.Bounds.Width - Width) / 2;
                Y = lastY = Client.Game.Scene.Camera.Bounds.Y + 20;
            }

            Build();
        }

        private void Build()
        {
            Clear();

            float opacityScale = GetOpacityScale();
            Alpha = opacityScale;

            Add(_bgOverlay = CustomGumpThemeManager.CreateBackground(
                Width,
                Height,
                0.78f));
            _bgOverlay.OpacityScaleOverride = opacityScale;

            BuildHeader();

            ScrollArea area = new ScrollArea(10, 30, Width - 20, Height - 50, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };

            Add(area);

            _dataBox = new VBoxContainer(Width - 40);
            area.Add(_dataBox);

            RequestUpdateContents();
        }


        private void BuildHeader()
        {
            Label l = new (
                "Equipment Durability",
                true,
                CustomGumpThemeManager.TitleHue);
            l.X = (Width >> 1) - (l.Width >> 1);
            l.Y = (l.Height >> 1) >> 1;

            Add(l);
        }

        public override void Dispose()
        {
            base.Dispose();
            lastX = X;
            lastY = Y;
        }

        protected override void UpdateContents()
        {
            _dataBox.Clear();
            Rectangle barBounds = Client.Game.Gumps.GetGump((uint)DurabilityColors.RED).UV;

            var items = World.DurabilityManager?.Durabilities ?? Enumerable.Empty<DurabiltyProp>();

            foreach (var durability in items.OrderBy(d => d.Percentage))
            {
                if (durability.MaxDurabilty <= 0)
                {
                    continue;
                }

                var item = World.Items.Get((uint)durability.Serial);

                if (item == null)
                {
                    continue;
                }

                var a = new Area();
                a.AcceptMouseInput = true;
                a.WantUpdateSize = false;
                a.CanMove = true;
                a.Height = 44;
                a.Width = Width - (a.X * 2) - 40;
                var rowBackground = new AlphaBlendControl(0.30f)
                {
                    Width = a.Width,
                    Height = a.Height - 2
                };
                CustomGumpThemeManager.ApplyDataSurface(rowBackground, 0.30f);
                rowBackground.Alpha *= GetOpacityScale();
                a.Add(rowBackground);

                const int REPAIR_BTN_W = 56;
                const int REPAIR_BTN_H = 18;
                int repairBtnX = a.Width - REPAIR_BTN_W - 4;
                ushort durabilityTextHue = durability.Percentage < 0.30f
                    ? (ushort)0x21
                    : durability.Percentage < 0.60f
                        ? (ushort)0x35
                        : CustomGumpThemeManager.TextHue;

                string itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Layer.ToString() : item.Name;
                Label name;
                a.Add(name = new Label(
                    itemName,
                    true,
                    durabilityTextHue,
                    maxwidth: repairBtnX - 8,
                    style: FontStyle.Cropped,
                    ishtml: true));
                name.AcceptMouseInput = true;
                name.SetTooltip(itemName);
                GumpPic red;
                a.Add(red = new GumpPic(0, name.Y + name.Height + 5, (ushort)DurabilityColors.RED, 0));

                DurabilityColors statusGump = DurabilityColors.GREEN;

                if (durability.Percentage < 0.7)
                {
                    statusGump = DurabilityColors.YELLOW;
                }
                else if (durability.Percentage < 0.95)
                {
                    statusGump = DurabilityColors.BLUE;
                }

                if (durability.Percentage > 0)
                {
                    a.Add(new GumpPicTiled(0, red.Y, (int)Math.Floor(barBounds.Width * durability.Percentage), barBounds.Height, (ushort)statusGump));
                }

                var durWidth = FontsLoader.Instance.GetWidthUnicode(0, $"{durability.Durabilty} / {durability.MaxDurabilty}");

                a.Add
                (
                    new Label(
                        $"{durability.Durabilty} / {durability.MaxDurabilty}",
                        true,
                        durabilityTextHue)
                    {
                        Y = red.Y - 2,
                        X = repairBtnX - 6 - durWidth
                    }
                );

                uint itemSerial = item.Serial;
                NiceButton repairBtn = new NiceButton(repairBtnX, name.Y + 1, REPAIR_BTN_W, REPAIR_BTN_H, ButtonAction.Default, "Repair")
                {
                    IsSelectable = false
                };
                CustomGumpThemeManager.StyleDataButton(
                    repairBtn,
                    CustomGumpThemeManager.TextHue);
                repairBtn.SetTooltip("Use a nearby Repair Bench (range 2) on this item");
                repairBtn.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                        TryRepairWithBench(itemSerial);
                };
                a.Add(repairBtn);

                _dataBox.Add(a);
            }
        }

        private static float GetOpacityScale() =>
            (ProfileManager.CurrentProfile?.DurabilityGumpOpacity ?? 100) / 100f;

        internal static void UpdateAllOpacity()
        {
            foreach (DurabilitysGump gump in UIManager.Gumps.OfType<DurabilitysGump>())
                gump.Build();
        }

        // Repair Bench item graphics. 0xA27F is the SA Repair Bench.
        // Add additional graphics here if your shard uses variants.
        private static readonly ushort[] REPAIR_BENCH_GRAPHICS = { 0xA27F };
        private const int REPAIR_BENCH_RANGE = 2;

        // ───── Repair Bench automation state ─────
        // When [Repair] is clicked, we record (itemSerial, craft) and double-click the bench.
        // PacketHandlers.CreateGump consults this on the next received gump and, if the
        // gump is a Repair Bench, auto-clicks the corresponding craft arrow.
        public static uint PendingRepairItem;
        public static RepairCraft PendingRepairCraft;
        public static long PendingRepairExpire; // discard arming if no bench gump arrives in time

        private static void TryRepairWithBench(uint itemSerial)
        {
            if (World.Player == null)
                return;

            Item bench = FindNearestRepairBench();
            if (bench == null)
            {
                GameActions.Print("No Repair Bench within range 2.", 0x21);
                return;
            }

            Item item = World.Items.Get(itemSerial);
            RepairCraft craft = item != null ? GuessCraft(item) : RepairCraft.Blacksmithy;

            PendingRepairItem = itemSerial;
            PendingRepairCraft = craft;
            PendingRepairExpire = (long)Time.Ticks + 5000;

            // Pre-arm auto-target; the bench requests a target after we click the craft arrow.
            TargetManager.SetAutoTarget(itemSerial, TargetType.Neutral, CursorTarget.Object);
            GameActions.DoubleClick(bench.Serial);
        }

        /// <summary>
        /// Heuristic: pick the most likely craft skill for an item from its Layer + name.
        /// User can override per-shard later. Wrong guesses are recoverable — the user
        /// just clicks the correct arrow manually.
        /// </summary>
        public static RepairCraft GuessCraft(Item item)
        {
            if (item == null) return RepairCraft.Blacksmithy;

            switch (item.Layer)
            {
                case Layer.Earrings:
                case Layer.Necklace:
                case Layer.Ring:
                case Layer.Bracelet:
                    return RepairCraft.Tinkering;

                case Layer.OneHanded:
                case Layer.TwoHanded:
                {
                    string n = (item.Name ?? string.Empty).ToLowerInvariant();
                    if (n.Contains("bow") || n.Contains("crossbow") || n.Contains("arrow"))
                        return RepairCraft.Fletching;
                    if (n.Contains("staff") || n.Contains("club"))
                        return RepairCraft.Carpentry;
                    return RepairCraft.Blacksmithy;
                }

                // Wearable armor — default Blacksmithy. User can pick on shard with leather/cloth differentiation.
                default:
                    return RepairCraft.Blacksmithy;
            }
        }

        /// <summary>
        /// Finds the craft-arrow Button at the given row index (0..6 = Tinkering..Fletching)
        /// in the Repair Bench server gump, and submits a server response so the bench targets
        /// our pre-armed item. Returns true if the click was issued.
        /// </summary>
        public static bool AutoClickRepairCraft(Gump benchGump, int rowIndex)
        {
            if (benchGump == null || benchGump.IsDisposed) return false;
            if (rowIndex < 0 || rowIndex > 6) return false;

            // The bench has 7 left-side craft arrows, 7 right-side "Add Charges" balls,
            // plus a "Repair All" and "Add Charges" button. We want the LEFT-COLUMN arrows
            // sorted by Y. Identify candidates by X position (assume left half of gump).
            List<Button> leftButtons = new List<Button>();
            int halfX = benchGump.Width > 0 ? benchGump.Width / 2 : 250;
            foreach (Control c in benchGump.Children)
            {
                if (c is Button b && b.ButtonAction == ButtonAction.Activate && b.X < halfX)
                {
                    leftButtons.Add(b);
                }
            }
            if (leftButtons.Count < 7)
                return false;

            leftButtons.Sort((a, b) => a.Y.CompareTo(b.Y));
            Button target = leftButtons[rowIndex];

            GameActions.ReplyGump(benchGump.LocalSerial, benchGump.ServerSerial, target.ButtonID);
            return true;
        }

        private static Item FindNearestRepairBench()
        {
            Item best = null;
            int bestDist = int.MaxValue;

            foreach (Item it in World.Items.Values)
            {
                if (it == null || it.IsDestroyed || !it.OnGround)
                    continue;

                bool isBench = false;
                for (int g = 0; g < REPAIR_BENCH_GRAPHICS.Length; g++)
                {
                    if (it.Graphic == REPAIR_BENCH_GRAPHICS[g]) { isBench = true; break; }
                }
                if (!isBench)
                    continue;

                int d = it.Distance;
                if (d > REPAIR_BENCH_RANGE) continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = it;
                }
            }

            return best;
        }

        protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
            Build();
            lastWidth = newWidth;
            lastHeight = newHeight;
            if (_bgOverlay != null)
            {
                _bgOverlay.Width = newWidth;
                _bgOverlay.Height = newHeight;
            }
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);

            writer.WriteAttributeString("lastX", X.ToString());
            writer.WriteAttributeString("lastY", Y.ToString());
            writer.WriteAttributeString("lastWidth", lastWidth.ToString());
            writer.WriteAttributeString("lastHeight", lastHeight.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);

            int.TryParse(xml.GetAttribute("lastX"), out X);
            int.TryParse(xml.GetAttribute("lastY"), out Y);
            int.TryParse(xml.GetAttribute("lastWidth"), out Width);
            int.TryParse(xml.GetAttribute("lastHeight"), out Height);
            Build();
        }
    }
}
