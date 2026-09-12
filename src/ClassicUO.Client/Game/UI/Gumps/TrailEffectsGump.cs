#region license
// TazUO addition. Movement trail and surface-track controls.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class TrailEffectsGump : Gump
    {
        private const int W = 610;
        private const int H = 490;
        private const int PAD = 12;
        private const int GAP = 6;
        private const int COLS = 3;

        public TrailEffectsGump() : base(0, 0)
        {
            X = 180;
            Y = 90;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.90f));
            Add(new Label("Movement Trail Effects", true, CustomGumpThemeManager.TitleHue, font: 1)
            {
                X = PAD,
                Y = 9
            });
            Add(new Label("Realistic tracks and fantasy trails are independent and may be combined.",
                true, CustomGumpThemeManager.DimHue, W - PAD * 2, font: 1)
            {
                X = PAD,
                Y = 29
            });

            int y = 55;
            AddToggle(PAD, y, 185, "Surface tracks", ProfileManager.CurrentProfile?.FootstepGraphicsEnabled == true,
                enabled =>
                {
                    if (ProfileManager.CurrentProfile != null)
                        ProfileManager.CurrentProfile.FootstepGraphicsEnabled = enabled;
                });
            AddToggle(PAD + 195, y, 185, "Surface particles", SceneryInteractionManager.SurfaceParticlesEnabled,
                enabled => SceneryInteractionManager.SurfaceParticlesEnabled = enabled);
            AddToggle(PAD + 390, y, 185, "Fantasy trail", MoveTrailOverlay.Enabled,
                MoveTrailOverlay.SetEnabled);

            y += 38;
            Add(new Label("FANTASY STYLE", true, CustomGumpThemeManager.GroupHue, font: 1)
            {
                X = PAD,
                Y = y
            });
            y += 22;

            MovementTrailStyle[] styles =
            {
                MovementTrailStyle.None, MovementTrailStyle.Blood, MovementTrailStyle.ShadowSmoke,
                MovementTrailStyle.ArcaneSparks, MovementTrailStyle.Fire, MovementTrailStyle.Frost,
                MovementTrailStyle.Poison, MovementTrailStyle.Holy, MovementTrailStyle.Necromantic,
                MovementTrailStyle.Lightning, MovementTrailStyle.FlowerPetals, MovementTrailStyle.FallingLeaves,
                MovementTrailStyle.GlowingRunes, MovementTrailStyle.Stardust, MovementTrailStyle.Ethereal,
                MovementTrailStyle.Rainbow, MovementTrailStyle.LavaCracks
            };
            int buttonWidth = (W - PAD * 2 - GAP * (COLS - 1)) / COLS;
            for (int i = 0; i < styles.Length; i++)
            {
                MovementTrailStyle style = styles[i];
                int column = i % COLS;
                int row = i / COLS;
                bool selected = MoveTrailOverlay.Style == style;
                AddButton(
                    PAD + column * (buttonWidth + GAP),
                    y + row * 27,
                    buttonWidth,
                    22,
                    (selected ? "● " : string.Empty) + MoveTrailOverlay.GetStyleName(style),
                    () =>
                    {
                        MoveTrailOverlay.SetStyle(style);
                        if (style == MovementTrailStyle.None) MoveTrailOverlay.SetEnabled(false);
                        Reopen();
                    },
                    selected ? (ushort)0x44 : (ushort)0x35);
            }

            y += ((styles.Length + COLS - 1) / COLS) * 27 + 10;
            Add(new Label("TRAIL DENSITY / SIZE", true, CustomGumpThemeManager.GroupHue, font: 1)
            {
                X = PAD,
                Y = y
            });
            y += 23;
            AddSlider("Intensity", y, 25, 200, MoveTrailOverlay.IntensityPercent,
                value => MoveTrailOverlay.IntensityPercent = value, "%");
            y += 34;
            AddSlider("Lifetime", y, 1, 8, Math.Max(1, MoveTrailOverlay.LifetimeMs / 1000),
                value => MoveTrailOverlay.LifetimeMs = value * 1000, " seconds");

            AddButton(PAD, H - 31, W - PAD * 2, 21, "Reset trail defaults", ResetDefaults, 0x35);
        }

        public override GumpType GumpType => GumpType.None;

        private void AddToggle(int x, int y, int width, string label, bool enabled, Action<bool> changed)
        {
            AddButton(x, y, width, 24, label + ": " + (enabled ? "ON" : "OFF"), () =>
            {
                changed(!enabled);
                Reopen();
            }, enabled ? (ushort)0x44 : (ushort)0x21);
        }

        private void AddSlider(string label, int y, int min, int max, int value, Action<int> changed, string suffix)
        {
            Add(new Label(label + " (" + value + suffix + ")", true,
                CustomGumpThemeManager.TextHue, 150, font: 1) { X = PAD, Y = y + 2 });
            var slider = new HSliderBar(165, y + 3, W - 190, min, max, value,
                HSliderBarStyle.MetalWidgetRecessedBar, true, 1, CustomGumpThemeManager.TextHue);
            slider.ValueChanged += (sender, args) => changed(slider.Value);
            Add(slider);
        }

        private void AddButton(int x, int y, int width, int height, string text, Action action, ushort hue)
        {
            var button = new NiceButton(x, y, width, height, ButtonAction.Activate, text, font: 1, hue: hue)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtonType.Left) action();
            };
            Add(button);
        }

        private void ResetDefaults()
        {
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.FootstepGraphicsEnabled = true;
            SceneryInteractionManager.SurfaceParticlesEnabled = true;
            MoveTrailOverlay.SetStyle(MovementTrailStyle.ShadowSmoke);
            MoveTrailOverlay.IntensityPercent = 100;
            MoveTrailOverlay.LifetimeMs = 3000;
            MoveTrailOverlay.SetEnabled(false);
            Reopen();
        }

        private void Reopen()
        {
            int x = X;
            int y = Y;
            Dispose();
            UIManager.Add(new TrailEffectsGump
            {
                X = x,
                Y = y
            });
        }
    }
}
