using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ExplorationModeGump : Gump
    {
        private readonly Label _position;
        private long _refreshAt;
        internal ExplorationModeGump() : base(0, 0)
        {
            Width = 510; Height = 110; WantUpdateSize = false;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("EXPLORATION  •  Character remains live", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 12, Y = 10 });
            Add(_position = new Label("", true, CustomGumpThemeManager.TextHue, 380, font: 1) { X = 12, Y = 34 });
            Add(new Label("Right mouse / WASD / arrows  •  Q: rise  •  E: descend", true,
                CustomGumpThemeManager.DimHue, 484, font: 1) { X = 12, Y = 64 });
            Add(new Label("Shift: faster  •  Wheel: zoom  •  Enter: chat  •  Esc: return", true,
                CustomGumpThemeManager.DimHue, 484, font: 1) { X = 12, Y = 84 });
            var button = new NiceButton(406, 30, 90, 28, ButtonAction.Activate, "Return") { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) ExplorationMode.Stop(); };
            Add(button);
            var bounds = Client.Game.Scene.Camera.Bounds;
            X = (int)(bounds.X / UIManager.InterfaceScale) + 12;
            Y = (int)(bounds.Y / UIManager.InterfaceScale) + 12;
            SetInScreen();
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!ExplorationMode.Active) { Dispose(); return; }
            base.Update();
            if (Time.Ticks < _refreshAt) return;
            _refreshAt = (long)Time.Ticks + 250;
            _position.Text = $"Bird: {(int)ExplorationMode.Position.X}, {(int)ExplorationMode.Position.Y}  •  Z {ExplorationMode.FlightZ:0}  •  Map {World.MapIndex}";
        }
        public override void Dispose() { if (IsDisposed) return; base.Dispose(); ExplorationMode.Stop(); }
    }
}
