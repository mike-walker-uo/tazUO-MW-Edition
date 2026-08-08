#region license
// TazUO addition.
#endregion

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Celebrates the shard's two known legendary-creature journal messages.
    /// Exact phrase matching avoids turning ordinary player chat into an alarm.
    /// </summary>
    internal static class LegendaryCreatureAlert
    {
        private const long DURATION_MS = 8000;
        private const long MIN_GAP_MS = 15000;
        private const int BANNER_WIDTH = 560;

        private static bool _hooked;
        private static long _triggeredAt;
        private static long _lastAlertAt;
        private static int _soundStage;

        internal static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMessage;
            _hooked = true;
        }

        internal static void ResetSession()
        {
            _triggeredAt = 0;
            _lastAlertAt = 0;
            _soundStage = 0;
        }

        internal static bool IsLegendarySpawnMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            return text.IndexOf(
                    "you sense a legendary creature has appeared nearby",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
                || text.IndexOf(
                    "everyone senses a legendary creature nearby",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

        private static void OnMessage(object sender, MessageEventArgs e)
        {
            if (!IsLegendarySpawnMessage(e?.Text)) return;

            long now = (long)Time.Ticks;
            if (_lastAlertAt != 0 && now - _lastAlertAt < MIN_GAP_MS) return;

            _lastAlertAt = now;
            _triggeredAt = Math.Max(1, now);
            _soundStage = 0;

            ToastManager.Show(
                "LEGENDARY CREATURE NEARBY! Search the area!",
                0x0035,
                (uint)DURATION_MS
            );
            PlaySound(0x0038);
        }

        internal static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (_triggeredAt == 0 || !World.InGame) return;

            long age = (long)Time.Ticks - _triggeredAt;
            if (age < 0 || age >= DURATION_MS)
            {
                _triggeredAt = 0;
                return;
            }

            PlayFanfare(age);

            Rectangle bounds = Client.Game.Scene.Camera.Bounds;
            float progress = age / (float)DURATION_MS;
            float appear = Math.Min(1f, age / 180f);
            float fade = age < 6500
                ? 1f
                : Math.Max(0f, (DURATION_MS - age) / 1500f);
            float alpha = appear * fade;
            float pulse =
                1f + 0.055f * (float)Math.Sin(age * 0.014f);
            Vector2 center = new Vector2(
                bounds.X + bounds.Width * 0.5f,
                bounds.Y + 150f
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawBanner(batcher, center, alpha, pulse);

            batcher.SetBlendState(BlendState.Additive);
            DrawCrownAndBurst(batcher, center, progress, alpha, pulse);
            DrawFireworks(batcher, bounds, age, alpha);
            DrawConfetti(batcher, bounds, age, alpha);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawBanner(
            UltimaBatcher2D batcher,
            Vector2 center,
            float alpha,
            float pulse)
        {
            int width = (int)(BANNER_WIDTH * pulse);
            int height = (int)(72f * pulse);
            int x = (int)center.X - width / 2;
            int y = (int)center.Y - height / 2;
            Texture2D white = SolidColorTextureCache.GetTexture(Color.White);

            batcher.Draw(
                SolidColorTextureCache.GetTexture(new Color(16, 7, 25)),
                new Rectangle(x, y, width, height),
                ShaderHueTranslator.GetHueVector(0, false, alpha * 0.82f)
            );
            batcher.DrawRectangle(
                white,
                x,
                y,
                width,
                height,
                ShaderHueTranslator.GetHueVector(0x0035, false, alpha)
            );
            batcher.DrawRectangle(
                white,
                x + 3,
                y + 3,
                width - 6,
                height - 6,
                ShaderHueTranslator.GetHueVector(0x048D, false, alpha * 0.75f)
            );

            TextBox title = OverlayTextCache.Get(
                "legendary-creature-alert-title",
                "LEGENDARY CREATURE NEARBY!",
                27f,
                new Color(255, 220, 74)
            );
            title.Alpha = alpha;
            title.Draw(
                batcher,
                (int)center.X - title.Width / 2,
                y + 7
            );

            TextBox subtitle = OverlayTextCache.Get(
                "legendary-creature-alert-subtitle",
                "A legendary pet has spawned — search the area!",
                16f,
                new Color(255, 238, 190)
            );
            subtitle.Alpha = alpha;
            subtitle.Draw(
                batcher,
                (int)center.X - subtitle.Width / 2,
                y + 43
            );
        }

        private static void DrawCrownAndBurst(
            UltimaBatcher2D batcher,
            Vector2 bannerCenter,
            float progress,
            float alpha,
            float pulse)
        {
            Vector2 center = bannerCenter - new Vector2(0f, 58f);
            Color gold = new Color(255, 190, 38);
            Color pale = new Color(255, 250, 193);
            Color violet = new Color(210, 91, 255);
            float rotation = progress * MathHelper.TwoPi * 0.8f;

            for (int i = 0; i < 24; i++)
            {
                float angle = i * MathHelper.TwoPi / 24f + rotation;
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                float inner = 42f * pulse;
                float outer =
                    (67f + (i % 4 == 0 ? 24f : 0f)) * pulse;
                CombatVisualEffect.DrawLine(
                    batcher,
                    center + direction * inner,
                    center + direction * outer,
                    i % 3 == 0 ? pale : i % 2 == 0 ? violet : gold,
                    alpha * (i % 4 == 0 ? 0.9f : 0.5f),
                    i % 4 == 0 ? 3f : 1.5f,
                    0f
                );
            }

            Vector2[] crown =
            {
                new Vector2(-30f, 15f),
                new Vector2(-37f, -16f),
                new Vector2(-13f, 0f),
                new Vector2(0f, -30f),
                new Vector2(13f, 0f),
                new Vector2(37f, -16f),
                new Vector2(30f, 15f)
            };

            for (int i = 0; i < crown.Length - 1; i++)
            {
                Vector2 start = center + crown[i] * pulse;
                Vector2 end = center + crown[i + 1] * pulse;
                CombatVisualEffect.DrawLine(
                    batcher,
                    start,
                    end,
                    new Color(72, 27, 12),
                    alpha,
                    7f,
                    0f
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    start,
                    end,
                    gold,
                    alpha,
                    3.5f,
                    0f
                );
            }

            CombatVisualEffect.DrawLine(
                batcher,
                center + new Vector2(-31f, 15f) * pulse,
                center + new Vector2(31f, 15f) * pulse,
                pale,
                alpha,
                5f,
                0f
            );
        }

        private static void DrawFireworks(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            long age,
            float alpha)
        {
            for (int burstIndex = 0; burstIndex < 4; burstIndex++)
            {
                float cycle = ((age + burstIndex * 530) % 1900) / 1900f;
                if (cycle > 0.48f) continue;

                float burst = cycle / 0.48f;
                float burstAlpha = alpha * (1f - burst);
                Vector2 center = new Vector2(
                    bounds.X + bounds.Width * (0.16f + burstIndex * 0.225f),
                    bounds.Y + bounds.Height * (0.24f + (burstIndex & 1) * 0.13f)
                );
                Color color = burstIndex % 3 == 0
                    ? new Color(255, 199, 45)
                    : burstIndex % 3 == 1
                        ? new Color(220, 89, 255)
                        : new Color(84, 209, 255);

                for (int ray = 0; ray < 14; ray++)
                {
                    float angle =
                        ray * MathHelper.TwoPi / 14f + burstIndex * 0.37f;
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(angle),
                        (float)Math.Sin(angle)
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        center + direction * (8f + burst * 17f),
                        center + direction * (18f + burst * 72f),
                        ray % 4 == 0 ? Color.White : color,
                        burstAlpha,
                        ray % 4 == 0 ? 2.8f : 1.6f,
                        0f
                    );
                }
            }
        }

        private static void DrawConfetti(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            long age,
            float alpha)
        {
            Color[] colors =
            {
                new Color(255, 205, 42),
                new Color(228, 82, 255),
                new Color(74, 218, 255),
                new Color(255, 94, 104),
                new Color(111, 255, 132)
            };
            int travelHeight = Math.Max(1, bounds.Height + 180);

            for (int i = 0; i < 72; i++)
            {
                uint hash = Mix((uint)(i + 1) * 0x9E3779B9u);
                float x =
                    bounds.X
                    + hash % (uint)Math.Max(1, bounds.Width)
                    + (float)Math.Sin(age * 0.004f + i * 1.7f) * 17f;
                float y =
                    bounds.Y
                    - 80f
                    + (age * (0.055f + (i % 5) * 0.009f)
                        + (hash >> 10) % (uint)travelHeight)
                    % travelHeight;
                float size = 2f + (hash >> 22) % 4;
                CombatVisualEffect.DrawPoint(
                    batcher,
                    new Vector2(x, y),
                    colors[i % colors.Length],
                    alpha * (0.58f + (i % 3) * 0.16f),
                    size,
                    0f
                );
            }
        }

        private static void PlayFanfare(long age)
        {
            if (_soundStage == 0 && age >= 260)
            {
                PlaySound(0x0045);
                _soundStage = 1;
            }
            if (_soundStage == 1 && age >= 620)
            {
                PlaySound(0x004C);
                _soundStage = 2;
            }
            if (_soundStage == 2 && age >= 980)
            {
                PlaySound(0x0055);
                _soundStage = 3;
            }
            if (_soundStage == 3 && age >= 1450)
            {
                PlaySound(0x0045);
                _soundStage = 4;
            }
        }

        private static void PlaySound(ushort sound)
        {
            try
            {
                Client.Game?.Audio?.PlaySound(sound);
            }
            catch
            {
                // The visual remains useful if a client sound is unavailable.
            }
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
