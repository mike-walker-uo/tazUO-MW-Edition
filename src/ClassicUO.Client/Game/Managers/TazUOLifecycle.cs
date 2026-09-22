#region license
// TazUO addition.
#endregion

using System;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>Owns TazUO profile/session state across relog and character switches.</summary>
    public static class TazUOLifecycle
    {
        private static string _activeProfilePath;

        public static void OnSceneLoad()
        {
            string path = ProfileManager.ProfilePath;
            if (!string.IsNullOrEmpty(_activeProfilePath) &&
                string.Equals(_activeProfilePath, path, StringComparison.OrdinalIgnoreCase)) return;

            ResetState();
            _activeProfilePath = path;
            ApplyProfileMigrations(ProfileManager.CurrentProfile);
            WaterEnhancementManager.ApplyProfile(ProfileManager.CurrentProfile);
            TerrainMaterialManager.ApplyProfile(ProfileManager.CurrentProfile);
            TazUOFeatureSettings.Load();
            ApplyFootstepVisualDefaults(ProfileManager.CurrentProfile);
            ApplyCombatOverlayDefaults(ProfileManager.CurrentProfile);
            AutomationCoordinator.ResetForProfile(ProfileManager.CurrentProfile?.AutomationEnabled ?? true);
            BandageSettings.EnsureLoaded();
            CommandAliasManager.Load();
            CommandMetadata.Synchronize(CommandManager.Commands.Keys);
            PinnedCommandManager.Load();
        }

        internal static void ApplyProfileMigrations(Profile profile)
        {
            if (profile == null) return;

            if (profile.NativeGlobalChatPreferenceVersion < 1)
            {
                // The replacement historically defaulted on. Version 0 profiles
                // include the short-lived opt-in default and must be restored.
                profile.UseNativeGlobalChatReplacement = true;
                profile.NativeGlobalChatPreferenceVersion = 1;
            }

            if (profile.WaterStyleDefaultsVersion < 1)
            {
                // Move only the former default to Storm Swell. Preserve every
                // other explicitly selected water style.
                if (profile.EnhancedWaterStyle == WaterEnhancementManager.WATER_STYLE_NATURAL)
                    profile.EnhancedWaterStyle = WaterEnhancementManager.WATER_STYLE_STORM;

                profile.WaterStyleDefaultsVersion = 1;
            }

            if (profile.WaterIntensityDefaultsVersion < 1)
            {
                // Move only the former 100% default to 200%. Preserve every
                // other explicitly selected intensity.
                if (profile.WaterMaterialIntensity == 100)
                    profile.WaterMaterialIntensity =
                        WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT;

                profile.WaterIntensityDefaultsVersion = 1;
            }
        }

        internal static void ApplyCombatOverlayDefaults(Profile profile)
        {
            if (profile == null || profile.CombatOverlayDefaultsVersion >= 3) return;

            if (profile.CombatOverlayDefaultsVersion < 1)
            {
                UI.EffectsBundle.Crit = false;
                UI.TargetingYouAura.Enabled = false;
            }

            if (profile.CombatOverlayDefaultsVersion < 2)
            {
                UI.DamageSourceLineOverlay.Enabled = false;
                UI.NearestHostileLine.Enabled = false;
            }

            UI.HealReceivedPulse.Enabled = false;
            profile.CombatOverlayDefaultsVersion = 3;
        }

        internal static void ApplyFootstepVisualDefaults(Profile profile)
        {
            if (profile == null || profile.FootstepVisualDefaultsVersion >= 1) return;

            // The former defaults were persisted as enabled in existing profiles.
            // Reset them once so upgrades receive the new blood-free defaults.
            UI.MoveTrailOverlay.Enabled = false;
            UI.MoveTrailOverlay.ResetSession();
            UI.MobBloodOverlay.Enabled = false;
            UI.MobBloodOverlay.ResetSession();
            profile.BloodDecalsEnabled = false;
            profile.FootstepGraphicsEnabled = true;
            profile.FootstepVisualDefaultsVersion = 1;
        }

        public static void OnSceneUnload()
        {
            TazUOFeatureSettings.Save();
            BandageSettings.Save();
            ResetState();
            _activeProfilePath = null;
        }

        private static void ResetState()
        {
            BodyScaleManager.ResetForProfile();
            BodyHueManager.ResetForProfile();
            SpellAbilityEffectSettings.ResetForProfile();
            BandageSettings.ResetForProfile();
            QuickLoadoutManager.ResetForProfile();
            CheckpointManager.ResetForProfile();
            GuildHueMap.ResetForProfile();
            JournalKeywordToastManager.ResetForProfile();
            SnippetManager.ResetForProfile();
            TrashContainerManager.ResetForProfile();
            NotesManager.ResetForProfile();
            JournalPersistence.ResetForProfile();
            PinnedSerialManager.ResetForProfile();
            AutoHitListManager.ResetForProfile();
            SystemMessageMuteManager.ResetForProfile();
            CommandAliasManager.ResetForProfile();
            PinnedCommandManager.ResetForProfile();
            CrashRecoveryManager.ResetForProfile();
            AlertCenterManager.ResetForProfile();
            UI.Gumps.ToastManager.ResetForProfile();

            AutoBandageManager.ResetForProfile();
            PetBandageManager.ResetForProfile();
            ExternalBandageManager.ResetForProfile();
            BandageStockWarner.ResetForProfile();
            BandageScheduler.ResetForProfile();
            AutomationCoordinator.ResetForProfile(true);

            CommandHistoryManager.Clear();
            CombatLogManager.Clear();
            DamageSessionTracker.Reset();
            LootHistoryManager.Clear();
            SkillGainTracker.Reset();
            ReflectCounterManager.ResetSessionQuiet();
            DamageTypeTagManager.ResetSession();
            AggroIndicatorManager.ResetSession();
            PetWatcherManager.ResetSession();
            TargetingHistoryManager.ResetSession();
            DeathMarkerManager.ResetSession();
            WaypointRecorder.ResetSession();
            AfkReplyManager.ResetSession();
            AutoFollowManager.ResetSession();
            AutoCloseEmptyCorpse.ResetSession();
            AutoMountManager.ResetSession();
            AutoOpenBackpackManager.ResetSession();
            AutoOpenPaperdollManager.ResetSession();
            AutoRearmManager.ResetSession();
            AutoRespawnTargetManager.ResetSession();
            AutoSayThanksManager.ResetSession();
            AutoStealthManager.ResetSession();
            AutoStopOnDeathManager.ResetSession();
            AutoVendorCloseManager.ResetSession();
            CombatStateManager.ResetSession();
            DeathLogManager.ResetSession();
            DeathRecapManager.ResetSession();
            FullHpToastManager.ResetSession();
            HiddenStateWatcher.ResetSession();
            HostileLoiteringWarner.ResetSession();
            HungerThirstAlertManager.ResetSession();
            IdleMonitorManager.ResetSession();
            JournalOpenerManager.ResetSession();
            ManaStamAlertManager.ResetSession();
            NotorietyChangeWatcher.ResetSession();
            PetGuardTintManager.ResetSession();
            PetLoyaltyAlertManager.ResetSession();
            PingSpikeWarner.ResetSession();
            PoisonAlertManager.ResetSession();
            StatChangeAlertManager.ResetSession();
            UI.HealReceivedPulse.ResetSession();
            UI.OnslaughtDeliveryEffect.ResetSession();
            UI.ConsecrateWeaponEffect.ResetSession();
            UI.DivineFuryEffect.ResetSession();
            UI.SacredJourneyEffect.ResetSession();
            UI.AbilityOverheadEffect.ResetSession();
            UI.LegendaryCreatureAlert.ResetSession();
            ClassicUO.Game.GameObjects.EnhancedSpellVisualTrigger.ResetSession();
            UI.MobileCache.Clear();
            UI.EffectsBundle.ResetSession();
            UI.GhostFadeOverlay.ResetSession();
            UI.MobBloodOverlay.ResetSession();
            UI.MoveTrailOverlay.ResetSession();
            UI.DamageSourceLineOverlay.ResetSession();
            UI.CorpseFadeOverlay.ResetSession();
            UI.Gumps.GlobalChatHistory.Instance.Clear();
            UI.Gumps.GuildChatHistory.Instance.Clear();
            UI.Gumps.NearbySpeechHistory.Instance.Clear();

            TazUOFeatureSettings.ResetToDefaults();
            DayCyclePreviewManager.ResetSession();
            EnvironmentShowcaseManager.ResetSession();
            EnvironmentControlManager.ResetSession();
            SceneryInteractionManager.ResetSession();
            TerrainMaterialManager.ResetSession();
            WaterEnhancementManager.ResetSession();
            TazUOManagerScheduler.Reset();
        }
    }
}
