// TazUO addition: equipment target editor and optimized loadout viewer.

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentGuruGump : Gump
    {
        private const int WIDTH = 1000;
        private const int HEIGHT = 700;
        private const int LEFT_X = 24;
        private const int LEFT_WIDTH = 320;
        private const int RIGHT_X = 364;
        private const int RIGHT_WIDTH = 612;

        private static readonly GoalDefinition[] _goalDefinitions =
        {
            new GoalDefinition("Strength", "STR"),
            new GoalDefinition("Dexterity", "DEX"),
            new GoalDefinition("Intelligence", "INT"),
            new GoalDefinition("HitPoints", "Hit points"),
            new GoalDefinition("Stamina", "Stamina"),
            new GoalDefinition("Mana", "Mana"),
            new GoalDefinition("HitChanceIncrease", "HCI"),
            new GoalDefinition("DefenseChanceIncrease", "DCI"),
            new GoalDefinition("DamageIncrease", "DI"),
            new GoalDefinition("SwingSpeedIncrease", "SSI"),
            new GoalDefinition("LowerManaCost", "LMC"),
            new GoalDefinition("LowerReagentCost", "LRC"),
            new GoalDefinition("SpellDamageIncrease", "SDI"),
            new GoalDefinition("HitPointRegeneration", "HP regen"),
            new GoalDefinition("StaminaRegeneration", "Stamina regen"),
            new GoalDefinition("ManaRegeneration", "Mana regen"),
            new GoalDefinition("PhysicalResist", "Physical resist"),
            new GoalDefinition("FireResist", "Fire resist"),
            new GoalDefinition("ColdResist", "Cold resist"),
            new GoalDefinition("PoisonResist", "Poison resist"),
            new GoalDefinition("EnergyResist", "Energy resist")
        };

        private readonly Dictionary<string, GoalInput> _goalInputs =
            new Dictionary<string, GoalInput>(StringComparer.Ordinal);
        private readonly Dictionary<string, GoalInput> _skillGoalInputs =
            new Dictionary<string, GoalInput>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Checkbox> _goalMustInputs =
            new Dictionary<string, Checkbox>(StringComparer.Ordinal);
        private readonly Dictionary<string, Checkbox> _skillMustInputs =
            new Dictionary<string, Checkbox>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Checkbox> _goalPreserveInputs =
            new Dictionary<string, Checkbox>(StringComparer.Ordinal);
        private readonly Dictionary<string, Checkbox> _skillPreserveInputs =
            new Dictionary<string, Checkbox>(StringComparer.OrdinalIgnoreCase);
        private readonly VBoxContainer _goalBox;
        private readonly VBoxContainer _resultBox;
        private readonly Label _detected;
        private readonly Label _skills;
        private readonly Label _fixedItems;
        private readonly Label _status;
        private readonly NiceButton _buildButton;
        private readonly NiceButton _analyzeButton;
        private readonly NiceButton[] _loadoutButtons = new NiceButton[4];
        private readonly List<EquipmentGuruManager.RecommendedItem> _visibleChanges =
            new List<EquipmentGuruManager.RecommendedItem>();

        private EquipmentGuruManager.EquipmentGuruBuild _selectedBuild =
            EquipmentGuruManager.EquipmentGuruBuild.Auto;
        private EquipmentGuruManager.EquipmentGuruBuild _goalBuild;
        private EquipmentGuruManager.Analysis _analysis;
        private int _selectedLoadout;
        private bool _analysisQueued;
        private long _analyzeAt;
        private long _nextResistanceCheck;
        private int _lastResistanceSignature;

        internal EquipmentGuruGump() : base(0, 0)
        {
            X = ProfileManager.CurrentProfile?.EquipmentGuruPosition.X ?? 150;
            Y = ProfileManager.CurrentProfile?.EquipmentGuruPosition.Y ?? 80;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.EquipmentGuru, 0.97f));
            AddSurface(20, 10, WIDTH - 40, 52, 0.54f);
            Add(new Label("EQUIPMENT GURU", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), 580, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Analyze real skills and equipped totals, then build better sets from the Item Finder catalog.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), 820, font: 1)
            {
                X = 24,
                Y = 38
            });
            NiceButton close = CreateButton(948, 18, 28, 28, "X", 99);
            close.SetTooltip("Close Equipment Guru");
            Add(close);
            AddAccent(24, 61, WIDTH - 48);

            _goalBuild = EquipmentGuruManager.DetectBuild();
            Add(_buildButton = CreateButton(24, 74, 176, 32, string.Empty, 1));
            Add(CreateButton(208, 74, 112, 32, "Reset goals", 2));
            Add(_analyzeButton = CreateButton(328, 74, 140, 32, "Analyze gear", 3));
            Add(CreateButton(474, 74, 92, 32, "Rules", 5));
            Add(CreateButton(572, 74, 108, 32, "Advanced", 6));
            Add(new Label("Auto uses the strongest real combat or magic skill. Goal edits save per character and build.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), 286, font: 1)
            {
                X = 690,
                Y = 82
            });

            Add(new Label("CHARACTER READ", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH, font: 1)
            {
                X = LEFT_X,
                Y = 124
            });
            AddSurface(LEFT_X, 148, LEFT_WIDTH, 134, 0.44f);
            Add(_detected = new Label(string.Empty, true,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH - 16, font: 1)
            {
                X = LEFT_X + 8,
                Y = 157
            });
            Add(new Label("Real skills (base / effective)", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH - 16, font: 1)
            {
                X = LEFT_X + 8,
                Y = 181
            });
            Add(_skills = new Label("Analyze to read skills.", true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH - 16, font: 1,
                style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = LEFT_X + 8,
                Y = 202
            });
            Add(new Label("Locked weapon / spellbook / talisman", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH - 16, font: 1)
            {
                X = LEFT_X + 8,
                Y = 227
            });
            Add(_fixedItems = new Label("Analyze to read equipment.", true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH - 16, font: 1,
                style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = LEFT_X + 8,
                Y = 248
            });

            Add(new Label("EDITABLE TARGETS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), LEFT_WIDTH, font: 1)
            {
                X = LEFT_X,
                Y = 298
            });
            AddSurface(LEFT_X, 322, LEFT_WIDTH, 334, 0.44f);
            var goalScroll = new ScrollArea(LEFT_X + 4, 326, LEFT_WIDTH - 8, 326, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            _goalBox = new VBoxContainer(goalScroll.Width - goalScroll.ScrollBarWidth() - 3, 0, 0);
            goalScroll.Add(_goalBox);
            Add(goalScroll);

            Add(new AlphaBlendControl(0.65f)
            {
                X = 354,
                Y = 123,
                Width = 1,
                Height = 533,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.EquipmentGuru)
            });

            Add(new Label("LOADOUT OPTIONS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), RIGHT_WIDTH, font: 1)
            {
                X = RIGHT_X,
                Y = 124
            });
            for (int i = 0; i < _loadoutButtons.Length; i++)
            {
                _loadoutButtons[i] = CreateButton(RIGHT_X + i * 105, 146, 99, 30,
                    $"Loadout {i + 1}", 10 + i);
                _loadoutButtons[i].SetTooltip("Show this equipment option and its tradeoffs.");
                Add(_loadoutButtons[i]);
            }
            NiceButton saveLoadout = CreateButton(844, 146, 132, 30, "Save loadout", 4);
            saveLoadout.SetTooltip("Save the selected recommendation for the -loadout command.");
            Add(saveLoadout);

            AddSurface(RIGHT_X, 184, RIGHT_WIDTH, 472, 0.44f);
            var resultScroll = new ScrollArea(RIGHT_X + 4, 188, RIGHT_WIDTH - 8, 464, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            resultScroll.Add(_resultBox = new VBoxContainer(
                resultScroll.Width - resultScroll.ScrollBarWidth() - 3, 0, 0));
            Add(resultScroll);

            AddAccent(24, 674, WIDTH - 48);
            Add(_status = new Label("Adjust targets, then Analyze gear.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 679
            });

            RefreshBuildAndGoals();
            ShowPlaceholder();
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;

        public override void Dispose()
        {
            if (!IsDisposed && _goalInputs.Count == _goalDefinitions.Length)
            {
                SaveCurrentGoals();
            }

            base.Dispose();
        }

        public override void Update()
        {
            base.Update();
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.EquipmentGuruPosition = Location;

            if (_analysisQueued && Time.Ticks >= _analyzeAt)
            {
                _analysisQueued = false;
                _analyzeButton.SetText("Analyze gear");
                RunAnalysis();
            }

            if (_analysis != null && !_analysisQueued && Time.Ticks >= _nextResistanceCheck)
            {
                _nextResistanceCheck = (long)Time.Ticks + 1000;
                if (EquipmentGuruManager.LiveResistanceSignature() != _lastResistanceSignature)
                    RunAnalysis(false);
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99)
            {
                Dispose();
                return;
            }

            if (buttonID == 1)
            {
                SaveCurrentGoals();
                int count = Enum.GetValues(typeof(EquipmentGuruManager.EquipmentGuruBuild)).Length;
                _selectedBuild = (EquipmentGuruManager.EquipmentGuruBuild)
                    (((int)_selectedBuild + 1) % count);
                RefreshBuildAndGoals();
                return;
            }

            if (buttonID == 2)
            {
                EquipmentGuruManager.EquipmentGuruGoals goals =
                    EquipmentGuruManager.ResetGoals(_goalBuild);
                RebuildGoalRows(goals);
                LoadInputs(goals);
                _status.Text = $"{EquipmentGuruManager.BuildName(_goalBuild)} defaults restored.";
                return;
            }

            if (buttonID == 3)
            {
                if (_analysisQueued)
                {
                    _analysisQueued = false;
                    _analyzeButton.SetText("Analyze gear");
                    _status.Text = "Analysis cancelled.";
                }
                else
                {
                    SaveCurrentGoals();
                    _analysisQueued = true;
                    _analyzeAt = (long)Time.Ticks + 250;
                    _analyzeButton.SetText("Cancel");
                    _status.Text = "Preparing catalog and candidate analysis…";
                }
                return;
            }

            if (buttonID == 4)
            {
                SaveSelectedLoadout();
                return;
            }

            if (buttonID == 5)
            {
                EquipmentGuruRulesGump rules = UIManager.GetGump<EquipmentGuruRulesGump>();
                if (rules == null) UIManager.Add(new EquipmentGuruRulesGump(this));
                else rules.BringOnTop();
                return;
            }

            if (buttonID == 6)
            {
                EquipmentGuruAdvancedGoalsGump advanced = UIManager.GetGump<EquipmentGuruAdvancedGoalsGump>();
                if (advanced == null) UIManager.Add(new EquipmentGuruAdvancedGoalsGump(_goalBuild));
                else advanced.BringOnTop();
                return;
            }

            if (buttonID >= 10 && buttonID < 14)
            {
                int loadout = buttonID - 10;
                if (_analysis != null && loadout < _analysis.Loadouts.Count)
                {
                    _selectedLoadout = loadout;
                    RenderSelectedLoadout();
                    _status.Text = "Viewing "
                        + KindLabel(_analysis.Loadouts[_selectedLoadout].Kind)
                        + ". Current values are left; selected values are right.";
                }
                return;
            }

            if (buttonID >= 2000)
            {
                int index = buttonID - 2000;
                if (index >= 0 && index < _visibleChanges.Count)
                {
                    EquipmentGuruManager.ToggleExcluded(_visibleChanges[index].Item.Serial);
                    _status.Text = $"Excluded {_visibleChanges[index].Item.Name}; analyzing again.";
                    RunAnalysis();
                }
            }
            else if (buttonID >= 1000)
            {
                int index = buttonID - 1000;

                if (index >= 0 && index < _visibleChanges.Count)
                {
                    ItemFinderManager.Locate(_visibleChanges[index].Item);
                    _status.Text = $"Locating {_visibleChanges[index].Item.Name}.";
                }
            }
        }

        private static string KindLabel(EquipmentGuruManager.LoadoutKind kind)
        {
            switch (kind)
            {
                case EquipmentGuruManager.LoadoutKind.Upgrade: return "upgrade";
                case EquipmentGuruManager.LoadoutKind.MustTradeoff: return "MIN/KEEP tradeoff";
                default: return "current equipment";
            }
        }

        private void RefreshBuildAndGoals()
        {
            EquipmentGuruManager.EquipmentGuruBuild detected = EquipmentGuruManager.DetectBuild();
            _goalBuild = _selectedBuild == EquipmentGuruManager.EquipmentGuruBuild.Auto
                ? detected
                : _selectedBuild;
            _buildButton.SetText(_selectedBuild == EquipmentGuruManager.EquipmentGuruBuild.Auto
                ? $"Build: Auto ({EquipmentGuruManager.BuildName(detected)})"
                : $"Build: {EquipmentGuruManager.BuildName(_selectedBuild)}");
            EquipmentGuruManager.EquipmentGuruGoals goals =
                EquipmentGuruManager.GetGoals(_goalBuild);
            RebuildGoalRows(goals);
            LoadInputs(goals);
            _detected.Text = $"Detected: {EquipmentGuruManager.BuildName(detected)}";
        }

        private void RunAnalysis(bool saveGoals = true)
        {
            EquipmentGuruManager.EquipmentGuruGoals goals = ReadInputs();
            if (saveGoals)
                EquipmentGuruManager.SaveGoals(_goalBuild, goals);
            _analysis = EquipmentGuruManager.Analyze(_selectedBuild, goals);
            _lastResistanceSignature = EquipmentGuruManager.LiveResistanceSignature();
            _nextResistanceCheck = (long)Time.Ticks + 1000;
            _selectedLoadout = 0;

            _detected.Text = $"Detected: {EquipmentGuruManager.BuildName(_analysis.DetectedBuild)}";
            _skills.Text = _analysis.UsedSkills.Count == 0
                ? "No combat or magic skills at 70+ detected."
                : string.Join("  •  ", _analysis.UsedSkills.ConvertAll(skill =>
                    $"{skill.Name} {skill.Base:0.#}/{skill.Value:0.#}"));
            _fixedItems.Text = _analysis.FixedItems ?? "None detected";
            _fixedItems.SetTooltip(_analysis.FixedItems ?? string.Empty);
            _status.Text = _analysis.Status ?? "Analysis complete.";

            for (int i = 0; i < _loadoutButtons.Length; i++)
            {
                _loadoutButtons[i].SetText(i < _analysis.Loadouts.Count
                    ? $"Loadout {i + 1}"
                    : "—");
            }

            RenderSelectedLoadout();
        }

        internal void RulesChanged()
        {
            _status.Text = "Equipment rules changed. Analyze again to refresh recommendations.";
        }

        private void RenderSelectedLoadout()
        {
            _resultBox.Clear();
            _visibleChanges.Clear();

            if (_analysis == null || _analysis.Loadouts.Count == 0)
            {
                ShowPlaceholder();
                return;
            }

            if (_selectedLoadout >= _analysis.Loadouts.Count)
            {
                _selectedLoadout = 0;
            }

            EquipmentGuruManager.Loadout loadout = _analysis.Loadouts[_selectedLoadout];
            UpdateLoadoutButtons();
            int improvedTargets = loadout.Metrics.FindAll(metric =>
                metric.CountsAsTarget && IsImprovedMetric(metric)).Count;
            string heading;
            switch (loadout.Kind)
            {
                case EquipmentGuruManager.LoadoutKind.Upgrade:
                    heading = $"UPGRADE  •  +{ScorePercent(loadout):0.0}%  •  {improvedTargets} TARGET(S)  •  {loadout.Changes.Count} REPLACEMENT(S)";
                    break;
                case EquipmentGuruManager.LoadoutKind.MustTradeoff:
                    heading = $"MIN/KEEP TRADEOFF  •  {ScorePercent(loadout):0.0}%  •  {loadout.Changes.Count} REPLACEMENT(S)";
                    break;
                default:
                    heading = loadout.MeetsRequirements
                        ? "CURRENT SET  •  BASELINE  •  ALL MIN/KEEP REQUIREMENTS MET"
                        : "CURRENT SET  •  BASELINE  •  MIN/KEEP REQUIREMENT MISSED";
                    break;
            }
            _resultBox.Add(new HeadingRow(heading, _resultBox.Width - 4));
            _resultBox.Add(new MessageRow(BuildSummary(loadout),
                _resultBox.Width - 4, 52));
            _resultBox.Add(new MessageRow(
                "CURRENT  →  SELECTED LOADOUT  /  TARGET",
                _resultBox.Width - 4, 30));

            for (int i = 0; i < loadout.Metrics.Count; i += 2)
            {
                EquipmentGuruManager.Metric left = loadout.Metrics[i];
                EquipmentGuruManager.Metric right = i + 1 < loadout.Metrics.Count
                    ? loadout.Metrics[i + 1]
                    : null;
                _resultBox.Add(new MetricRow(left, right, _resultBox.Width - 4));
            }

            _resultBox.Add(new HeadingRow("ITEM REPLACEMENTS", _resultBox.Width - 4));

            if (loadout.Changes.Count == 0)
            {
                _resultBox.Add(new MessageRow(
                    loadout.Kind == EquipmentGuruManager.LoadoutKind.Current
                        ? "This is the equipped baseline. No items are replaced."
                        : "No item replacements are required.",
                    _resultBox.Width - 4));
                return;
            }

            foreach (EquipmentGuruManager.RecommendedItem change in loadout.Changes)
            {
                int index = _visibleChanges.Count;
                _visibleChanges.Add(change);
                _resultBox.Add(new ChangeRow(change, index, _resultBox.Width - 4));
            }
        }

        private void UpdateLoadoutButtons()
        {
            int upgradeNumber = 0;
            int tradeoffNumber = 0;
            for (int i = 0; i < _loadoutButtons.Length; i++)
            {
                if (_analysis == null || i >= _analysis.Loadouts.Count)
                {
                    _loadoutButtons[i].SetText("—");
                }
                else
                {
                    EquipmentGuruManager.Loadout loadout = _analysis.Loadouts[i];
                    string label;
                    switch (loadout.Kind)
                    {
                        case EquipmentGuruManager.LoadoutKind.Upgrade:
                            label = $"Upgrade {++upgradeNumber}";
                            break;
                        case EquipmentGuruManager.LoadoutKind.MustTradeoff:
                            label = $"Tradeoff {++tradeoffNumber}";
                            break;
                        default:
                            label = "Current";
                            break;
                    }
                    _loadoutButtons[i].SetText(i == _selectedLoadout
                        ? $"● {label}"
                        : label);
                }
            }
        }

        private double ScorePercent(EquipmentGuruManager.Loadout loadout)
        {
            if (_analysis == null || Math.Abs(_analysis.CurrentScore) < 0.0001)
            {
                return 0;
            }

            return loadout.Improvement / _analysis.CurrentScore * 100.0;
        }

        private static string BuildSummary(EquipmentGuruManager.Loadout loadout)
        {
            var fixedTargets = new List<string>();
            var losses = new List<string>();

            foreach (EquipmentGuruManager.Metric metric in loadout.Metrics)
            {
                int current = EffectiveMetric(metric, metric.Current);
                int recommended = EffectiveMetric(metric, metric.Recommended);
                if (metric.CountsAsTarget && !metric.LowerIsBetter
                    && current < metric.Target && recommended >= metric.Target)
                {
                    fixedTargets.Add(metric.Name);
                }
                else if (metric.CountsAsTarget && metric.LowerIsBetter
                    && recommended < current)
                {
                    fixedTargets.Add(metric.Name);
                }
                if (recommended < current && !metric.LowerIsBetter)
                {
                    losses.Add($"{metric.Name} -{current - recommended}");
                }
                else if (metric.LowerIsBetter && recommended > current)
                {
                    losses.Add($"{metric.Name} +{recommended - current}");
                }
            }

            string fixes = fixedTargets.Count == 0
                ? "Fixes: none"
                : "Fixes: " + string.Join(", ", fixedTargets.Take(4));
            string costs = losses.Count == 0
                ? "Costs: none"
                : "Costs: " + string.Join(", ", losses.Take(4))
                    + (losses.Count > 4 ? ", …" : string.Empty);
            string requirements = loadout.MeetsRequirements
                ? "MIN/KEEP: met"
                : "MIN/KEEP: missed";
            return $"{fixes}  •  {costs}  •  {requirements}";
        }

        private static int EffectiveMetric(EquipmentGuruManager.Metric metric, int value)
        {
            return metric.DisplayCap > 0 ? Math.Min(value, metric.DisplayCap) : value;
        }

        private void SaveSelectedLoadout()
        {
            if (_analysis == null || _analysis.Loadouts.Count == 0)
            {
                _status.Text = "Analyze equipment before saving a loadout.";
                return;
            }

            if (_selectedLoadout >= _analysis.Loadouts.Count)
            {
                _selectedLoadout = 0;
            }

            string name = $"Guru {EquipmentGuruManager.BuildName(_goalBuild)} {_selectedLoadout + 1}";
            bool saved = QuickLoadoutManager.SaveSet(name,
                _analysis.Loadouts[_selectedLoadout].Items);
            _status.Text = saved
                ? $"Saved '{name}'. Use -loadout load {name}."
                : "Could not save this loadout.";
        }

        private void ShowPlaceholder()
        {
            _resultBox.Clear();
            _resultBox.Add(new MessageRow(
                "Equipment Guru keeps the equipped weapon or spellbook and talisman fixed. "
                + "A shield may be replaced when used with a one-handed weapon. Results use equipped items "
                + "plus wearable items already stored in the Item Finder catalog. Each detected real skill "
                + "has an editable final target; matching equipment skill bonuses reduce the real points needed. "
                + "Targets stop adding score once reached. MIN enforces the entered value; KEEP prevents lowering the current value.",
                _resultBox.Width - 4, 112));
        }

        private static bool IsImprovedMetric(EquipmentGuruManager.Metric metric)
        {
            int current = EffectiveMetric(metric, metric.Current);
            int recommended = EffectiveMetric(metric, metric.Recommended);
            if (metric.LowerIsBetter)
            {
                return recommended < current;
            }

            return recommended > current
                && (metric.Target <= 0 || current < metric.Target);
        }

        private void SaveCurrentGoals()
        {
            EquipmentGuruManager.SaveGoals(_goalBuild, ReadInputs());
        }

        private void LoadInputs(EquipmentGuruManager.EquipmentGuruGoals goals)
        {
            foreach (GoalDefinition definition in _goalDefinitions)
            {
                _goalInputs[definition.Key].SetText(GetGoal(goals, definition.Key).ToString());
                _goalMustInputs[definition.Key].IsChecked =
                    goals.MustTargets.Contains(definition.Key);
                _goalPreserveInputs[definition.Key].IsChecked =
                    goals.PreserveTargets.Contains(definition.Key);
            }

            foreach (KeyValuePair<string, GoalInput> input in _skillGoalInputs)
            {
                goals.SkillTargets.TryGetValue(input.Key, out int target);
                input.Value.SetText(target.ToString());
                _skillMustInputs[input.Key].IsChecked =
                    goals.MustTargets.Contains("SkillTarget:" + input.Key);
                _skillPreserveInputs[input.Key].IsChecked =
                    goals.PreserveTargets.Contains("SkillTarget:" + input.Key);
            }
        }

        private void RebuildGoalRows(EquipmentGuruManager.EquipmentGuruGoals goals)
        {
            _goalBox.Clear();
            _goalInputs.Clear();
            _skillGoalInputs.Clear();
            _goalMustInputs.Clear();
            _skillMustInputs.Clear();
            _goalPreserveInputs.Clear();
            _skillPreserveInputs.Clear();

            foreach (GoalDefinition definition in _goalDefinitions)
            {
                var row = new GoalRow(definition.Label, _goalBox.Width - 4);
                _goalInputs[definition.Key] = row.Input;
                _goalMustInputs[definition.Key] = row.Minimum;
                _goalPreserveInputs[definition.Key] = row.Preserve;
                _goalBox.Add(row);
            }

            foreach (EquipmentGuruManager.SkillSnapshot skill in
                EquipmentGuruManager.GetActiveSkills(goals))
            {
                var row = new GoalRow(
                    $"{skill.Name} target (real {skill.Base:0.#})", _goalBox.Width - 4);
                row.Input.SetTooltip(
                    $"Desired final {skill.Name}. Equipment bonuses reduce the real skill points needed; cap {skill.Cap:0.#}.");
                _skillGoalInputs[skill.Name] = row.Input;
                _skillMustInputs[skill.Name] = row.Minimum;
                _skillPreserveInputs[skill.Name] = row.Preserve;
                _goalBox.Add(row);
            }
        }

        private EquipmentGuruManager.EquipmentGuruGoals ReadInputs()
        {
            EquipmentGuruManager.EquipmentGuruGoals goals =
                EquipmentGuruManager.GetGoals(_goalBuild);

            foreach (GoalDefinition definition in _goalDefinitions)
            {
                int value = 0;
                int.TryParse(_goalInputs[definition.Key].Text, out value);
                SetGoal(goals, definition.Key, Math.Max(0, value));
                SetRequirement(goals, definition.Key,
                    _goalMustInputs[definition.Key].IsChecked,
                    _goalPreserveInputs[definition.Key].IsChecked);
            }

            foreach (KeyValuePair<string, GoalInput> input in _skillGoalInputs)
            {
                int value = 0;
                int.TryParse(input.Value.Text, out value);
                goals.SkillTargets[input.Key] = Math.Max(0, value);
                SetRequirement(goals, "SkillTarget:" + input.Key,
                    _skillMustInputs[input.Key].IsChecked,
                    _skillPreserveInputs[input.Key].IsChecked);
            }

            return goals;
        }

        private static void SetRequirement(EquipmentGuruManager.EquipmentGuruGoals goals,
            string key, bool minimum, bool preserve)
        {
            if (minimum)
            {
                goals.MustTargets.Add(key);
            }
            else
            {
                goals.MustTargets.Remove(key);
            }

            if (preserve)
            {
                goals.PreserveTargets.Add(key);
            }
            else
            {
                goals.PreserveTargets.Remove(key);
            }
        }

        private static int GetGoal(EquipmentGuruManager.EquipmentGuruGoals goals, string key)
        {
            switch (key)
            {
                case "Strength": return goals.Strength;
                case "Dexterity": return goals.Dexterity;
                case "Intelligence": return goals.Intelligence;
                case "HitPoints": return goals.HitPoints;
                case "Stamina": return goals.Stamina;
                case "Mana": return goals.Mana;
                case "HitChanceIncrease": return goals.HitChanceIncrease;
                case "DefenseChanceIncrease": return goals.DefenseChanceIncrease;
                case "DamageIncrease": return goals.DamageIncrease;
                case "SwingSpeedIncrease": return goals.SwingSpeedIncrease;
                case "LowerManaCost": return goals.LowerManaCost;
                case "LowerReagentCost": return goals.LowerReagentCost;
                case "FasterCasting": return goals.FasterCasting;
                case "FasterCastRecovery": return goals.FasterCastRecovery;
                case "SpellDamageIncrease": return goals.SpellDamageIncrease;
                case "HitPointRegeneration": return goals.HitPointRegeneration;
                case "StaminaRegeneration": return goals.StaminaRegeneration;
                case "ManaRegeneration": return goals.ManaRegeneration;
                case "PhysicalResist": return ResistTarget(goals.PhysicalResist, goals.Resist);
                case "FireResist": return ResistTarget(goals.FireResist, goals.Resist);
                case "ColdResist": return ResistTarget(goals.ColdResist, goals.Resist);
                case "PoisonResist": return ResistTarget(goals.PoisonResist, goals.Resist);
                case "EnergyResist": return ResistTarget(goals.EnergyResist, goals.Resist);
                case "Luck": return goals.Luck;
                default: return 0;
            }
        }

        private static void SetGoal(EquipmentGuruManager.EquipmentGuruGoals goals,
            string key, int value)
        {
            switch (key)
            {
                case "Strength": goals.Strength = value; break;
                case "Dexterity": goals.Dexterity = value; break;
                case "Intelligence": goals.Intelligence = value; break;
                case "HitPoints": goals.HitPoints = value; break;
                case "Stamina": goals.Stamina = value; break;
                case "Mana": goals.Mana = value; break;
                case "HitChanceIncrease": goals.HitChanceIncrease = value; break;
                case "DefenseChanceIncrease": goals.DefenseChanceIncrease = value; break;
                case "DamageIncrease": goals.DamageIncrease = value; break;
                case "SwingSpeedIncrease": goals.SwingSpeedIncrease = value; break;
                case "LowerManaCost": goals.LowerManaCost = value; break;
                case "LowerReagentCost": goals.LowerReagentCost = value; break;
                case "FasterCasting": goals.FasterCasting = value; break;
                case "FasterCastRecovery": goals.FasterCastRecovery = value; break;
                case "SpellDamageIncrease": goals.SpellDamageIncrease = value; break;
                case "HitPointRegeneration": goals.HitPointRegeneration = value; break;
                case "StaminaRegeneration": goals.StaminaRegeneration = value; break;
                case "ManaRegeneration": goals.ManaRegeneration = value; break;
                case "PhysicalResist": goals.PhysicalResist = value; break;
                case "FireResist": goals.FireResist = value; break;
                case "ColdResist": goals.ColdResist = value; break;
                case "PoisonResist": goals.PoisonResist = value; break;
                case "EnergyResist": goals.EnergyResist = value; break;
                case "Luck": goals.Luck = value; break;
            }
        }

        private static int ResistTarget(int individualTarget, int sharedTarget)
        {
            return individualTarget >= 0 ? individualTarget : sharedTarget;
        }

        private void AddSurface(int x, int y, int width, int height, float alpha)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.EquipmentGuru, alpha));
        }

        private void AddAccent(int x, int y, int width)
        {
            Add(new AlphaBlendControl(0.75f)
            {
                X = x,
                Y = y,
                Width = width,
                Height = 1,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.EquipmentGuru)
            });
        }

        private static NiceButton CreateButton(int x, int y, int width, int height,
            string text, int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.EquipmentGuru);
        }

        private sealed class GoalDefinition
        {
            internal GoalDefinition(string key, string label)
            {
                Key = key;
                Label = label;
            }

            internal string Key { get; }
            internal string Label { get; }
        }

        private sealed class GoalRow : Control
        {
            internal GoalRow(string label, int width)
            {
                Width = width;
                Height = 31;
                Add(new Label(label, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru),
                    width - 190, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 6,
                    Y = 7
                });
                Add(Minimum = new Checkbox(0x00D2, 0x00D3, "MIN", 1,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
                {
                    X = width - 184,
                    Y = 7
                });
                Minimum.SetTooltip("Minimum: reject a loadout below the entered target.");
                Add(Preserve = new Checkbox(0x00D2, 0x00D3, "KEEP", 1,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
                {
                    X = width - 128,
                    Y = 7
                });
                Preserve.SetTooltip("Keep: reject a loadout below your current value.");
                Minimum.ValueChanged += (sender, e) =>
                {
                    if (Minimum.IsChecked)
                        Preserve.IsChecked = false;
                };
                Preserve.ValueChanged += (sender, e) =>
                {
                    if (Preserve.IsChecked)
                        Minimum.IsChecked = false;
                };
                Add(FeatureGumpArtwork.CreateSurface(width - 66, 3, 58, 25,
                    FeatureGumpArtworkKind.EquipmentGuru, 0.68f, true));
                Add(Input = new GoalInput
                {
                    X = width - 61,
                    Y = 7,
                    Width = 48,
                    Height = 18
                });
            }

            internal GoalInput Input { get; }
            internal Checkbox Minimum { get; }
            internal Checkbox Preserve { get; }
        }

        private sealed class GoalInput : StbTextBox
        {
            internal GoalInput()
                : base(1, 9999, 48, true,
                    hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru))
            {
                NumbersOnly = true;
            }

            private bool _selectAll;

            internal override void OnFocusEnter()
            {
                _selectAll = !IsFocused;
                base.OnFocusEnter();

                if (_selectAll)
                {
                    SelectAll();
                }
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                base.OnMouseUp(x, y, button);

                if (button == MouseButtonType.Left && _selectAll)
                {
                    SelectAll();
                    _selectAll = false;
                }
            }
        }

        private sealed class HeadingRow : Control
        {
            internal HeadingRow(string text, int width)
            {
                Width = width;
                Height = 34;
                AcceptMouseInput = false;
                Add(new Label(text, true,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru),
                    width - 12, font: 1)
                {
                    X = 6,
                    Y = 8
                });
            }
        }

        private sealed class MetricRow : Control
        {
            internal MetricRow(EquipmentGuruManager.Metric left,
                EquipmentGuruManager.Metric right, int width)
            {
                Width = width;
                Height = 28;
                AcceptMouseInput = false;
                AddMetric(left, 6, width / 2 - 10);

                if (right != null)
                {
                    AddMetric(right, width / 2 + 4, width / 2 - 10);
                }
            }

            private void AddMetric(EquipmentGuruManager.Metric metric, int x, int width)
            {
                int current = EffectiveMetric(metric, metric.Current);
                int recommended = EffectiveMetric(metric, metric.Recommended);
                bool improved = IsImprovedMetric(metric);
                bool worsened = metric.LowerIsBetter
                    ? recommended > current
                    : recommended < current;
                ushort hue = improved
                    ? FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru)
                    : worsened
                        ? (ushort)0x0021
                        : FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru);
                string target = metric.Target > 0 && !metric.LowerIsBetter
                    ? $" / {metric.Target}"
                    : string.Empty;
                string requirement = metric.IsPreserve
                    ? "  [KEEP]"
                    : metric.IsMust ? "  [MIN]" : string.Empty;
                Add(new Label(
                    $"{metric.Name}  {FormatMetric(metric.Current, metric.DisplayCap)} → "
                    + $"{FormatMetric(metric.Recommended, metric.DisplayCap)}{target}{requirement}",
                    true, hue, width, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = x,
                    Y = 5
                });
            }

            private static string FormatMetric(int value, int cap)
            {
                return cap > 0 && value > cap
                    ? $"{cap} ({value} raw)"
                    : value.ToString();
            }
        }

        private sealed class ChangeRow : Control
        {
            private EquipmentComparisonGump _comparison;

            internal ChangeRow(EquipmentGuruManager.RecommendedItem change, int index, int width)
            {
                Width = width;
                Height = 58;
                Add(new AlphaBlendControl(0.28f)
                {
                    Width = width,
                    Height = 56,
                    BaseColor = Color.Black
                });
                Add(new Label(change.Layer.ToString(), true,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru),
                    96, font: 1)
                {
                    X = 8,
                    Y = 6
                });
                Add(new Label(change.Item.Name, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru),
                    width - 204, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 105,
                    Y = 6
                });
                Add(new Label($"replaces {change.CurrentName}  •  {change.Item.Location}", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru),
                    width - 122, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 31
                });
                MouseEnter += (sender, e) =>
                {
                    if (_comparison == null || _comparison.IsDisposed)
                    {
                        _comparison = new EquipmentComparisonGump(change, this);
                        UIManager.Add(_comparison);
                    }
                };
                NiceButton locate = CreateButton(width - 150, 15, 68, 28, "Locate", 1000 + index);
                locate.SetTooltip("Locate this replacement item.");
                Add(locate);
                NiceButton exclude = CreateButton(width - 78, 15, 70, 28, "Exclude", 2000 + index);
                exclude.SetTooltip("Exclude this item from future recommendations.");
                Add(exclude);
            }
        }

        private sealed class EquipmentComparisonGump : Gump
        {
            private const int COMPARISON_WIDTH = 760;
            private const int COLUMN_WIDTH = 360;
            private const int ROW_HEIGHT = 18;
            private readonly Control _hoverReference;

            internal EquipmentComparisonGump(EquipmentGuruManager.RecommendedItem change,
                Control hoverReference) : base(0, 0)
            {
                _hoverReference = hoverReference;
                X = Mouse.Position.X + 12;
                Y = Mouse.Position.Y + 12;
                Width = COMPARISON_WIDTH;
                CanMove = false;
                AcceptMouseInput = false;
                CanCloseWithRightClick = false;
                WantUpdateSize = false;

                List<ComparisonLine> current = ParseProperties(change.CurrentItem);
                List<ComparisonLine> replacement = ParseProperties(change.Item);
                Compare(current, replacement);
                int rowCount = Math.Max(current.Count, replacement.Count);
                Height = Math.Max(120,
                    92 + rowCount * ROW_HEIGHT);

                Add(FeatureGumpArtwork.CreateSurface(0, 0, Width, Height,
                    FeatureGumpArtworkKind.EquipmentGuru, 0.98f, true));
                Add(new Label("EQUIPPED", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru),
                    COLUMN_WIDTH - 20, font: 1)
                {
                    X = 18,
                    Y = 10
                });
                Add(new Label(change.CurrentName ?? "Empty", true,
                    FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru),
                    COLUMN_WIDTH - 20, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 18,
                    Y = 31
                });
                Add(new Label("REPLACEMENT", true,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru),
                    COLUMN_WIDTH - 20, font: 1)
                {
                    X = 390,
                    Y = 10
                });
                Add(new Label(change.Item?.Name ?? "Unknown item", true,
                    FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru),
                    COLUMN_WIDTH - 20, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 390,
                    Y = 31
                });
                Add(new AlphaBlendControl(0.7f)
                {
                    X = 379,
                    Y = 10,
                    Width = 1,
                    Height = Height - 20,
                    BaseColor = FeatureGumpArtwork.BorderColor(
                        FeatureGumpArtworkKind.EquipmentGuru)
                });
                AddPropertyRows(current, 18);
                AddPropertyRows(replacement, 390);
                Add(new Label("GREEN  better, added, or removed penalty", true,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru),
                    COLUMN_WIDTH - 20, font: 1)
                {
                    X = 18,
                    Y = 66 + rowCount * ROW_HEIGHT
                });
                Add(new Label("RED  worse, lost, or new penalty", true, 0x0021,
                    COLUMN_WIDTH - 20, font: 1)
                {
                    X = 390,
                    Y = 66 + rowCount * ROW_HEIGHT
                });
                SetInScreen();
            }

            public override bool ShouldBeSaved => false;

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (_hoverReference == null || _hoverReference.IsDisposed
                    || !_hoverReference.MouseIsOver)
                {
                    Dispose();
                    return false;
                }

                return base.Draw(batcher, x, y);
            }

            private void AddPropertyRows(List<ComparisonLine> lines, int x)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    ComparisonLine line = lines[i];
                    Add(new Label(line.Text, true, HueFor(line.Tone),
                        COLUMN_WIDTH - 20, font: 1,
                        style: FontStyle.BlackBorder | FontStyle.Cropped)
                    {
                        X = x,
                        Y = 58 + i * ROW_HEIGHT
                    });
                }
            }

            private static List<ComparisonLine> ParseProperties(
                ItemFinderManager.SearchResult item)
            {
                var result = new List<ComparisonLine>();

                if (string.IsNullOrWhiteSpace(item?.AllProperties)
                    || string.Equals(item.AllProperties, "No item properties loaded",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new ComparisonLine
                    {
                        Text = "No item properties loaded.",
                        Key = "noproperties"
                    });
                    return result;
                }

                var properties = new ItemPropertiesData(
                    (item.Name ?? "Item") + "\n" + item.AllProperties);

                foreach (ItemPropertiesData.SinglePropertyData property in
                    properties.singlePropertyData)
                {
                    string text = CleanText(property.OriginalString);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    result.Add(new ComparisonLine
                    {
                        Text = text,
                        Key = NormalizeProperty(property.Name),
                        First = property.FirstValue,
                        Second = property.SecondValue,
                        HasFirst = property.FirstValue != double.MinValue,
                        HasSecond = property.SecondValue != double.MinValue
                    });
                }

                return result;
            }

            private static void Compare(List<ComparisonLine> current,
                List<ComparisonLine> replacement)
            {
                var usedReplacement = new bool[replacement.Count];

                foreach (ComparisonLine oldLine in current)
                {
                    int match = -1;
                    for (int i = 0; i < replacement.Count; i++)
                    {
                        if (!usedReplacement[i]
                            && string.Equals(oldLine.Key, replacement[i].Key,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            match = i;
                            break;
                        }
                    }

                    if (match < 0)
                    {
                        bool removedPenalty = IsPenalty(oldLine.Key);
                        oldLine.Tone = removedPenalty ? ComparisonTone.Good : ComparisonTone.Bad;
                        oldLine.Text += removedPenalty ? "  [REMOVED]" : "  [LOST]";
                        continue;
                    }

                    usedReplacement[match] = true;
                    ComparisonLine newLine = replacement[match];
                    int comparison = CompareValue(oldLine, newLine);
                    if (comparison > 0)
                    {
                        newLine.Tone = ComparisonTone.Good;
                        newLine.Text += "  [BETTER]";
                    }
                    else if (comparison < 0)
                    {
                        newLine.Tone = ComparisonTone.Bad;
                        newLine.Text += "  [WORSE]";
                    }
                }

                for (int i = 0; i < replacement.Count; i++)
                {
                    if (usedReplacement[i]) continue;
                    ComparisonLine line = replacement[i];
                    if (line.Text.EndsWith(":", StringComparison.Ordinal)) continue;
                    bool penalty = IsPenalty(line.Key);
                    line.Tone = penalty ? ComparisonTone.Bad : ComparisonTone.Good;
                    line.Text += penalty ? "  [NEW PENALTY]" : "  [NEW]";
                }
            }

            private static int CompareValue(ComparisonLine current,
                ComparisonLine replacement)
            {
                if (!current.HasFirst || !replacement.HasFirst)
                {
                    return 0;
                }

                double oldValue = current.Key.Contains("durability") && current.HasSecond
                    ? current.Second : current.First;
                double newValue = replacement.Key.Contains("durability") && replacement.HasSecond
                    ? replacement.Second : replacement.First;
                double difference = newValue - oldValue;

                if (IsLowerBetter(current.Key)) difference = -difference;
                return difference > 0.001 ? 1 : difference < -0.001 ? -1 : 0;
            }

            private static bool IsLowerBetter(string key)
            {
                return key.Contains("weight") || key.Contains("strengthrequirement")
                    || key.Contains("skillrequirement");
            }

            private static bool IsPenalty(string key)
            {
                return key.Contains("cursed") || key.Contains("brittle")
                    || key.Contains("antique") || key.Contains("cannotberepaired")
                    || key.Contains("massive") || key.Contains("unwieldy")
                    || key.Contains("ephemeral") || key.Contains("prized");
            }

            private static string NormalizeProperty(string value)
            {
                var result = new System.Text.StringBuilder();
                foreach (char c in value ?? string.Empty)
                {
                    if (char.IsLetter(c)) result.Append(char.ToLowerInvariant(c));
                }
                string key = result.ToString();
                return key.EndsWith("resistmax", StringComparison.Ordinal)
                    ? key.Substring(0, key.Length - 3)
                    : key;
            }

            private static string CleanText(string value)
            {
                string result = value ?? string.Empty;
                int start;
                while ((start = result.IndexOf("/c[",
                    StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    int end = result.IndexOf(']', start + 3);
                    if (end < 0) break;
                    result = result.Remove(start, end - start + 1);
                }
                return result.Replace("/cd", string.Empty).Trim();
            }

            private static ushort HueFor(ComparisonTone tone)
            {
                switch (tone)
                {
                    case ComparisonTone.Good:
                        return FeatureGumpArtwork.AccentHue(
                            FeatureGumpArtworkKind.EquipmentGuru);
                    case ComparisonTone.Bad:
                        return 0x0021;
                    default:
                        return FeatureGumpArtwork.TextHue(
                            FeatureGumpArtworkKind.EquipmentGuru);
                }
            }

            private enum ComparisonTone
            {
                Neutral,
                Good,
                Bad
            }

            private sealed class ComparisonLine
            {
                internal string Text;
                internal string Key;
                internal double First;
                internal double Second;
                internal bool HasFirst;
                internal bool HasSecond;
                internal ComparisonTone Tone;
            }
        }

        private sealed class MessageRow : Control
        {
            internal MessageRow(string text, int width, int height = 58)
            {
                Width = width;
                Height = height;
                AcceptMouseInput = false;
                Add(new Label(text, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru),
                    width - 20, font: 1)
                {
                    X = 10,
                    Y = 10
                });
            }
        }
    }
}
