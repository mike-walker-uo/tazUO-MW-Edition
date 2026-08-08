#region license
// TazUO addition.
#endregion

using System;

namespace ClassicUO.Game.Managers
{
    /// <summary>Runs optional feature polling at 5 Hz behind a shared fault boundary.</summary>
    public static class TazUOManagerScheduler
    {
        private const long INTERVAL_MS = 200;

        private sealed class Feature
        {
            public readonly string Name;
            public readonly Action Tick;
            public Feature(string name, Action tick) { Name = name; Tick = tick; }
        }

        private static readonly Feature[] _features =
        {
            new Feature("BandageSettings", BandageSettings.Tick),
            new Feature("FeatureSettings", TazUOFeatureSettings.Tick),
            // Priority order for competing automatic actions: emergency care,
            // cure/heal consumables, cure spell, refresh, then bandages.
            new Feature("EmergencyHeal", EmergencyHealManager.Tick),
            new Feature("AutoCurePotion", AutoCurePotionManager.Tick),
            new Feature("AutoHealPotion", AutoHealPotionManager.Tick),
            new Feature("PoisonCure", PoisonCureManager.Tick),
            new Feature("AutoRefreshPotion", AutoRefreshPotionManager.Tick),
            new Feature("AutoBandage", AutoBandageManager.Tick),
            new Feature("PetBandage", PetBandageManager.Tick),
            new Feature("BandageStockWarn", BandageStockWarner.Tick),
            new Feature("ExternalBandage", ExternalBandageManager.Tick),
            new Feature("ReagentWatcher", ReagentWatcherManager.Tick),
            new Feature("AggroIndicator", AggroIndicatorManager.Tick),
            new Feature("PetWatcher", PetWatcherManager.Tick),
            new Feature("AutoRespawnTarget", AutoRespawnTargetManager.Tick),
            new Feature("CrashRecovery", CrashRecoveryManager.Tick),
            new Feature("WaypointRecorder", WaypointRecorder.Tick),
            new Feature("DeathMarker", DeathMarkerManager.Tick),
            new Feature("AutoStealth", AutoStealthManager.Tick),
            new Feature("TargetRangeWarn", TargetRangeWarnManager.Tick),
            new Feature("AutoHitList", AutoHitListManager.Tick),
            new Feature("AutoBuff", AutoBuffManager.Tick),
            new Feature("AutoMount", AutoMountManager.Tick),
            new Feature("PoisonAlert", PoisonAlertManager.Tick),
            new Feature("BandageTimer", BandageTimerManager.Tick),
            new Feature("ManaStamAlert", ManaStamAlertManager.Tick),
            new Feature("AutoRearm", AutoRearmManager.Tick),
            new Feature("AutoFollow", AutoFollowManager.Tick),
            new Feature("StatChangeAlert", StatChangeAlertManager.Tick),
            new Feature("DeathRecap", DeathRecapManager.Tick),
            new Feature("AutoCloseEmptyCorpse", AutoCloseEmptyCorpse.Tick),
            new Feature("InventoryFullWarn", InventoryFullWarner.Tick),
            new Feature("AutoOpenBackpack", AutoOpenBackpackManager.Tick),
            new Feature("TargetingHistory", TargetingHistoryManager.Tick),
            new Feature("IdleMonitor", IdleMonitorManager.Tick),
            new Feature("AutoVendorClose", AutoVendorCloseManager.Tick),
            new Feature("CombatState", CombatStateManager.Tick),
            new Feature("AutoOpenPaperdoll", AutoOpenPaperdollManager.Tick),
            new Feature("CountdownTimer", CountdownTimerManager.Tick),
            new Feature("StartupBanner", StartupBannerManager.Tick),
            new Feature("NotorietyWatcher", NotorietyChangeWatcher.Tick),
            new Feature("HiddenWatcher", HiddenStateWatcher.Tick),
            new Feature("WindowFocusMute", WindowFocusMuteManager.Tick),
            new Feature("DeathLog", DeathLogManager.Tick),
            new Feature("PingSpikeWarn", PingSpikeWarner.Tick),
            new Feature("JournalOpener", JournalOpenerManager.Tick),
            new Feature("HostileLoiter", HostileLoiteringWarner.Tick),
            new Feature("FullHpToast", FullHpToastManager.Tick),
            new Feature("AutoStopOnDeath", AutoStopOnDeathManager.Tick),
            new Feature("ParagonGlow", ParagonGlowManager.Tick),
            new Feature("MobBlood", UI.MobBloodOverlay.Tick),
            new Feature("GhostFade", UI.GhostFadeOverlay.Tick),
            new Feature("SpawnTimer", SpawnTimerManager.Tick),
            new Feature("AmbientWeather", AmbientWeatherManager.Tick),
            new Feature("Ambience", UI.AmbienceOverlay.Tick)
        };

        private static long _nextTick;

        public static void Tick()
        {
            if (Time.Ticks < _nextTick) return;
            _nextTick = (long)Time.Ticks + INTERVAL_MS;
            foreach (Feature feature in _features)
                FeatureDiagnostics.Guard(feature.Name, feature.Tick);
        }

        public static void Reset() => _nextTick = 0;
    }
}
