using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal class MultipleToolTipGump : Gump
    {
        private readonly CustomToolTip[] toolTips;
        private readonly Control hoverReference;
        private readonly StaticPic[] _art;
        private readonly AlphaBlendControl[] _swatches;
        private readonly TextBox[] _headings;
        private readonly TextBox _footer;
        private readonly NiceButton _pin;
        private bool _pinned;
        private long _closeAt;
        private int _screenWidth;
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly uint[] _serials;
        public static bool SSIsEnabled;
        public static int SSX, SSY, SSWidth, SSHeight;

        public MultipleToolTipGump(int x, int y, CustomToolTip[] toolTips, Control hoverReference) : base(0, 0)
        {
            this.toolTips = toolTips;
            _serials = toolTips.Select(t => t.ComparedItem.Serial).ToArray();
            this.hoverReference = hoverReference;
            _art = new StaticPic[toolTips.Length];
            _swatches = new AlphaBlendControl[toolTips.Length];
            _headings = new TextBox[toolTips.Length];
            WantUpdateSize = false;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            CanCloseWithEsc = true;
            Add(CreateText("ITEM COMPARISON", 12, 12));
            _pin = new NiceButton(0, 10, 142, 28, ButtonAction.Activate, "Pin comparison",
                hue: _profile.TooltipTextHue, font: 1)
            {
                IsSelectable = false,
                ButtonParameter = 1,
                DisplayBorder = true,
                CanMove = false
            };
            Add(_pin);
            for (int i = 0; i < toolTips.Length; i++)
            {
                var tooltip = toolTips[i];
                tooltip.RemoveHoverReference();
                tooltip.OnOPLLoaded += LayoutContent;
                var item = tooltip.ComparedItem;
                _headings[i] = CreateText((i == 0 ? "Candidate" : "Equipped") + $" / hue 0x{item.Hue:X4}", 0, 48);
                Add(_headings[i]);
                var art = _art[i] = new StaticPic(item.Graphic, item.Hue) { AcceptMouseInput = false, CanMove = false };
                float scale = Math.Min(2f, 76f / Math.Max(1, Math.Max(art.Width, art.Height)));
                art.Width = (int)(art.Width * scale);
                art.Height = (int)(art.Height * scale);
                Add(art);
                _swatches[i] = new AlphaBlendControl(1f)
                {
                    Width = 12, Height = 12,
                    BaseColor = ComparisonPropertyRows.HueSwatch(item.Hue),
                    AcceptMouseInput = false, CanMove = false
                };
                Add(_swatches[i]);
                Add(tooltip);
            }
            _footer = CreateText("Right-click / Esc closes.", 12, 0);
            Add(_footer);
            LayoutContent();
            X = x;
            Y = y;
            SSIsEnabled = true;
        }

        private TextBox CreateText(string value, int x, int y)
        {
            var text = TextBox.GetOne(value, _profile.SelectedToolTipFont, _profile.SelectedToolTipFontSize,
                _profile.TooltipTextHue, TextBox.RTLOptions.Default());
            text.X = x;
            text.Y = y;
            return text;
        }

        private void LayoutContent()
        {
            if (IsDisposed) return;
            _screenWidth = (int)(Client.Game.Window.ClientBounds.Width / UIManager.InterfaceRenderScale);
            int maxColumn = Math.Min(600, Math.Max(180, (_screenWidth - 24) / toolTips.Length - 16));
            foreach (var tooltip in toolTips)
                tooltip.SetComparisonWidth(maxColumn);
            int column = Math.Max(180, toolTips.Max(t => t.Width));
            int headingHeight = 0;
            foreach (var heading in _headings)
            {
                heading.Width = column;
                heading.Update();
                headingHeight = Math.Max(headingHeight, heading.Height);
            }
            int artTop = 48 + headingHeight + 8;
            int contentTop = artTop + 96;
            Width = 24 + column * toolTips.Length + 16 * (toolTips.Length - 1);
            for (int i = 0; i < toolTips.Length; i++)
            {
                int left = 12 + i * (column + 16);
                _headings[i].X = left;
                _art[i].X = left + (column - _art[i].Width) / 2;
                _art[i].Y = artTop;
                _swatches[i].X = left + (column - 12) / 2;
                _swatches[i].Y = artTop + 80;
                toolTips[i].X = left + (column - toolTips[i].Width) / 2;
                toolTips[i].Y = contentTop;
            }
            _pin.X = Width - _pin.Width - 12;
            _footer.Y = contentTop + toolTips.Max(t => t.Height) + 12;
            _footer.Width = Width - 24;
            _footer.Update();
            Height = _footer.Y + _footer.Height + 12;
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID != 1) return;
            _pinned = !_pinned;
            _pin.SetText(_pinned ? "Unpin comparison" : "Pin comparison");
            _closeAt = 0;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return false; }
            for (int i = 0; i < toolTips.Length; i++)
                if (toolTips[i].ComparedItem.IsDestroyed || toolTips[i].ComparedItem.Serial != _serials[i]) { Dispose(); return false; }
            Control hovered = UIManager.MouseOverControl;
            if (_pinned || hoverReference.MouseIsOver || hovered == this || hovered?.RootParent == this
                || UIManager.DraggingControl == this) _closeAt = 0;
            else if (_closeAt == 0) _closeAt = (long)Time.Ticks + 400;
            else if (Time.Ticks >= _closeAt) { Dispose(); return false; }
            float scale = UIManager.InterfaceRenderScale;
            if (_screenWidth != (int)(Client.Game.Window.ClientBounds.Width / scale))
                LayoutContent();
            x = Math.Max(0, Math.Min(x, _screenWidth - Width));
            y = Math.Max(0, Math.Min(y, (int)(Client.Game.Window.ClientBounds.Height / scale) - Height));
            X = x;
            Y = y;
            SSX = (int)(x * scale);
            SSY = (int)(y * scale);
            SSWidth = (int)(Width * scale);
            SSHeight = (int)(Height * scale);
            CustomToolTip.DrawBackground(batcher, x, y, Width, Height);
            return base.Draw(batcher, x, y);
        }

        public override void Dispose()
        {
            foreach (var tooltip in toolTips) tooltip.OnOPLLoaded -= LayoutContent;
            base.Dispose();
            SSIsEnabled = UIManager.Gumps.OfType<MultipleToolTipGump>().Any(g => !g.IsDisposed);
        }
    }
}
