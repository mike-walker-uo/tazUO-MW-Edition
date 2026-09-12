// TazUO MW Edition addition.
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ProfileRecoveryGump : Gump
    {
        private readonly CrashRecoveryManager.SnapshotInfo[] _snapshots;

        public ProfileRecoveryGump() : base(0, 0)
        {
            X = 200;
            Y = 180;
            Width = 390;
            _snapshots = CrashRecoveryManager.GetSnapshots();
            Height = 64 + _snapshots.Length * 28;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            var background = new AlphaBlendControl(0.85f) { Width = Width, Height = Height };
            CustomGumpThemeManager.ApplyDataSurface(background, 0.85f);
            Add(background);
            Add(new Label("Profile Recovery", true, 0x0481, font: 1) { X = 8, Y = 6 });
            Add(new Label("Select a snapshot. It will be restored on next client start.", true, 0x03B2, font: 1) { X = 8, Y = 24 });

            if (_snapshots.Length == 0)
                Add(new Label("No recovery snapshots available yet.", true, 0x0021, font: 1) { X = 8, Y = 46 });

            for (int i = 0; i < _snapshots.Length; i++)
            {
                var button = new NiceButton(8, 48 + i * 28, Width - 16, 22,
                    ButtonAction.Activate, _snapshots[i].Created.ToString("yyyy-MM-dd HH:mm:ss"))
                { ButtonParameter = i + 1, IsSelectable = false };
                Add(button);
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            int index = buttonID - 1;
            if (index < 0 || index >= _snapshots.Length) return;
            if (CrashRecoveryManager.QueueRestore(_snapshots[index].Stamp))
            {
                GameActions.Print("Profile recovery queued. Restart the client to apply it.", 0x35);
                Dispose();
            }
        }
    }
}
