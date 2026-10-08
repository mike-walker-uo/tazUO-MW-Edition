using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ClientEnhancementsGump : BaseOptionsGump
    {
        private readonly Profile _profile;
        private readonly VisualEnhancementSettings _visual;
        private readonly VBoxContainer[] _pageContent = new VBoxContainer[7];

        internal ClientEnhancementsGump() : base(980, 720, "Client enhancements")
        {
            _profile = ProfileManager.CurrentProfile;
            _visual = _profile.VisualEnhancements ?? (_profile.VisualEnhancements = new VisualEnhancementSettings());
            CenterXInScreen(); CenterYInScreen();
            Category("Graphics", 1); Category("Weather & ambience", 2); Category("Interface & themes", 3);
            Category("Chat mentions", 4); Category("Spell details", 5); Category("Settings history", 6);
            Check("Adapt optional particle density to frame time", _visual.AdaptiveParticles, b => _visual.AdaptiveParticles = b, 1);
            Slider("Unused optional texture budget (MB)", 32, 512, _visual.TextureBudgetMB, n => _visual.TextureBudgetMB = n, 1);
            Check("Soften particle contacts", _visual.SoftParticles, b => _visual.SoftParticles = b, 1);
            Check("Linear light compositing", _visual.LinearLight, b => { _visual.LinearLight = b; Client.Game.GetScene<GameScene>()?.SetPostProcessingSettings(); }, 1);
            Note("Linear light blends lighting in linear colour space, changing its appearance and using extra GPU passes. Leave off for classic UO lighting.", 1);
            Check("World edge anti-aliasing (FXAA)", _visual.WorldAntiAliasing, b => _visual.WorldAntiAliasing = b, 1);
            Slider("Anti-aliasing strength (%)", 0, 100, _visual.WorldAntiAliasingStrength, n => _visual.WorldAntiAliasingStrength = n, 1);
            Slider("Pixel art filter sharpness (%)", 0, 100, _visual.PixelArtSharpness, n => _visual.PixelArtSharpness = n, 1);
            Note("Choose pixel art or xbr under Options > Video > Misc, with post processing enabled. Scaling applies when enlarging the world. FXAA leaves UI, text and healthbars sharp.", 1);
            Check("Smooth magnifier image", _profile.MagnifierSmooth, b => _profile.MagnifierSmooth = b, 1);
            Check("Bloom on lamps and magic", _visual.SelectiveBloom, b => _visual.SelectiveBloom = b, 1);
            Slider("Bloom strength", 0, 100, _visual.BloomStrength, n => _visual.BloomStrength = n, 1);
            Check("Match fog, water and spell lighting", _visual.UnifiedLightPalette, b => _visual.UnifiedLightPalette = b, 1);
            Check("Grounding shadows", _visual.ContactShadows, b => _visual.ContactShadows = b, 1);
            Check("Use local high-resolution art pack", _visual.LocalArtPack, b => _visual.LocalArtPack = b, 1);
            Text("Art pack folder", _visual.ArtPackFolder, s => _visual.ArtPackFolder = s, 1);
            Action("Reload art pack", () => LocalArtPack.Reload(), 1);
            Note("Art packs need manifest.json and local sprite images. Missing replacements retain native art.", 1);
            Check("Enable ambient weather", _profile.AmbientWeatherEnabled, b => { _profile.AmbientWeatherEnabled = b; AmbientWeatherManager.Configure(b); }, 2);
            Check("Schedule ambient weather by biome, season and time", _visual.BiomeWeather, b => _visual.BiomeWeather = b, 2);
            Check("Blend compatible weather transitions", _visual.WeatherCrossfade, b => _visual.WeatherCrossfade = b, 2);
            Slider("Lightning flash", 0, 100, _visual.LightningFlash, n => _visual.LightningFlash = n, 2);
            Slider("Thunder volume", 0, 100, _visual.ThunderVolume, n => _visual.ThunderVolume = n, 2);
            Slider("Weather camera shake", 0, 100, _visual.WeatherShake, n => _visual.WeatherShake = n, 2);
            Check("Anchor fog to the world", _visual.WorldAnchoredFog, b => _visual.WorldAnchoredFog = b, 2);
            Check("Blend regional ambience sound beds", _visual.RegionalSoundBeds, b => _visual.RegionalSoundBeds = b, 2);
            _pageContent[2].Add(new ComboBoxWithLabel("Regional soundscape", 170, 285, RegionalSoundscapes.Names,
                RegionalSoundscapes.Normalize(_visual.RegionalSoundscape), (n, _) => { _visual.RegionalSoundscape = (byte)n; RegionalAmbience.Reset(); }, true), 2);
            Note("Classic UO preserves the original bed palette. Wilderness adds swamp frogs and desert wind; Quiet lowers gain and lengthens pauses. Uses your installed UO sounds; no downloads or redistributed audio.", 2);
            Check("Use optional local regional ambience pack", _visual.LocalSoundscapePack, b => _visual.LocalSoundscapePack = b, 2);
            Text("Soundscape pack folder", _visual.SoundscapePackFolder, s => _visual.SoundscapePackFolder = s, 2);
            Action("Reload soundscape pack", () => LocalSoundscapePack.Reload(), 2);
            Action("Show pack name, license and load result", () => GameActions.Print(LocalSoundscapePack.Status, 0x35), 2);
            Note("manifest.json declares name, license and region WAV paths. PCM16 mono / 22050 Hz / up to 30 s each. Invalid or missing regions retain native UO sounds. Combat cues are unchanged.", 2);
            Check("Seasonal petals, leaves and frost", _visual.SeasonalDetails, b => _visual.SeasonalDetails = b, 2);
            int map = World.MapIndex;
            Slider("Seasonal particle intensity on this map", 0, 200, (int)(VisualBudget.MapDensity * 100), n => _visual.MapParticleIntensity[map] = n, 2);
            Check("Scale interface and text independently of world zoom", _profile.InterfaceScaling, b => _profile.InterfaceScaling = b, 3);
            Slider("Interface and text scale (%)", 75, 200, (int)(_profile.InterfaceScale * 100), n => _profile.InterfaceScale = n / 100f, 3);
            Check("Reduce theme decoration, retain palette", _profile.ReducedThemeDecoration, b => _profile.ReducedThemeDecoration = b, 3);
            Check("Rotate gump themes daily", _profile.RotateGumpThemesDaily, CustomGumpThemeManager.SetDailyRotation, 3);
            Action("Browse themes and favorites", () => UIManager.Add(new GumpThemeSelectorGump()), 3);
            string[] windows = { "Global chat", "Guild chat", "Nearby speech", "World explorer", "Item finder", "Restock", "Command palette", "Environment controls", "Paperdoll" };
            string[] types = { "GlobalChatGump", "GuildChatGump", "NearbySpeechGump", "WorldExplorerGump", "ItemFinderGump", "RestockAgentGump", "CommandPaletteGump", "EnvironmentControlGump", "PaperDollGump" };
            string[] themes = new[] { "Use global theme" }.Concat(CustomGumpThemeManager.AvailableThemes.Select(t => t.ToString())).ToArray();
            for (int i = 0; i < types.Length; i++)
            {
                string type = types[i];
                int selected = _profile.GumpThemeOverrides != null && _profile.GumpThemeOverrides.TryGetValue(type, out byte value)
                    ? Array.IndexOf(CustomGumpThemeManager.AvailableThemes, CustomGumpThemeManager.ResolveSavedTheme(value)) + 1 : 0;
                _pageContent[3].Add(new ComboBoxWithLabel(windows[i], 145, 285, themes, selected,
                    (n, _) => CustomGumpThemeManager.SetWindowTheme(type, n == 0 ? (CustomGumpTheme?)null : CustomGumpThemeManager.AvailableThemes[n - 1]), true), 3);
            }
            Check("Highlight mentions", _profile.ChatMentionsEnabled, b => _profile.ChatMentionsEnabled = b, 4);
            Text("Guild tag", _profile.ChatMentionGuildTag, s => _profile.ChatMentionGuildTag = s, 4);
            Text("Words (comma-separated)", _profile.ChatMentionWords, s => _profile.ChatMentionWords = s, 4);
            Note("Your character name is included automatically. Highlights retain channel and player colors.", 4);
            Check("Prioritize player and target effects", _visual.PrioritizeCombatEffects, b => _visual.PrioritizeCombatEffects = b, 5);
            Action("Compare classic and enhanced effects", () => UIManager.Add(new EffectComparisonGump()), 5);
            foreach (SpellAbilityEffectEntry entry in SpellAbilityEffectSettings.Entries)
            {
                string key = entry.Id.ToString();
                if (!_visual.Effects.TryGetValue(key, out EffectDetailSettings detail)) detail = new EffectDetailSettings();
                EffectDetailSettings settings = detail;
                Note(entry.Name, 5);
                Slider("Intensity", 0, 200, settings.Intensity, n => { settings.Intensity = n; _visual.Effects[key] = settings; }, 5);
                Slider("Density", 0, 200, settings.Density, n => { settings.Density = n; _visual.Effects[key] = settings; }, 5);
                Slider("Glow", 0, 200, settings.Glow, n => { settings.Glow = n; _visual.Effects[key] = settings; }, 5);
            }
            Note("Last 50 saved option edits. Undo restores only fields in that edit. Window positions are excluded.", 6);
            foreach (SettingsHistory.Entry entry in SettingsHistory.Read(ProfileManager.ProfilePath).AsEnumerable().Reverse())
            {
                Note(entry.Time + " — " + string.Join(", ", entry.Before.Keys), 6);
                Action("Undo this edit", () => { SettingsHistory.Undo(entry); Dispose(); UIManager.Add(new ClientEnhancementsGump()); }, 6);
            }
            Action("Save changes", Save, 6);
            ChangePage(1);
        }

        private void Category(string label, int page)
        {
            MainContent.AddToLeft(CategoryButton(label, page, MainContent.LeftWidth));
            var scroll = new ScrollArea(0, 0, MainContent.RightWidth, MainContent.Height) { ActivePage = page };
            MainContent.AddToRight(scroll, false, page);
            _pageContent[page] = new VBoxContainer(MainContent.RightWidth - ThemeSettings.SCROLL_BAR_WIDTH,
                0, ThemeSettings.TOP_PADDING) { ActivePage = page };
            scroll.Add(_pageContent[page]);
        }
        private void Check(string label, bool value, Action<bool> change, int page) =>
            _pageContent[page].Add(new CheckboxWithLabel(label,
                maxWidth: _pageContent[page].Width - ThemeSettings.CHECKBOX_SIZE - 5,
                isChecked: value, valueChanged: change), page);
        private void Slider(string label, int min, int max, int value, Action<int> change, int page) =>
            _pageContent[page].Add(new SliderWithLabel(label, 0, 150, min, max, Math.Max(min, Math.Min(max, value)), change), page);
        private void Text(string label, string value, Action<string> change, int page)
        {
            InputFieldWithLabel field = null;
            field = new InputFieldWithLabel(label, 260, value ?? "", onTextChange: (_, __) => { if (field != null) change(field.Text); });
            _pageContent[page].Add(field, page);
        }
        private void Note(string text, int page) => _pageContent[page].Add(new Label(text, true,
            CustomGumpThemeManager.TextHue, _pageContent[page].Width, font: 1), page);
        private void Action(string label, Action action, int page)
        {
            var button = new ModernButton(0, 0, 340, 34, ButtonAction.Activate, label, ThemeSettings.BUTTON_FONT_COLOR);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) action(); };
            _pageContent[page].Add(button, page);
        }
        private void Save() { if (_profile != null && !string.IsNullOrEmpty(ProfileManager.ProfilePath)) _profile.Save(ProfileManager.ProfilePath, false, true); }
        public override void Dispose() { Save(); base.Dispose(); }
    }
}
