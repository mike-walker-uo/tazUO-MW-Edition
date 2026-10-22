using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RazorScriptsGump : AnchorableGump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly RazorScriptsAdapter _adapter = new RazorScriptsAdapter();
        private readonly List<Row> _rows = new List<Row>();
        private Control _surface;
        private AlphaBlendControl _input;
        private Label _title, _connection, _feedback, _empty;
        private ScrollArea _scroll;
        private StbTextBox _search;
        private Checkbox _runningOnly;
        private NiceButton _stopAll, _pinAll, _mode;
        private readonly ResizeDrag _resize = new ResizeDrag();
        private ResizeGrip _resizeGrip;
        private bool _compact;
        private Point _fullSize, _compactSize;
        private Point _windowSize;
        private string _lastBoundsCorrection = "none";
        private uint _nextBoundsReport;
        private Point _viewSize;
        private bool _recoverBounds = true;
        private uint _nextRefresh;
        private string _catalog = "";
        private RazorScriptsAdapter.Snapshot _shownSnapshot;
        private bool _shownBusy;
        private int Left => _compact ? 12 : 12 + CustomThemeArt.ContentInset;

        internal RazorScriptsGump() : base(0, 0)
        {
            _compact = _profile?.RazorScriptsCompact ?? false;
            _fullSize = _profile?.RazorScriptsSize ?? new Point(360, 500);
            _compactSize = _profile?.RazorScriptsCompactSize ?? new Point(180, 300);
            Point position = _profile?.RazorScriptsPosition ?? new Point(120, 120);
            X = position.X; Y = position.Y;
            CanMove = CanCloseWithRightClick = AcceptMouseInput = true;
            Point size = LimitSize(_compact ? _compactSize : _fullSize, _compact, ViewSize());
            _windowSize = size; Width = size.X; Height = size.Y;
            _surface = CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f);
            _surface.WantUpdateSize = false;
            Add(_surface);
            WantUpdateSize = false;
            Add(_title = new Label("Razor Scripts", true, CustomGumpThemeManager.TitleHue,
                font: 1, style: FontStyle.Cropped));
            Add(_mode = Button("-", ToggleCompact, 22));
            Add(_connection = new Label("Checking Razor...", true, CustomGumpThemeManager.TextHue,
                font: 1, style: FontStyle.Cropped));
            Add(_stopAll = Button("Stop All", () => Act(RazorScriptsAdapter.ActionKind.StopAll, null), 86));
            _stopAll.SetTooltip("Stop all Razor scripts, including scripts running in Razor's editor.");
            Add(_pinAll = Button("P", () => Pin(null), 22));
            _pinAll.SetTooltip("Pin Razor Stop All to the desktop for this Razor profile.");
            _input = new AlphaBlendControl(0.6f) { Height = 25 };
            CustomGumpThemeManager.ApplyInputSurface(_input, 0.6f); Add(_input);
            Add(_search = new StbTextBox(1, 128, 0, true, FontStyle.None, CustomGumpThemeManager.TextHue)
                { Height = 22, PlaceHolderText = "Search scripts..." });
            _search.SetTooltip("Search registered scripts by name.");
            _search.TextChanged += (_, __) => FilterChanged();
            Add(_runningOnly = new Checkbox(0x00D2, 0x00D3, "Running only", 1, CustomGumpThemeManager.TextHue));
            _runningOnly.SetTooltip("Show only running scripts, without empty rows.");
            _runningOnly.ValueChanged += (_, __) => FilterChanged();
            Add(_scroll = new ScrollArea(Left, 152, Width - Left * 2, Math.Max(16, Height - 188), true));
            Add(_empty = new Label("", true, CustomGumpThemeManager.DimHue,
                font: 1, style: FontStyle.Cropped));
            Add(_feedback = new Label("", true, CustomGumpThemeManager.TextHue,
                font: 1, style: FontStyle.Cropped));
            Add(_resizeGrip = new ResizeGrip(0, 0, 16, 16));
            _resizeGrip.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtonType.Left && !Keyboard.Alt && !IsLocked)
                {
                    _resize.Begin(Mouse.Position, _windowSize);
                    Log.Trace("Razor Scripts resize begin: " + DescribeBounds());
                }
            };
            _resizeGrip.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtonType.Left || !_resize.Active) return;
                if (!IsLocked) ApplySize(_resize.SizeAt(Mouse.Position, _compact, ViewSize()));
                _resize.End(); RememberBounds();
                Log.Trace("Razor Scripts resize end: " + DescribeBounds());
            };
            Layout(); KeepInScreen(); _adapter.Refresh();
        }

        internal static void Toggle()
        {
            if (!CloseAll()) UIManager.Add(new RazorScriptsGump());
        }
        private static bool CloseAll()
        {
            bool found = false;
            foreach (var gump in UIManager.Gumps)
                if (gump is RazorScriptsGump scripts && !scripts.IsDisposed)
                { found = true; scripts.Dispose(); }
            return found;
        }
        internal static void Reset()
        {
            CloseAll();
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null) return;
            profile.RazorScriptsPosition = new Point(120, 120);
            profile.RazorScriptsSize = new Point(360, 500);
            profile.RazorScriptsCompactSize = new Point(180, 300);
            profile.RazorScriptsCompact = false;
            UIManager.Add(new RazorScriptsGump());
        }
        internal static void ReportBounds()
        {
            var windows = UIManager.Gumps.OfType<RazorScriptsGump>().Where(g => !g.IsDisposed).ToArray();
            string report = "Razor Scripts bounds v3: windows=" + windows.Length + "; "
                + string.Join("; ", windows.Select(g => g.DescribeBounds()));
            Log.Trace(report);
            GameActions.Print(report, 0x35);
        }
        private string DescribeBounds() => "position=" + X + "," + Y + ", size=" + Width + "x" + Height
            + ", expected=" + _windowSize.X + "x" + _windowSize.Y
            + ", surface=" + _surface.Width + "x" + _surface.Height
            + ", compact=" + _compact + ", drag=" + _resize.Active
            + ", scale=" + UIManager.InterfaceRenderScale + ", viewport=" + ViewSize()
            + ", last correction=" + _lastBoundsCorrection;
        public override GumpType GumpType => GumpType.RazorScripts;
        private NiceButton Button(string text, Action action, int width = 54, int height = 22)
        {
            var button = new NiceButton(0, 0, width, height, ButtonAction.Activate, text, font: 1)
                { IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button);
            button.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left && button.IsEnabled) action(); };
            return button;
        }
        private void Act(RazorScriptsAdapter.ActionKind kind, string id)
        {
            _adapter.Act(kind, id, _adapter.Current.Profile);
            UpdateRows();
        }
        private void Pin(string id)
        {
            var snapshot = _adapter.Current;
            if (snapshot.Available) PinnedCommandManager.Pin(RazorScriptsAdapter.PinCommand(snapshot.Profile, id));
        }
        private void ToggleCompact()
        {
            _resize.End(); RememberBounds();
            _compact = !_compact;
            ApplySize(_compact ? _compactSize : _fullSize);
            Layout(); KeepInScreen(); RememberBounds();
        }
        private void FilterChanged()
        {
            _catalog = "";
            _scroll?.ResetScrollbarPosition();
            RebuildRows();
        }
        public override void Update()
        {
            if (IsDisposed) return;
            Point viewSize = ViewSize();
            if (_recoverBounds || _viewSize != viewSize)
            {
                _recoverBounds = false; _viewSize = viewSize;
                _resize.End(); ApplySize(_windowSize); KeepInScreen();
            }
            if (_resize.Active)
            {
                if (!Mouse.LButtonPressed || IsLocked) _resize.End();
                else ApplySize(_resize.SizeAt(Mouse.Position, _compact, viewSize));
            }
            WantUpdateSize = false; // Row extents must never resize the window.
            SynchronizeBounds();
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks >= _nextRefresh)
            { _nextRefresh = Time.Ticks + 500; _adapter.Refresh(); }
            RazorScriptsAdapter.Snapshot snapshot = _adapter.Current;
            bool busy = _adapter.Busy;
            if (!ReferenceEquals(snapshot, _shownSnapshot) || busy != _shownBusy)
            {
                _shownSnapshot = snapshot; _shownBusy = busy;
                string catalog = snapshot.Profile + "\n" + string.Join("\n",
                    RazorScriptsAdapter.Filter(snapshot.Scripts, _search.Text, _runningOnly.IsChecked)
                        .Select(s => s.Id + "\t" + s.Name));
                if (_catalog != catalog) { _catalog = catalog; RebuildRows(); }
                UpdateRows();
            }
            RememberBounds();
        }
        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed) return false;
            SynchronizeBounds();
            if (!batcher.ClipBegin(x, y, _windowSize.X, _windowSize.Y)) return false;
            try { return base.Draw(batcher, x, y); }
            finally { batcher.ClipEnd(); }
        }
        private void SynchronizeBounds()
        {
            if (Width != _windowSize.X || Height != _windowSize.Y
                || _surface.Width != _windowSize.X || _surface.Height != _windowSize.Y)
            {
                _lastBoundsCorrection = Width + "x" + Height + "/surface " + _surface.Width + "x" + _surface.Height;
                if (Time.Ticks >= _nextBoundsReport)
                {
                    _nextBoundsReport = Time.Ticks + 2000;
                    Log.Trace("Razor Scripts bounds correction: " + DescribeBounds());
                }
            }
            if (Width != _windowSize.X || Height != _windowSize.Y)
            {
                Width = _windowSize.X; Height = _windowSize.Y;
                Layout();
            }
            _surface.WantUpdateSize = false;
            _surface.Width = _windowSize.X; _surface.Height = _windowSize.Y;
        }
        private void RebuildRows()
        {
            if (_scroll == null) return;
            _scroll.Clear(); _rows.Clear();
            int index = 0, width = _scroll.Width - 20;
            foreach (var script in RazorScriptsAdapter.Filter(_adapter.Current.Scripts, _search.Text, _runningOnly.IsChecked))
            {
                string id = script.Id;
                var row = new Row { Id = id };
                var content = new DataBox(0, index++ * (_compact ? 34 : 48), width, _compact ? 32 : 46)
                    { WantUpdateSize = false };
                content.Add(row.Name = new Label(script.Name, true, CustomGumpThemeManager.TextHue,
                    Math.Max(25, width - 26), 1, FontStyle.Cropped));
                row.Name.SetTooltip(script.Id);
                content.Add(row.Pin = Button("P", () => Pin(id), 22, 16));
                row.Pin.X = width - 22;
                row.Pin.SetTooltip("Pin this script to the desktop. Click its desktop button to run/stop it.");
                content.Add(row.Status = new Label("", true, CustomGumpThemeManager.DimHue, font: 1)
                    { Y = _compact ? 16 : 25 });
                if (_compact)
                {
                    content.Add(row.Toggle = Button("Run", () =>
                    {
                        var current = _adapter.Current.Scripts.FirstOrDefault(s => s.Id == id);
                        if (current?.Running != null)
                            Act(current.Running == true ? RazorScriptsAdapter.ActionKind.Stop : RazorScriptsAdapter.ActionKind.Run, id);
                    }, 42, 16));
                    row.Toggle.X = width - 42; row.Toggle.Y = 16;
                }
                else
                {
                    content.Add(row.Run = Button("Run", () => Act(RazorScriptsAdapter.ActionKind.Run, id)));
                    content.Add(row.Stop = Button("Stop", () => Act(RazorScriptsAdapter.ActionKind.Stop, id)));
                    row.Run.X = width - 114; row.Stop.X = width - 56;
                    row.Run.Y = row.Stop.Y = 23;
                }
                _scroll.Add(content); _rows.Add(row);
            }
            UpdateRows();
            _scroll.SlowUpdate();
        }
        private void UpdateRows()
        {
            if (_scroll == null) return;
            var snapshot = _adapter.Current;
            bool enabled = snapshot.Available && !_adapter.Busy;
            string connection = snapshot.Available
                ? "Razor available · " + snapshot.Profile + " · " + snapshot.RunningCount + " running"
                : "Razor unavailable";
            int unknown = snapshot.Scripts.Count(s => s.Running == null);
            if (snapshot.Available && unknown != 0) connection += " · " + unknown + " unknown";
            _connection.Text = connection; _connection.SetTooltip(connection);
            _title.Text = _compact ? "Razor · " + (snapshot.Available ? snapshot.RunningCount.ToString() : "?") : "Razor Scripts";
            _title.Hue = snapshot.Available ? CustomGumpThemeManager.TitleHue : CustomGumpThemeManager.DimHue;
            _title.SetTooltip(connection + (_compact ? "\nExpand to change search and Running only filters." : ""));
            _feedback.Text = snapshot.Feedback; _feedback.SetTooltip(snapshot.Feedback);
            Enable(_stopAll, enabled && snapshot.AnyRunning);
            Enable(_pinAll, snapshot.Available);
            _search.IsEnabled = _runningOnly.IsEnabled = snapshot.Available;
            var byId = snapshot.Scripts.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            foreach (Row row in _rows)
            {
                if (!byId.TryGetValue(row.Id, out var script)) continue;
                row.Status.Text = script.Error != null ? "Unknown" : script.Running == true ? "Running" : "Stopped";
                row.Status.Hue = script.Running == true ? (ushort)0x0044 : CustomGumpThemeManager.DimHue;
                row.Status.SetTooltip(script.Error ?? row.Status.Text);
                Enable(row.Pin, snapshot.Available);
                if (_compact)
                {
                    row.Status.Text = script.Running == null ? "?" : script.Running == true ? "On" : "Off";
                    row.Toggle.SetText(script.Running == true ? "Stop" : "Run");
                    Enable(row.Toggle, enabled && script.Running != null);
                }
                else
                {
                    Enable(row.Run, enabled && script.Running == false);
                    Enable(row.Stop, enabled && script.Running == true);
                }
            }
            _empty.Text = !snapshot.Available ? "Razor unavailable"
                : snapshot.Scripts.Length == 0 ? "No registered scripts."
                : _runningOnly.IsChecked && snapshot.RunningCount == 0 ? unknown == 0
                    ? snapshot.AnyRunning ? "No registered scripts running." : "No scripts running."
                    : "Running status could not be read for some scripts."
                : !RazorScriptsAdapter.Filter(snapshot.Scripts, _search.Text, _runningOnly.IsChecked).Any()
                    ? "No scripts match these filters." : "";
            _empty.SetTooltip(_empty.Text);
            LayoutScroll();
        }
        private static void Enable(NiceButton button, bool enabled)
        { button.IsEnabled = enabled; button.TextLabel.Hue = enabled ? CustomGumpThemeManager.TextHue : CustomGumpThemeManager.DimHue; }
        private void LayoutScroll()
        {
            _scroll.X = Left; _scroll.Y = _compact ? 50 : 152;
            _scroll.Width = Width - Left * 2;
            _feedback.IsVisible = !_compact || !string.IsNullOrEmpty(_feedback.Text);
            int footer = _feedback.IsVisible ? (_compact ? 22 : 30) : 8;
            int height = Math.Max(16, Height - _scroll.Y - footer);
            bool resized = _scroll.Height != height;
            _scroll.Height = height;
            _scroll.UpdateScrollbarPosition();
            _feedback.X = Left; _feedback.Y = Height - (_compact ? 20 : 24); _feedback.SetMaxWidth(Width - Left * 2);
            _empty.X = Left; _empty.Y = _scroll.Y + 2; _empty.SetMaxWidth(_scroll.Width - 20);
            if (resized) _scroll.SlowUpdate();
        }
        private void Layout()
        {
            GroupMatrixWidth = Width; GroupMatrixHeight = Height;
            _resizeGrip.X = Width - _resizeGrip.Width;
            _resizeGrip.Y = Height - _resizeGrip.Height;
            _surface.Width = Width; _surface.Height = Height;
            _title.X = Left; _title.Y = _compact ? 6 : 14; _title.SetMaxWidth(Width - Left * 2 - 26);
            _mode.X = Width - Left - 22; _mode.Y = _compact ? 4 : 10;
            _mode.SetText(_compact ? "+" : "-"); _mode.SetTooltip(_compact ? "Expand Razor Scripts" : "Minimize Razor Scripts");
            _connection.IsVisible = _input.IsVisible = _search.IsVisible = _runningOnly.IsVisible = !_compact;
            _connection.X = Left; _connection.Y = 40; _connection.SetMaxWidth(Width - Left * 2);
            _stopAll.X = Left; _stopAll.Y = _compact ? 26 : 64;
            _stopAll.Width = _compact ? Math.Min(100, Width - Left * 2 - 26) : 86;
            _stopAll.Height = _compact ? 20 : 22;
            _stopAll.TextLabel.SetMaxWidth(_stopAll.Width);
            _stopAll.TextLabel.Y = (_stopAll.Height - _stopAll.TextLabel.Height) / 2;
            _pinAll.X = _compact ? Width - Left - 22 : Left + 90; _pinAll.Y = _stopAll.Y;
            _input.X = Left; _input.Y = 92; _input.Width = Width - Left * 2;
            _search.X = Left + 4; _search.Y = 96; _search.Width = _input.Width - 8;
            _runningOnly.X = Left; _runningOnly.Y = 122;
            LayoutScroll(); RebuildRows();
        }
        private void ApplySize(Point requested)
        {
            Point size = LimitSize(requested, _compact, ViewSize());
            if (_windowSize == size && Width == size.X && Height == size.Y) return;
            _windowSize = size; Width = size.X; Height = size.Y;
            Layout();
        }

        // A drag owns an immutable starting size; persistence and layout cannot rebase it.
        internal sealed class ResizeDrag
        {
            private Point _pointer, _size;
            internal bool Active { get; private set; }
            internal void Begin(Point pointer, Point size)
            { _pointer = pointer; _size = size; Active = true; }
            internal Point SizeAt(Point pointer, bool compact, Point viewport) =>
                LimitSize(_size + (pointer - _pointer), compact, viewport);
            internal void End() => Active = false;
        }
        internal static Point LimitSize(Point size, bool compact, Point viewport)
        {
            int minWidth = compact ? 148 : 300, minHeight = compact ? 104 : 240;
            return new Point(Math.Max(minWidth, Math.Min(size.X, viewport.X)),
                Math.Max(minHeight, Math.Min(size.Y, viewport.Y)));
        }
        private static Point ViewSize()
        {
            Rectangle bounds = Client.Game.Window.ClientBounds;
            float scale = Math.Max(0.1f, UIManager.InterfaceRenderScale);
            return new Point((int)(bounds.Width / scale), (int)(bounds.Height / scale));
        }
        private void KeepInScreen()
        {
            Point viewport = ViewSize();
            X = Math.Max(0, Math.Min(X, viewport.X - Width));
            Y = Math.Max(0, Math.Min(Y, viewport.Y - Height));
        }
        private void RememberBounds()
        {
            if (_compact) _compactSize = _windowSize;
            else _fullSize = _windowSize;
            if (_profile == null) return;
            _profile.RazorScriptsPosition = new Point(X, Y);
            _profile.RazorScriptsSize = _fullSize; _profile.RazorScriptsCompactSize = _compactSize;
            _profile.RazorScriptsCompact = _compact;
        }
        public override void Dispose()
        {
            if (!IsDisposed) { _resize.End(); RememberBounds(); _adapter.Dispose(); }
            base.Dispose();
        }
        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);
            writer.WriteAttributeString("w", _windowSize.X.ToString()); writer.WriteAttributeString("h", _windowSize.Y.ToString());
            writer.WriteAttributeString("compact", _compact.ToString());
        }
        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
            if (bool.TryParse(xml.GetAttribute("compact"), out bool compact)) _compact = compact;
            int w = int.TryParse(xml.GetAttribute("w"), out int savedW) ? savedW : Width;
            int h = int.TryParse(xml.GetAttribute("h"), out int savedH) ? savedH : Height;
            _resize.End();
            ApplySize(new Point(w, h)); Layout();
            _recoverBounds = true;
        }
        private sealed class Row
        {
            internal string Id;
            internal Label Name, Status;
            internal NiceButton Run, Stop, Toggle, Pin;
        }
    }
}
