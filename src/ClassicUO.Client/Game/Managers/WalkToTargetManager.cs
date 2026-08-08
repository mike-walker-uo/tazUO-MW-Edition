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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Prompts a target cursor, then auto-walks to the picked entity / tile
    /// via Pathfinder.WalkTo. Useful for click-to-move beyond mouse-click
    /// radius. `-walkto`. Pairs nicely with macro hotkey.
    /// </summary>
    public static class WalkToTargetManager
    {
        public static async void Prompt()
        {
            GameActions.Print("Walk-to: click target tile or entity...", 0x35);
            await TargetHelper.TargetAsync();
            var lt = TargetManager.LastTargetInfo;
            if (lt == null || !lt.IsSet) return;

            if (lt.IsEntity)
            {
                var ent = World.Get(lt.Serial);
                if (ent == null) { GameActions.Print("Target lost.", 0x21); return; }
                bool ok = Pathfinder.WalkTo(ent.X, ent.Y, ent.Z, 1);
                GameActions.Print(ok
                    ? $"Walking to 0x{ent.Serial:X8} ({ent.X},{ent.Y})."
                    : $"No path to 0x{ent.Serial:X8}.",
                    (ushort)(ok ? 0x35 : 0x21));
                return;
            }

            // Tile or static — LastTargetInfo carries the coords.
            int x = lt.X, y = lt.Y;
            sbyte z = lt.Z;
            if (x == 0 || x == 0xFFFF) { GameActions.Print("No tile selected.", 0x21); return; }
            if (TryWalkWithFallback(x, y, z))
                GameActions.Print($"Walking to {x},{y}.", 0x35);
            else
                GameActions.Print($"No path to {x},{y}.", 0x21);
        }

        /// <summary>
        /// Pathfinder caps node count; long routes fail. Probe progressively-
        /// closer waypoints (80%, 60%, 40%, 20% of the way) until one succeeds.
        /// </summary>
        private static bool TryWalkWithFallback(int x, int y, sbyte z)
        {
            if (World.Player == null) return false;
            sbyte pz = World.Player.Z;
            // The Z from LastTargetInfo can be land-Z when the surface is a
            // static; pathfinder rejects when Z mismatches the navigable layer.
            // Query the actual land-Z, then also try player's Z and a few
            // common offsets as broad fallbacks.
            sbyte landZ = World.Map?.GetTileZ(x, y) ?? z;

            foreach (int d in new[] { 1, 2, 3, 0 })
            {
                if (Pathfinder.WalkTo(x, y, landZ, d)) return true;
                if (z != landZ && Pathfinder.WalkTo(x, y, z, d)) return true;
                if (pz != landZ && Pathfinder.WalkTo(x, y, pz, d)) return true;
            }

            // Long-route fallback: probe progressively-closer waypoints along the line.
            int px = World.Player.X, py = World.Player.Y;
            float[] fractions = { 0.8f, 0.6f, 0.4f, 0.2f };
            for (int i = 0; i < fractions.Length; i++)
            {
                int tx = px + (int)((x - px) * fractions[i]);
                int ty = py + (int)((y - py) * fractions[i]);
                if (tx == px && ty == py) continue;
                if (Pathfinder.WalkTo(tx, ty, pz, 1))
                {
                    GameActions.Print($"(Partial: {tx},{ty})", 0x44);
                    return true;
                }
            }
            return false;
        }
    }
}
