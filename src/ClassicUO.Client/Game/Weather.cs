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
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MathHelper = Microsoft.Xna.Framework.MathHelper;

namespace ClassicUO.Game
{
    public enum WeatherType
    {
        WT_RAIN = 0,
        WT_STORM_APPROACH,
        WT_SNOW,
        WT_STORM_BREWING,

        WT_INVALID_0 = 0xFE,
        WT_INVALID_1 = 0xFF
    }

    internal enum WeatherSource
    {
        None,
        Server,
        Ambient,
        Manual
    }

    internal class Weather
    {
        // TazUO: raised particle cap (upstream 70) — rain/storm fill a modern
        // viewport poorly at 70. Server count is multiplied per type below.
        private const int MAX_WEATHER_EFFECT = 400;
        private const float SIMULATION_TIME = 37.0f;

        private readonly WeatherEffect[] _effects = new WeatherEffect[MAX_WEATHER_EFFECT];
        private readonly GroundDecalManager _groundDecals;
        private uint _timer, _windTimer, _lastTick;

        // TazUO: splash + lightning state.
        private const int MAX_SPLASHES = 24;
        private const uint SPLASH_LIFE_MS = 300;
        private readonly Splash[] _splashes = new Splash[MAX_SPLASHES];
        private int _splashIdx;
        private uint _nextSplash;
        private uint _lightningAt;
        // Independent strike scheduler — storms flash on their own cadence,
        // not just when the wind shifts.
        private uint _nextStrike;
        // Peak alpha of the current flash — full storms flash brighter than
        // brewing storms; distant strikes dimmer than overhead ones.
        private float _lightningPeak = 0.55f;
        // Thunder arrives after the flash, delayed by strike distance.
        private uint _thunderAt;
        private uint _thunderEchoAt;
        private int _thunderEchoSound;
        // Visible bolt polyline (main + optional fork), built per strike.
        private readonly Point[] _boltPts = new Point[10];
        private int _boltCount;
        private readonly Point[] _forkPts = new Point[4];
        private int _forkCount;
        // Rainbow after rain: one roll per rain, shown ~30s.
        private uint _rainbowUntil;
        private bool _rainbowRolled;
        // Smoothed ambient light attenuation (see ApplyLight).
        private float _lightAtten;
        private uint _lastLightTick;
        // Environment memory. These deliberately survive Reset/Generate so
        // rain leaves a wet world and weather changes blend instead of pop.
        private float _wetness;
        private float _coldness;
        private float _visualIntensity;
        private float _snowCover;
        private bool _frontFromLeft;
        private float _frontCoverage;


        // TazUO: blizzard mode — layered on WT_SNOW. Fast diagonal flakes,
        // white-out haze, heavier accumulation, stronger light attenuation.
        public bool Blizzard { get; set; }
        // TazUO: heavy-snowfall mode — layered on WT_SNOW. Calm but massive:
        // big dense flakes, faster fall, thicker ground cover, soft haze.
        public bool HeavySnow { get; set; }
        // TazUO: tempest mode — layered on WT_STORM_APPROACH. Torrential
        // gusty rain, near-constant lightning, deepest darkening.
        public bool Tempest { get; set; }
        // TazUO: fog — standalone ambient condition (no falling particles).
        // Drifting mist banks + grey wash + dimmed light.
        public bool Fog { get; private set; }
        // TazUO: hail — layered on WT_SNOW. Fast pellets that bounce once.
        public bool Hail { get; set; }
        // TazUO: sleet — layered on WT_RAIN. Mixed rain streaks + wet flakes.
        public bool Sleet { get; set; }

        public WeatherType? CurrentWeather { get; private set; }
        public WeatherType Type { get; private set; }
        public WeatherSource Source { get; private set; }
        // True while the current weather is still running (CurrentWeather
        // stays set after the 6-min timer expires; this doesn't).
        public bool IsActive => CurrentWeather.HasValue && _timer >= Time.Ticks;
        public byte Count { get; private set; }
        // ushort: with the raised cap + density multipliers this can exceed 255.
        public ushort CurrentCount { get; private set; }
        public byte Temperature{ get; private set; }
        public sbyte Wind { get; private set; }
        public float Wetness => _wetness;
        public float Coldness => _coldness;
        public float VisualIntensity => _visualIntensity;
        public float SnowCover => _snowCover;
        public float LightningStrength => Math.Max(0f, CurrentFlashStrength());

        public Weather(GroundDecalManager groundDecals)
        {
            _groundDecals = groundDecals ?? throw new ArgumentNullException(nameof(groundDecals));
        }

        private static float SinOscillate(float freq, int range, uint current_tick)
        {
            float anglef = (int) (current_tick / 2.7777f * freq) % 360;

            return (float)Math.Sin(MathHelper.ToRadians(anglef)) * range;
        }


        public void Reset()
        {
            Type = 0;
            Count = Temperature = 0;
            CurrentCount = 0;
            Wind = 0;
            _windTimer = _timer = 0;
            CurrentWeather = null;
            Source = WeatherSource.None;
            Blizzard = false;
            HeavySnow = false;
            Tempest = false;
            Fog = false;
            Hail = false;
            Sleet = false;
            _lightningAt = 0;
            _nextStrike = 0;
            _thunderAt = 0;
            _thunderEchoAt = 0;
            _thunderEchoSound = 0;
            _boltCount = 0;
            _forkCount = 0;
            _rainbowUntil = 0;
            _rainbowRolled = false;
            _nextSplash = 0;
            _frontCoverage = 0f;
            for (int i = 0; i < MAX_SPLASHES; i++) _splashes[i].Born = 0;
        }

        public void SetFog(WeatherSource source)
        {
            Reset();
            Fog = true;
            Source = source;
            _frontFromLeft = RandomHelper.RandomBool();
        }

        /// <summary>
        /// Flash strength 0..peak for the currently running lightning strike
        /// (bright pop → gap → fading afterflash). Pure read — expiry is
        /// handled in Draw.
        /// </summary>
        private float CurrentFlashStrength()
        {
            if (_lightningAt == 0 || Time.Ticks < _lightningAt) return 0f;
            uint age = Time.Ticks - _lightningAt;
            if (age < 70) return _lightningPeak;
            if (age < 130) return _lightningPeak * 0.18f;
            if (age < 230) return _lightningPeak * 0.64f * (1f - (age - 130) / 100f);
            return -1f; // expired
        }

