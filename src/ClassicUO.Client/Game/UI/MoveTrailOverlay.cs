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
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI
{
    public enum MovementTrailStyle : byte
    {
        None,
        Blood,
        ShadowSmoke,
        ArcaneSparks,
        Fire,
        Frost,
        Poison,
        Holy,
        Necromantic,
        Lightning,
        FlowerPetals,
        FallingLeaves,
        GlowingRunes,
        Stardust,
        Ethereal,
        Rainbow,
        LavaCracks
    }

    /// <summary>
    /// Bounded, world-anchored movement trail. Open its controls with `-trailfx`.
    /// </summary>
    public static class MoveTrailOverlay
    {
        private struct TrailPoint
        {
            public int X, Y;
            public sbyte Z;
            public int Seed;
            public byte Variant;
            public uint Born;
            public MovementTrailStyle Style;
        }

        private readonly struct TrailArt
        {
            public readonly ushort Graphic;
            public readonly ushort Hue;
            public readonly float Scale;

            public TrailArt(ushort graphic, ushort hue, float scale)
            {
                Graphic = graphic;
                Hue = hue;
                Scale = scale;
            }
        }

        public static bool Enabled = false;
        public static MovementTrailStyle Style { get; set; } = MovementTrailStyle.ShadowSmoke;
        public static int IntensityPercent { get; set; } = 100;
        public static int LifetimeMs { get; set; } = 3000;

        private const int CAPACITY = 32;
        private static readonly TrailPoint[] _points = new TrailPoint[CAPACITY];
        private static int _count;
        private static int _head;
        private static bool _hooked;
        private static int _lastX = int.MinValue, _lastY = int.MinValue;
        private static int _emitPhase;

        private static readonly (ushort graphic, float scale)[] BloodPalette =
        {
            (0x122F, 0.55f), (0x122E, 0.55f), (0x122D, 0.50f),
            (0x122F, 0.42f), (0x122E, 0.45f), (0x122D, 0.40f),
            (0x122C, 0.32f), (0x122B, 0.28f)
        };

        // Standard UO static/effect artwork. Multiple frames/items per style keep
        // a trail varied while remaining compatible with the installed UO art files.
        private static readonly TrailArt[] ShadowPalette =
        {
            new TrailArt(0x372B, 0x0386, 0.52f), new TrailArt(0x372C, 0x0386, 0.50f),
            new TrailArt(0x372D, 0x0386, 0.48f)
        };
        private static readonly TrailArt[] ArcanePalette =
        {
            new TrailArt(0x375D, 0x048D, 0.68f), new TrailArt(0x375E, 0x005A, 0.66f),
            new TrailArt(0x375F, 0x048D, 0.64f), new TrailArt(0x376D, 0x005A, 0.66f),
            new TrailArt(0x376E, 0x048D, 0.64f), new TrailArt(0x376F, 0x005A, 0.62f)
        };
        private static readonly TrailArt[] FirePalette =
        {
            new TrailArt(0x398C, 0, 0.58f), new TrailArt(0x3990, 0, 0.56f),
            new TrailArt(0x3996, 0, 0.58f), new TrailArt(0x399B, 0, 0.55f)
        };
        private static readonly TrailArt[] FrostPalette =
        {
            new TrailArt(0x375D, 0x005A, 0.68f), new TrailArt(0x375E, 0x0481, 0.66f),
            new TrailArt(0x375F, 0x005A, 0.64f), new TrailArt(0x376D, 0x0481, 0.66f),
            new TrailArt(0x376E, 0x005A, 0.64f), new TrailArt(0x376F, 0x0481, 0.62f)
        };
        private static readonly TrailArt[] PoisonPalette =
        {
            new TrailArt(0x3914, 0, 0.58f), new TrailArt(0x3919, 0, 0.56f),
            new TrailArt(0x3920, 0, 0.58f), new TrailArt(0x3927, 0, 0.55f)
        };
        private static readonly TrailArt[] HolyPalette =
        {
            new TrailArt(0x375D, 0x0035, 0.70f), new TrailArt(0x375E, 0x0481, 0.68f),
            new TrailArt(0x375F, 0x0035, 0.66f), new TrailArt(0x376D, 0x0481, 0.68f),
            new TrailArt(0x376E, 0x0035, 0.66f), new TrailArt(0x376F, 0x0481, 0.64f)
        };
        private static readonly TrailArt[] NecromanticPalette =
        {
            new TrailArt(0x372B, 0x048D, 0.52f), new TrailArt(0x372C, 0x03B2, 0.50f),
            new TrailArt(0x372D, 0x048D, 0.48f)
        };
        private static readonly TrailArt[] LightningPalette =
        {
            new TrailArt(0x381B, 0x005A, 0.82f), new TrailArt(0x381C, 0x0481, 0.78f),
            new TrailArt(0x381D, 0x005A, 0.80f), new TrailArt(0x381E, 0x0481, 0.76f)
        };
        private static readonly TrailArt[] FlowerPalette =
        {
            new TrailArt(0x0C83, 0, 0.42f), new TrailArt(0x0C84, 0, 0.40f),
            new TrailArt(0x0C85, 0, 0.42f), new TrailArt(0x0C86, 0, 0.38f)
        };
        private static readonly TrailArt[] LeafPalette =
        {
            new TrailArt(0x0C83, 0x0035, 0.26f), new TrailArt(0x0C84, 0x0021, 0.25f),
            new TrailArt(0x0C85, 0x0044, 0.26f), new TrailArt(0x0C86, 0x0035, 0.24f)
        };
        private static readonly TrailArt[] RunePalette =
        {
            new TrailArt(0x1F14, 0x005A, 1.05f), new TrailArt(0x1F15, 0x048D, 1.05f),
            new TrailArt(0x1F16, 0x0035, 1.00f), new TrailArt(0x1F17, 0x005A, 1.00f)
        };
        private static readonly TrailArt[] StardustPalette =
        {
            new TrailArt(0x375D, 0x0481, 0.58f), new TrailArt(0x375E, 0x005A, 0.56f),
            new TrailArt(0x376D, 0x0035, 0.58f), new TrailArt(0x376E, 0x048D, 0.56f)
        };
        private static readonly TrailArt[] EtherealPalette =
        {
            new TrailArt(0x37C5, 0x005A, 0.64f), new TrailArt(0x37C6, 0x0481, 0.62f),
            new TrailArt(0x37C7, 0x005A, 0.60f)
        };
        private static readonly TrailArt[] RainbowPalette =
        {
            new TrailArt(0x375E, 0x0021, 0.64f), new TrailArt(0x375E, 0x0035, 0.64f),
            new TrailArt(0x375E, 0x0044, 0.64f), new TrailArt(0x375E, 0x005A, 0.64f),
            new TrailArt(0x375E, 0x048D, 0.64f)
        };
        private static readonly TrailArt[] LavaPalette =
        {
            new TrailArt(0x398C, 0x0021, 0.60f), new TrailArt(0x3991, 0x0021, 0.58f),
            new TrailArt(0x3996, 0x0035, 0.60f), new TrailArt(0x399C, 0x0021, 0.57f)
        };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            ClassicUO.Game.Managers.EventSink.OnPositionChanged += (sender, args) => Push();
            _hooked = true;
        }

        private static void Push()
        {
            if (!Enabled || Style == MovementTrailStyle.None || CUOEnviroment.SafeGraphicsMode
                || World.Player == null) return;

            int x = World.Player.X, y = World.Player.Y;
            if (x == _lastX && y == _lastY) return;
            _lastX = x;
            _lastY = y;

            int intensity = Math.Max(25, Math.Min(200, IntensityPercent));
            _emitPhase = (_emitPhase + 37) % 100;
            if (intensity < 100 && _emitPhase >= intensity) return;

            ref TrailPoint point = ref _points[_head];
            point.X = x;
            point.Y = y;
            point.Z = World.Player.Z;
            point.Born = Time.Ticks;
            point.Style = Style;
            point.Seed = unchecked(x * 397 ^ y * 7919 ^ (int)Time.Ticks);
            TrailArt[] palette = GetPalette(Style);
            point.Variant = (byte)(palette == null ? 0 : Positive(point.Seed) % palette.Length);
            _head = (_head + 1) % CAPACITY;
            if (_count < CAPACITY) _count++;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled || _count == 0 || CUOEnviroment.SafeGraphicsMode) return;

            uint now = Time.Ticks;
            int lifetime = Math.Max(750, Math.Min(8000, LifetimeMs));
            float intensity = Math.Max(25, Math.Min(200, IntensityPercent)) / 100f;

            for (int i = 0; i < _count; i++)
            {
                int idx = ((_head - _count + i) % CAPACITY + CAPACITY) % CAPACITY;
                ref TrailPoint point = ref _points[idx];
                uint age = now - point.Born;
                if (age >= lifetime) continue;

                float t = age / (float)lifetime;
                float alpha = Math.Min(1f, 0.82f * intensity) * (1f - t);
                Point screen = PathPreview.TileToScreen(point.X, point.Y, point.Z);
                if (point.Style == MovementTrailStyle.Blood)
                {
                    DrawBlood(batcher, screen, point.Seed, alpha);
                    continue;
                }

                int rise = 0;
                if (point.Style == MovementTrailStyle.ShadowSmoke
                    || point.Style == MovementTrailStyle.Fire
                    || point.Style == MovementTrailStyle.Poison
                    || point.Style == MovementTrailStyle.Necromantic)
                {
                    rise = (int)(t * 10f);
                }

                int jitterX = ((point.Seed >> 5) & 3) - 1;
                int jitterY = ((point.Seed >> 9) & 3) - 1;
                DrawUoArt(batcher, screen, point, alpha, intensity, rise, jitterX, jitterY);
            }
        }

        private static void DrawBlood(UltimaBatcher2D batcher, Point screen, int seed, float alpha)
        {
            int paletteIndex = ((seed >> 3) & 0x7fffffff) % BloodPalette.Length;
            var entry = BloodPalette[paletteIndex];
            ref readonly var art = ref Client.Game.Arts.GetArt(entry.graphic);
            if (art.Texture == null) return;
            float scale = entry.scale * (1f + ((((seed >> 11) & 31) - 15) / 150f));
            int width = Math.Max(4, (int)(art.UV.Width * scale));
            int height = Math.Max(3, (int)(art.UV.Height * scale));
            batcher.Draw(
                art.Texture,
                new Rectangle(screen.X - width / 2, screen.Y - height / 2, width, height),
                art.UV,
                ShaderHueTranslator.GetHueVector(0, false, alpha));
        }

        private static void DrawUoArt(
            UltimaBatcher2D batcher,
            Point screen,
            TrailPoint point,
            float alpha,
            float intensity,
            int rise,
            int jitterX,
            int jitterY)
        {
            TrailArt[] palette = GetPalette(point.Style);
            if (palette == null || palette.Length == 0) return;

            int start = point.Variant % palette.Length;
            for (int i = 0; i < palette.Length; i++)
            {
                TrailArt entry = palette[(start + i) % palette.Length];
                ref readonly var art = ref Client.Game.Arts.GetArt(entry.Graphic);
                if (art.Texture == null) continue;

                float scale = entry.Scale * (0.85f + intensity * 0.15f);
                scale *= 1f + (((point.Seed >> 11) & 15) - 7) / 100f;
                int width = Math.Max(4, (int)(art.UV.Width * scale));
                int height = Math.Max(4, (int)(art.UV.Height * scale));
                batcher.Draw(
                    art.Texture,
                    new Rectangle(
                        screen.X - width / 2 + jitterX,
                        screen.Y - height / 2 - rise + jitterY,
                        width,
                        height),
                    art.UV,
                    ShaderHueTranslator.GetHueVector(entry.Hue, false, alpha));
                return;
            }
        }

        private static TrailArt[] GetPalette(MovementTrailStyle style)
        {
            switch (style)
            {
                case MovementTrailStyle.ShadowSmoke: return ShadowPalette;
                case MovementTrailStyle.ArcaneSparks: return ArcanePalette;
                case MovementTrailStyle.Fire: return FirePalette;
                case MovementTrailStyle.Frost: return FrostPalette;
                case MovementTrailStyle.Poison: return PoisonPalette;
                case MovementTrailStyle.Holy: return HolyPalette;
                case MovementTrailStyle.Necromantic: return NecromanticPalette;
                case MovementTrailStyle.Lightning: return LightningPalette;
                case MovementTrailStyle.FlowerPetals: return FlowerPalette;
                case MovementTrailStyle.FallingLeaves: return LeafPalette;
                case MovementTrailStyle.GlowingRunes: return RunePalette;
                case MovementTrailStyle.Stardust: return StardustPalette;
                case MovementTrailStyle.Ethereal: return EtherealPalette;
                case MovementTrailStyle.Rainbow: return RainbowPalette;
                case MovementTrailStyle.LavaCracks: return LavaPalette;
                default: return null;
            }
        }

        private static int Positive(int value)
        {
            return value & 0x7FFFFFFF;
        }

        public static string GetStyleName(MovementTrailStyle style)
        {
            switch (style)
            {
                case MovementTrailStyle.ShadowSmoke: return "Shadow smoke";
                case MovementTrailStyle.ArcaneSparks: return "Arcane sparks";
                case MovementTrailStyle.FlowerPetals: return "Flower petals";
                case MovementTrailStyle.FallingLeaves: return "Falling leaves";
                case MovementTrailStyle.GlowingRunes: return "Glowing runes";
                case MovementTrailStyle.LavaCracks: return "Lava cracks";
                default: return style.ToString();
            }
        }

        public static void SetEnabled(bool enabled)
        {
            EnsureHooked();
            Enabled = enabled;
            if (!enabled) ResetSession();
            GameActions.Print($"Movement trail {(enabled ? "ON" : "OFF")}.",
                (ushort)(enabled ? 0x35 : 0x21));
        }

        public static void SetStyle(MovementTrailStyle style)
        {
            Style = style;
            ResetSession();
        }

        public static void ResetSession()
        {
            _count = 0;
            _head = 0;
            _lastX = _lastY = int.MinValue;
            _emitPhase = 0;
        }
    }
}
