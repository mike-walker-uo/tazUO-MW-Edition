using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Utility;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ProfileTransferGump : Gump
    {
        private readonly Profile _source = ProfileManager.CurrentProfile;
        private readonly string _sourcePath = ProfileManager.ProfilePath;
        private readonly List<(ProfileTransfer.Destination Destination, Checkbox Check)> _destinations = new();
        private readonly List<(ProfileTransferCategory Category, Checkbox Check)> _categories = new();
        private readonly Label _selection, _status;
        private static bool _transferring;

        internal static void Open()
        {
            if (ProfileManager.CurrentProfile == null) return;
            var existing = UIManager.GetGump<ProfileTransferGump>();
            if (existing != null) { existing.BringOnTop(); return; }
            UIManager.Add(new ProfileTransferGump());
        }

        private ProfileTransferGump() : base(0, 0)
        {
            Width = 780; Height = 580;
            X = 80; Y = 60;
            CanMove = true; CanCloseWithRightClick = true; AcceptMouseInput = true; WantUpdateSize = false;
            var frame = new ResizePic(0x0A28) { Width = Width, Height = Height };
            CustomGumpThemeManager.ApplyFrame(frame); Add(frame);
            Text("Transfer settings from " + _source.CharacterName, 16, 12, 730);
            Text("Choose destination characters and categories. Changes apply when each destination next loads.", 16, 38, 730);
            Text("Destination profiles", 16, 70, 370);
            Text("Settings categories", 416, 70, 340);
            Button(16, 96, 104, "Select all", 1);
            Button(126, 96, 124, "Same server", 2);
            Button(256, 96, 104, "Select none", 3);
            Button(416, 96, 164, "All settings", 4);
            Button(586, 96, 164, "Select none", 5);

            // ScrollArea owns its scrollbar. Add rows directly; never clear its Children collection.
            var profiles = new ScrollArea(16, 126, 380, 310, true) { CanMove = false };
            var categories = new ScrollArea(416, 126, 340, 310, true) { CanMove = false };
            Add(profiles); Add(categories);
            string error = null;
            try
            {
                int y = 0;
                foreach (var destination in ProfileTransfer.FindDestinations(ProfileManager.ProfileRootPath(), _sourcePath))
                {
                    var check = Check(destination.Label, 330, y);
                    check.SetTooltip(destination.Path);
                    profiles.Add(check); _destinations.Add((destination, check));
                    y += Math.Max(26, check.Height + 6);
                }
                if (_destinations.Count == 0)
                    profiles.Add(new Label("No other saved profiles found.", true, CustomGumpThemeManager.DataTextHue, 350, font: 1));
            }
            catch (Exception ex) { error = "Could not list profiles: " + ex.Message; }
            int row = 0;
            foreach (var category in ProfileTransferCatalog.All)
            {
                var check = Check(ProfileTransferCatalog.Labels[(int)category], 290, row);
                var fields = ProfileTransferCatalog.JsonCategories.Count(p => p.Value == category);
                var files = ProfileTransferCatalog.Files.TryGetValue(category, out var names) ? names : Array.Empty<string>();
                check.SetTooltip(fields + " profile options" + (files.Length == 0 ? "" : "\nFiles: " + string.Join(", ", files))
                    + (category == ProfileTransferCategory.SkillButtons ? "\nOnly skill buttons, with positions and locks. Skill groups stay separate." : "")
                    + (category == ProfileTransferCategory.SpellIcons ? "\nOnly individual spell icons, with positions, locks, scale and hotkey display. Spell bars and macros stay separate." : "")
                    + (category == ProfileTransferCategory.GumpLayout ? "\nOther windows only. Skill buttons and spell icons have their own categories." : "")
                    + (category == ProfileTransferCategory.SkillButtons || category == ProfileTransferCategory.SpellIcons
                        ? "\nAnchor groups keep selected members; links to unselected windows are not copied." : "")
                    + (category == ProfileTransferCategory.Enhancements ? "\nIncludes all Client enhancements tabs and extra feature toggles." : ""));
                categories.Add(check); _categories.Add((category, check));
                row += Math.Max(26, check.Height + 6);
            }
            _selection = Text("", 16, 445, 730);
            Text("Skill buttons and spell icons transfer separately from other layouts. All settings includes both.\nKeep destination characters logged out. Item-linked settings may need retargeting on another character.", 16, 472, 730);
            Button(16, 525, 210, "Transfer selected", 6);
            Button(650, 525, 100, "Close", 7);
            _status = Text(error == null ? "Backups are saved in each destination's transfer-backups folder."
                : "Could not list profiles. Hover here for details.", 16, 552, 730);
            _status.SetTooltip(error ?? "Global client options, such as FPS and login music, already apply to every profile.\n"
                + "Journal history and previous backups are not transferred. Missing selected source files reset that category's file settings to defaults.");
            UpdateSelection(); SetInScreen();
        }

        private Label Text(string text, int x, int y, int width)
        {
            var label = new Label(text, true, CustomGumpThemeManager.DataTextHue, width, font: 1) { X = x, Y = y };
            Add(label); return label;
        }

        private void Button(int x, int y, int width, string text, int id)
        {
            var button = new NiceButton(x, y, width, 22, ButtonAction.Activate, text)
                { ButtonParameter = id, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button); Add(button);
        }

        private Checkbox Check(string text, int width, int y)
        {
            var check = new Checkbox(0x00D2, 0x00D3, text, 1, CustomGumpThemeManager.DataTextHue, true, width) { Y = y };
            check.ValueChanged += (_, __) => UpdateSelection();
            return check;
        }

        private void UpdateSelection()
        {
            if (_selection == null) return;
            _selection.Text = $"{_destinations.Count(d => d.Check.IsChecked)} / {_destinations.Count} destination profiles; "
                + $"{_categories.Count(c => c.Check.IsChecked)} / {_categories.Count} settings categories selected.";
        }

        public override void OnButtonClick(int buttonID)
        {
            if (_transferring) { _status.Text = "A settings transfer is running. Please wait."; return; }
            switch (buttonID)
            {
                case 1: foreach (var d in _destinations) d.Check.IsChecked = true; break;
                case 2:
                    foreach (var d in _destinations)
                        d.Check.IsChecked = string.Equals(d.Destination.Server, FileSystemHelper.RemoveInvalidChars(_source.ServerName), StringComparison.OrdinalIgnoreCase);
                    break;
                case 3: foreach (var d in _destinations) d.Check.IsChecked = false; break;
                case 4: foreach (var c in _categories) c.Check.IsChecked = true; break;
                case 5: foreach (var c in _categories) c.Check.IsChecked = false; break;
                case 6: Transfer(); break;
                case 7: Dispose(); break;
            }
            UpdateSelection();
        }

        private void Transfer()
        {
            if (!ReferenceEquals(_source, ProfileManager.CurrentProfile) || !World.InGame)
            { _status.Text = "Active character changed. Reopen Settings transfers."; return; }
            var targets = _destinations.Where(d => d.Check.IsChecked).Select(d => d.Destination).ToArray();
            var categories = new HashSet<ProfileTransferCategory>(_categories.Where(c => c.Check.IsChecked).Select(c => c.Category));
            if (targets.Length == 0 || categories.Count == 0)
            { _status.Text = "Select at least one destination and one category."; return; }
            ProfileTransfer.Snapshot snapshot;
            try { snapshot = ProfileTransfer.Capture(categories); }
            catch (Exception ex)
            {
                _status.Text = "Could not capture source settings. Hover here for details.";
                _status.SetTooltip(ex.Message); return;
            }
            _transferring = true;
            _status.Text = "Transferring selected settings...";
            _ = Task.Run(() =>
            {
                int changed = 0, unchanged = 0;
                var errors = new List<string>();
                foreach (var target in targets)
                {
                    try
                    {
                        var result = ProfileTransfer.Apply(snapshot, target.Path);
                        if (result.ChangedFiles > 0) changed++; else unchanged++;
                    }
                    catch (Exception ex) { errors.Add(target.Label + ": " + ex.Message); }
                }
                MainThreadQueue.EnqueueAction(() =>
                {
                    _transferring = false;
                    if (!ReferenceEquals(_source, ProfileManager.CurrentProfile)) return;
                    string summary = $"Transferred {changed} profiles; {unchanged} unchanged; {errors.Count} failed.";
                    GameActions.Print(summary, errors.Count == 0 ? (ushort)0x35 : (ushort)0x21);
                    if (!IsDisposed)
                    {
                        _status.Text = summary;
                        _status.SetTooltip(errors.Count == 0 ? "Backups: destination profile / transfer-backups. Reload destination characters to apply."
                            : string.Join("\n", errors));
                    }
                    foreach (string error in errors) GameActions.Print(error, 0x21);
                });
            });
        }
    }
}