        /// <summary>
        /// True while weather needs to influence ambient light. Forces the
        /// light render pass on even in full daylight (GameScene.UseLights is
        /// normally false then, which would skip ApplyLight entirely).
        /// </summary>
        public bool WantsAmbientLight =>
            (CurrentWeather.HasValue && _timer >= Time.Ticks)
            || Fog
            || Math.Abs(_lightAtten) > 0.005f
            || _lightningAt != 0;

        /// <summary>
        /// Weather-driven ambient light: storms darken the world, snow
        /// brightens it slightly, and lightning momentarily lights everything
        /// up. Called from GameScene.DrawLights with the base light level
        /// (0 = black, 1 = full bright); returns the adjusted level.
        /// </summary>
        private float GetLightAttenuationTarget()
        {
            float target = 0f;
            if (CurrentWeather.HasValue && _timer >= Time.Ticks)
            {
                switch (Type)
                {
                    case WeatherType.WT_RAIN:           target = 0.08f;  break;
                    case WeatherType.WT_STORM_BREWING:  target = 0.14f;  break;
                    case WeatherType.WT_STORM_APPROACH: target = Tempest ? 0.30f : 0.22f; break;
                    case WeatherType.WT_SNOW:
                        target = Blizzard ? 0.12f : HeavySnow ? 0.05f : Hail ? 0.06f : -0.05f;
                        break;
                }
            }

            if (Fog && target < 0.10f)
            {
                target = 0.10f;
            }

            return target * _visualIntensity;
        }

        public void Update()
        {
            uint now = Time.Ticks;
            uint elapsed = _lastLightTick == 0 ? 16u : now - _lastLightTick;
            _lastLightTick = now;
            if (elapsed > 250)
            {
                elapsed = 250;
            }

            bool active = CurrentWeather.HasValue && _timer >= now;
            bool rain = active && (Type == WeatherType.WT_RAIN
                || Type == WeatherType.WT_STORM_APPROACH
                || Type == WeatherType.WT_STORM_BREWING);
            bool cold = active && (Type == WeatherType.WT_SNOW || Sleet || Hail);

            float intensityTarget = active || Fog ? 1f : 0f;
            float intensityTau = intensityTarget > _visualIntensity ? 2800f : 9000f;
            float intensityBlend = 1f - (float)Math.Exp(-elapsed / intensityTau);
            _visualIntensity += (intensityTarget - _visualIntensity) * intensityBlend;

            float frontBlend = 1f - (float)Math.Exp(-elapsed / (intensityTarget > _frontCoverage ? 8500f : 4500f));
            _frontCoverage += (intensityTarget - _frontCoverage) * frontBlend;

            // Rain soaks in quickly, then leaves a two-minute visual memory.
            float wetTarget = rain ? 1f : 0f;
            float wetTau = rain ? 6000f : 90000f;
            _wetness += (wetTarget - _wetness) * (1f - (float)Math.Exp(-elapsed / wetTau));

            // Cold air and frost trail snow more briefly than wet ground trails rain.
            float coldTarget = cold ? 1f : 0f;
            float coldTau = cold ? 5000f : 45000f;
            _coldness += (coldTarget - _coldness) * (1f - (float)Math.Exp(-elapsed / coldTau));

            // Snow accumulates slowly, survives the shower and melts faster
            // around exposed heat sources through the decal system.
            bool snow = active && Type == WeatherType.WT_SNOW && !Hail && !Sleet;
            float snowTarget = snow ? 1f : 0f;
            float snowTau = snow ? (HeavySnow || Blizzard ? 9000f : 18000f) : 120000f;
            _snowCover += (snowTarget - _snowCover) * (1f - (float)Math.Exp(-elapsed / snowTau));

            // About 90% settled after 1.5s, independent of frame rate.
            float blend = 1f - (float)Math.Exp(-elapsed / 650f);
            _lightAtten += (GetLightAttenuationTarget() - _lightAtten) * blend;
        }

        public float ApplyLight(float lightLevel)
        {
            float v = lightLevel - _lightAtten;

            float flash = CurrentFlashStrength();
            if (flash > 0f)
            {
                v += flash * 0.8f; // lightning lights up the whole scene
            }

            return MathHelper.Clamp(v, 0.05f, 1f);
        }

        public void Generate(WeatherType type, byte count, byte temp, WeatherSource source)
        {
            bool announce = count > 0
                && (!IsActive || CurrentWeather != type || Source != source);

            Reset();

            _frontFromLeft = RandomHelper.RandomBool();

            Type = type;
            Count = (byte) Math.Min(MAX_WEATHER_EFFECT, (int) count);
            Temperature = temp;
            _timer = Time.Ticks + Constants.WEATHER_TIMER;

            _lastTick = Time.Ticks;

            if (Type == WeatherType.WT_INVALID_0 || Type == WeatherType.WT_INVALID_1)
            {
                _timer = 0;
                CurrentWeather = null;

                return;
            }

            if (Count > 0)
            {
                CurrentWeather = type;
                Source = source;
            }

            switch (type)
            {
                case WeatherType.WT_RAIN:
                    if (announce)
                    {
                        GameActions.Print
                        (
                            ResGeneral.ItBeginsToRain,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );
                    }

                    break;

                case WeatherType.WT_STORM_APPROACH:
                    if (announce)
                    {
                        GameActions.Print
                        (
                            ResGeneral.AFierceStormApproaches,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );
                        PlayWind();
                    }

                    break;

                case WeatherType.WT_SNOW:
                    if (announce)
                    {
                        GameActions.Print
                        (
                            ResGeneral.ItBeginsToSnow,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );
                        PlayWind();
                    }

                    break;

                case WeatherType.WT_STORM_BREWING:
                    if (announce)
                    {
                        GameActions.Print
                        (
                            ResGeneral.AStormIsBrewing,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );
                        PlayWind();
                    }

                    break;
            }


            _windTimer = 0;

            // TazUO: denser particles than the server's count — rain doubles,
            // storms triple. Capped by MAX_WEATHER_EFFECT.
            int mult = type == WeatherType.WT_STORM_APPROACH || type == WeatherType.WT_STORM_BREWING ? 3 : 2;
            int target = SceneryInteractionManager.ScaleCount(Math.Min(MAX_WEATHER_EFFECT, Count * mult));

            while (CurrentCount < target)
            {
                ref WeatherEffect effect = ref _effects[CurrentCount++];
                effect.X = RandomHelper.GetValue(0, Client.Game.Scene.Camera.Bounds.Width);
                effect.Y = RandomHelper.GetValue(0, Client.Game.Scene.Camera.Bounds.Height);
                // Depth/variation seeds: ScaleRatio drives speed + streak
                // length + alpha ("near" drops are faster/longer/brighter).
                effect.ScaleRatio = RandomHelper.GetValue(0, 100) / 100f;
                effect.ID = (uint)RandomHelper.GetValue(0, 100_000);
                effect.GroundY = RandomHelper.GetValue(
                    Client.Game.Scene.Camera.Bounds.Height * 35 / 100,
                    Client.Game.Scene.Camera.Bounds.Height * 95 / 100);
            }
        }

