using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class CompactPartyHudGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;

        internal CompactPartyHudGump() : base(0, 0)
        {
            Width = 160;
            Height = CompactPartyHud.RowCount * 16 + 4;
            CanMove = AcceptMouseInput = true;
            CanCloseWithRightClick = WantUpdateSize = false;
            Rectangle bounds = Client.Game.Scene.Camera.Bounds;
            Point saved = _profile?.CompactPartyHudPosition ?? new Point(-1, -1);
            X = saved.X >= 0 ? saved.X : (int)(bounds.Right / UIManager.InterfaceScale) - Width - 20;
            Y = saved.Y >= 0 ? saved.Y : (int)(bounds.Y / UIManager.InterfaceScale) + 108;
            SetInScreen();
            SetTooltip("Drag to move the Party HUD. Use -partyhud off to hide it.");
        }

        public override bool ShouldBeSaved => false; // Enabled state and position are stored separately.

        public override void Update()
        {
            int rows = CompactPartyHud.RowCount;
            if (rows == 0) { Dispose(); return; }
            Height = rows * 16 + 4;
            base.Update();
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed) return false;
            FeatureDiagnostics.Guard("CompactPartyHud", batcher, new Point(x, y),
                (b, position) => CompactPartyHud.Draw(b, position.X, position.Y));
            return base.Draw(batcher, x, y);
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            RememberPosition();
        }

        private void RememberPosition()
        {
            if (_profile != null) _profile.CompactPartyHudPosition = new Point(X, Y);
        }

        public override void Dispose()
        {
            if (!IsDisposed) RememberPosition();
            base.Dispose();
        }
    }
}
