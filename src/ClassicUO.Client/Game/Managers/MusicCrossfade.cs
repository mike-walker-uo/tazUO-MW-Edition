using System;

namespace ClassicUO.Game.Managers
{
    internal sealed class MusicCrossfade
    {
        private uint _start;
        private int _duration;
        private float _fromPeace = 1f, _fromWar, _toPeace = 1f, _toWar;
        internal float Peace { get; private set; } = 1f;
        internal float War { get; private set; }
        internal bool Active { get; private set; }

        internal void Begin(uint now, int duration, bool toWar)
        {
            Update(now);
            _fromPeace = Peace; _fromWar = War;
            _toPeace = toWar ? 0f : 1f; _toWar = toWar ? 1f : 0f;
            _start = now; _duration = Math.Max(0, Math.Min(1000, duration)); Active = true;
            Update(now);
        }
        internal void Update(uint now)
        {
            if (!Active) return;
            float t = _duration == 0 ? 1f : Math.Min(1f, (now - _start) / (float)_duration);
            t = t * t * (3f - 2f * t);
            Peace = _fromPeace + (_toPeace - _fromPeace) * t;
            War = _fromWar + (_toWar - _fromWar) * t;
            if (t >= 1f) Active = false;
        }
        internal void Reset() { Peace = 1f; War = 0f; Active = false; }
    }
}
