#region license
// TazUO addition. Searchable command palette with descriptions, live state,
// conditional parameter editors, filters, execution and persistent pins.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.LegionScripting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class CommandPaletteGump : Gump
    {
        private enum StateFilter
        {
            All,
            Active,
            Inactive
        }

        private const int W = 820;
        private const int H = 540;
        private const int PAD = 8;
        private const int TITLE_H = 28;
        private const int SEARCH_H = 24;
        private const int COL_NAME = 188;
        private const int COL_DESC = 282;
        private const int COL_ARGS = 170;
        private const int COL_RUN = 42;
        private const int COL_PIN = 42;
        private const int COL_TOGGLE = 34;
        private const int ROW_H = 27;
        private const int HEADER_H = 22;

        private readonly StbTextBox _search;
        private readonly ScrollArea _scroll;
        private readonly NiceButton _allFilterButton;
        private readonly NiceButton _activeFilterButton;
        private readonly NiceButton _inactiveFilterButton;
        private readonly HashSet<string> _collapsed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Drafts survive closing/reopening the palette during this client run.
        private static readonly Dictionary<string, string> _argInputs =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private StateFilter _stateFilter;
        private bool _rebuildRequested;
        private long _nextStatusRefresh;
        private int _lastStateSignature;

        public CommandPaletteGump() : base(0, 0)
        {
            X = 200;
            Y = 120;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.85f));

            var title = new Label("Command Palette", true, CustomGumpThemeManager.TitleHue, W, font: 1)
            {
                X = 0,
                Y = 4
            };
            title.X = (W - title.Width) / 2;
            Add(title);

            int y = TITLE_H + 2;
            Add(new Label("Search:", true, CustomGumpThemeManager.TextHue, font: 1)
            {
                X = PAD,
                Y = y + 4
            });

            _search = new StbTextBox(1, 5, 300, hue: CustomGumpThemeManager.TextHue)
            {
                X = PAD + 54,
                Y = y,
                Width = 300,
                Height = SEARCH_H,
                Multiline = false
            };
            _search.SetText(string.Empty);
            var searchBackground = new AlphaBlendControl(0.65f)
            {
                Width = _search.Width,
                Height = _search.Height
            };
            CustomGumpThemeManager.ApplyInputSurface(searchBackground, 0.65f);
            _search.Add(searchBackground);
            _search.TextChanged += (s, e) => Rebuild();
            Add(_search);

            int filterX = _search.X + _search.Width + 14;
            _allFilterButton = AddFilterButton(filterX, y, 54, "All", StateFilter.All);
            _activeFilterButton = AddFilterButton(filterX + 58, y, 64, "Active", StateFilter.Active);
            _inactiveFilterButton = AddFilterButton(filterX + 126, y, 72, "Inactive", StateFilter.Inactive);
            UpdateFilterButtonStyles();

            Add(new Label("green = active/open", true, CustomGumpThemeManager.DimHue, font: 1)
            {
                X = filterX + 206,
                Y = y + 4
            });

            int scrollY = y + SEARCH_H + 6;
            _scroll = new ScrollArea(PAD, scrollY, W - PAD * 2, H - scrollY - PAD, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            Add(_scroll);

            _lastStateSignature = GetStateSignature();
            Rebuild();
        }

        private NiceButton AddFilterButton(int x, int y, int width, string text, StateFilter filter)
        {
            var button = new NiceButton(x, y, width, SEARCH_H, ButtonAction.Activate, text, font: 1)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtonType.Left || _stateFilter == filter) return;
                _stateFilter = filter;
                UpdateFilterButtonStyles();
                Rebuild();
            };
            Add(button);
            return button;
        }

        private void UpdateFilterButtonStyles()
        {
            SetFilterButtonStyle(_allFilterButton, _stateFilter == StateFilter.All);
            SetFilterButtonStyle(_activeFilterButton, _stateFilter == StateFilter.Active);
            SetFilterButtonStyle(_inactiveFilterButton, _stateFilter == StateFilter.Inactive);
        }

        private static void SetFilterButtonStyle(NiceButton button, bool active)
        {
            if (button == null) return;

            button.AlwaysShowBackground = false;
            button.BackgroundColor = Color.White;
            button.BorderColor = Color.LightGray;
            button.Alpha = 0.25f;
            CustomGumpThemeManager.StyleButton(button);
            if (!active) return;

            button.AlwaysShowBackground = true;
            button.BackgroundColor = new Color(26, 92, 46, 220);
            button.BorderColor = new Color(70, 210, 100);
        }

        private void Rebuild()
        {
            _scroll.Clear();
            string search = (_search?.Text ?? string.Empty).Trim();

            var byCategory =
                new Dictionary<string, List<(string name, CommandMetadata.Entry entry)>>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in CommandManager.Commands.Keys)
            {
                if (!CommandMetadata.IsVisibleInPalette(name)) continue;

                CommandMetadata.Entry entry = CommandMetadata.Get(name);
                string category = entry?.Category ?? "Other";
                string description = entry?.Description ?? string.Empty;
                bool? active = GetActiveState(name);

                if (_stateFilter == StateFilter.Active && active != true) continue;
                if (_stateFilter == StateFilter.Inactive && active != false) continue;

                if (search.Length > 0 &&
                    name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    description.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!byCategory.TryGetValue(category, out var list))
                {
                    list = new List<(string, CommandMetadata.Entry)>();
                    byCategory[category] = list;
                }

                list.Add((name, entry));
            }

            int rowY = 0;
            foreach (string category in CommandMetadata.CategoryOrder)
            {
                if (byCategory.TryGetValue(category, out var list))
                {
                    EmitCategory(category, list, ref rowY);
                }
            }

            foreach (var pair in byCategory)
            {
                bool known = false;
                foreach (string category in CommandMetadata.CategoryOrder)
                {
                    if (string.Equals(category, pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known) EmitCategory(pair.Key, pair.Value, ref rowY);
            }
        }

        private void EmitCategory(
            string category,
            List<(string name, CommandMetadata.Entry entry)> list,
            ref int y)
        {
            list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));
            bool collapsed = _collapsed.Contains(category);
            AddCategoryHeader(category, (collapsed ? "▶ " : "▼ ") + category + "  (" + list.Count + ")", ref y);

            if (!collapsed)
            {
                foreach (var item in list) AddCommandRow(item.name, item.entry, ref y);
            }

            y += 4;
        }

        private void AddCategoryHeader(string category, string text, ref int y)
        {
            var button = new NiceButton(0, y, _scroll.Width - 20, HEADER_H,
                ButtonAction.Activate, text, font: 1,
                align: TEXT_ALIGN_TYPE.TS_LEFT, hue: 0x35)
            {
                IsSelectable = false,
                DisplayBorder = false
            };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                if (_collapsed.Contains(category)) _collapsed.Remove(category);
                else _collapsed.Add(category);
                Rebuild();
            };
            _scroll.Add(button);
            y += HEADER_H + 2;
        }

        private void AddCommandRow(string name, CommandMetadata.Entry entry, ref int y)
        {
            int x = 4;
            string description = entry?.Description;
            if (string.IsNullOrWhiteSpace(description))
                description = $"Custom command -{name}.";

            string usage = entry?.Usage ?? string.Empty;
            string tooltip = usage.Length == 0
                ? $"{description}\nCommand: -{name}"
                : $"{description}\nUsage: -{name} {usage}";

            var nameLabel = new Label("-" + name, true, CustomGumpThemeManager.TitleHue, COL_NAME, font: 1)
            {
                X = x,
                Y = y + 4,
                AcceptMouseInput = true
            };
            nameLabel.SetTooltip(tooltip, 440);
            _scroll.Add(nameLabel);
            x += COL_NAME;

            var descriptionLabel = new Label(description, true, CustomGumpThemeManager.TextHue,
                COL_DESC, font: 1, style: FontStyle.Cropped)
            {
                X = x,
                Y = y + 4,
                AcceptMouseInput = true
            };
            descriptionLabel.SetTooltip(tooltip, 440);
            _scroll.Add(descriptionLabel);
            x += COL_DESC;

            bool isToggle = IsToggleUsage(usage);
            bool needsArgument = usage.Length > 0 && !IsPureToggleUsage(usage);
            string[] choices = needsArgument ? ExtractPickerChoices(usage, isToggle) : null;

            if (needsArgument)
            {
                string initial = GetInitialArgument(name, choices);
                _argInputs[name] = initial;
                string argTooltip = "Allowed parameters: " + usage;

                if (choices != null && choices.Length > 0)
                {
                    int selected = Array.FindIndex(choices,
                        value => string.Equals(value, initial, StringComparison.OrdinalIgnoreCase));
                    if (selected < 0) selected = 0;
                    _argInputs[name] = choices[selected];

                    var picker = new Combobox(x, y, COL_ARGS - 4, choices, selected,
                        maxHeight: 180, font: 1)
                    {
                        AcceptMouseInput = true
                    };
                    picker.SetTooltip(argTooltip, 440);
                    picker.OnOptionSelected += (s, index) =>
                    {
                        if (index >= 0 && index < choices.Length) _argInputs[name] = choices[index];
                    };
                    _scroll.Add(picker);
                }
                else
                {
                    var input = new StbTextBox(1, 3, 128, hue: CustomGumpThemeManager.TextHue)
                    {
                        X = x,
                        Y = y + 3,
                        Width = COL_ARGS - 4,
                        Height = 20,
                        Multiline = false
                    };
                    input.SetText(initial);
                    input.SetTooltip(argTooltip, 440);
                    var inputBackground = new AlphaBlendControl(0.65f)
                    {
                        Width = input.Width,
                        Height = input.Height
                    };
                    CustomGumpThemeManager.ApplyInputSurface(inputBackground, 0.65f);
                    input.Add(inputBackground);
                    input.TextChanged += (s, e) => _argInputs[name] = input.Text ?? string.Empty;
                    _scroll.Add(input);
                }
            }

            x += COL_ARGS;
            bool? active = GetActiveState(name);
            int pinX;

            if (isToggle)
            {
                var onButton = CreateActionButton(x, y + 3, COL_TOGGLE, "ON", 0x44, active == true);
                onButton.MouseUp += (s, e) =>
                {
                    if (e.Button != MouseButtonType.Left) return;
                    CommandManager.Execute(name, new[] { name, "on" });
                    RequestRebuild();
                };
                _scroll.Add(onButton);

                var offButton = CreateActionButton(x + COL_TOGGLE + 2, y + 3, COL_TOGGLE,
                    "OFF", 0x21, active == false && active.HasValue);
                offButton.MouseUp += (s, e) =>
                {
                    if (e.Button != MouseButtonType.Left) return;
                    CommandManager.Execute(name, new[] { name, "off" });
                    RequestRebuild();
                };
                _scroll.Add(offButton);

                int afterToggle = x + (COL_TOGGLE + 2) * 2;
                if (needsArgument)
                {
                    var runButton = CreateActionButton(afterToggle, y + 3, COL_RUN, "Run", 0x35, false);
                    runButton.MouseUp += (s, e) =>
                    {
                        if (e.Button != MouseButtonType.Left) return;
                        ExecuteWithArgs(name, GetExecutionArgument(name));
                        RequestRebuild();
                    };
                    _scroll.Add(runButton);
                    pinX = afterToggle + COL_RUN + 4;
                }
                else
                {
                    pinX = afterToggle + 4;
                }
            }
            else
            {
                var runButton = CreateActionButton(x, y + 3, COL_RUN, "Run", 0x35, active == true);
                runButton.MouseUp += (s, e) =>
                {
                    if (e.Button != MouseButtonType.Left) return;
                    ExecuteWithArgs(name, GetExecutionArgument(name));
                    RequestRebuild();
                };
                _scroll.Add(runButton);
                pinX = x + COL_RUN + 4;
            }

            var pinButton = CreateActionButton(pinX, y + 3, COL_PIN, "Pin", 0x35, false);
            pinButton.SetTooltip(
                "Create a persistent movable button for this command. Drag pinned buttons together to group them; right-click one to remove it.",
                440);
            pinButton.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                PinnedCommandManager.Pin(BuildCommandLine(name, GetExecutionArgument(name)));
            };
            _scroll.Add(pinButton);

            y += ROW_H;
        }

        private static NiceButton CreateActionButton(
            int x, int y, int width, string text, ushort hue, bool active)
        {
            var button = new NiceButton(x, y, width, 20, ButtonAction.Activate, text, font: 1, hue: hue)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            ApplyActiveStyle(button, active, string.Equals(text, "OFF", StringComparison.Ordinal));
            return button;
        }

        private static void ApplyActiveStyle(NiceButton button, bool active, bool off)
        {
            if (!active) return;
            button.AlwaysShowBackground = true;
            button.BackgroundColor = off
                ? new Color(95, 24, 34, 220)
                : new Color(26, 92, 46, 220);
            button.BorderColor = off
                ? new Color(210, 65, 75)
                : new Color(70, 210, 100);
        }

        private static bool IsPureToggleUsage(string usage)
        {
            string[] parts = usage.Split('|');
            if (parts.Length == 0) return false;

            foreach (string part in parts)
            {
                string value = part.Trim().Trim('[', ']');
                if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value, "status", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsToggleUsage(string usage)
        {
            string normalized = (usage ?? string.Empty).Trim().Trim('[', ']');
            return normalized.StartsWith("on|off", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] ExtractPickerChoices(string usage, bool isToggle)
        {
            string normalized = usage.Trim().Trim('[', ']');
            if (normalized.IndexOf('<') >= 0 || normalized.IndexOf('>') >= 0 ||
                normalized.IndexOf(' ') >= 0 || normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            string[] parts = normalized.Split('|');
            var choices = new List<string>();
            foreach (string part in parts)
            {
                string value = part.Trim();
                if (value.Length == 0 || value == "...") return null;
                if (isToggle &&
                    (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(value, "status", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                choices.Add(value);
            }

            return choices.Count > 0 ? choices.ToArray() : null;
        }

        private static string GetInitialArgument(string name, string[] choices)
        {
            if (_argInputs.TryGetValue(name, out string saved)) return saved;

            string current = GetCurrentArgument(name);
            if (!string.IsNullOrEmpty(current)) return current;
            return choices != null && choices.Length > 0 ? choices[0] : string.Empty;
        }

        private static string GetArgument(string name)
        {
            return _argInputs.TryGetValue(name, out string value) ? value : string.Empty;
        }

        private static string GetExecutionArgument(string name)
        {
            string value = GetArgument(name).Trim();

            // The field intentionally contains only the configurable duration.
            // -daycycle requires its enable verb before that duration.
            if (string.Equals(name, "daycycle", StringComparison.OrdinalIgnoreCase) &&
                value.Length > 0 &&
                !value.StartsWith("on ", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                return "on " + value;
            }

            return value;
        }

        private static string GetCurrentArgument(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "bandagetimer": return BandageTimerManager.Duration.ToString();
                case "autobandage": return AutoBandageManager.ThresholdPercent.ToString();
                case "petbandage": return PetBandageManager.ThresholdPct.ToString();
                case "lowhp": return UI.LowHpVignette.ThresholdPct.ToString();
                case "manaalert": return ManaStamAlertManager.ManaPct.ToString();
                case "stamalert": return ManaStamAlertManager.StamPct.ToString();
                case "pingwarn": return PingSpikeWarner.SpikeMs.ToString();
                case "rangewarn": return TargetRangeWarnManager.WarnDistance.ToString();
                case "weightalert": return InventoryFullWarner.ThresholdPct.ToString();
                case "tilegrid": return UI.TileGridOverlay.Range.ToString();
                case "mobhp": return UI.CombatMobHpBars.Range.ToString();
                case "range": return UI.RangeIndicator.Range.ToString();
                case "radius": return (ProfileManager.CurrentProfile?.DisplayRadiusDistance ?? 5).ToString();
                case "idlewarn": return IdleMonitorManager.IdleSeconds.ToString();
                case "loiterwarn": return HostileLoiteringWarner.Range + " " + HostileLoiteringWarner.DwellSeconds;
                case "daycycle": return DayCyclePreviewManager.DEFAULT_SECONDS.ToString();
                case "waterstyle":
                    string waterStyle = WaterEnhancementManager.ArtworkStyleName.ToLowerInvariant();
                    if (waterStyle.StartsWith("gentle", StringComparison.Ordinal)) return "swell";
                    if (waterStyle.StartsWith("storm", StringComparison.Ordinal)) return "storm";
                    if (waterStyle.StartsWith("moonlit", StringComparison.Ordinal)) return "moonlit";
                    return waterStyle;
                case "overheadsize": return OverheadEffectSizeSettings.Name.ToLowerInvariant().Replace(" ", string.Empty);
                case "sceneryquality":
                    switch (ProfileManager.CurrentProfile?.SceneryQuality ?? 2)
                    {
                        case 0: return "low";
                        case 1: return "medium";
                        default: return "high";
                    }
                case "musicmode":
                    switch (ProfileManager.CurrentProfile?.MusicSelectionMode ?? 2)
                    {
                        case 0: return "original";
                        case 1: return "new";
                        default: return "mixed";
                    }
                case "gumptheme":
                    return CustomGumpThemeManager.Current.ToString().ToLowerInvariant();
                case "gumpopacitycustom": return (ProfileManager.CurrentProfile?.CustomGumpOpacity ?? 100).ToString();
                case "gumpopacitydurability": return (ProfileManager.CurrentProfile?.DurabilityGumpOpacity ?? 100).ToString();
                case "gumpopacitycontainer": return (ProfileManager.CurrentProfile?.ContainerOpacity ?? 100).ToString();
                case "gumpopacitycorpse": return (ProfileManager.CurrentProfile?.CorpseContainerOpacity ?? 50).ToString();
                case "gumpopacitygridborder": return (ProfileManager.CurrentProfile?.GridBorderAlpha ?? 100).ToString();
                case "gumpopacityjournal": return (ProfileManager.CurrentProfile?.JournalOpacity ?? 100).ToString();
                case "gumpopacitybuff": return (ProfileManager.CurrentProfile?.BuffBarOpacity ?? 100).ToString();
                case "gumpopacityslayer": return (ProfileManager.CurrentProfile?.SlayerBarOpacity ?? 100).ToString();
                case "gumpopacityhovermin": return (ProfileManager.CurrentProfile?.GumpHoverOpacityPercent ?? 100).ToString();
                default:
                    return string.Empty;
            }
        }

        private static bool? GetActiveState(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "autobandage": return AutoBandageManager.Enabled;
                case "petbandage": return PetBandageManager.Enabled;
                case "extbandage": return ExternalBandageManager.Enabled;
                case "bandagetimer": return BandageTimerManager.Enabled;
                case "automation": return AutomationCoordinator.Enabled;
                case "automount": return AutoMountManager.Enabled;
                case "autopack": return AutoOpenBackpackManager.Enabled;
                case "autopaper": return AutoOpenPaperdollManager.Enabled;
                case "autorearm": return AutoRearmManager.Enabled;
                case "autorespawn": return AutoRespawnTargetManager.Enabled;
                case "autostealth": return AutoStealthManager.Enabled;
                case "arrowglow": return ArrowGlowManager.Enabled;
                case "bodyscale": return BodyScaleManager.Enabled;
                case "buffexpire": return BuffExpiryToastManager.Enabled;
                case "combatstate": return CombatStateManager.Enabled;
                case "deathmarker": return DeathMarkerManager.Enabled;
                case "deathrecap": return DeathRecapManager.Enabled;
                case "dmgtype": return DamageTypeTagManager.Enabled;
                case "fullhptoast": return FullHpToastManager.Enabled;
                case "hiddenwatch": return HiddenStateWatcher.Enabled;
                case "hungeralert": return HungerThirstAlertManager.Enabled;
                case "killready": return KillReadyManager.Enabled;
                case "paragonglow": return ParagonGlowManager.Enabled;
                case "partyalert": return PartyInviteAlertManager.Enabled;
                case "petwatch": return PetWatcherManager.Enabled;
                case "poisonalert": return PoisonAlertManager.Enabled;
                case "poisoncure": return PoisonCureManager.Enabled;
                case "reagentwatch": return ReagentWatcherManager.Enabled;
                case "rangewarn": return TargetRangeWarnManager.Enabled;
                case "skillcap": return SkillCapTracker.Enabled;
                case "statalert": return StatChangeAlertManager.Enabled;
                case "focusmute": return WindowFocusMuteManager.Enabled;
                case "cdhud": return CooldownHud.Enabled;
                case "cursordist": return CursorDistanceOverlay.Enabled;
                case "cursorhint": return CursorHintOverlay.Enabled;
                case "healpulse": return HealReceivedPulse.Enabled;
                case "healbutton": return UIManager.GetGump<HealSelfButtonGump>() != null;
                case "notdot": return NotorietyDotOverlay.Enabled;
                case "pinghud": return PingHudOverlay.Enabled;
                case "partyhud": return UI.CompactPartyHud.Enabled;
                case "timerhud": return TimerStackHud.Enabled;
                case "wind": return WindParticlesOverlay.Enabled;
                case "castbar": return UI.CastProgressOverlay.Enabled;
                case "compactbars": return UI.CompactBarsOverlay.Enabled;
                case "compass": return UI.CompassOverlay.Enabled;
                case "corpsefade": return UI.CorpseFadeOverlay.Enabled;
                case "dmgsourceline": return UI.DamageSourceLineOverlay.Enabled;
                case "ghostfade": return UI.GhostFadeOverlay.Enabled;
                case "hitflash": return UI.ScreenflashOnHit.Enabled;
                case "hostileline": return UI.NearestHostileLine.Enabled;
                case "mobblood": return UI.MobBloodOverlay.Enabled;
                case "pethp": return UI.PetHpBarsOverlay.Enabled;
                case "reflectcount": return ReflectCounterManager.Enabled;
                case "targetingyou": return UI.TargetingYouAura.Enabled;
                case "trail": return UI.MoveTrailOverlay.Enabled;
                case "trailfx": return UIManager.GetGump<TrailEffectsGump>() != null;
                case "warborder": return UI.WarModeBorder.Enabled;
                case "arrowline": return UI.QuestArrowLine.Enabled;
                case "containerbadge": return UI.OpenContainerBadge.Enabled;
                case "targethighlight": return UI.LastTargetHighlight.Enabled;
                case "tgtcross": return UI.TargetCrosshair.Enabled;
                case "lowhp": return UI.LowHpVignette.Enabled;
                case "tilegrid": return UI.TileGridOverlay.Range > 0;
                case "mobhp": return UI.CombatMobHpBars.Range > 0;
                case "range": return UI.RangeIndicator.Range > 0;
                case "radius": return ProfileManager.CurrentProfile?.DisplayRadius ?? false;
                case "manaalert": return ManaStamAlertManager.ManaPct > 0;
                case "stamalert": return ManaStamAlertManager.StamPct > 0;
                case "pingwarn": return PingSpikeWarner.SpikeMs > 0;
                case "weightalert": return InventoryFullWarner.ThresholdPct > 0;
                case "idlewarn": return IdleMonitorManager.IdleSeconds > 0;
                case "loiterwarn": return HostileLoiteringWarner.Enabled;
                case "daycycle": return DayCyclePreviewManager.Enabled;
                case "ambientweather": return AmbientWeatherManager.Enabled;
                case "waterenhancement": return WaterEnhancementManager.ArtworkEnabled;
                case "wateratmosphere": return WaterEnhancementManager.AtmosphereEnabled;
                case "environment": return UIManager.GetGump<EnvironmentControlGump>() != null;
                case "gumpthemes": return UIManager.GetGump<GumpThemeSelectorGump>() != null;
                case "gumpopacityaltscroll": return ProfileManager.CurrentProfile?.EnableAlphaScrollingOnGumps ?? false;
                case "gumpopacityhoverboost": return ProfileManager.CurrentProfile?.BoostGumpOpacityOnHover ?? false;
                case "globalchat": return UIManager.GetGump<GlobalChatGump>() != null;
                case "guildchat": return UIManager.GetGump<GuildChatGump>() != null;
                case "speechhistory": return UIManager.GetGump<NearbySpeechGump>() != null;
                case "damagetracker": return UIManager.GetGump<DamageTrackerGump>() != null;
                case "options": return UIManager.GetGump<ModernOptionsGump>() != null;
                case "perfhud": return UIManager.GetGump<PerfHudGump>() != null;
                case "playerinfo": return UIManager.GetGump<PlayerInfoFloater>() != null;
                case "toastanchor": return UIManager.GetGump<ToastAnchorGump>() != null;
                case "musicplayer": return UIManager.GetGump<MusicPlayerGump>() != null;
                case "spelleffects": return UIManager.GetGump<SpellAbilityEffectsGump>() != null;
                case "bandageopts": return UIManager.GetGump<BandageOptionsGump>() != null;
                case "openjournal": return UIManager.GetGump<ResizableJournal>() != null;
                case "paperdoll": return UIManager.GetGump<PaperDollGump>() != null;
                case "sb": return UIManager.GetGump<ScriptBrowser>() != null;
                case "spellbook": return UIManager.GetGump<SpellbookGump>() != null;
                case "commands": return true;
                default: return CommandMetadata.GetRuntimeState(name);
            }
        }

        private int GetStateSignature()
        {
            unchecked
            {
                int result = CommandMetadata.StateVersion;
                foreach (string name in CommandManager.Commands.Keys)
                {
                    bool? state = GetActiveState(name);
                    if (state.HasValue) result = result * 31 + name.GetHashCode() + (state.Value ? 1 : 0);
                }
                return result;
            }
        }

        private void RequestRebuild()
        {
            _rebuildRequested = true;
        }

        public override void Update()
        {
            base.Update();

            if (Time.Ticks >= _nextStatusRefresh)
            {
                _nextStatusRefresh = (long)Time.Ticks + 500;
                int signature = GetStateSignature();
                if (signature != _lastStateSignature)
                {
                    _lastStateSignature = signature;
                    _rebuildRequested = true;
                }
            }

            if (_rebuildRequested)
            {
                _rebuildRequested = false;
                Rebuild();
            }
        }

        private static void ExecuteWithArgs(string name, string argText)
        {
            argText = (argText ?? string.Empty).Trim();
            string[] args;
            if (argText.Length == 0)
            {
                args = new[] { name };
            }
            else
            {
                string[] parts = argText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                args = new string[parts.Length + 1];
                args[0] = name;
                Array.Copy(parts, 0, args, 1, parts.Length);
            }
            CommandManager.Execute(name, args);
        }

        private static string BuildCommandLine(string name, string argText)
        {
            string args = (argText ?? string.Empty).Trim();
            return args.Length == 0 ? "-" + name : "-" + name + " " + args;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            var texture = SolidColorTextureCache.GetTexture(new Color(80, 80, 80, 255));
            var hue = ShaderHueTranslator.GetHueVector(0, false, 0.7f);
            batcher.Draw(texture, new Rectangle(x, y, W, 1), hue);
            batcher.Draw(texture, new Rectangle(x, y + H - 1, W, 1), hue);
            batcher.Draw(texture, new Rectangle(x, y, 1, H), hue);
            batcher.Draw(texture, new Rectangle(x + W - 1, y, 1, H), hue);
            return true;
        }
    }
}
