#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.Xml;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Minimal damage / DPS tracker gump.
    /// Reads rolling 15s DPS from each Mobile via GameObject.GetCurrentDPS().
    /// Shows top N targets sorted desc by DPS, refreshed every 500ms.
    /// </summary>
    internal class DamageTrackerGump : AnchorableGump
    {
        private const int DEFAULT_WIDTH = 280;
        private const int ROW_HEIGHT = 18;
        private const int HEADER_HEIGHT = 36;
        private const int MAX_ROWS = 30;
        private const int MIN_WIDTH = 220;
        private const int MIN_HEIGHT = HEADER_HEIGHT + ROW_HEIGHT * 3 + 4;

        private static int _lastW = DEFAULT_WIDTH;
        private static int _lastH = HEADER_HEIGHT + ROW_HEIGHT * 10 + 4;

        private readonly AlphaBlendControl _background;
        private readonly Label _header;
        private readonly Label _subHeader;
        private readonly NiceButton _resetButton;
        private readonly NiceButton _modeButton;
        private readonly Label[] _rows = new Label[MAX_ROWS];
        private readonly HitBox[] _rowHits = new HitBox[MAX_ROWS];
        private readonly uint[] _rowSerials = new uint[MAX_ROWS];
        // Bar overlay state: percent (0..1) and notoriety-derived bar hue per row.
        private readonly float[] _rowBarPercent = new float[MAX_ROWS];
        private readonly ushort[] _rowBarHue = new ushort[MAX_ROWS];
        private int _visibleRowCount;
        private readonly HitBox _resizeGrip;
        private bool _resizing;
        private int _resizeStartX, _resizeStartY, _startW, _startH;

        private long _refreshTime;

        private enum FilterMode { All, Outgoing, Incoming }
        private FilterMode _mode = FilterMode.Outgoing;

        // Reused snapshot buffer to avoid per-refresh allocations.
        private readonly List<(uint serial, double dps, long total, string name, ushort hue)> _snapshot = new();

        public DamageTrackerGump() : this(100, 100) { }

        public DamageTrackerGump(int x, int y) : base(0, 0)
        {
            X = x;
            Y = y;
            Width = _lastW;
            Height = _lastH;

            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = Width;
            GroupMatrixHeight = Height;

            Add(_background = new AlphaBlendControl(0.7f) { Width = Width, Height = Height });
            CustomGumpThemeManager.ApplyDataSurface(_background, 0.7f);

            Add(_header = new Label("Damage Tracker", true, 0x0481, font: 1)
            {
                X = 6,
                Y = 2
            });

            Add(_subHeader = new Label(string.Empty, true, 0x03B2, font: 1)
            {
                X = 6,
                Y = 18
            });

            Add(_modeButton = new NiceButton(Width - 116, 2, 56, 16, ButtonAction.Activate, ModeText())
            {
                ButtonParameter = 2,
                IsSelectable = false
            });

            Add(_resetButton = new NiceButton(Width - 56, 2, 50, 16, ButtonAction.Activate, "Reset")
            {
                ButtonParameter = 1,
                IsSelectable = false
            });

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rows[i] = new Label(string.Empty, true, 0x0481, font: 1, maxwidth: Width - 8)
                {
                    X = 6,
                    Y = HEADER_HEIGHT + (i * ROW_HEIGHT)
                };
                Add(_rows[i]);

                int rowIndex = i; // capture for closure
                _rowHits[i] = new HitBox(0, HEADER_HEIGHT + (i * ROW_HEIGHT), Width - 4, ROW_HEIGHT, "Click to attack target", 0f);
                _rowHits[i].MouseUp += (s, e) =>
                {
                    if (e.Button != MouseButtonType.Left) return;
                    uint serial = _rowSerials[rowIndex];
                    if (serial == 0) return;
                    if (World.Player != null && serial == World.Player.Serial) return;
                    GameActions.Attack(serial);
                };
                Add(_rowHits[i]);
            }

            _resizeGrip = new HitBox(Width - 12, Height - 12, 12, 12, "Drag to resize", 0.5f);
            _resizeGrip.MouseDown += (s, e) =>
            {
                _resizing = true;
                _resizeStartX = Mouse.Position.X;
                _resizeStartY = Mouse.Position.Y;
                _startW = Width;
                _startH = Height;
            };
            _resizeGrip.MouseUp += (s, e) => _resizing = false;
            Add(_resizeGrip);

            ApplyVisibleRowCount();
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 1)
            {
                DamageSessionTracker.Reset();
            }
            else if (buttonID == 2)
            {
                _mode = (FilterMode)(((int)_mode + 1) % 3);
                _modeButton.TextLabel.Text = ModeText();
                _refreshTime = 0; // force immediate refresh
            }
            else
            {
                base.OnButtonClick(buttonID);
            }
        }

        private string ModeText()
        {
            switch (_mode)
            {
                case FilterMode.All: return "All";
                case FilterMode.Outgoing: return "Outgoing";
                case FilterMode.Incoming: return "Incoming";
            }
            return "?";
        }

        public override GumpType GumpType => GumpType.DamageTracker;

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;

            if (_resizing)
            {
                if (Mouse.LButtonPressed)
                {
                    int newW = Math.Max(MIN_WIDTH, _startW + (Mouse.Position.X - _resizeStartX));
                    int newH = Math.Max(MIN_HEIGHT, _startH + (Mouse.Position.Y - _resizeStartY));
                    if (newW != Width || newH != Height)
                        Resize(newW, newH);
                }
                else
                {
                    _resizing = false;
                }
            }

            if (Time.Ticks < _refreshTime)
                return;

            _refreshTime = (long)Time.Ticks + 500;

            BuildSnapshot();
            RenderRows();
        }

        private int VisibleRowCount()
        {
            int n = (Height - HEADER_HEIGHT - 4) / ROW_HEIGHT;
            if (n < 1) n = 1;
            if (n > MAX_ROWS) n = MAX_ROWS;
            return n;
        }

        private void ApplyVisibleRowCount()
        {
            int visible = VisibleRowCount();
            for (int i = 0; i < MAX_ROWS; i++)
            {
                bool show = i < visible;
                _rows[i].IsVisible = show;
                _rowHits[i].IsVisible = show;
            }
        }

        private void Resize(int w, int h)
        {
            Width = w;
            Height = h;
            _lastW = w;
            _lastH = h;

            _background.Width = w;
            _background.Height = h;

            _modeButton.X = w - 116;
            _resetButton.X = w - 56;

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rowHits[i].Width = w - 4;
                // Row label maxwidth is set on construction; long names may clip if you make
                // the window narrower than default. Reopen the gump to rebuild rows at the
                // new width, or keep width >= the default.
            }

            _resizeGrip.X = w - 12;
            _resizeGrip.Y = h - 12;

            GroupMatrixWidth = w;
            GroupMatrixHeight = h;
            ApplyVisibleRowCount();
        }

        private void BuildSnapshot()
        {
            _snapshot.Clear();

            uint playerSerial = World.Player?.Serial ?? 0;

            foreach (Mobile m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed)
                    continue;

                bool isPlayer = m.Serial == playerSerial;
                if (_mode == FilterMode.Outgoing && isPlayer) continue;
                if (_mode == FilterMode.Incoming && !isPlayer) continue;

                long total = DamageSessionTracker.GetTotal(m.Serial);
                double dps = m.HasRecentDamage ? m.GetCurrentDPS() : 0;

                if (dps <= 0 && total <= 0)
                    continue;

                ushort hue = Notoriety.GetHue(m.NotorietyFlag);
                _snapshot.Add((m.Serial, dps, total, string.IsNullOrEmpty(m.Name) ? "<unknown>" : m.Name, hue));
            }

            _snapshot.Sort(_byDpsDesc);
        }

        private static readonly Comparison<(uint serial, double dps, long total, string name, ushort hue)> _byDpsDesc =
            (a, b) =>
            {
                int c = b.dps.CompareTo(a.dps);
                return c != 0 ? c : b.total.CompareTo(a.total);
            };

        private void RenderRows()
        {
            // Sub-header: total scoped to current filter mode.
            double secs = DamageSessionTracker.SessionSeconds;
            long modeTotal = _mode switch
            {
                FilterMode.Outgoing => DamageSessionTracker.OutgoingTotal,
                FilterMode.Incoming => DamageSessionTracker.IncomingTotal,
                _ => DamageSessionTracker.GrandTotal,
            };
            double avgDps = modeTotal / secs;
            string sub = $"{ModeText()}: {modeTotal} dmg / {secs:0}s = {avgDps:0.#} dps avg";
            if (_subHeader.Text != sub)
                _subHeader.Text = sub;

            int visible = VisibleRowCount();
            int shown = Math.Min(_snapshot.Count, visible);

            // Reference value for bar normalization = top row's DPS (or 1 if none).
            double topDps = shown > 0 ? _snapshot[0].dps : 0;
            if (topDps <= 0) topDps = 1;

            for (int i = 0; i < shown; i++)
            {
                var entry = _snapshot[i];
                string text = $"{entry.name}  {entry.dps:0.#} dps  ({entry.total} total)";
                if (_rows[i].Text != text)
                    _rows[i].Text = text;
                if (_rows[i].Hue != entry.hue)
                    _rows[i].Hue = entry.hue;
                _rowSerials[i] = entry.serial;
                _rowBarPercent[i] = (float)System.Math.Min(1.0, entry.dps / topDps);
                _rowBarHue[i] = entry.hue;
            }

            for (int i = shown; i < MAX_ROWS; i++)
            {
                if (_rows[i].Text.Length != 0)
                    _rows[i].Text = string.Empty;
                _rowSerials[i] = 0;
                _rowBarPercent[i] = 0;
            }

            _visibleRowCount = shown;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (!base.Draw(batcher, x, y)) return false;

            // Per-row mini-bar at the bottom of each row, width proportional to
            // (row dps / top dps). Drawn after the label so it sits underneath text.
            const int BAR_H = 3;
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(30, 30, 30, 180));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            int rowAreaTop = y + HEADER_HEIGHT;
            int barAreaX = x + 4;
            int barAreaW = Width - 8;

            for (int i = 0; i < _visibleRowCount && i < MAX_ROWS; i++)
            {
                int by = rowAreaTop + (i + 1) * ROW_HEIGHT - BAR_H - 1;
                batcher.Draw(bg, new Rectangle(barAreaX, by, barAreaW, BAR_H), hueNeutral);

                int fill = (int)(barAreaW * _rowBarPercent[i]);
                if (fill < 1) continue;

                Vector3 hueBar = ShaderHueTranslator.GetHueVector(_rowBarHue[i] == 0 ? (ushort)0x0044 : _rowBarHue[i], false, 1f);
                Texture2D barTex = SolidColorTextureCache.GetTexture(Color.White);
                batcher.Draw(barTex, new Rectangle(barAreaX, by, fill, BAR_H), hueBar);
            }

            return true;
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer); // base already writes type/x/y/serial/...
            writer.WriteAttributeString("w", Width.ToString());
            writer.WriteAttributeString("h", Height.ToString());
            writer.WriteAttributeString("mode", ((int)_mode).ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml); // X/Y are restored by the outer RestoreGumps loop
            if (int.TryParse(xml.GetAttribute("w"), out int w) && w >= MIN_WIDTH &&
                int.TryParse(xml.GetAttribute("h"), out int h) && h >= MIN_HEIGHT)
            {
                Resize(w, h);
            }
            if (int.TryParse(xml.GetAttribute("mode"), out int mode) && mode >= 0 && mode <= 2)
            {
                _mode = (FilterMode)mode;
                if (_modeButton != null) _modeButton.TextLabel.Text = ModeText();
            }
        }
    }
}
