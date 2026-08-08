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

using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Stores a designated "trash" container serial (per profile). `-trash`
    /// double-clicks it to open. `-trashdrop` queues moving a clicked item
    /// into the trash container via MoveItemQueue. Quick disposal flow.
    /// </summary>
    public static class TrashContainerManager
    {
        private const string FILENAME = "trash.serial";
        public static uint Serial;
        private static bool _loaded;

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "trash.serial");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            string line = ProfileDataStore.ReadAllText(FILENAME)?.Trim();
            if (uint.TryParse(line, System.Globalization.NumberStyles.HexNumber, null, out uint v))
                Serial = v;
        }

        private static void Save()
        {
            ProfileDataStore.WriteAllText(FILENAME, Serial.ToString("X"));
        }

        public static void ResetForProfile()
        {
            Serial = 0;
            _loaded = false;
        }

        public static async void Pick()
        {
            GameActions.Print("Click your trash container...", 0x35);
            await TargetHelper.TargetObject((ent) =>
            {
                if (ent == null || ent.Serial == 0) return;
                Serial = ent.Serial;
                Save();
                GameActions.Print($"Trash container = 0x{Serial:X8}.", 0x35);
            });
        }

        public static void Open()
        {
            EnsureLoaded();
            if (Serial == 0) { GameActions.Print("No trash container set. -trash pick first.", 0x21); return; }
            GameActions.DoubleClick(Serial);
        }

        public static async void Drop()
        {
            EnsureLoaded();
            if (Serial == 0) { GameActions.Print("No trash container set. -trash pick first.", 0x21); return; }
            GameActions.Print("Click item to trash...", 0x35);
            await TargetHelper.TargetObject((ent) =>
            {
                if (ent == null) return;
                var item = World.Items.Get(ent.Serial);
                if (item == null) { GameActions.Print("Not an item.", 0x21); return; }
                MoveItemQueue.Instance.Enqueue(item.Serial, Serial, item.Amount, 0xFFFF, 0xFFFF);
            });
        }
    }
}
