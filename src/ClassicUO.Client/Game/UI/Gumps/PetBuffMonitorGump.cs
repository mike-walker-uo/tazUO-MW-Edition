using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PetBuffMonitorGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly ScrollArea _scroll;
        private readonly Label _header;
        private readonly List<Tuple<PetBuffMonitor.Status, Label>> _details = new List<Tuple<PetBuffMonitor.Status, Label>>();
        private int _revision = -1;
        private uint _nextRefresh;

        internal PetBuffMonitorGump() : base(0, 0)
        {
            Point saved = _profile?.PetBuffMonitorPosition ?? new Point(180, 180);
            X = saved.X; Y = saved.Y; Width = 460; Height = 410;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true;
            WantUpdateSize = false;
            Control surface = CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f);
            surface.WantUpdateSize = false; Add(surface);
            int left = 12 + CustomThemeArt.ContentInset, width = Width - left * 2;
            Add(_header = new Label("Pet buffs / debuffs", true, CustomGumpThemeManager.TitleHue, width, 1, FontStyle.Cropped)
                { X = left, Y = left });
            var icons = new Checkbox(0x00D2, 0x00D3, "Overhead icons", 1, CustomGumpThemeManager.TextHue)
                { X = left, Y = left + 28, IsChecked = _profile?.PetBuffOverheadIcons ?? true };
            icons.ValueChanged += (_, __) => { if (_profile != null) _profile.PetBuffOverheadIcons = icons.IsChecked; };
            Add(icons);
            Add(new Label("? = inferred, not confirmed active. Amber icons show recent cues for 10s.",
                true, CustomGumpThemeManager.DimHue, width, 1) { X = left, Y = left + 61 });
            int top = left + 105;
            Add(_scroll = new ScrollArea(left, top, width, Height - top - left - 49, true));
            Add(new Label("Inferred observations clear after 30s; these are not spell timers.\nClose keeps monitoring. Use -petbuffs off to disable.",
                true, CustomGumpThemeManager.DimHue, width, 1) { X = left, Y = Height - left - 42 });
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;

        internal static void Toggle()
        {
            var open = UIManager.GetGump<PetBuffMonitorGump>();
            if (open != null && !open.IsDisposed) { open.Dispose(); return; }
            if (!World.InGame) return;
            if (!PetBuffMonitor.Enabled) PetBuffMonitor.SetEnabled(true);
            UIManager.Add(new PetBuffMonitorGump());
        }

        internal static void Command(string[] args)
        {
            if (args != null && args.Length > 1 && string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase))
            {
                PetBuffMonitor.SetEnabled(false);
                UIManager.GetGump<PetBuffMonitorGump>()?.Dispose();
            }
            else if (args != null && args.Length > 1 && string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase))
            {
                if (!PetBuffMonitor.Enabled) PetBuffMonitor.SetEnabled(true);
                var open = UIManager.GetGump<PetBuffMonitorGump>();
                if (World.InGame && (open == null || open.IsDisposed)) UIManager.Add(new PetBuffMonitorGump());
            }
            else Toggle();
        }

        public override void Update()
        {
            if (!PetBuffMonitor.Enabled || !ReferenceEquals(_profile, ProfileManager.CurrentProfile)) { Dispose(); return; }
            base.Update();
            uint now = Time.Ticks;
            if (now < _nextRefresh) return;
            _nextRefresh = now + 500;
            var tracker = PetBuffMonitor.Current;
            if (_revision != tracker.Revision)
            {
                _revision = tracker.Revision;
                Rebuild();
            }
            foreach (var row in _details)
            {
                string text = row.Item1.Detail(now);
                if (row.Item2.Text != text) row.Item2.Text = text;
            }
        }

        private void Rebuild()
        {
            var tracker = PetBuffMonitor.Current;
            _header.Text = "Pet buffs / debuffs - " + tracker.Pets.Count + " active pets";
            _scroll.Clear(); _details.Clear();
            int y = 0, width = _scroll.Width - 23;
            if (tracker.Pets.Count == 0)
                AddLine("No active pets visible. Pets are detected automatically using the server's renamable-pet flag.",
                    CustomGumpThemeManager.DimHue, 0, ref y, width);
            foreach (PetBuffMonitor.Pet pet in tracker.Pets.Values)
            {
                AddLine(pet.Name + " [0x" + pet.Serial.ToString("X8") + "]", CustomGumpThemeManager.TitleHue, 0, ref y, width);
                if (pet.Statuses.Count == 0)
                    AddLine("No statuses detected; hidden effects may still be present.", CustomGumpThemeManager.DimHue, 0, ref y, width);
                foreach (PetBuffMonitor.Status status in pet.Statuses.Values)
                {
                    ushort graphic = PetBuffMonitor.IconGraphic(status.Kind);
                    if (graphic != 0)
                    {
                        var icon = new GumpPic(0, y + 2, graphic, 0) { Width = 26, Height = 26, CanMove = false };
                        icon.SetTooltip(status.Name); _scroll.Add(icon);
                    }
                    bool inferred = status.Source == PetBuffMonitor.Evidence.Observed;
                    var title = new Label((inferred ? "? " : "") + status.Name, true, CustomGumpThemeManager.TextHue,
                        width - 35, 1, FontStyle.Cropped) { X = 35, Y = y };
                    title.SetTooltip(inferred ? "Observed status cue; current activity and duration are unknown."
                        : "Confirmed by " + (status.Source == PetBuffMonitor.Evidence.Flag ? "mobile status flags." : "a pet-addressed buff packet."));
                    _scroll.Add(title);
                    var detail = new Label(status.Detail(Time.Ticks), true, CustomGumpThemeManager.DimHue,
                        width - 35, 1, FontStyle.Cropped) { X = 35, Y = y + 19 };
                    _scroll.Add(detail); _details.Add(Tuple.Create(status, detail));
                    y += 43;
                }
                y += 12;
            }
            _scroll.SlowUpdate();
        }

        private void AddLine(string text, ushort hue, int x, ref int y, int width)
        {
            var label = new Label(text, true, hue, width, 1) { X = x, Y = y };
            _scroll.Add(label); y += label.Height + 7;
        }

        public override void Dispose()
        {
            if (!IsDisposed && _profile != null) _profile.PetBuffMonitorPosition = new Point(X, Y);
            base.Dispose();
        }
    }
}
