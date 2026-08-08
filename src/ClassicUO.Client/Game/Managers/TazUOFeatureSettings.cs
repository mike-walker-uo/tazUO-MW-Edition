#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Persists lightweight feature toggles that predate Profile properties.
    /// Explicit bindings keep the store limited to user-facing TazUO options.
    /// </summary>
    public static class TazUOFeatureSettings
    {
        private const string FILENAME = "tazuo_features.tsv";

        private sealed class Binding
        {
            public string Key;
            public FieldInfo Field;
            public PropertyInfo Property;
            public object DefaultValue;
            public Type ValueType;

            public object Get() => Field != null ? Field.GetValue(null) : Property.GetValue(null, null);
            public void Set(object value)
            {
                if (Field != null) Field.SetValue(null, value);
                else Property.SetValue(null, value, null);
            }
        }

        private static readonly List<Binding> _bindings = new List<Binding>();
        private static readonly Dictionary<string, Binding> _byKey =
            new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);
        private static long _nextAutosave;

        static TazUOFeatureSettings()
        {
            BindEnabled(
                typeof(AfkReplyManager), typeof(ArrowGlowManager), typeof(AutoBuffManager),
                typeof(AutoCloseEmptyCorpse), typeof(AutoCurePotionManager), typeof(AutoHealPotionManager),
                typeof(AutoHitListManager), typeof(AutoMountManager), typeof(AutoOpenBackpackManager),
                typeof(AutoOpenPaperdollManager), typeof(AutoRearmManager), typeof(AutoRefreshPotionManager),
                typeof(AutoRespawnTargetManager), typeof(AutoSayThanksManager), typeof(AutoStealthManager),
                typeof(AutoStopOnDeathManager), typeof(AutoVendorCloseManager), typeof(BandageTimerManager),
                typeof(BuffExpiryToastManager), typeof(CombatStateManager), typeof(DamageTypeTagManager),
                typeof(DeathLogManager), typeof(DeathMarkerManager), typeof(DeathRecapManager),
                typeof(EmergencyHealManager), typeof(EventSinkLogger), typeof(FullHpToastManager),
                typeof(HiddenStateWatcher), typeof(HostileLoiteringWarner), typeof(HungerThirstAlertManager),
                typeof(IdleMonitorManager), typeof(ItemDropSoundManager), typeof(JournalKeywordToastManager),
                typeof(KillReadyManager), typeof(NotorietyChangeWatcher), typeof(ParagonGlowManager),
                typeof(PartyInviteAlertManager), typeof(PetLoyaltyAlertManager), typeof(PetWatcherManager),
                typeof(PoisonAlertManager), typeof(PoisonCureManager), typeof(ReagentWatcherManager),
                typeof(ReflectCounterManager), typeof(SkillCapTracker), typeof(StartupBannerManager),
                typeof(StatChangeAlertManager), typeof(SystemMessageMuteManager), typeof(TargetRangeWarnManager),
                typeof(WindowFocusMuteManager),

                typeof(UI.CastProgressOverlay), typeof(UI.CompactBarsOverlay), typeof(UI.CompactPartyHud),
                typeof(UI.CompassOverlay), typeof(UI.CooldownHud), typeof(UI.CorpseFadeOverlay),
                typeof(UI.CursorDistanceOverlay), typeof(UI.CursorHintOverlay), typeof(UI.DamageSourceLineOverlay),
                typeof(UI.DismountTiltOverlay), typeof(UI.GhostFadeOverlay), typeof(UI.HealReceivedPulse),
                typeof(UI.HostileEdgeHighlight), typeof(UI.LastTargetHighlight), typeof(UI.LowHpScreenTint),
                typeof(UI.LowHpVignette), typeof(UI.MobBloodOverlay), typeof(UI.MoveTrailOverlay),
                typeof(UI.NearestHostileLine), typeof(UI.NotorietyDotOverlay), typeof(UI.OffscreenEnemyArrow),
                typeof(UI.OpenContainerBadge), typeof(UI.PetHpBarsOverlay), typeof(UI.PingHudOverlay),
                typeof(UI.QuestArrowLine), typeof(UI.ScreenflashOnHit), typeof(UI.TargetCrosshair),
                typeof(UI.TargetingYouAura), typeof(UI.TileHoverLiftOverlay), typeof(UI.TimerStackHud),
                typeof(UI.WarModeBorder), typeof(UI.WindParticlesOverlay)
            );

            Bind(typeof(AggroIndicatorManager), "DrawEnabled");
            Bind(typeof(AggroIndicatorManager), "PlayAlertSound");
            Bind(typeof(AfkReplyManager), "Message");
            Bind(typeof(ArrowGlowManager), "ArrowScale");
            Bind(typeof(ArrowGlowManager), "GlowHue");
            Bind(typeof(ArrowGlowManager), "GlowGraphic");
            Bind(typeof(AutoBuffManager), "SpellName");
            Bind(typeof(AutoBuffManager), "WatchLabel");
            Bind(typeof(AutoCloseEmptyCorpse), "CloseDelayMs");
            Bind(typeof(AutoFollowManager), "KeepDistance");
            Bind(typeof(AutoHealPotionManager), "ThresholdPct");
            Bind(typeof(AutoHitListManager), "AlsoAttack");
            Bind(typeof(AutoMountManager), "MountSerial");
            Bind(typeof(AutoRearmManager), "OneHandedSerial");
            Bind(typeof(AutoRearmManager), "TwoHandedSerial");
            Bind(typeof(AutoRefreshPotionManager), "ThresholdPct");
            Bind(typeof(AutoRespawnTargetManager), "AlsoAttack");
            Bind(typeof(AutoSayThanksManager), "Phrase");
            Bind(typeof(AutoVendorCloseManager), "MaxDistance");
            Bind(typeof(ClipboardSayManager), "MaxLength");
            Bind(typeof(CombatStateManager), "OutOfCombatMs");
            Bind(typeof(DeathRecapManager), "WindowSeconds");
            Bind(typeof(EmergencyHealManager), "ThresholdPct");
            Bind(typeof(EmergencyHealManager), "SpellName");
            Bind(typeof(HostileLoiteringWarner), "Range");
            Bind(typeof(HostileLoiteringWarner), "DwellSeconds");
            Bind(typeof(IdleMonitorManager), "IdleSeconds");
            Bind(typeof(InventoryFullWarner), "ThresholdPct");
            Bind(typeof(ItemDropSoundManager), "Range");
            Bind(typeof(JournalOpenerManager), "AutoOnDeath");
            Bind(typeof(KillReadyManager), "ThresholdPct");
            Bind(typeof(ManaStamAlertManager), "ManaPct");
            Bind(typeof(ManaStamAlertManager), "StamPct");
            Bind(typeof(ParagonGlowManager), "GlowHue");
            Bind(typeof(ParagonGlowManager), "GlowGraphic");
            Bind(typeof(PetWatcherManager), "HpThresholdPercent");
            Bind(typeof(PetWatcherManager), "DistanceWarn");
            Bind(typeof(PingSpikeWarner), "SpikeMs");
            Bind(typeof(PoisonCureManager), "SpellName");
            Bind(typeof(ReagentWatcherManager), "Threshold");
            Bind(typeof(SkillCapTracker), "WarnDelta");
            Bind(typeof(TargetRangeWarnManager), "WarnDistance");
            Bind(typeof(UI.CombatMobHpBars), "Range");
            Bind(typeof(UI.GroundLootFinder), "Range");
            Bind(typeof(UI.HideTrashOverlay), "Range");
            Bind(typeof(UI.RangeIndicator), "Range");
            Bind(typeof(UI.TileGridOverlay), "Range");
        }

        private static void BindEnabled(params Type[] types)
        {
            foreach (Type type in types) Bind(type, "Enabled");
        }

        private static void Bind(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            FieldInfo field = type.GetField(memberName, flags);
            PropertyInfo property = field == null ? type.GetProperty(memberName, flags) : null;
            if (field == null && (property == null || !property.CanRead || !property.CanWrite)) return;

            var binding = new Binding
            {
                Key = type.FullName + "." + memberName,
                Field = field,
                Property = property,
                ValueType = field != null ? field.FieldType : property.PropertyType
            };
            binding.DefaultValue = binding.Get();
            _bindings.Add(binding);
            _byKey[binding.Key] = binding;
        }

        public static void Load()
        {
            ResetToDefaults();

            foreach (string line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(new[] { '\t' }, 2);
                if (parts.Length != 2 || !_byKey.TryGetValue(parts[0], out Binding binding)) continue;

                try
                {
                    object value = ConvertValue(parts[1], binding.ValueType);
                    binding.Set(value);
                }
                catch (Exception ex)
                {
                    FeatureDiagnostics.RecordFailure("FeatureSettings:" + parts[0], ex);
                }
            }
        }

        public static void Save()
        {
            var lines = new List<string>(_bindings.Count);
            foreach (Binding binding in _bindings)
            {
                object value = binding.Get();
                lines.Add(binding.Key + "\t" + SerializeValue(value, binding.ValueType));
            }
            lines.Sort(StringComparer.Ordinal);
            ProfileDataStore.WriteAllLines(FILENAME, lines);
        }

        public static void Tick()
        {
            if (Time.Ticks < _nextAutosave) return;
            _nextAutosave = (long)Time.Ticks + 30_000;
            Save();
        }

        public static void ResetToDefaults()
        {
            foreach (Binding binding in _bindings) binding.Set(binding.DefaultValue);
            _nextAutosave = 0;
        }

        private static object ConvertValue(string text, Type type)
        {
            if (type == typeof(bool)) return bool.Parse(text);
            if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(uint)) return uint.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(string))
            {
                if (!text.StartsWith("b64:", StringComparison.Ordinal)) return text;
                return Encoding.UTF8.GetString(Convert.FromBase64String(text.Substring(4)));
            }
            if (type.IsEnum) return Enum.Parse(type, text, true);
            return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        }

        private static string SerializeValue(object value, Type type)
        {
            if (type == typeof(string))
                return "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes((string)value ?? string.Empty));
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
