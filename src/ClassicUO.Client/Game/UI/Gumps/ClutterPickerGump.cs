using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ClutterPickerGump : Gump
    {
        private readonly List<uint> _serials;
        private readonly bool _targeting = TargetManager.IsTargeting;
        private readonly Profile _profile = ProfileManager.CurrentProfile;

        private ClutterPickerGump(List<Entity> entities) : base(0, 0)
        {
            _serials = entities.Select(e => e.Serial).ToList();
            Width = 420; Height = 100 + Math.Min(8, entities.Count) * 48;
            WantUpdateSize = false; CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true; AcceptMouseInput = true;
            X = (int)(Mouse.Position.X / UIManager.InterfaceScale) + 12;
            Y = (int)(Mouse.Position.Y / UIManager.InterfaceScale) + 12;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 1f));
            Add(new Label("PICK THROUGH CLUTTER", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 18, Y = 16 });
            Add(new Label(_targeting ? "Choose the exact target. Right-click / Esc cancels." : "Choose an object to use. Right-click / Esc closes.",
                true, CustomGumpThemeManager.DimHue, 380, font: 1) { X = 18, Y = 40 });
            var area = new ScrollArea(16, 70, 388, Height - 85, true);
            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                string name = World.OPL.TryGetNameAndData(entity.Serial, out string n, out _) ? n : entity.Name;
                if (string.IsNullOrWhiteSpace(name)) name = entity is Mobile ? "Mobile" : "Item";
                if (entity is Item item)
                    area.Add(new ResizableStaticPic(item.Graphic, 36, 36) { X = 0, Y = i * 48, Hue = item.Hue, AcceptMouseInput = false });
                var button = new NiceButton(42, i * 48, 320, 42, ButtonAction.Activate,
                    name + $"  /  {entity.Serial:X8}", hue: CustomGumpThemeManager.TextHue, font: 1)
                    { IsSelectable = false, ButtonParameter = i + 1 };
                button.SetTooltip(entity.Serial); area.Add(button);
            }
            Add(area); SetInScreen();
        }

        internal static void Open()
        {
            if (!World.InGame || !UIManager.IsMouseOverWorld || ExplorationMode.Active || Client.Game.GameCursor.ItemHold.Enabled) return;
            var candidates = new List<Entity>();
            var seen = new HashSet<uint>();
            var previous = SelectedObject.Object;
            SelectedObject.TranslatedMousePositionByViewport = Client.Game.Scene.Camera.MouseToWorldPosition();
            try
            {
                foreach (Entity entity in Client.Game.GetScene<Scenes.GameScene>().ClutterCandidates())
                {
                    if (!seen.Add(entity.Serial) || entity.IsDestroyed || !entity.AllowedToDraw || entity.Distance > World.ClientViewRange
                        || entity is Item item && !item.OnGround || entity.FrameInfo.Width == 0 && entity is Mobile) continue;
                    SelectedObject.Object = null; // Existing item hit tests skip the already-selected object.
                    if (entity.CheckMouseSelection()) candidates.Add(entity);
                    if (candidates.Count >= 64) break;
                }
            }
            finally { SelectedObject.Object = previous; }
            if (candidates.Count == 0) { GameActions.Print("No selectable objects under the pointer.", 0x35); return; }
            UIManager.GetGump<ClutterPickerGump>()?.Dispose();
            UIManager.Add(new ClutterPickerGump(candidates.OrderByDescending(e => e.Z).ThenBy(e => e.Serial).ToList()));
            Mouse.CancelDoubleClick = true;
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID < 1 || buttonID > _serials.Count) return;
            Entity entity = World.Get(_serials[buttonID - 1]);
            if (entity == null || entity.IsDestroyed) { GameActions.Print("Object is no longer available.", 0x35); Dispose(); return; }
            if (_targeting)
            {
                if (TargetManager.IsTargeting) TargetManager.Target(entity.Serial);
                else GameActions.Print("The targeting cursor is no longer active.", 0x35);
            }
            else if (!TargetManager.IsTargeting) GameActions.DoubleClick(entity.Serial);
            Dispose();
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            base.Update();
        }
    }
}
