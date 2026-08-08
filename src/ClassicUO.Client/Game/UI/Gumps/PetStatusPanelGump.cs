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
using ClassicUO.Configuration;
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
    /// Anchorable panel listing nearby pets/followers (using TazUO's
    /// IsRenamable + non-Enemy/non-Invulnerable predicate). Shows HP/Mana/Stam,
    /// distance, and a row of group commands.
    /// </summary>
    internal class PetStatusPanelGump : AnchorableGump
    {
        private const int DEFAULT_WIDTH = 300;
        private const int DEFAULT_HEIGHT = 280;
        private const int ROW_HEIGHT = 38;
        private const int HEADER_H = 22;
        private const int FOOTER_H = 26;
        private const int MIN_WIDTH = 220;
        private const int MIN_HEIGHT = HEADER_H + ROW_HEIGHT * 2 + FOOTER_H + 4;

        private static int _lastX = 260, _lastY = 260;
        private static int _lastW = DEFAULT_WIDTH, _lastH = DEFAULT_HEIGHT;

        private readonly AlphaBlendControl _background;
        private readonly Label _header;
        private readonly NiceButton _btnFollow, _btnGuard, _btnStay, _btnStop, _btnKill;
        private readonly HitBox _resizeGrip;

        private readonly List<PetRow> _rows = new List<PetRow>();
        private long _refreshTime;
        private bool _resizing;
        private int _resizeStartX, _resizeStartY, _startW, _startH;

        public PetStatusPanelGump() : this(_lastX, _lastY) { }

        public PetStatusPanelGump(int x, int y) : base(0, 0)
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

            Add(_background = new AlphaBlendControl(0.65f) { Width = Width, Height = Height });
            CustomGumpThemeManager.ApplyDataSurface(_background, 0.65f);

            Add(_header = new Label("Pet Status", true, 0x0481, font: 1) { X = 6, Y = 3 });

            int btnW = 50;
            int btnY = Height - FOOTER_H + 4;
            int x0 = 6;
            Add(_btnFollow = new NiceButton(x0, btnY, btnW, 18, ButtonAction.Activate, "Follow") { ButtonParameter = 1, IsSelectable = false });
            Add(_btnGuard = new NiceButton(x0 + btnW + 4, btnY, btnW, 18, ButtonAction.Activate, "Guard") { ButtonParameter = 2, IsSelectable = false });
            Add(_btnStay = new NiceButton(x0 + (btnW + 4) * 2, btnY, btnW, 18, ButtonAction.Activate, "Stay") { ButtonParameter = 3, IsSelectable = false });
            Add(_btnStop = new NiceButton(x0 + (btnW + 4) * 3, btnY, btnW, 18, ButtonAction.Activate, "Stop") { ButtonParameter = 4, IsSelectable = false });
            Add(_btnKill = new NiceButton(x0 + (btnW + 4) * 4, btnY, btnW, 18, ButtonAction.Activate, "Kill") { ButtonParameter = 5, IsSelectable = false });

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
        }

        public override GumpType GumpType => GumpType.PetStatusPanel;

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1: SendCommand("all follow me"); break;
                case 2: SendCommand("all guard me"); break;
                case 3: SendCommand("all stay"); break;
                case 4: SendCommand("all stop"); break;
                case 5: SendCommand("all kill"); break;
                default: base.OnButtonClick(buttonID); break;
            }
        }

        private void SendCommand(string text)
        {
            ushort hue = ProfileManager.CurrentProfile?.SpeechHue ?? (ushort)0x0035;
            GameActions.Say(text, hue);
        }

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
                    if (newW != Width || newH != Height) Resize(newW, newH);
                }
                else
                {
                    _resizing = false;
                }
            }

            if (Time.Ticks >= _refreshTime)
            {
                _refreshTime = (long)Time.Ticks + 500;
                RebuildRows();
            }
            else
            {
                for (int i = 0; i < _rows.Count; i++)
                    _rows[i].Refresh();
            }
        }

        // Reused per-instance snapshot buffer; avoids per-refresh allocation.
        private readonly List<Mobile> _pets = new List<Mobile>();
        private static readonly Comparison<Mobile> _byDistance = (a, b) => a.Distance.CompareTo(b.Distance);

        private void RebuildRows()
        {
            // Snapshot followers.
            _pets.Clear();
            var pets = _pets;
            if (World.InGame && World.Player != null)
            {
                int range = World.ClientViewRange;
                foreach (Mobile m in World.Mobiles.Values)
                {
                    if (m == null || m.IsDestroyed) continue;
                    if (m == World.Player) continue;
                    if (!m.IsRenamable) continue;
                    if (m.NotorietyFlag == NotorietyFlag.Invulnerable) continue;
                    if (m.NotorietyFlag == NotorietyFlag.Enemy) continue;
                    if (m.Distance > range) continue;
                    pets.Add(m);
                }
                pets.Sort(_byDistance);
            }

            // Tear down old rows.
            for (int i = _rows.Count - 1; i >= 0; i--)
                _rows[i].DisposeRow();
            _rows.Clear();

            int rowAreaTop = HEADER_H;
            int rowAreaH = Height - HEADER_H - FOOTER_H - 4;
            int maxRows = rowAreaH / ROW_HEIGHT;
            if (maxRows < 1) maxRows = 1;

            int count = Math.Min(pets.Count, maxRows);
            for (int i = 0; i < count; i++)
            {
                var row = new PetRow(this, pets[i], 4, rowAreaTop + i * ROW_HEIGHT, Width - 8, ROW_HEIGHT);
                _rows.Add(row);
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

            int btnY = h - FOOTER_H + 4;
            _btnFollow.Y = btnY;
            _btnGuard.Y = btnY;
            _btnStay.Y = btnY;
            _btnStop.Y = btnY;
            _btnKill.Y = btnY;

            _resizeGrip.X = w - 12;
            _resizeGrip.Y = h - 12;

            GroupMatrixWidth = w;
            GroupMatrixHeight = h;
            RebuildRows();
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer); // base already writes type/x/y/serial/...
            writer.WriteAttributeString("w", Width.ToString());
            writer.WriteAttributeString("h", Height.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml); // X/Y are restored by the outer RestoreGumps loop
            if (int.TryParse(xml.GetAttribute("w"), out int w) && w >= MIN_WIDTH &&
                int.TryParse(xml.GetAttribute("h"), out int h) && h >= MIN_HEIGHT)
                Resize(w, h);
        }

        public override void Dispose()
        {
            _lastX = X;
            _lastY = Y;
            for (int i = 0; i < _rows.Count; i++) _rows[i].DisposeRow();
            _rows.Clear();
            base.Dispose();
        }

        // ───── Row ─────

        private sealed class PetRow
        {
            private readonly Mobile _mob;
            private readonly Label _name;
            private readonly Label _distance;
            private readonly StatBar _hp, _mana, _stam;
            private readonly HitBox _hit;
            private readonly PetStatusPanelGump _owner;
            private int _x, _y, _w;

            public PetRow(PetStatusPanelGump owner, Mobile mob, int x, int y, int width, int height)
            {
                _owner = owner;
                _mob = mob;
                _x = x; _y = y; _w = width;

                ushort nameHue = Notoriety.GetHue(mob.NotorietyFlag);
                _name = new Label(string.IsNullOrEmpty(mob.Name) ? "<unknown pet>" : mob.Name, true, nameHue, font: 1, maxwidth: width - 50)
                {
                    X = x + 4,
                    Y = y + 1
                };
                _distance = new Label("d=" + mob.Distance, true, 0x03B2, font: 1)
                {
                    X = x + width - 50,
                    Y = y + 1
                };

                _hp = new StatBar(x + 4, y + 14, width - 12, 4, 0x0044);
                _mana = new StatBar(x + 4, y + 20, width - 12, 4, 0x005A);
                _stam = new StatBar(x + 4, y + 26, width - 12, 4, 0x0035);

                _hit = new HitBox(x, y, width, height, "Set as last attack target", 0f);
                _hit.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                    {
                        // Single-click selects pet via attack — equivalent to 'all kill <pet>' targeting.
                        // Doesn't kill the pet; just makes it the active LastAttack target.
                        TargetManager.LastAttack = _mob.Serial;
                    }
                };

                owner.Add(_hp);
                owner.Add(_mana);
                owner.Add(_stam);
                owner.Add(_name);
                owner.Add(_distance);
                owner.Add(_hit);

                Refresh();
            }

            public void Refresh()
            {
                if (_mob == null) return;
                _hp.Set(_mob.Hits, _mob.HitsMax);
                _mana.Set(_mob.Mana, _mob.ManaMax);
                _stam.Set(_mob.Stamina, _mob.StaminaMax);
                string d = "d=" + _mob.Distance;
                if (_distance.Text != d) _distance.Text = d;
            }

            public void DisposeRow()
            {
                _name?.Dispose();
                _distance?.Dispose();
                _hp?.Dispose();
                _mana?.Dispose();
                _stam?.Dispose();
                _hit?.Dispose();
            }
        }

        // Tiny progress bar control. Renders a single coloured rect inside a darker frame.
        private sealed class StatBar : Control
        {
            private readonly Texture2D _tex;
            private readonly ushort _hue;
            private int _cur, _max;

            public StatBar(int x, int y, int w, int h, ushort hue)
            {
                X = x; Y = y; Width = w; Height = h;
                _hue = hue;
                _tex = SolidColorTextureCache.GetTexture(Color.White);
                AcceptMouseInput = false;
                CanMove = true;
                WantUpdateSize = false;
            }

            public void Set(int cur, int max)
            {
                _cur = cur;
                _max = max;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                Vector3 hueBg = ShaderHueTranslator.GetHueVector(0x0386, false, 0.4f);
                batcher.Draw(_tex, new Rectangle(x, y, Width, Height), hueBg);

                if (_max > 0)
                {
                    int fill = (int)((float)_cur / _max * Width);
                    if (fill < 0) fill = 0;
                    if (fill > Width) fill = Width;
                    Vector3 hueFg = ShaderHueTranslator.GetHueVector(_hue, false, 1f);
                    batcher.Draw(_tex, new Rectangle(x, y, fill, Height), hueFg);
                }
                return true;
            }
        }
    }
}
