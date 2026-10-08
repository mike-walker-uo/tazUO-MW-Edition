using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class QueuedOperationsGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly ScrollArea _rows;
        private long _refreshAt;
        private readonly List<Row> _controls = new();
        private sealed class Row { internal QueuedOperation Operation; internal Label Summary; internal NiceButton Cancel; }
        internal QueuedOperationsGump() : base(0, 0)
        {
            Width = 560; Height = 190; WantUpdateSize = false;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true; AcceptMouseInput = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("QUEUED ACTIONS", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 16, Y = 12 });
            Add(new Label("Cancel stops unsent work. Closing this panel does not cancel actions.", true,
                CustomGumpThemeManager.TextHue, 520, font: 1) { X = 16, Y = 36 });
            Add(_rows = new ScrollArea(12, 62, 536, 116, true));
            CenterXInViewPort(); CenterYInViewPort();
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            base.Update();
            if (Time.Ticks < _refreshAt) return;
            _refreshAt = (long)Time.Ticks + 300;
            bool rebuild = _controls.Count != QueuedOperations.Recent.Count;
            for (int i = 0; !rebuild && i < _controls.Count; i++)
                rebuild = _controls[i].Operation != QueuedOperations.Recent[i];
            if (rebuild)
            {
                _rows.Clear(); _controls.Clear(); int y = 0;
                foreach (QueuedOperation operation in QueuedOperations.Recent)
                { AddRow(operation, y); y += 88; }
            }
            foreach (Row row in _controls)
            {
                string summary = row.Operation.Summary;
                if (row.Summary.Text != summary) row.Summary.Text = summary;
                row.Cancel.IsVisible = !row.Operation.Finished;
            }
        }
        private void AddRow(QueuedOperation operation, int y)
        {
            _rows.Add(new Label(operation.Name, true, CustomGumpThemeManager.TitleHue, 420, font: 1) { X = 4, Y = y });
            var row = new Row { Operation = operation };
            _rows.Add(row.Summary = new Label(operation.Summary, true, CustomGumpThemeManager.TextHue, 420, font: 1) { X = 4, Y = y + 20 });
            row.Cancel = new NiceButton(444, y, 65, 25, ButtonAction.Activate, "Cancel") { IsSelectable = false, IsVisible = !operation.Finished };
            row.Cancel.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) operation.Cancel(); };
            CustomGumpThemeManager.StyleDataButton(row.Cancel); _rows.Add(row.Cancel);
            _controls.Add(row);
        }
    }
}