        private void PlayWind()
        {
            PlaySound(RandomHelper.RandomList(0x014, 0x015, 0x016));
        }

        private void PlayThunder()
        {
            int sound = RandomHelper.RandomList(0x028, 0x206);
            PlaySound(sound);
            if (SceneryInteractionManager.AcousticEcho > 0.05f)
            {
                _thunderEchoSound = sound;
                _thunderEchoAt = Time.Ticks + (uint)RandomHelper.GetValue(380, 720);
            }
        }

        // Lightning strike: flash + visible bolt now, thunder later (sound
        // lags light by strike distance). Close strikes shake the camera.
        private void TriggerLightning()
        {
            float basePeak =
                Tempest ? 0.65f :
                Type == WeatherType.WT_STORM_BREWING ? 0.30f : 0.55f;

            // Distance 0 = overhead, 1 = far horizon.
            float dist = RandomHelper.GetValue(0, 100) / 100f;
            _lightningPeak = basePeak * (1f - 0.5f * dist);
            _lightningAt = Time.Ticks + (uint)RandomHelper.GetValue(0, 150);
            _thunderAt = _lightningAt + 150 + (uint)(dist * 2200);

            BuildBolt();

            // Only near strikes rattle the screen.
            if (dist < 0.35f && basePeak >= 0.55f
                && !(ProfileManager.CurrentProfile?.ReduceWeatherMotion ?? false))
            {
                try
                {
                    Client.Game.Scene.Camera.Shake(7f * _lightningPeak, 240);
                }
                catch { }
            }
        }

        // Jagged main bolt from the sky to a ground point, with an optional
        // fork branching from its midpoint.
        private void BuildBolt()
        {
            var bounds = Client.Game.Scene.Camera.Bounds;
            int w = bounds.Width;
            int h = bounds.Height;

            int startX = RandomHelper.GetValue(w * 15 / 100, w * 85 / 100);
            int endY = RandomHelper.GetValue(h * 55 / 100, h * 85 / 100);

            _boltCount = RandomHelper.GetValue(6, 9);
            int xx = startX;
            for (int i = 0; i < _boltCount; i++)
            {
                float t = i / (float)(_boltCount - 1);
                int yy = (int)(endY * t);
                if (i > 0 && i < _boltCount - 1)
                {
                    xx += RandomHelper.GetValue(-34, 34);
                }
                _boltPts[i] = new Point(xx, yy);
            }

            _forkCount = 0;
            if (RandomHelper.GetValue(0, 99) < 45)
            {
                int mid = _boltCount / 2;
                int fx = _boltPts[mid].X;
                int fy = _boltPts[mid].Y;
                int dir = RandomHelper.RandomBool() ? 1 : -1;
                _forkCount = _forkPts.Length;
                for (int i = 0; i < _forkCount; i++)
                {
                    fx += dir * RandomHelper.GetValue(12, 30);
                    fy += RandomHelper.GetValue(18, 40);
                    _forkPts[i] = new Point(fx, fy);
                }
            }
        }

        private void PlaySound(int sound, float scale = 1f)
        {
            // randomize the sound of the weather around the player
            int randX = RandomHelper.GetValue(10, 18);
            if (RandomHelper.RandomBool())
            {
                randX *= -1;
            }

            int randY = RandomHelper.GetValue(10, 18);
            if (RandomHelper.RandomBool())
            {
                randY *= -1;
            }

            Client.Game.Audio.PlaySoundWithDistance(
                sound,
                World.Player.X + randX,
                World.Player.Y + randY,
                SceneryInteractionManager.WeatherSoundScale * scale
            );
        }

        private bool IsInsideWeatherFront(float screenX, int width, uint seed)
        {
            if (_frontCoverage >= 0.995f) return true;
            float feather = Math.Max(48f, width * 0.12f);
            float jitter = ((seed * 1103515245u + 12345u) % 1000 / 999f - 0.5f) * feather;
            float covered = width * _frontCoverage;
            return _frontFromLeft
                ? screenX <= covered + jitter
                : screenX >= width - covered + jitter;
        }

        // Far particles and mist render before world objects. Near particles
        // render in Draw after objects, so weather has actual scene depth.
        public void DrawBackground(UltimaBatcher2D batcher, int x, int y)
        {
            Point size = new Point(Client.Game.Scene.Camera.Bounds.Width, Client.Game.Scene.Camera.Bounds.Height);
            if (Fog) DrawFogLayer(batcher, x, y, false);
            if (CurrentCount == 0 || !CurrentWeather.HasValue) return;

            for (int i = 0; i < CurrentCount; i++)
            {
                ref WeatherEffect effect = ref _effects[i];
                if (effect.ScaleRatio >= 0.34f
                    || !IsInsideWeatherFront(effect.X, size.X, effect.ID)
                    || !SceneryInteractionManager.IsWeatherVisibleAt(effect.X, effect.Y, size.X, size.Y, effect.ID)) continue;

                float light = SceneryInteractionManager.GetLightInfluence(effect.X, effect.Y, out Color lightColor);
                Color color = light > 0.02f
                    ? Color.Lerp(new Color(150, 175, 205, 255), lightColor, Math.Min(0.65f, light))
                    : new Color(150, 175, 205, 255);
                float alpha = (0.18f + effect.ScaleRatio * 0.22f) * _visualIntensity;
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);

                if (Type == WeatherType.WT_SNOW)
                {
                    batcher.Draw(SolidColorTextureCache.GetTexture(Color.Lerp(Color.White, color, 0.25f)),
                        new Rectangle(x + (int)effect.X, y + (int)effect.Y, 1, 1), hue);
                }
                else
                {
                    batcher.DrawLine(SolidColorTextureCache.GetTexture(color),
                        new Vector2(x + effect.X + 2, y + effect.Y - 3),
                        new Vector2(x + effect.X, y + effect.Y), hue, 1);
                }
            }
        }

