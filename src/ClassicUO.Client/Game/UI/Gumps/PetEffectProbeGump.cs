using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PetEffectProbeGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly PetEffectProbe.Session _session;
        private readonly Label _pet, _feedback;
        private readonly NiceButton _pause;
        private readonly ScrollArea _scroll;
        private readonly Dictionary<PetEffectProbe.Buff, Label> _buffRows = new Dictionary<PetEffectProbe.Buff, Label>();
        private uint _nextRefresh, _selected;
        private int _revision = -1;
        private bool _wasObservable;

        private PetEffectProbeGump() : base(0, 0)
        {
            _session = PetEffectProbe.Start();
            X = 100; Y = 100; Width = 680; Height = 520;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true;
            WantUpdateSize = false;
            Control surface = CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f);
            surface.WantUpdateSize = false;
            Add(surface);
            int left = 12 + CustomThemeArt.ContentInset;
            int width = Width - left * 2;
            Add(new Label("Pet buffs / debuffs - packet PoC", true, CustomGumpThemeManager.TitleHue, font: 1)
                { X = left, Y = 12 + CustomThemeArt.ContentInset });
            Add(_pet = new Label("", true, CustomGumpThemeManager.TextHue, width, 1, FontStyle.Cropped)
                { X = left, Y = 42 + CustomThemeArt.ContentInset });
            int buttonsY = 70 + CustomThemeArt.ContentInset;
            Add(Button("Target pet", left, buttonsY, 110, TargetPet));
            Add(_pause = Button("Pause", left + 118, buttonsY, 85, () => _session.Pause(Time.Ticks)));
            Add(Button("Clear", left + 211, buttonsY, 85, () => _session.Clear()));
            Add(Button("Copy report", left + 304, buttonsY, 115, () =>
            {
                bool copied = SDL3.SDL.SDL_SetClipboardText(_session.Report(Time.Ticks));
                _session.Feedback = copied ? "Report copied." : "Clipboard copy failed.";
                _session.Revision++;
            }));
            Add(new Label("Visual matches are candidates, not confirmed active statuses. Sound is positional only.",
                true, CustomGumpThemeManager.DimHue, width, 1) { X = left, Y = buttonsY + 32 });
            Add(_feedback = new Label("", true, CustomGumpThemeManager.TextHue, width, 1)
                { X = left, Y = Height - 52 - CustomThemeArt.ContentInset });
            int scrollY = buttonsY + 80;
            Add(_scroll = new ScrollArea(left, scrollY, width, _feedback.Y - scrollY - 10, true));
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;

        internal static void Toggle()
        {
            var open = UIManager.GetGump<PetEffectProbeGump>();
            if (open != null && !open.IsDisposed) open.Dispose();
            else if (World.InGame) UIManager.Add(new PetEffectProbeGump());
        }

        private NiceButton Button(string text, int x, int y, int width, Action action)
        {
            var button = new NiceButton(x, y, width, 24, ButtonAction.Activate, text, font: 1)
                { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) action(); };
            return button;
        }

        private void TargetPet()
        {
            if (TargetManager.IsTargeting)
                _session.Feedback = "Finish or cancel the current targeting cursor first.";
            else
            {
                TargetManager.SetTargeting(CursorTarget.PetEffectProbe, CursorType.Target, TargetType.Neutral);
                _session.Feedback = "Click your pet. Escape cancels; no request is sent to the server.";
            }
            _session.Revision++;
        }

        public override void Update()
        {
            if (!World.InGame || !ReferenceEquals(_profile, ProfileManager.CurrentProfile)
                || !ReferenceEquals(_session, PetEffectProbe.Current)) { Dispose(); return; }
            base.Update();
            uint now = Time.Ticks;
            if (now < _nextRefresh) return;
            _nextRefresh = now + 500;
            Mobile pet = World.Mobiles.Get(_session.Serial);
            bool observable = pet != null && !pet.IsDead;
            if (_selected != _session.Serial) { _selected = _session.Serial; _wasObservable = false; }
            if (_wasObservable && !observable)
            {
                _session.Buffs.Clear(); _session.Candidates.Clear();
                _session.Add(now, "Pet disappeared/died; current statuses cleared due to observation gap.");
            }
            _wasObservable = observable;
            _pet.Text = _session.Name + " [0x" + _session.Serial.ToString("X8") + "] - "
                + (_session.Paused ? "PAUSED" : _session.Serial == 0 ? "select a pet"
                    : !observable ? "not visible / dead; current status unknown" : "capturing");
            _feedback.Text = _session.Feedback;
            if (_pause.TextLabel.Text != (_session.Paused ? "Resume" : "Pause"))
                _pause.SetText(_session.Paused ? "Resume" : "Pause");
            if (_revision == _session.Revision)
            {
                bool layoutChanged = false;
                foreach (var row in _buffRows)
                {
                    int height = row.Value.Height;
                    string text = row.Key.Describe(now);
                    if (row.Value.Text != text) row.Value.Text = text;
                    layoutChanged |= height != row.Value.Height;
                }
                if (layoutChanged) Rebuild(now);
                return;
            }
            _revision = _session.Revision;
            Rebuild(now);
        }

        private void Rebuild(uint now)
        {
            _scroll.Clear(); _buffRows.Clear();
            int y = 0, width = _scroll.Width - 24;
            Row("SERVER-REPORTED STATUSES (pet serial)", CustomGumpThemeManager.TitleHue, width, ref y);
            if (_session.Buffs.Count == 0)
                Row("None received. This does not mean the pet has no buffs or debuffs.", CustomGumpThemeManager.DimHue, width, ref y);
            foreach (PetEffectProbe.Buff buff in _session.Buffs.Values)
                _buffRows[buff] = Row(buff.Describe(now), CustomGumpThemeManager.TextHue, width, ref y);
            y += 10;
            Row("VISUAL CANDIDATES - last observed; current activity / duration unknown", CustomGumpThemeManager.TitleHue, width, ref y);
            if (_session.Candidates.Count == 0)
                Row("No reference signature matched yet. UOAlive mappings are unverified.", CustomGumpThemeManager.DimHue, width, ref y);
            foreach (PetEffectProbe.Entry entry in _session.Candidates)
                Row(entry.Text + " (last observed at tick " + entry.SeenAt + ")", CustomGumpThemeManager.TextHue, width, ref y);
            y += 10;
            Row("PACKET EVIDENCE - newest first; last " + PetEffectProbe.Session.HistoryLimit + " entries", CustomGumpThemeManager.TitleHue, width, ref y);
            if (_session.History.Count == 0)
                Row("Waiting for new pet-addressed buffs, effects involving the pet, or effects/sounds within 1 tile.", CustomGumpThemeManager.DimHue, width, ref y);
            for (int i = _session.History.Count - 1; i >= 0; i--)
            {
                PetEffectProbe.Entry entry = _session.History[i];
                Row("[tick " + entry.SeenAt + "] " + entry.Text, CustomGumpThemeManager.TextHue, width, ref y);
            }
            _scroll.SlowUpdate();
        }

        private Label Row(string text, ushort hue, int width, ref int y)
        {
            var label = new Label(text, true, hue, width, 1) { X = 0, Y = y };
            _scroll.Add(label); y += label.Height + 5;
            return label;
        }

        public override void Dispose()
        {
            if (ReferenceEquals(_session, PetEffectProbe.Current)) PetEffectProbe.End();
            base.Dispose();
        }
    }
}
