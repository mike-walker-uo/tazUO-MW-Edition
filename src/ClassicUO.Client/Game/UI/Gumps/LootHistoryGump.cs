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
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Read-only viewer for LootHistoryManager. Lists recent auto-looted items
    /// with timestamps. Refresh on Update tick. Export-to-CSV button.
    /// </summary>
    internal class LootHistoryGump : Gump
    {
        private const int WIDTH = 380;
        private const int HEIGHT = 360;
        private const int HEADER_H = 26;
        private const int ROW_H = 16;
        private const int MAX_ROWS = 18;

        private static int _lastX = 220, _lastY = 220;

        private readonly AlphaBlendControl _bg;
        private readonly Label _header;
        private readonly NiceButton _clearButton, _exportButton;
        private readonly Label[] _rows = new Label[MAX_ROWS];
        private long _refreshTime;
        private int _lastShownCount = -1;

        public LootHistoryGump() : this(_lastX, _lastY) { }

        public LootHistoryGump(int x, int y) : base(0, 0)
        {
            X = x;
            Y = y;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(_bg = new AlphaBlendControl(0.7f) { Width = WIDTH, Height = HEIGHT });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.7f);
            Add(_header = new Label("Loot History (latest first)", true, 0x0481, font: 1) { X = 6, Y = 4 });
            Add(_clearButton = new NiceButton(WIDTH - 56, 4, 50, 16, ButtonAction.Activate, "Clear")
            {
                ButtonParameter = 1, IsSelectable = false
            });
            Add(_exportButton = new NiceButton(WIDTH - 112, 4, 50, 16, ButtonAction.Activate, "CSV")
            {
                ButtonParameter = 2, IsSelectable = false
            });

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rows[i] = new Label(string.Empty, true, 0x0481, font: 1, maxwidth: WIDTH - 12)
                {
                    X = 6,
                    Y = HEADER_H + i * ROW_H
                };
                Add(_rows[i]);
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1:
                    LootHistoryManager.Clear();
                    _refreshTime = 0;
                    break;
                case 2:
                    try
                    {
                        string folder = string.IsNullOrEmpty(ProfileManager.ProfilePath)
                            ? "."
                            : System.IO.Path.Combine(ProfileManager.ProfilePath, "exports");
                        System.IO.Directory.CreateDirectory(folder);
                        string path = System.IO.Path.Combine(folder, $"loot_{DateTime.Now:yyyyMMdd_HHmm}.csv");
                        LootHistoryManager.ExportCsv(path);
                        GameActions.Print("Loot exported: " + path, 0x35);
                    }
                    catch (Exception ex)
                    {
                        GameActions.Print("Export failed: " + ex.Message, 0x21);
                    }
                    break;
                default:
                    base.OnButtonClick(buttonID);
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 500;

            int count = LootHistoryManager.Count;
            if (count == _lastShownCount) return;
            _lastShownCount = count;

            // Render most-recent first.
            int idx = 0;
            // Snapshot via reverse iteration over Deque.
            var entries = new System.Collections.Generic.List<LootHistoryManager.Entry>();
            foreach (var e in LootHistoryManager.All()) entries.Add(e);
            for (int i = entries.Count - 1; i >= 0 && idx < MAX_ROWS; i--, idx++)
            {
                var e = entries[i];
                string line = $"{e.Time:HH:mm:ss}  {e.ItemName} x{e.Amount}";
                if (!string.IsNullOrEmpty(e.SourceName)) line += $"  ← {e.SourceName}";
                _rows[idx].Text = line;
            }
            for (; idx < MAX_ROWS; idx++)
                if (_rows[idx].Text.Length != 0) _rows[idx].Text = string.Empty;
        }

        public override void Dispose()
        {
            _lastX = X; _lastY = Y;
            base.Dispose();
        }
    }
}
