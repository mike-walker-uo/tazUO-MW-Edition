using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class QueuedOperationIndicatorGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly Label _text;
        private long _refreshAt;
        internal QueuedOperationIndicatorGump() : base(0, 0)
        {
            Width = 220; Height = 30; WantUpdateSize = false;
            CanCloseWithRightClick = true; CanCloseWithEsc = true;
            var button = new NiceButton(0, 0, Width, Height, ButtonAction.Activate, "") { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                var details = UIManager.GetGump<QueuedOperationsGump>();
                if (details == null) UIManager.Add(new QueuedOperationsGump()); else details.BringOnTop();
            };
            Add(button);
            Add(_text = new Label("Moving...", true, CustomGumpThemeManager.TextHue, 200, font: 1)
                { X = 10, Y = 6, AcceptMouseInput = false });
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile || !_profile.CompactTransferProgress) { Dispose(); return; }
            base.Update();
            var bounds = Client.Game.Scene.Camera.Bounds;
            X = System.Math.Max((int)(bounds.X / UIManager.InterfaceScale), (int)(bounds.Right / UIManager.InterfaceScale) - Width - 8);
            Y = (int)(bounds.Y / UIManager.InterfaceScale) + 8;
            if (Time.Ticks < _refreshAt) return;
            _refreshAt = (long)Time.Ticks + 250;
            int active = QueuedOperations.ActiveProgress(out int completed, out int total);
            if (active == 0) { Dispose(); return; }
            string text = total == 0 ? "Collecting items..." : $"Moving {completed}/{total}";
            if (active > 1) text += $" ({active} queues)";
            if (_text.Text != text) _text.Text = text;
        }
    }
}
