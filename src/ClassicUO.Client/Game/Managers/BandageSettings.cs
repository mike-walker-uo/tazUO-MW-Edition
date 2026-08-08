#region license
// TazUO addition.
#endregion

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Persists all bandage-related settings to bandage.tsv in the active
    /// profile directory. Loaded once on first <see cref="EnsureLoaded"/>;
    /// saved on demand via <see cref="MarkDirty"/> (debounced 1s) or
    /// explicitly via <see cref="Save"/>.
    /// </summary>
    public static class BandageSettings
    {
        private const string FILENAME = "bandage.tsv";
        private static bool _loaded;
        private static bool _dirty;
        private static long _nextFlush;
        private const long FLUSH_INTERVAL_MS = 1000;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                ApplyKey(bits[0], bits[1]);
            }
        }

        public static void MarkDirty()
        {
            _dirty = true;
            if (_nextFlush == 0) _nextFlush = (long)Time.Ticks + FLUSH_INTERVAL_MS;
        }

        /// <summary>Call from GameScene.Update — flushes pending save when due.</summary>
        public static void Tick()
        {
            if (!_dirty) return;
            if (Time.Ticks < _nextFlush) return;
            Save();
        }

        public static void Save()
        {
            _dirty = false;
            _nextFlush = 0;
            ProfileDataStore.Write(FILENAME, sw =>
            {
                // Priority
                sw.WriteLine($"Priority\t{(int)BandageScheduler.Priority}");

                // AutoBandage (player)
                sw.WriteLine($"Auto.Enabled\t{(AutoBandageManager.Enabled ? 1 : 0)}");
                sw.WriteLine($"Auto.Manual\t{(AutoBandageManager.ManualOverride ? 1 : 0)}");
                sw.WriteLine($"Auto.Threshold\t{AutoBandageManager.ThresholdPercent}");
                sw.WriteLine($"Auto.Cycle\t{AutoBandageManager.CycleMs}");
                sw.WriteLine($"Auto.BlockPoison\t{(AutoBandageManager.BlockOnPoisoned ? 1 : 0)}");
                sw.WriteLine($"Auto.BlockMortal\t{(AutoBandageManager.BlockOnMortal ? 1 : 0)}");
                sw.WriteLine($"Auto.BlockDead\t{(AutoBandageManager.BlockOnDead ? 1 : 0)}");

                // PetBandage
                sw.WriteLine($"Pet.Enabled\t{(PetBandageManager.Enabled ? 1 : 0)}");
                sw.WriteLine($"Pet.Manual\t{(PetBandageManager.ManualOverride ? 1 : 0)}");
                sw.WriteLine($"Pet.Threshold\t{PetBandageManager.ThresholdPct}");
                sw.WriteLine($"Pet.Range\t{PetBandageManager.MaxDistance}");
                sw.WriteLine($"Pet.BlockPoison\t{(PetBandageManager.BlockOnPoisoned ? 1 : 0)}");
                sw.WriteLine($"Pet.BlockMortal\t{(PetBandageManager.BlockOnMortal ? 1 : 0)}");
                sw.WriteLine($"Pet.BlockDead\t{(PetBandageManager.BlockOnDead ? 1 : 0)}");
                sw.WriteLine($"Pet.Mode\t{(int)PetBandageManager.Mode}");

                // ExternalBandage
                sw.WriteLine($"Ext.Enabled\t{(ExternalBandageManager.Enabled ? 1 : 0)}");
                sw.WriteLine($"Ext.Threshold\t{ExternalBandageManager.ThresholdPct}");
                sw.WriteLine($"Ext.BlockPoison\t{(ExternalBandageManager.BlockOnPoisoned ? 1 : 0)}");
                sw.WriteLine($"Ext.BlockMortal\t{(ExternalBandageManager.BlockOnMortal ? 1 : 0)}");
                sw.WriteLine($"Ext.BlockDead\t{(ExternalBandageManager.BlockOnDead ? 1 : 0)}");
                sw.WriteLine($"Ext.Target\t{ExternalBandageManager.TargetSerial}");

                // Low-stock
                sw.WriteLine($"Stock.Enabled\t{(BandageStockWarner.Enabled ? 1 : 0)}");
                sw.WriteLine($"Stock.Threshold\t{BandageStockWarner.Threshold}");
            });
        }

        public static void ResetForProfile()
        {
            _loaded = false;
            _dirty = false;
            _nextFlush = 0;
        }

        private static void ApplyKey(string key, string val)
        {
            if (!long.TryParse(val, out long lv) || lv < int.MinValue || lv > uint.MaxValue) return;
            int i = lv > int.MaxValue ? int.MaxValue : (int)lv;
            bool b = i != 0;
            switch (key)
            {
                case "Priority":         if (System.Enum.IsDefined(typeof(BandageScheduler.Pref), i)) BandageScheduler.Priority = (BandageScheduler.Pref)i; break;
                case "Auto.Enabled":     AutoBandageManager.SetEnabledQuiet(b);                break;
                case "Auto.Manual":      AutoBandageManager.SetManualOverride(b);              break;
                case "Auto.Threshold":   AutoBandageManager.SetThresholdQuiet(i);              break;
                case "Auto.Cycle":
                    AutoBandageManager.CycleMs = System.Math.Max(250, System.Math.Min(60000, lv));
                    PetBandageManager.CycleMs = AutoBandageManager.CycleMs;
                    break;
                case "Auto.BlockPoison": AutoBandageManager.BlockOnPoisoned = b;               break;
                case "Auto.BlockMortal": AutoBandageManager.BlockOnMortal   = b;               break;
                case "Auto.BlockDead":   AutoBandageManager.BlockOnDead     = b;               break;

                case "Pet.Enabled":      PetBandageManager.SetEnabledQuiet(b);                 break;
                case "Pet.Manual":       PetBandageManager.SetManualOverride(b);               break;
                case "Pet.Threshold":    PetBandageManager.ThresholdPct = System.Math.Max(5, System.Math.Min(99, i)); break;
                case "Pet.Range":        PetBandageManager.MaxDistance = System.Math.Max(1, System.Math.Min(12, i)); break;
                case "Pet.BlockPoison":  PetBandageManager.BlockOnPoisoned = b;                break;
                case "Pet.BlockMortal":  PetBandageManager.BlockOnMortal   = b;                break;
                case "Pet.BlockDead":    PetBandageManager.BlockOnDead     = b;                break;
                case "Pet.Mode":         if (System.Enum.IsDefined(typeof(PetBandageManager.MultiPetMode), i)) PetBandageManager.Mode = (PetBandageManager.MultiPetMode)i; break;

                case "Ext.Enabled":      ExternalBandageManager.Enabled = b;                   break;
                case "Ext.Threshold":    ExternalBandageManager.ThresholdPct = System.Math.Max(5, System.Math.Min(99, i)); break;
                case "Ext.BlockPoison":  ExternalBandageManager.BlockOnPoisoned = b;           break;
                case "Ext.BlockMortal":  ExternalBandageManager.BlockOnMortal   = b;           break;
                case "Ext.BlockDead":    ExternalBandageManager.BlockOnDead     = b;           break;
                case "Ext.Target":       if (lv >= 0 && lv <= uint.MaxValue) ExternalBandageManager.TargetSerial = (uint)lv; break;

                case "Stock.Enabled":    BandageStockWarner.Enabled = b;                       break;
                case "Stock.Threshold":  BandageStockWarner.Threshold = System.Math.Max(1, System.Math.Min(5000, i)); break;
            }
        }
    }
}
