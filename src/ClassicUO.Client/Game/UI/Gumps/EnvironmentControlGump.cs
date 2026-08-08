#region license
// TazUO addition. Clickable environment preview and scenery control panel.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EnvironmentControlGump : Gump
    {
        private const int W = 720;
        private const int H = 570;
        private const int PAD = 12;
        private const int COL_W = 224;
        private const int GAP = 12;
        private const int BUTTON_H = 20;
        private const int BUTTON_GAP = 4;
        private static ushort TITLE_HUE => CustomGumpThemeManager.TitleHue;
        private static ushort GROUP_HUE => CustomGumpThemeManager.GroupHue;
        private static ushort TEXT_HUE => CustomGumpThemeManager.TextHue;
        private static ushort DIM_HUE => CustomGumpThemeManager.DimHue;

        private readonly Label _status;
        private uint _nextStatusRefresh;

        public EnvironmentControlGump() : base(0, 0)
        {
            X = 140;
            Y = 70;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.88f));
            Add(new Label("Environment & Scenery Control", true, TITLE_HUE, font: 1)
            {
                X = PAD,
                Y = 8
            });
            Add(new Label("Session previews are local. Server weather packets can still replace manual weather.",
                true, DIM_HUE, W - PAD * 2, font: 1)
            {
                X = PAD,
                Y = 27
            });

            _status = new Label(string.Empty, true, TEXT_HUE, W - PAD * 2, font: 1)
            {
                X = PAD,
                Y = 45
            };
            Add(_status);

            int left = PAD;
            int middle = left + COL_W + GAP;
            int right = middle + COL_W + GAP;

            BuildWeather(left, 82);
            BuildSeason(left, 246);

            BuildLight(middle, 82);
            BuildAmbience(middle, 246);

            BuildLighting(right, 82);
            BuildShowcases(right, 290);

            AddButton(PAD, H - 31, W - PAD * 2, 21, "Restore server environment", RestoreServer,
                "Stop previews and restore server/profile light, server season, and non-server manual weather.", 0x35);

            RefreshStatus();
        }

        public override GumpType GumpType => GumpType.None;

        private void BuildWeather(int x, int y)
        {
            AddHeader(x, y, "WEATHER");
            y += 19;
            string[] labels =
            {
                "Clear", "Rain", "Snow", "Heavy snow", "Blizzard", "Storm",
                "Tempest", "Brewing", "Hail", "Sleet", "Fog"
            };
            string[] values =
            {
                "off", "rain", "snow", "heavysnow", "blizzard", "storm",
                "tempest", "brewing", "hail", "sleet", "fog"
            };
            AddGrid(x, y, labels, i => ApplyWeather(values[i]), 2);
        }

        private void BuildSeason(int x, int y)
        {
            AddHeader(x, y, "SEASON");
            y += 19;
            string[] labels = { "Auto", "Spring", "Summer", "Fall", "Winter", "Desolation" };
            string[] values = { "auto", "spring", "summer", "fall", "winter", "desolation" };
            AddGrid(x, y, labels, i => ApplySeason(values[i]), 2);
        }

        private void BuildLight(int x, int y)
        {
            AddHeader(x, y, "GLOBAL LIGHT / TIME");
            y += 19;
            string[] labels = { "Auto", "Day", "Dawn", "Dusk", "Moonlit", "Moonless" };
            Action[] actions =
            {
                ClearLight,
                () => ApplyLight(0, false),
                () => ApplyLight(8, true),
                () => ApplyLight(7, false),
                () => ApplyLight(12, false),
                () => ApplyLight(30, false)
            };
            AddGrid(x, y, labels, i => actions[i](), 2);
            y += 3 * (BUTTON_H + BUTTON_GAP) + 3;

            Add(new Label("Custom 0–30:", true, TEXT_HUE, font: 1) { X = x, Y = y + 2 });
            var input = new StbTextBox(1, 30, 30, hue: TEXT_HUE)
            {
                X = x + 92,
                Y = y,
                Width = 42,
                Height = BUTTON_H,
                Multiline = false,
                NumbersOnly = true
            };
            input.SetText(World.InGame ? World.Light.Overall.ToString() : "0");
            Add(input);
            AddButton(x + 140, y, COL_W - 140, BUTTON_H, "Apply", () =>
            {
                if (int.TryParse(input.Text, out int light)) ApplyLight(light, false);
            }, "Apply an exact client-side global light from 0 (day) to 30 (moonless).", 0x35);
            y += BUTTON_H + BUTTON_GAP + 3;

            AddButton(x, y, (COL_W - BUTTON_GAP) / 2, BUTTON_H, "Day cycle ON",
                () => Run("daycycle", "on", "120"), "Run a continuous 120-second day/night rotation.", 0x44);
            AddButton(x + (COL_W + BUTTON_GAP) / 2, y, (COL_W - BUTTON_GAP) / 2, BUTTON_H,
                "Day cycle OFF", () => Run("daycycle", "off"), null, 0x21);
        }

        private void BuildAmbience(int x, int y)
        {
            AddHeader(x, y, "AMBIENCE / PERFORMANCE");
            y += 19;
            AddPair(x, ref y, "Ambience ON", () => Run("ambience", "on"),
                "Ambience OFF", () => Run("ambience", "off"));
            int waterWidth = (COL_W - BUTTON_GAP * 3) / 4;
            AddButton(x, y, waterWidth, BUTTON_H, "Off", () => Run("waterenhancement", "off"));
            AddButton(x + waterWidth + BUTTON_GAP, y, waterWidth, BUTTON_H, "Natural",
                () => Run("waterstyle", "natural"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 2, y, waterWidth, BUTTON_H, "Waves",
                () => Run("waterstyle", "waves"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 3, y, waterWidth, BUTTON_H, "Choppy",
                () => Run("waterstyle", "choppy"));
            y += BUTTON_H + BUTTON_GAP;
            int swellWidth = (COL_W - BUTTON_GAP * 2) / 3;
            AddButton(x, y, swellWidth, BUTTON_H, "Gentle",
                () => Run("waterstyle", "swell"));
            AddButton(x + swellWidth + BUTTON_GAP, y, swellWidth, BUTTON_H, "Storm",
                () => Run("waterstyle", "storm"));
            AddButton(x + (swellWidth + BUTTON_GAP) * 2, y, swellWidth, BUTTON_H, "Moonlit",
                () => Run("waterstyle", "moonlit"));
            y += BUTTON_H + BUTTON_GAP;
            AddButton(x, y, waterWidth, BUTTON_H, "W 50%",
                () => Run("waterintensity", "50"));
            AddButton(x + waterWidth + BUTTON_GAP, y, waterWidth, BUTTON_H, "W 100%",
                () => Run("waterintensity", "100"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 2, y, waterWidth, BUTTON_H, "W 150%",
                () => Run("waterintensity", "150"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 3, y, waterWidth, BUTTON_H, "W 200%",
                () => Run("waterintensity", "200"));
            y += BUTTON_H + BUTTON_GAP;
            AddPair(x, ref y, "Water atmos. ON", () => Run("wateratmosphere", "on"),
                "Water atmos. OFF", () => Run("wateratmosphere", "off"));
            AddPair(x, ref y, "Ambient weather ON", () => Run("ambientweather", "on"),
                "Ambient weather OFF", () => Run("ambientweather", "off"));
            AddPair(x, ref y, "Motion full", () => Run("weathermotion", "on"),
                "Motion reduced", () => Run("weathermotion", "off"));

            int bw = (COL_W - BUTTON_GAP * 2) / 3;
            AddButton(x, y, bw, BUTTON_H, "Low", () => Run("sceneryquality", "low"));
            AddButton(x + bw + BUTTON_GAP, y, bw, BUTTON_H, "Medium",
                () => Run("sceneryquality", "medium"));
            AddButton(x + (bw + BUTTON_GAP) * 2, y, bw, BUTTON_H, "High",
                () => Run("sceneryquality", "high"));
            y += BUTTON_H + BUTTON_GAP;

            AddButton(x, y, waterWidth, BUTTON_H, "T 50%",
                () => Run("materialintensity", "all", "50"));
            AddButton(x + waterWidth + BUTTON_GAP, y, waterWidth, BUTTON_H, "T 100%",
                () => Run("materialintensity", "all", "100"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 2, y, waterWidth, BUTTON_H, "T 150%",
                () => Run("materialintensity", "all", "150"));
            AddButton(x + (waterWidth + BUTTON_GAP) * 3, y, waterWidth, BUTTON_H, "T 200%",
                () => Run("materialintensity", "all", "200"));
            y += BUTTON_H + BUTTON_GAP;

            AddButton(x, y, bw, BUTTON_H, "50%", () => SetAmbienceIntensity(50));
            AddButton(x + bw + BUTTON_GAP, y, bw, BUTTON_H, "100%", () => SetAmbienceIntensity(100));
            AddButton(x + (bw + BUTTON_GAP) * 2, y, bw, BUTTON_H, "150%", () => SetAmbienceIntensity(150));
            y += BUTTON_H + BUTTON_GAP;
        }

        private void BuildLighting(int x, int y)
        {
            AddHeader(x, y, "LIGHTING / SHADOWS");
            y += 19;
            AddPair(x, ref y, "Colored ON", () => SetProfileFlag("Colored lights", p => p.UseColoredLights = true),
                "Colored OFF", () => SetProfileFlag("Colored lights", p => p.UseColoredLights = false));
            AddPair(x, ref y, "Alternative ON", () => SetProfileFlag("Alternative lights", p => p.UseAlternativeLights = true),
                "Alternative OFF", () => SetProfileFlag("Alternative lights", p => p.UseAlternativeLights = false));
            AddPair(x, ref y, "Dark nights ON", () => SetProfileFlag("Dark nights", p => p.UseDarkNights = true),
                "Dark nights OFF", () => SetProfileFlag("Dark nights", p => p.UseDarkNights = false));
            AddPair(x, ref y, "Shadows ON", () => SetProfileFlag("Shadows", p => p.ShadowsEnabled = true),
                "Shadows OFF", () => SetProfileFlag("Shadows", p => p.ShadowsEnabled = false));
            AddPair(x, ref y, "Classic mode", () => SetProfileFlag("Classic shadows", p => p.ShadowMode = EnvironmentalShadowManager.MODE_CLASSIC),
                "Dynamic mode", () => SetProfileFlag("Dynamic shadows", p => p.ShadowMode = EnvironmentalShadowManager.MODE_DYNAMIC));
            AddPair(x, ref y, "Statics ON", () => SetProfileFlag("Static shadows", p => p.ShadowsStatics = true),
                "Statics OFF", () => SetProfileFlag("Static shadows", p => p.ShadowsStatics = false));
            AddPair(x, ref y, "Objects ON", () => SetProfileFlag("Object shadows", p => p.EnhancedObjectShadows = true),
                "Objects OFF", () => SetProfileFlag("Object shadows", p => p.EnhancedObjectShadows = false));
        }

        private void BuildShowcases(int x, int y)
        {
            AddHeader(x, y, "SHOWCASES");
            y += 19;
            AddPair(x, ref y, "Random ON", () => Run("envshowcase", "on"),
                "Random OFF", () => Run("envshowcase", "off"));
            AddPair(x, ref y, "Beautiful ON", () => Run("envshowcasebeautiful", "on"),
                "Beautiful OFF", () => Run("envshowcasebeautiful", "off"));
            AddButton(x, y, COL_W, BUTTON_H, "Individual material intensity...",
                OpenMaterialIntensity,
                "Open persistent 0–200% sliders for water and every terrain material.", 0x35);
            y += BUTTON_H + BUTTON_GAP;
            AddPair(x, ref y, "Material debug ON", () => Run("materialdebug", "on"),
                "Material debug OFF", () => Run("materialdebug", "off"));
        }

        private static void OpenMaterialIntensity()
        {
            var existing = UIManager.GetGump<MaterialIntensityGump>();
            if (existing != null && !existing.IsDisposed)
            {
                existing.Dispose();
                return;
            }
            UIManager.Add(new MaterialIntensityGump());
        }

        private void ApplyWeather(string value)
        {
            StopShowcase();
            Run("weather", value);
        }

        private void ApplySeason(string value)
        {
            StopShowcase();
            Run("seasontest", value);
        }

        private void ApplyLight(int light, bool dawn)
        {
            StopCycles();
            EnvironmentControlManager.ApplyLight(light, dawn);
        }

        private void ClearLight()
        {
            StopCycles();
            EnvironmentControlManager.ClearLight();
        }

        private static void StopCycles()
        {
            GameScene scene = Client.Game.GetScene<GameScene>();
            if (scene == null) return;
            if (EnvironmentShowcaseManager.Enabled) EnvironmentShowcaseManager.Stop(scene.Weather);
            if (DayCyclePreviewManager.Enabled) DayCyclePreviewManager.Stop();
        }

        private static void StopShowcase()
        {
            GameScene scene = Client.Game.GetScene<GameScene>();
            if (scene != null && EnvironmentShowcaseManager.Enabled)
                EnvironmentShowcaseManager.Stop(scene.Weather);
        }

        private static void RestoreServer()
        {
            GameScene scene = Client.Game.GetScene<GameScene>();
            if (scene == null) return;
            if (EnvironmentShowcaseManager.Enabled) EnvironmentShowcaseManager.Stop(scene.Weather);
            if (DayCyclePreviewManager.Enabled) DayCyclePreviewManager.Stop();
            EnvironmentControlManager.ClearLight(false);
            World.SetSeasonOverride(null);
            if (scene.Weather.Source == WeatherSource.Manual) scene.Weather.Reset();
            GameActions.Print("Server environment restored; profile ambience and lighting options kept.", 0x35);
        }

        private static void SetAmbienceIntensity(int value)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null) return;
            profile.DynamicAmbienceIntensity = value;
            GameActions.Print($"Ambience intensity {value}%.", 0x35);
        }

        private static void SetProfileFlag(string label, Action<Profile> setter)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null) return;
            setter(profile);
            GameActions.Print($"{label} updated.", 0x35);
        }

        private static void Run(string command, params string[] values)
        {
            string[] args = new string[values.Length + 1];
            args[0] = command;
            Array.Copy(values, 0, args, 1, values.Length);
            CommandManager.Execute(command, args);
        }

        private void AddHeader(int x, int y, string text)
        {
            Add(new Label(text, true, GROUP_HUE, font: 1) { X = x, Y = y });
            Add(new AlphaBlendControl(0.32f) { X = x, Y = y + 16, Width = COL_W, Height = 1 });
        }

        private void AddGrid(int x, int y, string[] labels, Action<int> action, int columns)
        {
            int bw = (COL_W - BUTTON_GAP * (columns - 1)) / columns;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                int bx = x + i % columns * (bw + BUTTON_GAP);
                int by = y + i / columns * (BUTTON_H + BUTTON_GAP);
                AddButton(bx, by, bw, BUTTON_H, labels[i], () => action(index));
            }
        }

        private void AddPair(int x, ref int y, string left, Action leftAction, string right, Action rightAction)
        {
            int bw = (COL_W - BUTTON_GAP) / 2;
            AddButton(x, y, bw, BUTTON_H, left, leftAction, null, 0x44);
            AddButton(x + bw + BUTTON_GAP, y, bw, BUTTON_H, right, rightAction, null, 0x21);
            y += BUTTON_H + BUTTON_GAP;
        }

        private void AddButton(int x, int y, int width, int height, string text, Action action,
            string tooltip = null, ushort hue = 0x35)
        {
            var button = new NiceButton(x, y, width, height, ButtonAction.Activate, text, font: 1, hue: hue)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            if (!string.IsNullOrEmpty(tooltip)) button.SetTooltip(tooltip);
            button.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                action();
                _nextStatusRefresh = 0;
            };
            Add(button);
        }

        private void RefreshStatus()
        {
            GameScene scene = Client.Game.GetScene<GameScene>();
            if (scene == null || !World.InGame)
            {
                _status.Text = "Not in game.";
                return;
            }

            string weather = WeatherName(scene.Weather);
            string mode = EnvironmentShowcaseManager.BeautifulEnabled ? "Beautiful showcase"
                : EnvironmentShowcaseManager.Enabled ? "Random showcase"
                : DayCyclePreviewManager.Enabled ? "Day cycle"
                : EnvironmentControlManager.Enabled ? "Fixed light" : "Manual/server";
            string quality = SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_LOW ? "Low"
                : SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_MEDIUM ? "Medium" : "High";
            _status.Text = $"{mode}  |  {weather}  |  {World.Season}  |  Light {World.Light.Overall}  |  "
                + $"Ambience {(AmbienceOverlay.Enabled ? "ON" : "OFF")}  |  "
                + $"Water {(WaterEnhancementManager.ArtworkEnabled ? WaterEnhancementManager.ArtworkStyleName : "OFF")}"
                + $"@{WaterEnhancementManager.IntensityPercent}%/{(WaterEnhancementManager.AtmosphereEnabled ? "atmos" : "no atmos")}"
                + $"  |  Terrain {TerrainMaterialManager.IntensitySummary}"
                + $"  |  Quality {quality}";
        }

        private static string WeatherName(Weather weather)
        {
            if (weather == null || !weather.IsActive && !weather.Fog) return "Clear";
            if (weather.Fog) return "Fog";
            if (weather.Tempest) return "Tempest";
            if (weather.Blizzard) return "Blizzard";
            if (weather.HeavySnow) return "Heavy snow";
            if (weather.Hail) return "Hail";
            if (weather.Sleet) return "Sleet";
            switch (weather.Type)
            {
                case WeatherType.WT_RAIN: return "Rain";
                case WeatherType.WT_SNOW: return "Snow";
                case WeatherType.WT_STORM_APPROACH: return "Storm";
                case WeatherType.WT_STORM_BREWING: return "Brewing storm";
                default: return "Clear";
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed || Time.Ticks < _nextStatusRefresh) return;
            _nextStatusRefresh = Time.Ticks + 250;
            RefreshStatus();
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            Texture2D texture = SolidColorTextureCache.GetTexture(new Color(85, 90, 100, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.75f);
            batcher.Draw(texture, new Rectangle(x, y, W, 1), hue);
            batcher.Draw(texture, new Rectangle(x, y + H - 1, W, 1), hue);
            batcher.Draw(texture, new Rectangle(x, y, 1, H), hue);
            batcher.Draw(texture, new Rectangle(x + W - 1, y, 1, H), hue);
            return true;
        }
    }
}
