#region license
// TazUO addition. Persistent water and terrain material intensity controls.
#endregion

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MaterialIntensityGump : Gump
    {
        private const int W = 440;
        private const int H = 350;
        private readonly HSliderBar[] _terrainSliders = new HSliderBar[6];
        private HSliderBar _waterSlider;
        private HSliderBar _allSlider;

        public MaterialIntensityGump() : base(0, 0)
        {
            X = 220;
            Y = 120;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.90f));
            Add(new Label("Material Intensity", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 12, Y = 9 });
            Add(new Label("0% = original art only, 100% = balanced, 200% = strongest detail",
                true, CustomGumpThemeManager.DimHue, W - 24, font: 1) { X = 12, Y = 28 });

            int y = 54;
            _waterSlider = AddSlider("Water", y, WaterEnhancementManager.IntensityPercent,
                value => WaterEnhancementManager.SetIntensity(value));
            y += 31;
            int common = TerrainMaterialManager.CommonIntensityPercent;
            _allSlider = AddSlider("All terrain preset", y, common < 0 ? 100 : common,
                ApplyAllTerrain);
            y += 31;

            ScenerySurface[] surfaces =
            {
                ScenerySurface.Sand,
                ScenerySurface.Grass,
                ScenerySurface.Mine,
                ScenerySurface.Dungeon,
                ScenerySurface.Dirt,
                ScenerySurface.Snow
            };
            string[] labels = { "Sand / desert", "Grass", "Mines", "Dungeons", "Dirt", "Snow" };
            string[] names = { "sand", "grass", "mine", "dungeon", "dirt", "snow" };
            for (int i = 0; i < surfaces.Length; i++)
            {
                int index = i;
                _terrainSliders[i] = AddSlider(labels[i], y,
                    TerrainMaterialManager.GetIntensityPercent(surfaces[i]),
                    value => TerrainMaterialManager.SetIntensity(names[index], value));
                y += 31;
            }

            AddButton(12, H - 29, W - 24, 20,
                "Reset defaults (water 200%, terrain 100%)", ResetAll);
        }

        public override GumpType GumpType => GumpType.None;

        private HSliderBar AddSlider(string label, int y, int value, Action<int> changed)
        {
            Add(new Label(label, true, CustomGumpThemeManager.TextHue, 118, font: 1) { X = 12, Y = y + 2 });
            var slider = new HSliderBar(132, y + 3, 250, 0, 200, value,
                HSliderBarStyle.MetalWidgetRecessedBar, true, 1, CustomGumpThemeManager.TextHue);
            slider.ValueChanged += (sender, e) => changed(slider.Value);
            Add(slider);
            return slider;
        }

        private void ApplyAllTerrain(int value)
        {
            TerrainMaterialManager.SetIntensity("all", value);
            for (int i = 0; i < _terrainSliders.Length; i++)
            {
                if (_terrainSliders[i] != null) _terrainSliders[i].Value = value;
            }
        }

        private void ResetAll()
        {
            _waterSlider.Value = WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT;
            _allSlider.Value = 100;
            WaterEnhancementManager.SetIntensity(
                WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT);
            TerrainMaterialManager.SetIntensity("all", 100);
        }

        private void AddButton(int x, int y, int width, int height, string text, Action action)
        {
            var button = new NiceButton(x, y, width, height, ButtonAction.Activate, text,
                font: 1, hue: 0x35)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtonType.Left) action();
            };
            Add(button);
        }

    }
}
