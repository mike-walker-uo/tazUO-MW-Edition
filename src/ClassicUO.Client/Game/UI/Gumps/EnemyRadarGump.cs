using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EnemyRadarGump : Gump
    {
        private const int W = 480;
        private const int H = 325;

        public EnemyRadarGump() : base(0, 0)
        {
            X = 180; Y = 100; Width = W; Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.9f));
            Add(new Label("Enemy radar", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 12, Y = 10 });
            Button(340, 8, 128, EnemyRadar.Enabled ? "Radar: ON" : "Radar: OFF", () =>
            {
                EnemyRadar.Enabled = !EnemyRadar.Enabled;
                Reopen();
            });

            string[] names = { "Whisper ring", "Soft density", "Fading threads" };
            for (int i = 0; i < names.Length; i++)
            {
                int style = i;
                Button(12 + i * 154, 46, 148, (EnemyRadar.Style == i ? "* " : "") + names[i], () =>
                {
                    EnemyRadar.Style = style;
                    EnemyRadar.Enabled = true;
                    Reopen();
                });
            }
            string[] descriptions =
            {
                "Fine ring and enemy dots. Coral = nearby; outlined dot = attack target.",
                "Soft directional patches. More mobs strengthen the tint; coral = close or crowded.",
                "Faint threads from your feet toward enemies, fading out. Crowds share directions."
            };
            Add(new Label(descriptions[EnemyRadar.Style], true, CustomGumpThemeManager.DimHue, W - 24, font: 1)
                { X = 12, Y = 78 });

            Slider("Size", 126, 100, 400, EnemyRadar.Size, value => EnemyRadar.Size = value);
            Slider("Opacity (%)", 158, 5, 100, EnemyRadar.Opacity, value => EnemyRadar.Opacity = value);
            Slider("Range (tiles)", 190, 1, Constants.MAX_VIEW_RANGE, EnemyRadar.Range, value => EnemyRadar.Range = value);
            var neutral = new Checkbox(0x00D2, 0x00D3, "Include grey / neutral mobs", 1,
                CustomGumpThemeManager.TextHue) { X = 12, Y = 226, IsChecked = EnemyRadar.IncludeNeutral };
            neutral.ValueChanged += (sender, args) =>
            {
                EnemyRadar.IncludeNeutral = neutral.IsChecked;
                TazUOFeatureSettings.Save();
            };
            Add(neutral);
            Add(new Label("Server-known contacts only; range also follows client view distance.\nHotkey: a ClientCommand macro with radar toggles the radar.",
                true, CustomGumpThemeManager.DimHue, W - 24, font: 1) { X = 12, Y = 260 });
        }

        private void Button(int x, int y, int width, string label, Action action)
        {
            var button = new NiceButton(x, y, width, 25, ButtonAction.Activate, label, font: 1)
                { IsSelectable = false, DisplayBorder = true };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtonType.Left) action();
            };
            Add(button);
        }

        private void Slider(string label, int y, int min, int max, int value, Action<int> changed)
        {
            Add(new Label(label, true, CustomGumpThemeManager.TextHue, font: 1) { X = 12, Y = y });
            var slider = new HSliderBar(150, y, W - 174, min, max, value,
                HSliderBarStyle.MetalWidgetRecessedBar, true, 1, CustomGumpThemeManager.TextHue);
            slider.ValueChanged += (sender, args) => changed(slider.Value);
            Add(slider);
        }

        private void Reopen()
        {
            TazUOFeatureSettings.Save();
            int x = X, y = Y;
            Dispose();
            UIManager.Add(new EnemyRadarGump { X = x, Y = y });
        }

        public override void Dispose()
        {
            if (!IsDisposed) TazUOFeatureSettings.Save();
            base.Dispose();
        }
    }
}
