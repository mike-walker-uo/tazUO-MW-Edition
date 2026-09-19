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
            public readonly Func<bool> IsActive;
            public Feature(string name, Action tick, Func<bool> isActive = null)
            {
                Name = name;
                Tick = tick;
                IsActive = isActive;
            }
        }

        private static readonly Feature[] _features =
        {
            new Feature("BandageSettings", BandageSettings.Tick),
            new Feature("FeatureSettings", TazUOFeatureSettings.Tick),
            // Priority order for competing automatic actions: emergency care,
            // cure/heal consumables, cure spell, refresh, then bandages.
            new Feature("EmergencyHeal", EmergencyHealManager.Tick, () => EmergencyHealManager.Enabled),
            new Feature("AutoCurePotion", AutoCurePotionManager.Tick, () => AutoCurePotionManager.Enabled),
            new Feature("AutoHealPotion", AutoHealPotionManager.Tick, () => AutoHealPotionManager.Enabled),
            new Feature("PoisonCure", PoisonCureManager.Tick, () => PoisonCureManager.Enabled),
            new Feature("AutoRefreshPotion", AutoRefreshPotionManager.Tick, () => AutoRefreshPotionManager.Enabled),
            // These two must keep polling while off because skill thresholds
            // can auto-enable them after the server sends skill data.
            new Feature("AutoBandage", AutoBandageManager.Tick),
            new Feature("PetBandage", PetBandageManager.Tick),
            new Feature("BandageStockWarn", BandageStockWarner.Tick, () => BandageStockWarner.Enabled),
            new Feature("ExternalBandage", ExternalBandageManager.Tick, () => ExternalBandageManager.Enabled),
            new Feature("ReagentWatcher", ReagentWatcherManager.Tick, () => ReagentWatcherManager.Enabled),
            new Feature("AggroIndicator", AggroIndicatorManager.Tick, () => AggroIndicatorManager.HasAggressors),
            new Feature("PetWatcher", PetWatcherManager.Tick, () => PetWatcherManager.Enabled),
            new Feature("PetLoyalty", PetLoyaltyAlertManager.Tick, () => PetLoyaltyAlertManager.Enabled),
            new Feature("AutoRespawnTarget", AutoRespawnTargetManager.Tick, () => AutoRespawnTargetManager.Enabled),
            new Feature("CrashRecovery", CrashRecoveryManager.Tick),
            new Feature("WaypointRecorder", WaypointRecorder.Tick, () => WaypointRecorder.Recording || WaypointRecorder.Playing),
            new Feature("DeathMarker", DeathMarkerManager.Tick, () => DeathMarkerManager.Enabled),
            new Feature("AutoStealth", AutoStealthManager.Tick, () => AutoStealthManager.Enabled),
            new Feature("TargetRangeWarn", TargetRangeWarnManager.Tick, () => TargetRangeWarnManager.Enabled),
            new Feature("AutoHitList", AutoHitListManager.Tick, () => AutoHitListManager.Enabled),
            new Feature("AutoBuff", AutoBuffManager.Tick, () => AutoBuffManager.Enabled),
            new Feature("AutoMount", AutoMountManager.Tick, () => AutoMountManager.Enabled),
            new Feature("PoisonAlert", PoisonAlertManager.Tick, () => PoisonAlertManager.Enabled),
            new Feature("BandageTimer", BandageTimerManager.Tick, () => BandageTimerManager.Enabled),
            new Feature("ManaStamAlert", ManaStamAlertManager.Tick, () => ManaStamAlertManager.ManaPct > 0 || ManaStamAlertManager.StamPct > 0),
            new Feature("AutoRearm", AutoRearmManager.Tick, () => AutoRearmManager.Enabled),
            new Feature("AutoFollow", AutoFollowManager.Tick, () => AutoFollowManager.Active),
            new Feature("StatChangeAlert", StatChangeAlertManager.Tick, () => StatChangeAlertManager.Enabled),
            new Feature("DeathRecap", DeathRecapManager.Tick, () => DeathRecapManager.Enabled),
            new Feature("AutoCloseEmptyCorpse", AutoCloseEmptyCorpse.Tick, () => AutoCloseEmptyCorpse.Enabled),
            new Feature("InventoryFullWarn", InventoryFullWarner.Tick, () => InventoryFullWarner.ThresholdPct > 0),
            new Feature("AutoOpenBackpack", AutoOpenBackpackManager.Tick, () => AutoOpenBackpackManager.Enabled),
            new Feature("TargetingHistory", TargetingHistoryManager.Tick),
            new Feature("BossHealthBar", UI.Gumps.BossHealthBarGump.AutoTick),
            new Feature("IdleMonitor", IdleMonitorManager.Tick, () => IdleMonitorManager.IdleSeconds > 0),
            new Feature("AutoVendorClose", AutoVendorCloseManager.Tick, () => AutoVendorCloseManager.Enabled),
            new Feature("CombatState", CombatStateManager.Tick, () => CombatStateManager.Enabled),
            new Feature("AutoOpenPaperdoll", AutoOpenPaperdollManager.Tick, () => AutoOpenPaperdollManager.Enabled),
            new Feature("CountdownTimer", CountdownTimerManager.Tick, () => CountdownTimerManager.HasPending),
            new Feature("StartupBanner", StartupBannerManager.Tick, () => StartupBannerManager.Enabled),
            new Feature("NotorietyWatcher", NotorietyChangeWatcher.Tick, () => NotorietyChangeWatcher.Enabled),
            new Feature("HiddenWatcher", HiddenStateWatcher.Tick, () => HiddenStateWatcher.Enabled),
            new Feature("WindowFocusMute", WindowFocusMuteManager.Tick, () => WindowFocusMuteManager.Enabled),
            new Feature("DeathLog", DeathLogManager.Tick, () => DeathLogManager.Enabled),
            new Feature("PingSpikeWarn", PingSpikeWarner.Tick, () => PingSpikeWarner.SpikeMs > 0),
            new Feature("JournalOpener", JournalOpenerManager.Tick, () => JournalOpenerManager.AutoOnDeath),
            new Feature("HostileLoiter", HostileLoiteringWarner.Tick, () => HostileLoiteringWarner.Enabled),
            new Feature("FullHpToast", FullHpToastManager.Tick, () => FullHpToastManager.Enabled),
            new Feature("AutoStopOnDeath", AutoStopOnDeathManager.Tick, () => AutoStopOnDeathManager.Enabled),
            new Feature("ParagonGlow", ParagonGlowManager.Tick, () => ParagonGlowManager.Enabled),
            new Feature("MobBlood", UI.MobBloodOverlay.Tick, () => UI.MobBloodOverlay.Enabled),
            new Feature("GhostFade", UI.GhostFadeOverlay.Tick, () => UI.GhostFadeOverlay.Enabled),
            new Feature("SpawnTimer", SpawnTimerManager.Tick, () => SpawnTimerManager.HasPending),
            new Feature("AmbientWeather", AmbientWeatherManager.Tick, () => AmbientWeatherManager.Enabled),
            new Feature("Ambience", UI.AmbienceOverlay.Tick, () => UI.AmbienceOverlay.Enabled)
        };

        private static long _nextTick;

        public static void Tick()
        {
            if (Time.Ticks < _nextTick) return;
            _nextTick = (long)Time.Ticks + INTERVAL_MS;
            foreach (Feature feature in _features)
            {
                if (feature.IsActive != null && !feature.IsActive()) continue;
                FeatureDiagnostics.Guard(feature.Name, feature.Tick);
            }
        }

        public static void Reset()
        {
            _nextTick = 0;
            UI.Gumps.BossHealthBarGump.ResetAuto();
        }
    }
}