        public void Draw(UltimaBatcher2D batcher, int x, int y, bool useAlternativeLights)
        {
            bool removeEffects = false;
            Point winsize = new Point(Client.Game.Scene.Camera.Bounds.Width, Client.Game.Scene.Camera.Bounds.Height);

            // Alternative lights do not use ApplyLight's ambient target. Apply
            // the same signed weather grade before their point lights are
            // composited, so lamps remain bright.
            if (useAlternativeLights && Math.Abs(_lightAtten) > 0.005f)
            {
                bool darken = _lightAtten > 0f;
                float alpha = Math.Abs(_lightAtten);
                Color grade = darken ? new Color(8, 12, 22, 255) : Color.White;
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(grade),
                    new Rectangle(x, y, winsize.X, winsize.Y),
                    ShaderHueTranslator.GetHueVector(0, false, alpha)
                );
            }

            // Fog is a standalone condition (no falling particles) — draw
            // before the weather-timer early-out.
            if (Fog)
            {
                DrawFog(batcher, x, y);
            }

            // Delayed thunder: sound arrives after the flash by distance.
            if (_thunderAt != 0 && Time.Ticks >= _thunderAt)
            {
                _thunderAt = 0;
                if (World.Player != null) PlayThunder();
            }
            if (_thunderEchoAt != 0 && Time.Ticks >= _thunderEchoAt)
            {
                _thunderEchoAt = 0;
                if (World.Player != null)
                    PlaySound(_thunderEchoSound, SceneryInteractionManager.AcousticEcho * 0.45f);
            }

            // Rainbow: rolled once when rain expires, shown ~30s. Drawn
            // before the early-out so it survives the particle fade.
            if (Type == WeatherType.WT_RAIN && CurrentWeather.HasValue
                && !_rainbowRolled && _timer < Time.Ticks)
            {
                _rainbowRolled = true;
                if (RandomHelper.GetValue(0, 99) < 35)
                {
                    _rainbowUntil = Time.Ticks + 30_000;
                }
            }
            if (_rainbowUntil > Time.Ticks)
            {
                DrawRainbow(batcher, x, y);
            }

            if (_timer < Time.Ticks)
            {
                if (CurrentCount == 0)
                {
                    return;
                }

                removeEffects = true;
            }
            else if (Type == WeatherType.WT_INVALID_0 || Type == WeatherType.WT_INVALID_1)
            {
                return;
            }

            uint passed = Time.Ticks - _lastTick;

            if (passed > 7000)
            {
                _lastTick = Time.Ticks;
                passed = 25;
            }

            bool windChanged = false;

            if (_windTimer < Time.Ticks)
            {
                if (_windTimer == 0)
                {
                    windChanged = true;
                }

                // Blizzards and tempests gust far more often.
                bool gusty = Blizzard || Tempest;
                _windTimer = Time.Ticks + (uint) (RandomHelper.GetValue(gusty ? 4 : 13, gusty ? 8 : 19) * 1000);

                sbyte lastWind = Wind;

                Wind = (sbyte) RandomHelper.GetValue(0, 4);

                if (RandomHelper.GetValue(0, 2) != 0)
                {
                    Wind *= -1;
                }

                if (Wind < 0 && lastWind > 0)
                {
                    Wind = 0;
                }
                else if (Wind > 0 && lastWind < 0)
                {
                    Wind = 0;
                }

                if (lastWind != Wind)
                {
                    windChanged = true;
                }
            }

            bool isStorm = Type == WeatherType.WT_STORM_APPROACH || Type == WeatherType.WT_STORM_BREWING;

            // TazUO: wind-change sounds were played once PER PARTICLE (up to
            // 70 stacked sounds). Play once.
            if (windChanged)
            {
                if (Type == WeatherType.WT_SNOW)
                {
                    PlayWind();
                }
                else if (isStorm)
                {
                    if (_visualIntensity > 0.45f && _nextStrike != 0)
                        TriggerLightning();
                    else
                        PlayWind();
                }
            }

            // Independent lightning cadence: tempests strike near-constantly,
            // full storms often, brewing storms occasionally.
            if (isStorm && !removeEffects && Time.Ticks >= _nextStrike)
            {
                if (_nextStrike != 0)
                {
                    TriggerLightning();
                }
                int min = Tempest ? 2000 : Type == WeatherType.WT_STORM_APPROACH ? 4500 : 9000;
                int max = Tempest ? 5000 : Type == WeatherType.WT_STORM_APPROACH ? 9000 : 16000;
                _nextStrike = Time.Ticks + (uint)RandomHelper.GetValue(min, max);
            }

