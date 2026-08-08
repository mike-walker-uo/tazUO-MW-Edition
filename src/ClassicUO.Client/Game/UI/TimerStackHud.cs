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

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Top-right stacked list of currently-active TazUO timers
    /// (bandage cycle, global action cooldown). Compact text rows with a
    /// dark backdrop. `-timerhud on|off`.
    /// </summary>
    public static class TimerStackHud
    {
        public static bool Enabled;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;

            // Build short lines for each active timer.
            System.Collections.Generic.List<string> rows = null;

            if (BandageTimerManager.InProgress)
                (rows ??= new System.Collections.Generic.List<string>()).Add("Bandage");

            if (GlobalActionCooldown.IsOnCooldown)
                (rows ??= new System.Collections.Generic.List<string>()).Add(
                    $"Cooldown {GlobalActionCooldown.RemainingMs / 100 / 10f:0.0}s");

            if (rows == null) return;

            int x = bounds.Right - 130;
            int y = bounds.Y + 28;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 170));

            int rowH = 14;
            batcher.Draw(bg, new Rectangle(x - 4, y - 2, 120, rows.Count * rowH + 4), hueNeutral);
            for (int i = 0; i < rows.Count; i++)
            {
                var tb = OverlayTextCache.Get("timerhud." + i, rows[i], 12, Color.White, 115);
                tb.Draw(batcher, x, y + i * rowH);
            }
        }
    }
}
