#region license
// TazUO addition. Compact themed group of persistent command buttons.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PinnedCommandGroupGump : Gump
    {
        private const int PAD = 3;
        private const int BUTTON_HEIGHT = 21;
        private readonly int _commandCount;
        private string _undockCommand;
        private readonly RazorScriptsAdapter _razor;
        private readonly List<RazorPin> _razorPins = new List<RazorPin>();
        private uint _nextRazorRefresh;
        private RazorScriptsAdapter.Snapshot _shownRazor;
        private bool _shownRazorBusy;

        internal int GroupId { get; }

        internal PinnedCommandGroupGump(int groupId, IReadOnlyList<string> commands) : base(0, 0)
        {
            GroupId = groupId;
            _commandCount = commands.Count;
            CanMove = true;
            CanCloseWithRightClick = false;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            int longest = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                string label = commands[i];
                if (RazorScriptsAdapter.TryReadPin(label, out _, out string script))
                {
                    if (_razor == null) _razor = new RazorScriptsAdapter();
                    label = script == null ? "Razor: Stop All" : "Stop " + RazorScriptsAdapter.PinName(script);
                }
                longest = Math.Max(longest, label?.Length ?? 0);
            }

            Width = Math.Max(76, Math.Min(230, 24 + longest * 7));
            Height = PAD * 2 + Math.Max(1, commands.Count) * BUTTON_HEIGHT;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.86f));

            for (int i = 0; i < commands.Count; i++)
            {
                string command = commands[i];
                bool razorPin = RazorScriptsAdapter.TryReadPin(command, out _, out string scriptId);
                var button = new NiceButton(PAD, PAD + i * BUTTON_HEIGHT, Width - PAD * 2, BUTTON_HEIGHT - 1,
                    ButtonAction.Activate, razorPin ? RazorScriptsAdapter.PinName(scriptId) : command, font: 1)
                {
                    IsSelectable = false,
                    DisplayBorder = true,
                    AlwaysShowBackground = true,
                    CanMove = true,
                    CanCloseWithRightClick = false
                };
                CustomGumpThemeManager.StyleButton(button);
                string controls = commands.Count > 1
                    ? "Left-click: run command\nDrag: move the group\nAlt+drag: undock this command\nAlt+click: undock beside the group\nDrop onto another pinned command to group\nRight-click: remove"
                    : "Left-click: run command\nDrag: move; drop onto another pinned command to group\nRight-click: remove";
                button.SetTooltip(controls, 350);
                if (razorPin) _razorPins.Add(new RazorPin { Button = button, Command = command, Controls = controls });
                string captured = command;
                button.MouseDown += (s, e) =>
                {
                    _undockCommand = e.Button == MouseButtonType.Left
                        && Keyboard.Alt && _commandCount > 1
                            ? captured
                            : null;
                };
                button.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                    {
                        if (Keyboard.Alt && _commandCount > 1)
                        {
                            PinnedCommandManager.Undock(
                                GroupId, captured, X + Width + 8, Y);
                        }
                        else
                        {
                            if (razorPin) { _razor.ExecutePin(captured); RefreshRazorPins(); }
                            else PinnedCommandManager.Execute(captured);
                        }
                    }
                    else if (e.Button == MouseButtonType.Right)
                        PinnedCommandManager.Remove(GroupId, captured);

                    _undockCommand = null;
                };
                Add(button);
            }
            RefreshRazorPins();
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed || _razor == null) return;
            if (Time.Ticks >= _nextRazorRefresh)
            { _nextRazorRefresh = Time.Ticks + 500; _razor.Refresh(); }
            if (!ReferenceEquals(_shownRazor, _razor.Current) || _shownRazorBusy != _razor.Busy) RefreshRazorPins();
        }

        private void RefreshRazorPins()
        {
            if (_razor == null) return;
            _shownRazor = _razor.Current; _shownRazorBusy = _razor.Busy;
            foreach (RazorPin pin in _razorPins)
            {
                bool enabled = _razor.PinState(pin.Command, out string label, out string tooltip);
                pin.Button.SetText(label);
                pin.Button.TextLabel.Hue = enabled ? CustomGumpThemeManager.TextHue : CustomGumpThemeManager.DimHue;
                // Keep right-click removal and drag/docking available even when Razor is offline.
                // ExecutePin gates left-click execution against availability, profile and status.
                pin.Button.SetTooltip(tooltip + "\n" + pin.Controls, 350);
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed) _razor?.Dispose();
            base.Dispose();
        }

        private sealed class RazorPin
        {
            internal NiceButton Button;
            internal string Command, Controls;
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);

            if (_undockCommand != null && _commandCount > 1)
            {
                string command = _undockCommand;
                _undockCommand = null;

                if (PinnedCommandManager.Undock(GroupId, command, X, Y))
                {
                    return;
                }
            }

            PinnedCommandManager.UpdatePosition(GroupId, X, Y);
            PinnedCommandManager.TryMergeNearby(this);
        }
    }
}
