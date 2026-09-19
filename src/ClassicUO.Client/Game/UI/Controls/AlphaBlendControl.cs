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
using ClassicUO.Renderer;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls
{
    public enum AlphaBlendMaterialStyle : byte
    {
        None,
        Parchment,
        Wood,
        Stone,
        Metal,
        Fabric,
        Water
    }

    public sealed class AlphaBlendControl : Control
    {
        private Vector3 hueVector;
        private ushort hue;

        public AlphaBlendControl(float alpha = 0.5f)
        {
            Alpha = alpha;
            AcceptMouseInput = false;
            hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha);
        }

        public ushort Hue
        {
            get => hue; set
            {
                hue = value;
                hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha);
            }
        }

        public Color BaseColor { get; set; } = Color.Black;

        internal bool ArtPanel { get; set; }

        public ushort MaterialGraphic { get; set; }

        public ushort MaterialHue { get; set; }

        public float MaterialAlpha { get; set; }

        public AlphaBlendMaterialStyle MaterialStyle { get; set; }

        public Color MaterialAccent { get; set; } = Color.Gray;

        public override void AlphaChanged(float oldValue, float newValue)
        {
            base.AlphaChanged(oldValue, newValue);
            hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (ArtPanel && CustomGumpThemeManager.IsArtTheme(CustomGumpThemeManager.Current))
            {
                CustomThemeArt.DrawPanel(batcher, x, y, Width, Height, Alpha);
                return true;
            }

            //Vector3 hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha);

            batcher.Draw
            (
                SolidColorTextureCache.GetTexture(BaseColor),
                new Rectangle
                (
                    x,
                    y,
                    Width,
                    Height
                ),
                hueVector
            );

            if (MaterialGraphic != 0 && MaterialAlpha > 0f)
            {
                ref readonly var gumpInfo = ref Client.Game.Gumps.GetGump(MaterialGraphic);

                if (gumpInfo.Texture != null)
                {
                    Vector3 materialHue = ShaderHueTranslator.GetHueVector(
                        MaterialHue,
                        false,
                        MathHelper.Clamp(Alpha * MaterialAlpha, 0f, 1f),
                        true
                    );
                    batcher.DrawTiled(
                        gumpInfo.Texture,
                        new Rectangle(x, y, Width, Height),
                        gumpInfo.UV,
                        materialHue
                    );
                }
            }

            DrawMaterialPattern(batcher, x, y);

            return true;
        }

        private void DrawMaterialPattern(UltimaBatcher2D batcher, int x, int y)
        {
            if (MaterialStyle == AlphaBlendMaterialStyle.None || MaterialAlpha <= 0f || Width < 8 || Height < 8)
            {
                return;
            }

            float strength = MathHelper.Clamp(Alpha * MaterialAlpha, 0f, 1f);

            switch (MaterialStyle)
            {
                case AlphaBlendMaterialStyle.Wood:
                    for (int row = 0, py = 24; py < Height; row++, py += 28)
                    {
                        if ((row & 1) != 0)
                        {
                            DrawMaterialRect(batcher, x, y + py - 24, Width, 24, Color.Black, strength * 0.09f);
                        }

                        DrawMaterialRect(batcher, x, y + py, Width, 1, MaterialAccent, strength * 0.42f);
                        DrawMaterialRect(batcher, x, y + py + 1, Width, 1, Color.Black, strength * 0.28f);
                        int jointOffset = row % 2 == 0 ? 42 : 86;
                        for (int px = jointOffset; px < Width; px += 96)
                        {
                            DrawMaterialRect(batcher, x + px, y + py - 27, 1, 27, Color.Black, strength * 0.24f);
                        }

                        int grainOffset = 14 + row * 37 % 74;
                        int grainY = y + py - 18 + row % 3 * 4;
                        for (int px = grainOffset; px < Width; px += 116)
                        {
                            DrawMaterialRect(batcher, x + px, grainY, Math.Min(26, Width - px), 1, MaterialAccent, strength * 0.18f);
                        }
                    }
                    break;
                case AlphaBlendMaterialStyle.Stone:
                    for (int row = 0, py = 30; py < Height; row++, py += 31)
                    {
                        DrawMaterialRect(batcher, x, y + py, Width, 1, Color.Black, strength * 0.32f);
                        int jointOffset = row % 2 == 0 ? 35 : 72;
                        for (int px = jointOffset; px < Width; px += 74)
                        {
                            DrawMaterialRect(batcher, x + px, y + py - 30, 1, 30, MaterialAccent, strength * 0.24f);
                        }
                    }
                    break;
                case AlphaBlendMaterialStyle.Metal:
                    for (int py = 42; py < Height; py += 43)
                    {
                        DrawMaterialRect(batcher, x, y + py, Width, 1, MaterialAccent, strength * 0.30f);
                        for (int px = 18; px < Width; px += 64)
                        {
                            DrawMaterialRect(batcher, x + px, y + py - 2, 2, 2, MaterialAccent, strength * 0.58f);
                        }
                    }
                    break;
                case AlphaBlendMaterialStyle.Fabric:
                    for (int py = 10; py < Height; py += 12)
                    {
                        DrawMaterialRect(batcher, x, y + py, Width, 1, MaterialAccent, strength * 0.12f);
                    }
                    for (int px = 10; px < Width; px += 12)
                    {
                        DrawMaterialRect(batcher, x + px, y, 1, Height, Color.Black, strength * 0.10f);
                    }
                    break;
                case AlphaBlendMaterialStyle.Water:
                    for (int row = 0, py = 16; py < Height; row++, py += 20)
                    {
                        int start = row % 2 == 0 ? 4 : 24;
                        for (int px = start; px < Width; px += 52)
                        {
                            DrawMaterialRect(batcher, x + px, y + py, Math.Min(24, Width - px), 1, MaterialAccent, strength * 0.30f);
                        }
                    }
                    break;
                case AlphaBlendMaterialStyle.Parchment:
                    for (int py = 13; py < Height; py += 19)
                    {
                        DrawMaterialRect(batcher, x, y + py, Width, 1, MaterialAccent, strength * 0.10f);
                    }
                    break;
            }
        }

        private static void DrawMaterialRect(
            UltimaBatcher2D batcher,
            int x,
            int y,
            int width,
            int height,
            Color color,
            float alpha)
        {
            if (width <= 0 || height <= 0 || alpha <= 0f)
            {
                return;
            }

            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(x, y, width, height),
                ShaderHueTranslator.GetHueVector(0, false, MathHelper.Clamp(alpha, 0f, 1f))
            );
        }
    }
}
