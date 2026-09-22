// TazUO addition: themed alert history and notification settings.

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
    internal sealed class AlertCenterGump : Gump
    {
        private const int WIDTH = 780;
        private const int HEIGHT = 590;
        private const int SIDEBAR_X = 24;
        private const int SIDEBAR_WIDTH = 140;
        private const int LIST_X = 178;
        private const int LIST_WIDTH = 578;

        private readonly Label _title;
        private readonly Label _status;
        private readonly NiceButton _categoryButton;
        private readonly VBoxContainer _rows;
        private bool _settingsPage;
        private bool _activeOnly;
        private int _severityFilter = -1;
        private int _categoryFilter = -1;
        private bool _refreshRequested;

        internal AlertCenterGump(
            bool settingsPage = false,
            bool activeOnly = false,
            int severityFilter = -1,
            int categoryFilter = -1) : base(0, 0)
        {
            _settingsPage = settingsPage;
            _activeOnly = activeOnly;
            _severityFilter = severityFilter;
            _categoryFilter = categoryFilter;
            X = ProfileManager.CurrentProfile?.AlertCenterPosition.X ?? 180;
            Y = ProfileManager.CurrentProfile?.AlertCenterPosition.Y ?? 70;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            AlertCenterManager.EnsureLoaded();
            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.AlertCenter, 0.96f));
            AddSurface(20, 10, WIDTH - 40, 52, 0.54f);
            Add(_title = new Label("ALERT CENTER", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.AlertCenter), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Review, snooze and control every client alert in one place.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 39
            });
            NiceButton close = CreateButton(728, 16, 28, 26, "X", 99);
            close.SetTooltip("Close Alert Center");
            Add(close);
            AddAccent(24, 62, WIDTH - 48);

            Add(CreateButton(24, 72, 110, 30, "HISTORY", 1));
            Add(CreateButton(142, 72, 110, 30, "SETTINGS", 2));
            Add(CreateButton(646, 72, 110, 30, "Clear history", 3));

            AddSurface(SIDEBAR_X, 112, SIDEBAR_WIDTH, 424, 0.36f);
            Add(new Label("FILTER", true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.AlertCenter),
                SIDEBAR_WIDTH - 16, font: 1)
            {
                X = SIDEBAR_X + 8,
                Y = 122
            });
            Add(CreateButton(SIDEBAR_X + 8, 149, SIDEBAR_WIDTH - 16, 30, "All alerts", 10));
            Add(CreateButton(SIDEBAR_X + 8, 185, SIDEBAR_WIDTH - 16, 30, "Active", 11));
            Add(CreateButton(SIDEBAR_X + 8, 221, SIDEBAR_WIDTH - 16, 30, "Critical", 14));
            Add(CreateButton(SIDEBAR_X + 8, 257, SIDEBAR_WIDTH - 16, 30, "Warnings", 13));
            Add(CreateButton(SIDEBAR_X + 8, 293, SIDEBAR_WIDTH - 16, 30, "Information", 12));

            Add(new Label("CATEGORY", true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.AlertCenter),
                SIDEBAR_WIDTH - 16, font: 1)
            {
                X = SIDEBAR_X + 8,
                Y = 341
            });
            Add(_categoryButton = CreateButton(
                SIDEBAR_X + 8, 365, SIDEBAR_WIDTH - 16, 46, "All categories", 15));
            Add(new Label("Muted alerts remain in history and are marked suppressed.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), SIDEBAR_WIDTH - 16, font: 1)
            {
                X = SIDEBAR_X + 8,
                Y = 435
            });

            AddSurface(LIST_X, 112, LIST_WIDTH, 424, 0.32f);
            var scroll = new ScrollArea(LIST_X + 4, 116, LIST_WIDTH - 8, 416, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            scroll.Add(_rows = new VBoxContainer(
                scroll.Width - scroll.ScrollBarWidth() - 3, 0, 0));
            Add(scroll);

            Add(_status = new Label("Right-click the window to close it.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), WIDTH - 48, font: 1,
                style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = 24,
                Y = 552
            });

            Rebuild();
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;
        internal bool SettingsPage => _settingsPage;
        internal bool ActiveOnly => _activeOnly;
        internal int SeverityFilter => _severityFilter;
        internal int CategoryFilter => _categoryFilter;

        internal void RequestRefresh()
        {
            _refreshRequested = true;
        }

        public override void Update()
        {
            base.Update();
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.AlertCenterPosition = Location;

            if (_refreshRequested)
            {
                _refreshRequested = false;
                Rebuild();
            }
        }

        public override void Dispose()
        {
            AlertCenterManager.Save();
            base.Dispose();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1:
                    _settingsPage = false;
                    Rebuild();
                    break;
                case 2:
                    _settingsPage = true;
                    Rebuild();
                    break;
                case 3:
                    AlertCenterManager.ClearHistory();
                    _status.Text = "Dismissed history cleared; active alarms were kept.";
                    Rebuild();
                    break;
                case 10:
                    _activeOnly = false;
                    _severityFilter = -1;
                    _categoryFilter = -1;
                    Rebuild();
                    break;
                case 11:
                    _activeOnly = true;
                    _severityFilter = -1;
                    Rebuild();
                    break;
                case 12:
                    SetSeverityFilter(AlertSeverity.Info);
                    break;
                case 13:
                    SetSeverityFilter(AlertSeverity.Warning);
                    break;
                case 14:
                    SetSeverityFilter(AlertSeverity.Critical);
                    break;
                case 15:
                    _categoryFilter++;

                    if (_categoryFilter >= Enum.GetValues(typeof(AlertCategory)).Length)
                    {
                        _categoryFilter = -1;
                    }

                    UpdateCategoryLabel();
                    Rebuild();
                    break;
                case 99:
                    Dispose();
                    break;
            }
        }

        private void SetSeverityFilter(AlertSeverity severity)
        {
            _activeOnly = false;
            _severityFilter = (int)severity;
            Rebuild();
        }

        private void Rebuild()
        {
            _rows.Clear();
            UpdateCategoryLabel();

            IReadOnlyList<AlertCenterEntry> history = AlertCenterManager.History;
            int active = history.Count(entry => entry.Active);
            int critical = history.Count(entry => entry.Active
                && AlertCenterManager.ParseSeverity(entry.Severity) == AlertSeverity.Critical);
            _title.Text = $"ALERT CENTER  •  {active} ACTIVE  •  {critical} CRITICAL";

            if (_settingsPage)
            {
                BuildSettings();
                return;
            }

            IEnumerable<AlertCenterEntry> filtered = history.OrderByDescending(entry => entry.UpdatedUtcTicks);

            if (_activeOnly)
            {
                filtered = filtered.Where(entry => entry.Active);
            }

            if (_severityFilter >= 0)
            {
                filtered = filtered.Where(entry => entry.Severity == _severityFilter);
            }

            if (_categoryFilter >= 0)
            {
                AlertCategory category = (AlertCategory)_categoryFilter;
                filtered = filtered.Where(entry =>
                    AlertCenterManager.ParseCategory(entry.Category) == category);
            }

            int count = 0;

            foreach (AlertCenterEntry entry in filtered)
            {
                _rows.Add(new AlertRow(this, entry, _rows.Width));
                count++;
            }

            if (count == 0)
            {
                _rows.Add(new Label("No alerts match this filter.", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), _rows.Width - 16, font: 1)
                {
                    X = 8,
                    Y = 14
                });
            }
        }

        private void BuildSettings()
        {
            _rows.Add(new Label(
                "Category controls affect future toasts. Severity overrides also affect new history entries.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), _rows.Width - 16, font: 1)
            {
                X = 8,
                Y = 8
            });

            foreach (AlertCategory category in Enum.GetValues(typeof(AlertCategory)))
            {
                _rows.Add(new CategorySettingsRow(this, category, _rows.Width));
            }

            _rows.Add(new Label("MUTED SOURCES", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.AlertCenter), _rows.Width - 16, font: 1)
            { X = 8, Y = 10 });
            foreach (string source in AlertCenterManager.MutedSources.ToArray())
                _rows.Add(new SourceSettingsRow(this, source, _rows.Width));
        }

        private void UpdateCategoryLabel()
        {
            _categoryButton.SetText(_categoryFilter < 0
                ? "All categories"
                : ((AlertCategory)_categoryFilter).ToString());
        }

        private void Dismiss(AlertCenterEntry entry)
        {
            bool active = entry.Active;
            AlertCenterManager.Dismiss(entry);
            _status.Text = active ? "Alert dismissed." : "History entry removed.";
            Rebuild();
        }

        private void Snooze(AlertCenterEntry entry)
        {
            AlertCategory category = AlertCenterManager.ParseCategory(entry.Category);
            AlertCenterManager.Snooze(category, 10);
            _status.Text = $"{category} alerts snoozed for 10 minutes.";
            Rebuild();
        }

        private void Mute(AlertCenterEntry entry)
        {
            AlertCenterManager.SetSourceMuted(entry.SourceKey, true);
            _status.Text = $"Alert source '{entry.SourceKey}' muted.";
            Rebuild();
        }

        private void OpenRelated(AlertCenterEntry entry)
        {
            AlertCategory category = AlertCenterManager.ParseCategory(entry.Category);
            bool opened = true;

            switch (category)
            {
                case AlertCategory.Equipment:
                    DurabilitysGump durability = UIManager.GetGump<DurabilitysGump>();
                    if (durability == null)
                        UIManager.Add(new DurabilitysGump());
                    else
                        durability.BringOnTop();
                    break;
                case AlertCategory.Pets:
                    PetStatusPanelGump pets = UIManager.GetGump<PetStatusPanelGump>();
                    if (pets == null)
                        UIManager.Add(new PetStatusPanelGump(260, 260));
                    else
                        pets.BringOnTop();
                    break;
                case AlertCategory.Creatures:
                    WorldMapGump map = UIManager.GetGump<WorldMapGump>();
                    if (map == null)
                        UIManager.Add(new WorldMapGump());
                    else
                        map.BringOnTop();
                    break;
                case AlertCategory.Inventory:
                case AlertCategory.Supplies:
                    RestockAgentGump restock = UIManager.GetGump<RestockAgentGump>();
                    if (restock == null)
                        UIManager.Add(new RestockAgentGump());
                    else
                        restock.BringOnTop();
                    break;
                case AlertCategory.Journal:
                    JournalOpenerManager.Open();
                    break;
                case AlertCategory.Combat:
                    CombatLogGump combat = UIManager.GetGump<CombatLogGump>();
                    if (combat == null)
                        UIManager.Add(new CombatLogGump());
                    else
                        combat.BringOnTop();
                    break;
                default:
                    opened = false;
                    break;
            }

            _status.Text = opened
                ? $"Opened the related {category} view."
                : "This alert has no related view.";
        }

        private void SettingChanged(string message)
        {
            _status.Text = message;
            Rebuild();
        }

        private void AddSurface(int x, int y, int width, int height, float alpha)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.AlertCenter, alpha));
        }

        private void AddAccent(int x, int y, int width)
        {
            Add(new AlphaBlendControl(0.72f)
            {
                X = x,
                Y = y,
                Width = width,
                Height = 1,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.AlertCenter)
            });
        }

        private static NiceButton CreateButton(
            int x,
            int y,
            int width,
            int height,
            string text,
            int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.AlertCenter);
        }

        private static NiceButton CreateRowButton(
            int x,
            int width,
            string text,
            int id)
        {
            return CreateButton(x, 44, width, 24, text, id);
        }

        private sealed class AlertRow : Control
        {
            private readonly AlertCenterGump _owner;
            private readonly AlertCenterEntry _entry;

            internal AlertRow(AlertCenterGump owner, AlertCenterEntry entry, int width)
            {
                _owner = owner;
                _entry = entry;
                Width = width;
                Height = 78;
                WantUpdateSize = false;

                var surface = new AlphaBlendControl(0.34f)
                {
                    Width = width,
                    Height = 75,
                    BaseColor = Color.Black
                };
                Add(surface);

                AlertSeverity severity = AlertCenterManager.ParseSeverity(entry.Severity);
                ushort severityHue = SeverityHue(severity);
                DateTime time = SafeLocalTime(entry.UpdatedUtcTicks);
                string state = entry.Active ? "ACTIVE" : entry.Suppressed ? "SUPPRESSED" : "HISTORY";
                string count = entry.Occurrences > 1 ? $" ×{entry.Occurrences}" : string.Empty;

                Add(new Label(severity.ToString().ToUpperInvariant(), true, severityHue,
                    82, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 6
                });
                Add(new Label($"{time:HH:mm}  {entry.Category}  {state}{count}", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.AlertCenter), width - 104, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 92,
                    Y = 6
                });
                int buttonsX = width - 290;
                Add(new Label(entry.Text, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.AlertCenter),
                    buttonsX - 16, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 25
                });

                Add(CreateRowButton(buttonsX, 48, "OPEN", 1));
                Add(CreateRowButton(buttonsX + 52, 66, "SNOOZE", 2));
                Add(CreateRowButton(buttonsX + 122, 74, "MUTE SRC", 3));
                Add(CreateRowButton(buttonsX + 202, 28, "X", 4));
            }

            public override void OnButtonClick(int buttonID)
            {
                switch (buttonID)
                {
                    case 1: _owner.OpenRelated(_entry); break;
                    case 2: _owner.Snooze(_entry); break;
                    case 3: _owner.Mute(_entry); break;
                    case 4: _owner.Dismiss(_entry); break;
                }
            }
        }

        private sealed class CategorySettingsRow : Control
        {
            private readonly AlertCenterGump _owner;
            private readonly AlertCategory _category;

            internal CategorySettingsRow(AlertCenterGump owner, AlertCategory category, int width)
            {
                _owner = owner;
                _category = category;
                Width = width;
                Height = 48;
                WantUpdateSize = false;

                var surface = new AlphaBlendControl(0.30f)
                {
                    Width = width,
                    Height = 45,
                    BaseColor = Color.Black
                };
                Add(surface);

                bool muted = AlertCenterManager.IsMuted(category);
                bool snoozed = AlertCenterManager.IsSnoozed(category);
                TimeSpan remaining = AlertCenterManager.SnoozeRemaining(category);
                int severity = AlertCenterManager.SeverityOverride(category);
                string severityText = severity < 0
                    ? "DEFAULT"
                    : AlertCenterManager.ParseSeverity(severity).ToString().ToUpperInvariant();
                string state = muted
                    ? "Muted"
                    : snoozed
                        ? $"Snoozed {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m"
                        : "Enabled";

                Add(new Label(category.ToString(), true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.AlertCenter),
                    112, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 13
                });
                Add(new Label(state, true,
                    muted || snoozed ? (ushort)0x0035 : FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.AlertCenter),
                    92, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 124,
                    Y = 13
                });
                Add(CreateSettingsButton(222, 74, muted ? "UNMUTE" : "MUTE", 1));
                Add(CreateSettingsButton(300, 104, snoozed ? "CLEAR SNOOZE" : "SNOOZE 10M", 2));
                Add(CreateSettingsButton(408, 118, "LEVEL: " + severityText, 3));
            }

            public override void OnButtonClick(int buttonID)
            {
                if (buttonID == 1)
                {
                    bool muted = AlertCenterManager.IsMuted(_category);
                    AlertCenterManager.SetMuted(_category, !muted);
                    _owner.SettingChanged($"{_category} alerts {(!muted ? "muted" : "enabled")}.");
                }
                else if (buttonID == 2)
                {
                    bool snoozed = AlertCenterManager.IsSnoozed(_category);
                    AlertCenterManager.Snooze(_category, snoozed ? 0 : 10);
                    _owner.SettingChanged(snoozed
                        ? $"{_category} snooze cleared."
                        : $"{_category} alerts snoozed for 10 minutes.");
                }
                else if (buttonID == 3)
                {
                    int severity = AlertCenterManager.CycleSeverityOverride(_category);
                    string value = severity < 0
                        ? "automatic"
                        : AlertCenterManager.ParseSeverity(severity).ToString().ToLowerInvariant();
                    _owner.SettingChanged($"{_category} severity set to {value}.");
                }
            }

            private static NiceButton CreateSettingsButton(
                int x,
                int width,
                string text,
                int id)
            {
                return FeatureGumpArtwork.CreateButton(x, 9, width, 27, text, id,
                    FeatureGumpArtworkKind.AlertCenter);
            }
        }

        private sealed class SourceSettingsRow : Control
        {
            internal SourceSettingsRow(AlertCenterGump owner, string source, int width)
            {
                Width = width; Height = 42;
                Add(new AlphaBlendControl(0.30f) { Width = width, Height = 39, BaseColor = Color.Black });
                Add(new Label(source, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.AlertCenter),
                    width - 120, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 11 });
                NiceButton unmute = FeatureGumpArtwork.CreateButton(width - 106, 6, 98, 28,
                    "UNMUTE", 1, FeatureGumpArtworkKind.AlertCenter);
                unmute.MouseUp += (sender, e) =>
                {
                    if (e.Button != MouseButtonType.Left) return;
                    AlertCenterManager.SetSourceMuted(source, false);
                    owner.SettingChanged($"Alert source '{source}' enabled.");
                };
                Add(unmute);
            }
        }

        private static ushort SeverityHue(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Critical: return 0x0021;
                case AlertSeverity.Warning: return 0x0035;
                default: return FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.AlertCenter);
            }
        }

        private static DateTime SafeLocalTime(long ticks)
        {
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            }
            catch
            {
                return DateTime.Now;
            }
        }
    }
}