            // Storm ambience: faint blue-grey color cast. The actual darkening
            // comes from ApplyLight lowering the world light level.
            if (isStorm && !removeEffects)
            {
                float tint = (Tempest ? 0.11f : 0.07f) * _visualIntensity;
                Vector3 tintHue = ShaderHueTranslator.GetHueVector(0, false, tint);
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(25, 30, 45, 255)),
                    new Rectangle(x, y, winsize.X, winsize.Y), tintHue);
            }

            // Blizzard white-out: pale haze over everything, pulsing slightly
            // with the wind so visibility "breathes". Heavy snowfall gets a
            // fainter, steady haze.
            if ((Blizzard || HeavySnow) && Type == WeatherType.WT_SNOW && !removeEffects)
            {
                float haze = Blizzard
                    ? 0.10f + 0.04f * (float) Math.Sin(Time.Ticks / 900f)
                    : 0.05f;
                haze *= _visualIntensity;
                Vector3 hazeHue = ShaderHueTranslator.GetHueVector(0, false, haze);
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(225, 230, 240, 255)),
                    new Rectangle(x, y, winsize.X, winsize.Y), hazeHue);
            }

            // Lightning: double flash (bright → gap → dimmer afterflash).
            // The screen overlay is subtle — most of the pop comes from
            // ApplyLight brightening the world light level. The visible bolt
            // draws during the first ~100ms.
            {
                float flash = CurrentFlashStrength();
                if (flash < 0f)
                {
                    _lightningAt = 0;
                    _boltCount = 0;
                    _forkCount = 0;
                }
                else if (flash > 0f)
                {
                    Vector3 flashHue = ShaderHueTranslator.GetHueVector(0, false, flash * 0.5f);
                    batcher.Draw(SolidColorTextureCache.GetTexture(new Color(235, 240, 255, 255)),
                        new Rectangle(x, y, winsize.X, winsize.Y), flashHue);

                    uint boltAge = Time.Ticks - _lightningAt;
                    if (boltAge < 100 && _boltCount > 1)
                    {
                        float boltAlpha = Math.Min(0.9f, flash * 1.5f);
                        Vector3 boltHue = ShaderHueTranslator.GetHueVector(0, false, boltAlpha);
                        Texture2D boltTex = SolidColorTextureCache.GetTexture(new Color(220, 230, 255, 255));
                        for (int i = 1; i < _boltCount; i++)
                        {
                            batcher.DrawLine(boltTex,
                                new Vector2(x + _boltPts[i - 1].X, y + _boltPts[i - 1].Y),
                                new Vector2(x + _boltPts[i].X, y + _boltPts[i].Y),
                                boltHue, 3);
                        }
                        if (_forkCount > 1)
                        {
                            Vector3 forkHue = ShaderHueTranslator.GetHueVector(0, false, boltAlpha * 0.7f);
                            for (int i = 1; i < _forkCount; i++)
                            {
                                batcher.DrawLine(boltTex,
                                    new Vector2(x + _forkPts[i - 1].X, y + _forkPts[i - 1].Y),
                                    new Vector2(x + _forkPts[i].X, y + _forkPts[i].Y),
                                    forkHue, 2);
                            }
                        }
                    }
                }
            }

            // Blizzard white-out vignette: soft white blobs hugging the
            // viewport edges — visibility drops off toward the screen rim.
            if (Blizzard && Type == WeatherType.WT_SNOW && !removeEffects)
            {
                Texture2D vBlob = GetFogBlob();
                int vs = (int)(winsize.Y * 0.9f);
                float vAlpha = 0.20f + 0.05f * (float) Math.Sin(Time.Ticks / 800f);
                Vector3 vHue = ShaderHueTranslator.GetHueVector(0, false, vAlpha);
                // Corners + edge midpoints, centered slightly off-screen.
                for (int i = 0; i < 8; i++)
                {
                    int px = (i % 3) * (winsize.X / 2);           // 0, w/2, w
                    int py = (i / 3) * (winsize.Y / 2);
                    if (px == winsize.X / 2 && py == winsize.Y / 2) continue; // skip center
                    batcher.Draw(vBlob,
                        new Rectangle(x + px - vs / 2, y + py - vs / 2, vs, vs), vHue);
                }
            }

            // Wind-blown debris during violent weather: small specks tumbling
            // across with the gust direction. Stateless (clock-derived).
            if ((Tempest || Blizzard) && !removeEffects)
            {
                Texture2D dTex = SolidColorTextureCache.GetTexture(
                    Tempest ? new Color(95, 85, 65, 255) : new Color(200, 205, 215, 255));
                bool leftward = Wind < 0;
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(22); i++)
                {
                    float speed = 0.25f + (i % 5) * 0.09f;
                    int span = winsize.X + 20;
                    int dx = (int)((Time.Ticks * speed + i * 397) % span) - 10;
                    if (leftward) dx = winsize.X - dx;
                    int dy = (i * 211) % winsize.Y
                           + (int)(18 * Math.Sin(Time.Ticks / 240f + i));
                    if (dy < 0 || dy >= winsize.Y) continue;
                    float dAlpha = 0.28f + 0.12f * (float) Math.Sin(Time.Ticks / 150f + i * 2.1f);
                    Vector3 dHue = ShaderHueTranslator.GetHueVector(0, false, dAlpha);
                    int ds = 2 + i % 2;
                    batcher.Draw(dTex, new Rectangle(x + dx, y + dy, ds, ds), dHue);
                }
            }

            Rectangle snowRect = new Rectangle(0, 0, 2, 2);

            for (int i = 0; i < CurrentCount; i++)
            {
                ref WeatherEffect effect = ref _effects[i];

                if (effect.X < x || effect.X > x + winsize.X || effect.Y < y || effect.Y > y + winsize.Y)
                {
                    if (removeEffects)
                    {
                        if (CurrentCount > 0)
                        {
                            CurrentCount--;
                        }
                        else
                        {
                            CurrentCount = 0;
                        }

                        continue;
                    }

                    effect.X = x + RandomHelper.GetValue(0, winsize.X);
                    effect.Y = y + RandomHelper.GetValue(0, winsize.Y);
                }


                switch (Type)
                {
                    case WeatherType.WT_RAIN:
                        // Depth-scaled speed: near drops (ScaleRatio→1) fall
                        // visibly faster than far ones.
                        float scaleRation = effect.ScaleRatio;
                        effect.SpeedX = -4.5f - scaleRation * 2.0f;
                        effect.SpeedY = 5.0f + scaleRation * 4.0f;

                        break;

                    case WeatherType.WT_STORM_BREWING:
                        effect.SpeedX = Wind * 1.5f;
                        effect.SpeedY = 1.5f;

                        break;

                    case WeatherType.WT_SNOW:
                    case WeatherType.WT_STORM_APPROACH:

                        // Hail: persistent velocity + gravity (needed for the
                        // bounce in the draw pass). SpeedAngle doubles as the
                        // "already bounced" flag.
                        if (Type == WeatherType.WT_SNOW && Hail)
                        {
                            if (effect.SpeedY == 0f && effect.SpeedAngle == 0f)
                            {
                                effect.SpeedY = 8f + effect.ScaleRatio * 4f;
                            }
                            else
                            {
                                effect.SpeedY += 0.30f * passed / (1000f / 60f);
                            }
                            effect.SpeedX = Wind * 0.5f;
                            break;
                        }

                        if (Type == WeatherType.WT_SNOW)
                        {
                            if (Blizzard)
                            {
                                // Driving diagonal snow: strong consistent
                                // lateral push + fast, depth-varied fall.
                                float gale = Wind == 0 ? 4f : Wind * 3f;
                                effect.SpeedX = gale;
                                effect.SpeedY = 3.5f + effect.ScaleRatio * 3f;
                            }
                            else if (HeavySnow)
                            {
                                // Massive but calm: heavy flakes drop faster
                                // than a flurry, little lateral drift.
                                effect.SpeedX = Wind * 1.5f;
                                effect.SpeedY = 1.8f + effect.ScaleRatio * 1.4f;
                            }
                            else
                            {
                                effect.SpeedX = Wind;
                                effect.SpeedY = 1.0f;
                            }
                        }
                        else if (Tempest)
                        {
                            // Torrential: violent lateral gusts + fast fall.
                            effect.SpeedX = Wind == 0 ? 3f : Wind * 2.5f;
                            effect.SpeedY = 8.0f + effect.ScaleRatio * 3f;
                        }
                        else
                        {
                            effect.SpeedX = Wind;
                            effect.SpeedY = 6.0f;
                        }

                        if (windChanged)
                        {
                            effect.SpeedAngle = MathHelper.ToDegrees((float) Math.Atan2(effect.SpeedX, effect.SpeedY));

                            effect.SpeedMagnitude = (float) Math.Sqrt(Math.Pow(effect.SpeedX, 2) + Math.Pow(effect.SpeedY, 2));
                        }

                        float speedAngle = effect.SpeedAngle;
                        float speedMagnitude = effect.SpeedMagnitude;

                        speedMagnitude += effect.ScaleRatio;

                        speedAngle += SinOscillate(0.4f, 20, Time.Ticks + effect.ID);

                        float rad = MathHelper.ToRadians(speedAngle);
                        effect.SpeedX = speedMagnitude * (float) Math.Sin(rad);
                        effect.SpeedY = speedMagnitude * (float) Math.Cos(rad);

                        break;
                }

                float speedOffset = passed / SIMULATION_TIME;
                bool drawForeground = effect.ScaleRatio >= 0.34f
                    && IsInsideWeatherFront(effect.X, winsize.X, effect.ID)
                    && SceneryInteractionManager.IsWeatherVisibleAt(
                        effect.X, effect.Y, winsize.X, winsize.Y, effect.ID);

                switch (Type)
                {
                    case WeatherType.WT_RAIN:
                    case WeatherType.WT_STORM_APPROACH:
                    // Upstream never drew WT_STORM_BREWING particles at all —
                    // render them as slow drizzle streaks.
                    case WeatherType.WT_STORM_BREWING:

                        effect.X += effect.SpeedX * speedOffset;
                        effect.Y += effect.SpeedY * speedOffset;

                        if (!drawForeground) break;

                        float rainLight = SceneryInteractionManager.GetLightInfluence(
                            effect.X, effect.Y, out Color rainLightColor);

                        // Sleet: ~40% of the drops render as wet flakes.
                        if (Sleet && effect.ID % 5 < 2)
                        {
                            Color sleetColor = Color.Lerp(
                                new Color(225, 230, 240, 255), rainLightColor, Math.Min(0.65f, rainLight));
                            batcher.Draw(SolidColorTextureCache.GetTexture(sleetColor),
                                new Rectangle(x + (int) effect.X, y + (int) effect.Y, 2, 2),
                                ShaderHueTranslator.GetHueVector(0, false, Math.Min(1f, 0.7f + rainLight * 0.25f)));
                            break;
                        }

                        // Streak: drawn backwards along the velocity vector.
                        // ScaleRatio (0..1) = "depth": near drops are longer,
                        // faster and brighter; far ones short and faint.
                        float depth = effect.ScaleRatio;
                        float mag = (float) Math.Sqrt(effect.SpeedX * effect.SpeedX + effect.SpeedY * effect.SpeedY);
                        if (mag < 0.01f) mag = 1f;
                        // Streak length scales with fall speed (slow drizzle =
                        // short ticks, driving rain = long streaks), 3..18px.
                        float len = (2f + depth * 2.5f) * (mag / 3f);
                        if (len < 3f) len = 3f;
                        if (len > 18f) len = 18f;
                        float tailX = effect.X - effect.SpeedX / mag * len;
                        float tailY = effect.Y - effect.SpeedY / mag * len;

                        float rainAlpha = Math.Min(1f, 0.30f + depth * 0.45f + rainLight * 0.22f);
                        Vector3 rainHue = ShaderHueTranslator.GetHueVector(0, false, rainAlpha);
                        Color rainColor = Color.Lerp(
                            new Color(170, 195, 230, 255), rainLightColor, Math.Min(0.72f, rainLight));

                        batcher.DrawLine
                        (
                           SolidColorTextureCache.GetTexture(rainColor),
                           new Vector2(x + tailX, y + tailY),
                           new Vector2(x + effect.X, y + effect.Y),
                           rainHue,
                           depth > 0.6f ? 2 : 1
                        );

                        break;

                    case WeatherType.WT_SNOW:

                        effect.X += effect.SpeedX * speedOffset;
                        effect.Y += effect.SpeedY * speedOffset;

                        // Hail: bounce once off the ground line, then respawn.
                        if (Hail)
                        {
                            if (effect.GroundY > 0 && effect.Y >= effect.GroundY && effect.SpeedY > 0)
                            {
                                if (effect.SpeedAngle == 0f)
                                {
                                    effect.SpeedAngle = 1f;
                                    effect.SpeedY = -effect.SpeedY * 0.35f;
                                }
                                else
                                {
                                    effect.X = x + RandomHelper.GetValue(0, winsize.X);
                                    effect.Y = y;
                                    effect.SpeedY = 0f;
                                    effect.SpeedAngle = 0f;
                                    effect.GroundY = RandomHelper.GetValue(winsize.Y * 35 / 100, winsize.Y * 95 / 100);
                                }
                            }

                            snowRect.X = x + (int) effect.X;
                            snowRect.Y = y + (int) effect.Y;
                            snowRect.Width = 2;
                            snowRect.Height = 2;
                            if (drawForeground)
                            {
                                float hailLight = SceneryInteractionManager.GetLightInfluence(
                                    effect.X, effect.Y, out Color hailLightColor);
                                batcher.Draw(SolidColorTextureCache.GetTexture(
                                        Color.Lerp(Color.White, hailLightColor, Math.Min(0.55f, hailLight))),
                                    snowRect, ShaderHueTranslator.GetHueVector(0, false,
                                        Math.Min(1f, 0.9f + hailLight * 0.10f)));
                            }
                            break;
                        }

                        // Landed? Deposit a ground spot and respawn at top.
                        if (effect.GroundY > 0 && effect.Y >= effect.GroundY && !removeEffects)
                        {
                            _groundDecals.AddFromScreen(
                                effect.X,
                                effect.Y,
                                winsize,
                                (byte)((HeavySnow ? 2 : 1) + effect.ID % 2),
                                GroundDecalKind.Snow
                            );
                            effect.X = x + RandomHelper.GetValue(0, winsize.X);
                            effect.Y = y;
                            effect.GroundY = RandomHelper.GetValue(winsize.Y * 35 / 100, winsize.Y * 95 / 100);
                        }

                        if (!drawForeground) break;

                        // Flake size from the per-flake seed (heavy snowfall =
                        // fatter flakes); gentle alpha shimmer so the field
                        // doesn't look static.
                        int flake = (HeavySnow ? 2 : 1) + (int)(effect.ID % 3);
                        snowRect.X = x + (int) effect.X;
                        snowRect.Y = y + (int) effect.Y;
                        snowRect.Width = flake;
                        snowRect.Height = flake;

                        float snowLight = SceneryInteractionManager.GetLightInfluence(
                            effect.X, effect.Y, out Color snowLightColor);
                        float shimmer = 0.65f + 0.35f * (float) Math.Sin((Time.Ticks + effect.ID * 13) / 280f);
                        shimmer = Math.Min(1f, shimmer + snowLight * 0.18f);
                        Vector3 snowHue = ShaderHueTranslator.GetHueVector(0, false, shimmer);

                        batcher.Draw
                        (
                            SolidColorTextureCache.GetTexture(
                                Color.Lerp(Color.White, snowLightColor, Math.Min(0.48f, snowLight))),
                            snowRect,
                            snowHue
                        );

                        break;
                }
            }

            // Rain splashes: short expanding ripples at random ground points.
            if ((Type == WeatherType.WT_RAIN || isStorm) && !removeEffects && CurrentCount > 0)
            {
                if (Time.Ticks >= _nextSplash)
                {
                    uint splashDelay = (uint)(Tempest ? 25 : isStorm ? 40 : 90);
                    if (SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_LOW)
                        splashDelay *= 2;
                    _nextSplash = Time.Ticks + splashDelay;
                    ref Splash sp = ref _splashes[_splashIdx++ % MAX_SPLASHES];
                    sp.X = x + RandomHelper.GetValue(0, winsize.X);
                    sp.Y = y + RandomHelper.GetValue(0, winsize.Y);
                    uint splashSeed = (uint)(_splashIdx * 977 + 31);
                    bool splashVisible = IsInsideWeatherFront(sp.X - x, winsize.X, splashSeed)
                        && SceneryInteractionManager.IsWeatherVisibleAt(
                            sp.X - x, sp.Y - y, winsize.X, winsize.Y, splashSeed);
                    sp.Born = splashVisible ? Time.Ticks : 0;

                    // Every few splashes leave a lingering puddle.
                    if (splashVisible && _splashIdx % 3 == 0)
                    {
                        _groundDecals.AddFromScreen(
                            sp.X - x,
                            sp.Y - y,
                            winsize,
                            (byte)RandomHelper.GetValue(2, 4),
                            GroundDecalKind.Puddle
                        );
                    }
                }
                DrawSplashes(batcher);
            }

            _lastTick = Time.Ticks;
        }

        // Soft radial-gradient blob generated once at runtime — solid rects
        // read as hard-edged boxes, this feathers out to fully transparent.
        private static Texture2D _fogBlob;
        private static Texture2D _fogWarmBlob, _fogBlueBlob, _fogGreenBlob;

        internal static Texture2D GetFogBlob()
        {
            if (_fogBlob != null && !_fogBlob.IsDisposed) return _fogBlob;

            const int S = 128;
            var data = new Color[S * S];
            const float half = S / 2f;
            for (int py = 0; py < S; py++)
            {
                for (int px = 0; px < S; px++)
                {
                    float dx = (px - half) / half;
                    float dy = (py - half) / half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);   // 0 center → 1 edge
                    float a = d >= 1f ? 0f : (1f - d) * (1f - d);    // quadratic falloff
                    byte b = (byte)(a * 255);
                    // Premultiplied slightly-grey white.
                    data[py * S + px] = new Color((byte)(b * 210 / 255), (byte)(b * 215 / 255), (byte)(b * 222 / 255), b);
                }
            }

            _fogBlob = new Texture2D(Client.Game.GraphicsDevice, S, S);
            _fogBlob.SetData(data);
            return _fogBlob;
        }

        private static Texture2D GetIlluminatedFogBlob(Color color)
        {
            if (color.G > color.R * 1.12f && color.G > color.B * 1.05f)
                return _fogGreenBlob != null && !_fogGreenBlob.IsDisposed
                    ? _fogGreenBlob : _fogGreenBlob = BuildTintedFogBlob(new Color(125, 225, 145, 255));
            if (color.B > color.R * 1.08f)
                return _fogBlueBlob != null && !_fogBlueBlob.IsDisposed
                    ? _fogBlueBlob : _fogBlueBlob = BuildTintedFogBlob(new Color(135, 175, 255, 255));
            return _fogWarmBlob != null && !_fogWarmBlob.IsDisposed
                ? _fogWarmBlob : _fogWarmBlob = BuildTintedFogBlob(new Color(255, 184, 105, 255));
        }

        private static Texture2D BuildTintedFogBlob(Color color)
        {
            const int size = 128;
            var data = new Color[size * size];
            const float half = size / 2f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = (px - half) / half;
                    float dy = (py - half) / half;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = distance >= 1f ? 0f : (1f - distance) * (1f - distance);
                    byte a = (byte)(alpha * 255);
                    data[py * size + px] = new Color(
                        (byte)(color.R * a / 255), (byte)(color.G * a / 255),
                        (byte)(color.B * a / 255), a);
                }
            }
            var texture = new Texture2D(Client.Game.GraphicsDevice, size, size);
            texture.SetData(data);
            return texture;
        }

        // Drifting mist banks: stateless — position derives from Time.Ticks,
        // so there's nothing to simulate. A uniform wash plus large soft
        // gradient blobs sliding at different speeds/heights.
        private void DrawFog(UltimaBatcher2D batcher, int x, int y)
        {
            DrawFogLayer(batcher, x, y, true);
        }

        private void DrawFogLayer(UltimaBatcher2D batcher, int x, int y, bool foreground)
        {
            var bounds = Client.Game.Scene.Camera.Bounds;
            int w = bounds.Width;
            int h = bounds.Height;

            if (foreground)
            {
                float wash = 0.08f * _frontCoverage * (1f - SceneryInteractionManager.Shelter * 0.72f);
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(210, 215, 222, 255)),
                    new Rectangle(x, y, w, h),
                    ShaderHueTranslator.GetHueVector(0, false, wash));
            }

            Texture2D blob = GetFogBlob();

            int count = SceneryInteractionManager.ScaleCount(10);
            for (int i = 0; i < count; i++)
            {
                if ((i % 2 == 0) == foreground) continue;
                int bw = 380 + (i * 137) % 320;              // blob width
                int bh = bw / 2;                             // flat ellipse
                float speed = 0.006f + (i % 3) * 0.005f;     // px per tick
                int span = w + bw;
                int bx = (int)((Time.Ticks * speed + i * 331) % span) - bw;
                int by = (i * 149) % Math.Max(1, h - bh / 2) - bh / 4;
                int cx = bx + bw / 2;
                int cy = by + bh / 2;
                uint seed = (uint)(i * 733 + 17);
                if (!IsInsideWeatherFront(cx, w, seed)
                    || !SceneryInteractionManager.IsWeatherVisibleAt(cx, cy, w, h, seed)) continue;

                // Gentle per-blob alpha breathing.
                float alpha = (foreground ? 0.30f : 0.17f)
                    + 0.08f * (float)Math.Sin(Time.Ticks / 1100f + i * 1.7f);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
                float light = SceneryInteractionManager.GetLightInfluence(cx, cy, out Color lightColor);
                Texture2D texture = light > 0.03f ? GetIlluminatedFogBlob(lightColor) : blob;
                if (light > 0.03f) hue = ShaderHueTranslator.GetHueVector(0, false, alpha * (0.35f + light * 0.65f));
                batcher.Draw(texture, new Rectangle(x + bx, y + by, bw, bh), hue);
            }
        }

        private static readonly Color[] _rainbowColors =
        {
            new Color(230, 60, 60, 255),    // red (outer)
            new Color(235, 150, 50, 255),   // orange
            new Color(235, 220, 70, 255),   // yellow
            new Color(90, 200, 90, 255),    // green
            new Color(80, 140, 230, 255),   // blue
            new Color(150, 90, 210, 255),   // violet (inner)
        };

        // Faint arc after rain: 6 color bands over a semicircle, fading in
        // for 4s and out over the last 4s of its ~30s life.
        private void DrawRainbow(UltimaBatcher2D batcher, int x, int y)
        {
            var bounds = Client.Game.Scene.Camera.Bounds;
            int w = bounds.Width;
            int h = bounds.Height;

            uint remaining = _rainbowUntil - Time.Ticks;
            uint elapsed = 30_000 - Math.Min(30_000, remaining);
            float fade = Math.Min(1f, Math.Min(remaining, elapsed) / 4000f);
            if (fade <= 0f) return;

            float cxr = w / 2f;
            float cyr = h + h * 0.35f;      // center below the screen
            float radius = h * 1.05f;

            const int SEGMENTS = 36;
            const float START = (float)(Math.PI * 1.15); // ~207°
            const float END = (float)(Math.PI * 1.85);   // ~333°

            for (int b = 0; b < _rainbowColors.Length; b++)
            {
                float r = radius - b * 5f;
                Texture2D tex = SolidColorTextureCache.GetTexture(_rainbowColors[b]);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.13f * fade);

                float prevX = cxr + r * (float)Math.Cos(START);
                float prevY = cyr + r * (float)Math.Sin(START);
                for (int i = 1; i <= SEGMENTS; i++)
                {
                    float t = START + (END - START) * i / SEGMENTS;
                    float px = cxr + r * (float)Math.Cos(t);
                    float py = cyr + r * (float)Math.Sin(t);
                    batcher.DrawLine(tex,
                        new Vector2(x + prevX, y + prevY),
                        new Vector2(x + px, y + py), hue, 5);
                    prevX = px;
                    prevY = py;
                }
            }
        }

        private void DrawSplashes(UltimaBatcher2D batcher)
        {
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(190, 210, 235, 255));
            uint now = Time.Ticks;
            for (int i = 0; i < MAX_SPLASHES; i++)
            {
                ref Splash sp = ref _splashes[i];
                if (sp.Born == 0) continue;
                uint age = now - sp.Born;
                if (age >= SPLASH_LIFE_MS) { sp.Born = 0; continue; }

                float t = age / (float)SPLASH_LIFE_MS;      // 0 → 1
                float alpha = 0.45f * (1f - t);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
                // Expanding flat ellipse hinted with 4 dots (iso ground plane).
                int rx = 2 + (int)(7f * t);
                int ry = rx / 2;
                batcher.Draw(tex, new Rectangle((int)sp.X - rx, (int)sp.Y, 2, 1), hue);
                batcher.Draw(tex, new Rectangle((int)sp.X + rx - 1, (int)sp.Y, 2, 1), hue);
                batcher.Draw(tex, new Rectangle((int)sp.X, (int)sp.Y - ry, 1, 1), hue);
                batcher.Draw(tex, new Rectangle((int)sp.X, (int)sp.Y + ry, 1, 1), hue);
            }
        }


        private struct WeatherEffect
        {
            public float SpeedX, SpeedY, X, Y, ScaleRatio, SpeedAngle, SpeedMagnitude;
            public uint ID;
            // Snow: screen Y at which this flake "lands" and becomes a
            // ground deposit.
            public float GroundY;
        }

        private struct Splash
        {
            public float X, Y;
            public uint Born; // 0 = free slot
        }

    }
}
