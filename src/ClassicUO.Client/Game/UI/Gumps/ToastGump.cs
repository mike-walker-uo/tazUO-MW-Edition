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
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Simple non-modal toast notifications. Stack in bottom-right of the
    /// screen, fade out as their lifetime expires. One sticky gump auto-
    /// instantiated on first call to ToastManager.Show.
    /// </summary>
    public static class ToastManager
    {
        public class Toast
        {
            public string Text;
            public ushort Hue;
            public long Created;
            public long ExpireAt;
        }

        private static readonly List<Toast> _toasts = new List<Toast>();

        public static IReadOnlyList<Toast> Toasts => _toasts;

        // Anchor offset for the toast stack. (0, 0) = default position
        // (bottom-right, 12px from edge, 60px from bottom).
        public static int AnchorX = 0;
        public static int AnchorY = 0;
        public static int ToastWidth = 280;
        public static bool AnchorEdit = false;

        // ---- persistence ----
        private static bool _loaded;
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (string line in ProfileDataStore.ReadAllLines("toast.tsv"))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                if (!int.TryParse(bits[1], out int v)) continue;
                switch (bits[0])
                {
                    case "AnchorX":    AnchorX = v; break;
                    case "AnchorY":    AnchorY = v; break;
                    case "ToastWidth": ToastWidth = System.Math.Max(120, System.Math.Min(800, v)); break;
                }
            }
        }

        public static void Save()
        {
            ProfileDataStore.Write("toast.tsv", sw =>
            {
                sw.WriteLine($"AnchorX\t{AnchorX}");
                sw.WriteLine($"AnchorY\t{AnchorY}");
                sw.WriteLine($"ToastWidth\t{ToastWidth}");
            });
        }

        public static void ResetForProfile()
        {
            _loaded = false;
            _toasts.Clear();
            AnchorX = 0;
            AnchorY = 0;
            ToastWidth = 280;
            AnchorEdit = false;
        }

        public static void Show(string text, ushort hue = 0x0481, uint durationMs = 4000)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(text)) return;
            _toasts.Add(new Toast
            {
                Text = text,
                Hue = hue,
                Created = (long)Time.Ticks,
                ExpireAt = (long)Time.Ticks + durationMs
            });
            // Keep at most 10 toasts on screen.
            while (_toasts.Count > 10) _toasts.RemoveAt(0);

            if (UIManager.GetGump<ToastGump>() == null)
                UIManager.Add(new ToastGump());
        }

        internal static void PruneExpired()
        {
            long now = (long)Time.Ticks;
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (_toasts[i].ExpireAt <= now) _toasts.RemoveAt(i);
        }
    }

    internal class ToastGump : Gump
    {
        private const int TOAST_H = 24;
        private const int TOAST_GAP = 4;
        private const long FADE_MS = 600;

        public ToastGump() : base(0, 0)
        {
            X = 0; Y = 0;
            Width = 4096; Height = 4096;
            CanMove = false;
            AcceptMouseInput = false;
            CanCloseWithRightClick = false;
            WantUpdateSize = false;
            LayerOrder = UILayer.Over;
            IsFromServer = false;
        }

        public override bool ShouldBeSaved => false;

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            ToastManager.PruneExpired();
            var toasts = ToastManager.Toasts;
            if (toasts.Count == 0) return true;

            var camera = Client.Game.Scene?.Camera;
            int screenW = camera != null ? camera.Bounds.Right : 800;
            int screenH = camera != null ? camera.Bounds.Bottom : 600;

            Texture2D tex = SolidColorTextureCache.GetTexture(Color.White);

            int idx = 0;
            for (int i = toasts.Count - 1; i >= 0 && idx < 6; i--, idx++)
            {
                var t = toasts[i];
                long remaining = t.ExpireAt - (long)Time.Ticks;
                float alpha = 1f;
                if (remaining < FADE_MS) alpha = remaining / (float)FADE_MS;
                if (alpha < 0) alpha = 0;

                int tx = screenW - ToastManager.ToastWidth - 12 + ToastManager.AnchorX;
                int ty = screenH - 60 - idx * (TOAST_H + TOAST_GAP) + ToastManager.AnchorY;

                // Background.
                Vector3 bgHue = ShaderHueTranslator.GetHueVector(0, false, 0.65f * alpha);
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(20, 20, 20)), new Rectangle(tx, ty, ToastManager.ToastWidth, TOAST_H), bgHue);

                // Border line.
                Vector3 borderHue = ShaderHueTranslator.GetHueVector(t.Hue == 0 ? (ushort)0x0481 : t.Hue, false, alpha);
                batcher.DrawRectangle(tex, tx, ty, ToastManager.ToastWidth, TOAST_H, borderHue);

                // Text label — drawn via a transient TextBox so we get the journal font.
                var tb = TextBox.GetOne(t.Text, ProfileManager.CurrentProfile?.SelectedTTFJournalFont,
                                       14, t.Hue == 0 ? 0x0481 : t.Hue,
                                       new TextBox.RTLOptions { Width = ToastManager.ToastWidth - 12 });
                tb.Alpha = alpha;
                tb.Draw(batcher, tx + 6, ty + 4);
                tb.Dispose();
            }

            return true;
        }
    }
}
