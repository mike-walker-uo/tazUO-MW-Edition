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

using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Tiny floater listing friends (from FriendsListManager) that are
    /// currently visible in World.Mobiles. Shows name + distance, sorted by
    /// distance. Refresh 1s. `-friendsfloater` toggles.
    /// </summary>
    internal class FriendsFloaterGump : AnchorableGump
    {
        private const int W = 180;
        private const int HEADER_H = 18;
        private const int ROW_H = 14;
        private const int MAX_ROWS = 8;

        private readonly AlphaBlendControl _bg;
        private readonly Label _header;
        private readonly Label[] _rows = new Label[MAX_ROWS];
        private long _refreshTime;

        public FriendsFloaterGump() : this(480, 60) { }

        public FriendsFloaterGump(int x, int y) : base(0, 0)
        {
            X = x; Y = y;
            Width = W; Height = HEADER_H + ROW_H * MAX_ROWS + 4;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = Width;
            GroupMatrixHeight = Height;

            Add(_bg = new AlphaBlendControl(0.65f) { Width = Width, Height = Height });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.65f);
            Add(_header = new Label("Friends nearby", true, 0x0481, font: 1) { X = 4, Y = 2 });

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rows[i] = new Label(string.Empty, true, 0x0044, font: 1, maxwidth: W - 8)
                {
                    X = 4,
                    Y = HEADER_H + i * ROW_H
                };
                Add(_rows[i]);
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 1000;
            if (World.Player == null) return;

            var seen = new List<(string name, int dist)>();
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed) continue;
                if (m == World.Player) continue;
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (!FriendsListManager.Instance.IsFriend(m.Name)) continue;
                seen.Add((m.Name, m.Distance));
            }
            seen.Sort((a, b) => a.dist.CompareTo(b.dist));

            int shown = System.Math.Min(seen.Count, MAX_ROWS);
            for (int i = 0; i < shown; i++)
            {
                string text = $"{seen[i].name}  (d={seen[i].dist})";
                if (_rows[i].Text != text) _rows[i].Text = text;
            }
            for (int i = shown; i < MAX_ROWS; i++)
                if (_rows[i].Text.Length != 0) _rows[i].Text = string.Empty;
        }
    }
}
