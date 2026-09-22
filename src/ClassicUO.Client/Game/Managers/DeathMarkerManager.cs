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
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Detects player death (transition from alive → dead) and drops a
    /// WorldMap user marker at the player's current coords. Useful for finding
    /// your corpse after a respawn. Toggleable; off by default.
    /// </summary>
    public static class DeathMarkerManager
    {
        public static bool Enabled;
        private static bool _wasAlive = true;
        private const long CHECK_INTERVAL_MS = 500;
        private static long _nextCheck;

        public static void ResetSession()
        {
            _wasAlive = true;
            _nextCheck = 0;
        }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheck) return;
            _nextCheck = (long)Time.Ticks + CHECK_INTERVAL_MS;

            bool alive = !World.Player.IsDead;
            if (_wasAlive && !alive)
            {
                // Just died.
                int x = World.Player.X, y = World.Player.Y;
                var map = World.MapIndex;
                var gump = UIManager.GetGump<WorldMapGump>();
                if (gump == null)
                {
                    gump = new WorldMapGump();
                    UIManager.Add(gump);
                }
                string name = $"Death {DateTime.Now:HH:mm}";
                gump.AddUserMarker(name, x, y, map, "red");
                ToastManager.Show($"Death marker placed at ({x},{y}).", 0x21, 6000,
                    "death-marker", AlertCategory.System, AlertSeverity.Info);
            }
            _wasAlive = alive;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Death marker auto-place {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}
