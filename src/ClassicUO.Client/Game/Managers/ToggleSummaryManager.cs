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
    /// `-summary` prints a one-shot ON/OFF status of every togglable TazUO
    /// extension. Saves the player squinting through `-help` to discover
    /// what's running. Update this list as new features are added.
    /// </summary>
    public static class ToggleSummaryManager
    {
        public static void Print()
        {
            GameActions.Print("--- TazUO toggles ---", 0x35);
            Row("autohit",        AutoHitListManager.Enabled);
            Row("itemdropsound",  ItemDropSoundManager.Enabled);
            Row("petloyalty",     PetLoyaltyAlertManager.Enabled);
            Row("automount",      AutoMountManager.Enabled);
            Row("poisonalert",    PoisonAlertManager.Enabled);
            Row("bandagetimer",   BandageTimerManager.Enabled);
            Row("autorearm",      AutoRearmManager.Enabled);
            Row("hungeralert",    HungerThirstAlertManager.Enabled);
            Row("emergencyheal",  EmergencyHealManager.Enabled);
            Row("statalert",      StatChangeAlertManager.Enabled);
            Row("deathrecap",     DeathRecapManager.Enabled);
            Row("autoclosecorpse",AutoCloseEmptyCorpse.Enabled);
            Row("autopack",       AutoOpenBackpackManager.Enabled);
            Row("autopaper",      AutoOpenPaperdollManager.Enabled);
            Row("combatstate",    CombatStateManager.Enabled);
            Row("vendorclose",    AutoVendorCloseManager.Enabled);
            Row("keyword",        JournalKeywordToastManager.Enabled);

            GameActions.Print("--- overlays ---", 0x35);
            Row("lowhp",          UI.LowHpVignette.Enabled);
            Row("mobhp range",    UI.CombatMobHpBars.Range > 0);
            Row("hidetrash",      UI.HideTrashOverlay.Range > 0);
            Row("offscreenarrow", UI.OffscreenEnemyArrow.Enabled);
            Row("castbar",        UI.CastProgressOverlay.Enabled);
            Row("cdhud",          UI.CooldownHud.Enabled);
            Row("trail",          UI.MoveTrailOverlay.Enabled);
            Row("compass",        UI.CompassOverlay.Enabled);
            Row("hitflash",       UI.ScreenflashOnHit.Enabled);
            Row("tgtcross",       UI.TargetCrosshair.Enabled);
            Row("tilegrid r",     UI.TileGridOverlay.Range > 0);
            GameActions.Print("---------------------", 0x35);
        }

        private static void Row(string name, bool on)
        {
            GameActions.Print($"  {(on ? "[X]" : "[ ]")} {name}", (ushort)(on ? 0x44 : 0x21));
        }
    }
}
