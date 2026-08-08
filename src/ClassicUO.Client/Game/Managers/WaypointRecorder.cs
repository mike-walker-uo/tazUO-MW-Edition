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

using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Records key waypoints (every WAYPOINT_DISTANCE tiles) while recording,
    /// and replays them sequentially via Pathfinder.WalkTo to retrace the path.
    /// Limited: replay assumes terrain hasn't changed and there are no
    /// blockers, since each leg uses the standard pathfinder.
    /// </summary>
    public static class WaypointRecorder
    {
        public const int WAYPOINT_DISTANCE = 5;
        public static bool Recording { get; private set; }
        public static bool Playing { get; private set; }
        private static readonly List<(int x, int y, int z)> _waypoints = new List<(int x, int y, int z)>();
        private static int _playIndex;
        private static bool _hooked;
        private static (int x, int y) _lastRecorded;

        public static IReadOnlyList<(int x, int y, int z)> Waypoints => _waypoints;

        public static void ResetSession()
        {
            Recording = false;
            Playing = false;
            _waypoints.Clear();
            _playIndex = 0;
            _lastRecorded = default;
            Pathfinder.StopAutoWalk();
        }

        public static void StartRecording()
        {
            EnsureHooked();
            _waypoints.Clear();
            Recording = true;
            Playing = false;
            if (World.Player != null)
            {
                _waypoints.Add((World.Player.X, World.Player.Y, World.Player.Z));
                _lastRecorded = (World.Player.X, World.Player.Y);
            }
            GameActions.Print("Waypoint recording STARTED.", 0x35);
        }

        public static void StopRecording()
        {
            Recording = false;
            GameActions.Print($"Waypoint recording STOPPED. {_waypoints.Count} point(s).", 0x35);
        }

        public static void Play()
        {
            if (_waypoints.Count < 2)
            {
                GameActions.Print("No recorded path.", 0x21);
                return;
            }
            Playing = true;
            _playIndex = 0;
            GameActions.Print($"Replaying {_waypoints.Count} waypoints.", 0x35);
            StepNext();
        }

        public static void Stop()
        {
            Playing = false;
            Pathfinder.StopAutoWalk();
            GameActions.Print("Waypoint replay stopped.", 0x21);
        }

        public static void Tick()
        {
            if (!Playing) return;
            if (Pathfinder.AutoWalking) return;
            // Step to next waypoint when current leg finished.
            StepNext();
        }

        private static void StepNext()
        {
            if (!Playing || _playIndex >= _waypoints.Count)
            {
                Playing = false;
                return;
            }
            var w = _waypoints[_playIndex++];
            Pathfinder.WalkTo(w.x, w.y, w.z, 0);
        }

        private static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnPositionChanged += OnPosition;
            _hooked = true;
        }

        private static void OnPosition(object sender, PositionChangedArgs e)
        {
            if (!Recording || World.Player == null) return;
            int dx = System.Math.Abs(World.Player.X - _lastRecorded.x);
            int dy = System.Math.Abs(World.Player.Y - _lastRecorded.y);
            int cheb = System.Math.Max(dx, dy);
            if (cheb >= WAYPOINT_DISTANCE)
            {
                _waypoints.Add((World.Player.X, World.Player.Y, World.Player.Z));
                _lastRecorded = (World.Player.X, World.Player.Y);
            }
        }
    }
}
