#region license
// TazUO addition.
#endregion

using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using SDL2;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Drag-handle + resize control for the toast stack. Dragging changes
    /// ToastManager.AnchorX/Y; the right-edge resize handle changes
    /// ToastManager.ToastWidth. Position and width are persisted to
    /// toast.tsv on Dispose.
    /// </summary>
    internal class ToastAnchorGump : Gump
    {
        private const int HANDLE_H = 32;
        private const int RESIZE_GRIP = 10;

        private long _nextSample;
        private int _lastX, _lastY;
        private AlphaBlendControl _bg;
        private Label _info;

        private bool _resizing;
        private int _resizeStartMouseX;
        private int _resizeStartWidth;

        public ToastAnchorGump() : base(0, 0)
        {
            ToastManager.EnsureLoaded();

            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            LayerOrder = UILayer.Over;
            Width = System.Math.Max(140, ToastManager.ToastWidth);
            Height = HANDLE_H;

            var camera = Client.Game.Scene?.Camera;
            int screenW = camera != null ? camera.Bounds.Right : 800;
            int screenH = camera != null ? camera.Bounds.Bottom : 600;
            X = screenW - Width - 12 + ToastManager.AnchorX;
            Y = screenH - 60 + ToastManager.AnchorY;
            _lastX = X; _lastY = Y;

            Add(_bg = new AlphaBlendControl(0.78f) { Width = Width, Height = HANDLE_H });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.78f);
            Add(_info = new Label("Drag • right-edge resize • RClick to save", true, 0x0481, font: 1) { X = 6, Y = 7 });
        }

        public override GumpType GumpType => GumpType.None;

        protected override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left && x >= Width - RESIZE_GRIP)
            {
                _resizing = true;
                _resizeStartMouseX = Mouse.Position.X;
                _resizeStartWidth = Width;
                CanMove = false;
                return;
            }
            base.OnMouseDown(x, y, button);
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (_resizing)
            {
                _resizing = false;
                CanMove = true;
                return;
            }
            base.OnMouseUp(x, y, button);
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;

            if (_resizing)
            {
                int delta = Mouse.Position.X - _resizeStartMouseX;
                int newW = _resizeStartWidth + delta;
                if (newW < 140) newW = 140;
                if (newW > 800) newW = 800;
                Width = newW;
                _bg.Width = newW;
                ToastManager.ToastWidth = newW;
            }

            if (X != _lastX || Y != _lastY)
            {
                _lastX = X; _lastY = Y;
                var camera = Client.Game.Scene?.Camera;
                int screenW = camera != null ? camera.Bounds.Right : 800;
                int screenH = camera != null ? camera.Bounds.Bottom : 600;
                ToastManager.AnchorX = X - (screenW - Width - 12);
                ToastManager.AnchorY = Y - (screenH - 60);
            }

            if (Time.Ticks >= _nextSample)
            {
                _nextSample = (long)Time.Ticks + 1000;
                ToastManager.Show("Toast anchor preview", 0x44, 1100);
            }
        }

        public override void Dispose()
        {
            ToastManager.Save();
            base.Dispose();
        }
    }
}
