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

using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Pinned tooltip — captures the current OPL of a targeted item and keeps
    /// it onscreen until the user closes it. Useful for comparing items or
    /// keeping a vendor item's stats visible while you browse.
    /// </summary>
    internal class StickyTooltipGump : Gump
    {
        private const int WIDTH = 280;
        private const int MIN_HEIGHT = 80;

        public StickyTooltipGump(uint itemSerial, string name, string data) : base(itemSerial, 0)
        {
            X = Mouse.Position.X + 10;
            Y = Mouse.Position.Y + 10;
            Width = WIDTH;
            Height = MIN_HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            LayerOrder = UILayer.Over;

            var bg = new AlphaBlendControl(0.85f) { Width = Width, Height = Height };
            CustomGumpThemeManager.ApplyDataSurface(bg, 0.85f);
            Add(bg);

            var title = new Label("📌 " + (string.IsNullOrEmpty(name) ? "Item " + itemSerial.ToString("X") : name),
                true, 0x0481, font: 1, maxwidth: WIDTH - 12)
            {
                X = 6,
                Y = 4
            };
            Add(title);

            // Body — render OPL using TextBox so multi-line text wraps.
            int bodyY = 4 + title.Height + 4;
            if (!string.IsNullOrEmpty(data))
            {
                var box = TextBox.GetOne(data,
                    ProfileManager.CurrentProfile?.SelectedTTFJournalFont,
                    14, 0x0481,
                    new TextBox.RTLOptions { Width = WIDTH - 12 });
                box.X = 6;
                box.Y = bodyY;
                Add(box);
                Height = bodyY + box.Height + 6;
            }
            else
            {
                var none = new Label("(no OPL data)", true, 0x03B2, font: 1) { X = 6, Y = bodyY };
                Add(none);
                Height = bodyY + 18;
            }

            bg.Height = Height;
        }

        public static void PinTargeted()
        {
            GameActions.Print("Target an item to pin its tooltip...", 0x35);
            _ = TargetHelper.TargetObject((ent) =>
            {
                if (!(ent is Game.GameObjects.Item item))
                {
                    GameActions.Print("Not an item.", 0x21);
                    return;
                }
                string name, data;
                World.OPL.TryGetNameAndData(item.Serial, out name, out data);
                UIManager.Add(new StickyTooltipGump(item.Serial, name ?? item.Name, data));
            });
        }
    }
}
