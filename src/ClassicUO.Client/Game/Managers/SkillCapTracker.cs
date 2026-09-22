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
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Subscribes to SkillGainTracker.Gained. When the matching player skill
    /// gets within WARN_DELTA of its Cap, a toast fires (once per skill until
    /// the gap re-widens).
    /// </summary>
    public static class SkillCapTracker
    {
        public static float WarnDelta = 1.0f;   // value within X.X of cap triggers warning
        public static bool Enabled = true;
        private static bool _hooked;
        private static readonly HashSet<string> _warned = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public static void EnsureHooked()
        {
            if (_hooked) return;
            SkillGainTracker.Gained += OnGain;
            _hooked = true;
        }

        private static void OnGain(SkillGainTracker.Entry entry, double amount)
        {
            if (!Enabled || World.Player == null) return;
            var skills = World.Player.Skills;
            if (skills == null) return;
            for (int i = 0; i < skills.Length; i++)
            {
                var s = skills[i];
                if (s == null) continue;
                if (!string.Equals(s.Name, entry.SkillName, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (s.Cap > 0 && s.Value >= s.Cap - WarnDelta)
                {
                    if (_warned.Add(s.Name))
                    {
                        ToastManager.Show($"{s.Name} near cap: {s.Value:0.0} / {s.Cap:0.0}", 0x35, 5000,
                            "skill-cap", AlertCategory.System, AlertSeverity.Info);
                    }
                }
                else if (s.Value < s.Cap - (WarnDelta + 2))
                {
                    // Re-arm once gap widens (e.g. cap raised).
                    _warned.Remove(s.Name);
                }
                break;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Skill-cap tracker {(on ? "ON" : "OFF")} (warn within {WarnDelta:0.0}).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}
