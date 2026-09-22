// TazUO addition: profile-scoped alert history, filtering and notification policy.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    public enum AlertCategory
    {
        General,
        Equipment,
        Pets,
        Creatures,
        Inventory,
        Supplies,
        Journal,
        Combat,
        System
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    internal static class AlertCenterManager
    {
        private const string FILE_NAME = "AlertCenter.json";
        private const int MAX_HISTORY = 200;
        private const long SAVE_DELAY_MS = 2000;
        private const long COALESCE_TICKS = TimeSpan.TicksPerSecond * 5;

        private static AlertCenterState _state = new AlertCenterState();
        private static bool _loaded;
        private static bool _dirty;
        private static long _nextId = 1;
        private static long _saveAt;

        internal static IReadOnlyList<AlertCenterEntry> History
        {
            get
            {
                EnsureLoaded();
                return _state.History;
            }
        }

        internal static IReadOnlyList<string> MutedSources
        {
            get { EnsureLoaded(); return _state.MutedSources; }
        }

        internal static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _state = new AlertCenterState();

            try
            {
                string json = ProfileDataStore.ReadAllText(FILE_NAME);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    _state = JsonSerializer.Deserialize(
                        json,
                        AlertCenterJsonContext.Default.AlertCenterState
                    ) ?? new AlertCenterState();
                }
            }
            catch (Exception ex)
            {
                FeatureDiagnostics.RecordFailure("AlertCenter:load", ex);
                _state = new AlertCenterState();
            }

            _state.History ??= new List<AlertCenterEntry>();
            _state.MutedCategories ??= new List<string>();
            _state.MutedSources ??= new List<string>();
            _state.SnoozedCategories ??= new Dictionary<string, long>();
            _state.CategorySeverityOverrides ??= new Dictionary<string, int>();

            foreach (AlertCenterEntry entry in _state.History)
            {
                entry.Active = false;
            }

            if (_state.History.Count > MAX_HISTORY)
            {
                _state.History = _state.History
                    .OrderByDescending(entry => entry.UpdatedUtcTicks)
                    .Take(MAX_HISTORY)
                    .ToList();
            }

            _nextId = _state.History.Count == 0
                ? 1
                : _state.History.Max(entry => entry.Id) + 1;
            RemoveExpiredSnoozes();
        }

        internal static bool RecordToast(
            string sourceKey,
            string text,
            ushort hue,
            bool persistent,
            AlertCategory? suppliedCategory = null,
            AlertSeverity? suppliedSeverity = null)
        {
            EnsureLoaded();

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            sourceKey = NormalizeSourceKey(sourceKey, text);
            AlertCategory category = suppliedCategory ?? InferCategory(sourceKey, text);
            AlertSeverity severity = EffectiveSeverity(category,
                suppliedSeverity ?? InferSeverity(text, hue));
            bool suppressed = IsMuted(category) || IsSourceMuted(sourceKey) || IsSnoozed(category);
            long now = DateTime.UtcNow.Ticks;
            AlertCenterEntry entry = null;

            if (persistent)
            {
                entry = _state.History.LastOrDefault(item =>
                    item.Active
                    && string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                entry = _state.History.LastOrDefault(item =>
                    !item.Persistent
                    && string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Text, text, StringComparison.Ordinal)
                    && now - item.UpdatedUtcTicks <= COALESCE_TICKS);
            }

            if (entry == null)
            {
                entry = new AlertCenterEntry
                {
                    Id = _nextId++,
                    CreatedUtcTicks = now,
                    Occurrences = 1
                };
                _state.History.Add(entry);
            }
            else
            {
                entry.Occurrences++;
            }

            entry.SourceKey = sourceKey;
            entry.Text = text.Trim();
            entry.Category = category.ToString();
            entry.Severity = (int)severity;
            entry.UpdatedUtcTicks = now;
            entry.Persistent = persistent;
            entry.Active = persistent && !suppressed;
            entry.Suppressed = suppressed;

            TrimHistory();
            MarkDirty();
            NotifyChanged();
            return !suppressed;
        }

        internal static void Dismiss(AlertCenterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            EnsureLoaded();
            bool wasActive = entry.Active;
            entry.Active = false;

            if (!wasActive)
            {
                _state.History.Remove(entry);
            }

            MarkDirty();

            if (wasActive && entry.Persistent && !string.IsNullOrEmpty(entry.SourceKey))
            {
                ToastManager.DismissByKey(entry.SourceKey, false);
            }

            NotifyChanged();
        }

        internal static void DismissBySource(string sourceKey)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(sourceKey))
            {
                return;
            }

            bool changed = false;

            foreach (AlertCenterEntry entry in _state.History)
            {
                if (entry.Active
                    && string.Equals(entry.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Active = false;
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirty();
                NotifyChanged();
            }
        }

        internal static void ClearHistory()
        {
            EnsureLoaded();
            _state.History.RemoveAll(entry => !entry.Active);
            MarkDirty();
            NotifyChanged();
        }

        internal static bool IsMuted(AlertCategory category)
        {
            EnsureLoaded();
            return _state.MutedCategories.Any(value =>
                string.Equals(value, category.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        internal static void SetMuted(AlertCategory category, bool muted)
        {
            EnsureLoaded();
            string name = category.ToString();
            _state.MutedCategories.RemoveAll(value =>
                string.Equals(value, name, StringComparison.OrdinalIgnoreCase));

            if (muted)
            {
                _state.MutedCategories.Add(name);
                DeactivateCategory(category);
            }

            MarkDirty();
            NotifyChanged();
        }

        internal static bool IsSourceMuted(string sourceKey)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(sourceKey) && _state.MutedSources.Any(value =>
                string.Equals(value, sourceKey, StringComparison.OrdinalIgnoreCase));
        }

        internal static void SetSourceMuted(string sourceKey, bool muted)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(sourceKey)) return;
            _state.MutedSources.RemoveAll(value =>
                string.Equals(value, sourceKey, StringComparison.OrdinalIgnoreCase));
            if (muted) _state.MutedSources.Add(sourceKey);
            foreach (AlertCenterEntry entry in _state.History.Where(entry =>
                string.Equals(entry.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)))
            {
                entry.Suppressed = muted;
                if (muted) entry.Active = false;
            }
            if (muted) ToastManager.DismissByKey(sourceKey, false);
            MarkDirty();
            NotifyChanged();
        }

        internal static bool IsSnoozed(AlertCategory category)
        {
            EnsureLoaded();
            string name = category.ToString();

            if (!_state.SnoozedCategories.TryGetValue(name, out long until))
            {
                return false;
            }

            if (until > DateTime.UtcNow.Ticks)
            {
                return true;
            }

            _state.SnoozedCategories.Remove(name);
            MarkDirty();
            return false;
        }

        internal static TimeSpan SnoozeRemaining(AlertCategory category)
        {
            EnsureLoaded();
            string name = category.ToString();
            return _state.SnoozedCategories.TryGetValue(name, out long until)
                ? TimeSpan.FromTicks(Math.Max(0, until - DateTime.UtcNow.Ticks))
                : TimeSpan.Zero;
        }

        internal static void Snooze(AlertCategory category, int minutes)
        {
            EnsureLoaded();
            string name = category.ToString();

            if (minutes <= 0)
            {
                _state.SnoozedCategories.Remove(name);
            }
            else
            {
                _state.SnoozedCategories[name] = DateTime.UtcNow.AddMinutes(minutes).Ticks;
                DeactivateCategory(category);
            }

            MarkDirty();
            NotifyChanged();
        }

        internal static int SeverityOverride(AlertCategory category)
        {
            EnsureLoaded();
            return _state.CategorySeverityOverrides.TryGetValue(category.ToString(), out int value)
                ? value
                : -1;
        }

        internal static int CycleSeverityOverride(AlertCategory category)
        {
            EnsureLoaded();
            string name = category.ToString();
            int current = SeverityOverride(category);
            int next = current >= (int)AlertSeverity.Critical ? -1 : current + 1;

            if (next < 0)
            {
                _state.CategorySeverityOverrides.Remove(name);
            }
            else
            {
                _state.CategorySeverityOverrides[name] = next;
            }

            MarkDirty();
            NotifyChanged();
            return next;
        }

        internal static AlertCategory ParseCategory(string value)
        {
            return Enum.TryParse(value, true, out AlertCategory category)
                ? category
                : AlertCategory.General;
        }

        internal static AlertSeverity ParseSeverity(int value)
        {
            return value >= (int)AlertSeverity.Info && value <= (int)AlertSeverity.Critical
                ? (AlertSeverity)value
                : AlertSeverity.Info;
        }

        internal static void Tick()
        {
            if (!_dirty || Time.Ticks < _saveAt)
            {
                return;
            }

            Save();
        }

        internal static void Save()
        {
            if (!_loaded || !_dirty)
            {
                return;
            }

            try
            {
                string json = JsonSerializer.Serialize(
                    _state,
                    AlertCenterJsonContext.Default.AlertCenterState
                );

                if (ProfileDataStore.WriteAllText(FILE_NAME, json))
                {
                    _dirty = false;
                }
                else
                {
                    _saveAt = (long)Time.Ticks + 5000;
                }
            }
            catch (Exception ex)
            {
                FeatureDiagnostics.RecordFailure("AlertCenter:save", ex);
                _saveAt = (long)Time.Ticks + 5000;
            }
        }

        internal static void ResetForProfile()
        {
            Save();
            _state = new AlertCenterState();
            _loaded = false;
            _dirty = false;
            _nextId = 1;
            _saveAt = 0;
        }

        private static AlertSeverity EffectiveSeverity(
            AlertCategory category,
            AlertSeverity inferred)
        {
            int configured = SeverityOverride(category);
            return configured < 0 ? inferred : ParseSeverity(configured);
        }

        private static AlertSeverity InferSeverity(string text, ushort hue)
        {
            if (text.IndexOf("DEATH", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("legendary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AlertSeverity.Critical;
            }

            if (hue == 0x0021
                || text.IndexOf("low ", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(" full", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("spike", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("out of range", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("POISONED", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("loitering", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AlertSeverity.Warning;
            }

            return AlertSeverity.Info;
        }

        private static AlertCategory InferCategory(string sourceKey, string text)
        {
            string value = (sourceKey + " " + text).ToLowerInvariant();

            if (value.Contains("durability") || value.Contains("repair"))
                return AlertCategory.Equipment;
            if (value.Contains("pet-loyalty") || value.Contains("pet loyalty")
                || value.StartsWith("pet-") || value.Contains("feed "))
                return AlertCategory.Pets;
            if (value.Contains("legendary") || value.Contains("paragon"))
                return AlertCategory.Creatures;
            if (value.Contains("inventory") || value.Contains("weight")
                || value.Contains("bag ") || value.Contains("backpack"))
                return AlertCategory.Inventory;
            if (value.Contains("bandage") || value.Contains("reagent")
                || value.Contains("restock") || value.Contains("supply"))
                return AlertCategory.Supplies;
            if (value.Contains("journal") || value.Contains("keyword"))
                return AlertCategory.Journal;
            if (value.Contains("combat") || value.Contains("damage")
                || value.Contains("poison") || value.Contains("mana")
                || value.Contains("stamina") || value.Contains("kill ready")
                || value.Contains("buff lost") || value.Contains("death"))
                return AlertCategory.Combat;
            if (value.Contains("ping") || value.Contains("idle")
                || value.Contains("automation") || value.Contains("target switched"))
                return AlertCategory.System;

            return AlertCategory.General;
        }

        private static string NormalizeSourceKey(string sourceKey, string text)
        {
            if (!string.IsNullOrWhiteSpace(sourceKey))
            {
                return sourceKey.Trim().ToLowerInvariant();
            }

            string normalized = new string(text
                .ToLowerInvariant()
                .Where(character => char.IsLetter(character) || character == ' ' || character == '-')
                .Take(48)
                .ToArray()).Trim();
            return string.IsNullOrEmpty(normalized) ? "general" : normalized;
        }

        private static void DeactivateCategory(AlertCategory category)
        {
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (AlertCenterEntry entry in _state.History)
            {
                if (!entry.Active || ParseCategory(entry.Category) != category)
                {
                    continue;
                }

                entry.Active = false;

                if (entry.Persistent && !string.IsNullOrEmpty(entry.SourceKey))
                {
                    sources.Add(entry.SourceKey);
                }
            }

            foreach (string source in sources)
            {
                ToastManager.DismissByKey(source, false);
            }
        }

        private static void RemoveExpiredSnoozes()
        {
            long now = DateTime.UtcNow.Ticks;
            var expired = _state.SnoozedCategories
                .Where(pair => pair.Value <= now)
                .Select(pair => pair.Key)
                .ToList();

            foreach (string key in expired)
            {
                _state.SnoozedCategories.Remove(key);
            }
        }

        private static void TrimHistory()
        {
            while (_state.History.Count > MAX_HISTORY)
            {
                int index = _state.History.FindIndex(entry => !entry.Active);
                _state.History.RemoveAt(index >= 0 ? index : 0);
            }
        }

        private static void MarkDirty()
        {
            _dirty = true;
            _saveAt = (long)Time.Ticks + SAVE_DELAY_MS;
        }

        private static void NotifyChanged()
        {
            UIManager.GetGump<AlertCenterGump>()?.RequestRefresh();
        }
    }

    internal sealed class AlertCenterState
    {
        public List<AlertCenterEntry> History { get; set; } = new List<AlertCenterEntry>();
        public List<string> MutedCategories { get; set; } = new List<string>();
        public List<string> MutedSources { get; set; } = new List<string>();
        public Dictionary<string, long> SnoozedCategories { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, int> CategorySeverityOverrides { get; set; } = new Dictionary<string, int>();
    }

    internal sealed class AlertCenterEntry
    {
        public long Id { get; set; }
        public string SourceKey { get; set; } = "general";
        public string Text { get; set; } = string.Empty;
        public string Category { get; set; } = AlertCategory.General.ToString();
        public int Severity { get; set; }
        public long CreatedUtcTicks { get; set; }
        public long UpdatedUtcTicks { get; set; }
        public int Occurrences { get; set; } = 1;
        public bool Persistent { get; set; }
        public bool Active { get; set; }
        public bool Suppressed { get; set; }
    }

    [JsonSerializable(typeof(AlertCenterState))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal sealed partial class AlertCenterJsonContext : JsonSerializerContext
    {
    }
}
