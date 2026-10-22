using System;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class FastLoginSlotsGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly DataBox _rows;
        private readonly Label _feedback;

        internal FastLoginSlotsGump() : base(0, 0)
        {
            X = 100; Y = 100; Width = 600; Height = 440;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true;
            WantUpdateSize = false;
            var surface = CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f);
            surface.WantUpdateSize = false; Add(surface);
            int left = 12 + CustomThemeArt.ContentInset;
            Add(new Label("Fast login - five character slots", true, CustomGumpThemeManager.TitleHue, font: 1)
                { X = left, Y = left });
            Add(new Label("Save current character. Replace changes that slot; Clear removes its saved login.", true,
                CustomGumpThemeManager.DimHue, Width - left * 2, 1) { X = left, Y = left + 29 });
            Add(_rows = new DataBox(left, left + 72, Width - left * 2, 270) { WantUpdateSize = false });
            Add(_feedback = new Label("Save/Replace keeps startup settings too. Passwords use the existing saved-account format.", true,
                CustomGumpThemeManager.TextHue, Width - left * 2, 1) { X = left, Y = Height - left - 37 });
            FastLoginManager.Slots.Reload(); Rebuild(); SetInScreen();
        }

        public override bool ShouldBeSaved => false;
        internal static void Toggle()
        {
            if (!World.InGame) return;
            var open = UIManager.GetGump<FastLoginSlotsGump>();
            if (open != null && !open.IsDisposed) open.Dispose();
            else UIManager.Add(new FastLoginSlotsGump());
        }
        public override void Update()
        {
            if (!World.InGame || !ReferenceEquals(_profile, ProfileManager.CurrentProfile)) { Dispose(); return; }
            base.Update();
        }

        private void Rebuild()
        {
            _rows.Clear();
            for (int index = 0; index < FastLoginManager.SlotCount; index++)
            {
                int slot = index, y = index * 53;
                Settings saved = FastLoginManager.Slots.Get(slot);
                string error = FastLoginManager.Slots.Error(slot);
                var title = new Label((slot + 1) + ". " + (saved?.FastLoginCharacter ?? (error == null ? "Empty slot" : "Unreadable slot")),
                    true, CustomGumpThemeManager.TitleHue, 220, 1, FontStyle.Cropped) { Y = y };
                title.SetTooltip(saved == null ? error ?? "Save the character you are currently playing."
                    : saved.FastLoginCharacter + "\n" + saved.Username + " / " + saved.LastServerName);
                _rows.Add(title);
                _rows.Add(new Label(saved == null ? "" : saved.Username + " / " + saved.LastServerName, true,
                    CustomGumpThemeManager.DimHue, 220, 1, FontStyle.Cropped) { Y = y + 21 });
                var save = Button(saved == null ? "Save current" : "Replace", 225, y, 106, () =>
                {
                    _feedback.Text = FastLoginManager.SaveCurrent(slot, out string failure)
                        ? "Slot " + (slot + 1) + " saved for " + FastLoginManager.Slots.Get(slot).FastLoginCharacter + "." : failure;
                    Rebuild();
                });
                save.IsEnabled = FastLoginManager.CanSave; _rows.Add(save);
                var copy = Button("Copy target", 337, y, 104, () =>
                {
                    string executable = Path.ChangeExtension(typeof(Client).Assembly.Location, ".exe");
                    bool copied = SDL3.SDL.SDL_SetClipboardText(FastLoginManager.ShortcutTarget(executable, slot));
                    _feedback.Text = copied ? "Shortcut target copied. Paste it into a Windows desktop shortcut." : "Clipboard copy failed.";
                });
                copy.IsEnabled = saved != null; copy.SetTooltip("Copy a desktop shortcut target. It contains only the executable path and slot number.");
                _rows.Add(copy);
                var clear = Button("Clear", 447, y, 62, () =>
                {
                    try { FastLoginManager.Slots.Clear(slot); _feedback.Text = "Slot " + (slot + 1) + " cleared."; Rebuild(); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    { _feedback.Text = "Could not clear slot. Check access to Data/FastLogin."; }
                });
                clear.IsEnabled = saved != null || error != null; _rows.Add(clear);
            }
        }

        private NiceButton Button(string text, int x, int y, int width, Action action)
        {
            var button = new NiceButton(x, y, width, 26, ButtonAction.Activate, text, font: 1) { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left && button.IsEnabled) action(); };
            return button;
        }
    }
}
