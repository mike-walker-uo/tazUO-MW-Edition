using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldGraphicsComparisonGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly WorldGraphicsSettings _before;
        private WorldGraphicsSettings _after;
        private readonly Label _status;

        internal WorldGraphicsComparisonGump(WorldGraphicsSettings before) : base(0, 0)
        {
            UIManager.GetGump<WorldGraphicsComparisonGump>()?.Dispose();
            _before = before; _after = WorldGraphicsSettings.Capture(_profile);
            Width = 430; Height = 210; WantUpdateSize = false;
            CanMove = true; CanCloseWithEsc = true; CanCloseWithRightClick = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(_status = new Label("Live viewport: AFTER", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 14, Y = 12 });
            Button("Before", 14, 42, () => Preview(_before, "BEFORE"));
            Button("After", 151, 42, () => Preview(_after, "AFTER"));
            string[] names = { "Crisp Classic", "Balanced", "Smooth" };
            for (int i = 0; i < names.Length; i++)
            {
                int preset = i;
                Button(names[i], 14 + i * 137, 80, () => { _after = WorldGraphicsSettings.Preset(preset); Preview(_after, names[preset]); });
            }
            Button("Keep after", 14, 118, () =>
            {
                _after.Apply(_profile); Dispose();
                var options = UIManager.GetGump<ClientEnhancementsGump>();
                if (options != null) options.GraphicsComparisonApplied(_before);
                else if (!string.IsNullOrEmpty(ProfileManager.ProfilePath)) _profile.Save(ProfileManager.ProfilePath, false, true);
            });
            Button("Cancel", 151, 118, Dispose);
            Add(new Label("Close / Esc cancels preview. Keep after saves the chosen settings.", true,
                CustomGumpThemeManager.DimHue, 400, font: 1) { X = 14, Y = 156 });
            CenterXInViewPort(); Y = 35; SetInScreen();
            Preview(_after, "AFTER");
        }
        private void Button(string text, int x, int y, System.Action action)
        {
            var button = new NiceButton(x, y, 128, 30, ButtonAction.Activate, text) { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) action(); };
            Add(button);
        }
        private void Preview(WorldGraphicsSettings settings, string name)
        {
            _status.Text = "Live viewport: " + name;
            WorldGraphicsPreview.Show(_profile, settings);
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || ProfileManager.CurrentProfile != _profile) { Dispose(); return; }
            base.Update();
        }
        public override void Dispose() { if (IsDisposed) return; WorldGraphicsPreview.Clear(); base.Dispose(); }
    }
}
