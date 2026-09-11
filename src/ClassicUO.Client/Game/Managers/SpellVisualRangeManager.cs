using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace ClassicUO.Game.Managers
{
    using System.Text.Json.Serialization;
    using ClassicUO.Utility.Logging;

    [JsonSerializable(typeof(SpellVisualRangeManager.SpellRangeInfo))]
    [JsonSerializable(typeof(SpellVisualRangeManager.SpellRangeInfo[]))]
    public partial class SpellVisualRangeJsonContext : JsonSerializerContext
    {
    }

    public class SpellVisualRangeManager
    {
        public static SpellVisualRangeManager Instance => instance ??= new SpellVisualRangeManager();

        public Vector2 LastCursorTileLoc { get; set; } = Vector2.Zero;
        public DateTime LastSpellTime { get; private set; } = DateTime.Now;
        public Dictionary<int, SpellRangeInfo> SpellRangeCache => spellRangeCache;

        private string savePath = Path.Combine(CUOEnviroment.ExecutablePath ?? "", "Data", "Profiles", "SpellVisualRange.json");
        private string overridePath = Path.Combine(ProfileManager.ProfilePath ?? "", "SpellVisualRange.json");

        private Dictionary<int, SpellRangeInfo> spellRangeCache = new Dictionary<int, SpellRangeInfo>();
        private Dictionary<int, SpellRangeInfo> spellRangeOverrideCache = new Dictionary<int, SpellRangeInfo>();
        private Dictionary<string, SpellRangeInfo> spellRangePowerWordCache = new Dictionary<string, SpellRangeInfo>();

        private bool loaded = false;
        private int _loadGeneration;
        private readonly SpellVisualRangeImportLifecycle _importLifecycle = new SpellVisualRangeImportLifecycle();
        private static SpellVisualRangeManager instance;

        private bool isCasting { get; set; } = false;
        private SpellRangeInfo currentSpell { get; set; }

        //Taken from Dust client
        private static readonly int[] stopAtClilocs = new int[]
        {
            500641,     // Your concentration is disturbed, thus ruining thy spell.
            502625,     // Insufficient mana. You must have at least ~1_MANA_REQUIREMENT~ Mana to use this spell.
            502630,     // More reagents are needed for this spell.
            500946,     // You cannot cast this in town!
            500015,     // You do not have that spell
            502643,     // You can not cast a spell while frozen.
            1061091,    // You cannot cast that spell in this form.
            502644,     // You have not yet recovered from casting a spell.
            1072060,    // You cannot cast a spell while calmed.
        };

        private SpellVisualRangeManager()
        {
            Load();
        }

        private void OnRawMessageReceived(object sender, MessageEventArgs e)
        {
            if (
                !loaded
                || ProfileManager.CurrentProfile?.EnableSpellIndicators != true
                || e?.Parent == null
                || !ReferenceEquals(e.Parent, World.Player)
                || string.IsNullOrEmpty(e.Text)
            )
            {
                return;
            }

            if (spellRangePowerWordCache.TryGetValue(e.Text.Trim(), out SpellRangeInfo spell))
            {
                SetCasting(spell);
            }
        }

        public void OnClilocReceived(int cliloc)
        {
            if (isCasting && stopAtClilocs.Contains(cliloc))
            {
                ClearCasting();
            }
        }

        private void SetCasting(SpellRangeInfo spell)
        {
            LastSpellTime = DateTime.Now;
            currentSpell = spell;
            isCasting = true;
            if (currentSpell != null && currentSpell.FreezeCharacterWhileCasting)
            {
                World.Player.Flags |= Flags.Frozen;
            }
            EventSink.InvokeSpellCastBegin(spell.ID);
        }

        public void ClearCasting()
        {
            isCasting = false;
            currentSpell = null;
            LastSpellTime = DateTime.MinValue;
            World.Player.Flags &= ~Flags.Frozen;
        }

        public SpellRangeInfo GetCurrentSpell()
        {
            return currentSpell;
        }

        #region Load and unload
        public void OnSceneLoad()
        {
            EventSink.RawMessageReceived += OnRawMessageReceived;
        }

        public void OnSceneUnload()
        {
            _loadGeneration++;
            _importLifecycle.Cancel();
            Save();
            EventSink.RawMessageReceived -= OnRawMessageReceived;
            instance = null;
        }
        #endregion

        public bool IsTargetingAfterCasting()
        {
            if (!loaded || currentSpell == null || !isCasting || ProfileManager.CurrentProfile == null || !ProfileManager.CurrentProfile.EnableSpellIndicators)
            {
                return false;
            }

            if (TargetManager.IsTargeting || (currentSpell.ShowCastRangeDuringCasting && IsCastingWithoutTarget()))
            {
                if (LastSpellTime + TimeSpan.FromSeconds(currentSpell.MaxDuration) > DateTime.Now)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsCastingWithoutTarget()
        {
            if (!loaded || currentSpell == null || !isCasting || currentSpell.CastTime <= 0 || TargetManager.IsTargeting || ProfileManager.CurrentProfile == null || !ProfileManager.CurrentProfile.EnableSpellIndicators)
            {
                return false;
            }

            if (LastSpellTime + TimeSpan.FromSeconds(currentSpell.MaxDuration) > DateTime.Now)
            {
                if (LastSpellTime + TimeSpan.FromSeconds(currentSpell.CastTime) > DateTime.Now)
                {
                    return true;
                }
                else if (currentSpell.FreezeCharacterWhileCasting)
                {
                    World.Player.Flags &= ~Flags.Frozen;
                }
            }
            else if (currentSpell.FreezeCharacterWhileCasting)
            {
                World.Player.Flags &= ~Flags.Frozen;
            }

            return false;
        }

        public ushort ProcessHueForTile(ushort hue, GameObject o)
        {
            if (!loaded || currentSpell == null) { return hue; }

            if (currentSpell.CastRange > 0 && o.Distance <= currentSpell.CastRange)
            {
                hue = currentSpell.Hue;
            }

            int cDistance = o.DistanceFrom(LastCursorTileLoc);

            if (currentSpell.CursorSize > 0 && cDistance < currentSpell.CursorSize)
            {
                if (currentSpell.IsLinear)
                {
                    if (GetDirection(new Vector2(World.Player.X, World.Player.Y), LastCursorTileLoc) == SpellDirection.EastWest)
                    { //X
                        if (o.Y == LastCursorTileLoc.Y)
                        {
                            hue = currentSpell.CursorHue;
                        }
                    }
                    else
                    { //Y
                        if (o.X == LastCursorTileLoc.X)
                        {
                            hue = currentSpell.CursorHue;
                        }
                    }
                }
                else
                {
                    hue = currentSpell.CursorHue;
                }
            }

            return hue;
        }

        private static SpellDirection GetDirection(Vector2 from, Vector2 to)
        {
            int dx = (int)(from.X - to.X);
            int dy = (int)(from.Y - to.Y);
            int rx = (dx - dy) * 44;
            int ry = (dx + dy) * 44;

            if (rx >= 0 && ry >= 0)
            {
                return SpellDirection.SouthNorth;
            }
            else if (rx >= 0)
            {
                return SpellDirection.EastWest;
            }
            else if (ry >= 0)
            {
                return SpellDirection.EastWest;
            }
            else
            {
                return SpellDirection.SouthNorth;
            }
        }

        #region Save and load
        internal SpellVisualRangeImportRequest BeginConfigurationImport()
        {
            return _importLifecycle.Begin();
        }

        internal bool IsConfigurationImportCurrent(SpellVisualRangeImportRequest request)
        {
            return _importLifecycle.IsCurrent(request);
        }

        internal bool TryLoadConfigurationImport(SpellVisualRangeImportRequest request, string json)
        {
            if (!_importLifecycle.IsCurrent(request) || !LoadFromString(json))
                return false;

            // A successful explicit import owns the resulting configuration. Prevent the
            // asynchronous initial file load from replacing it if that callback arrives later.
            _loadGeneration++;
            return true;
        }

        private Timer saveTimer;
        private readonly object saveLock = new object();
        private readonly object saveFileLock = new object();
        private volatile bool hasPendingChanges = false;
        private string pendingSaveJson;
        private void Load()
        {
            loaded = false;
            spellRangeCache.Clear();
            int generation = ++_loadGeneration;
            string targetPath = savePath;
            var assembly = GetType().Assembly;
            string resourceName = assembly.GetName().Name + ".Game.Managers.DefaultSpellIndicatorConfig.json";

            Task.Run(() =>
            {
                string json = null;
                Exception loadError = null;

                try
                {
                    if (File.Exists(targetPath))
                    {
                        json = File.ReadAllText(targetPath);
                    }
                    else
                    {
                        using Stream stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            using var reader = new StreamReader(stream);
                            json = reader.ReadToEnd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    loadError = ex;
                }

                MainThreadQueue.EnqueueAction(() =>
                {
                    if (generation != _loadGeneration) return;

                    if (loadError != null)
                    {
                        Log.Error(loadError.ToString());
                    }

                    if (string.IsNullOrEmpty(json) || !LoadFromString(json))
                    {
                        loaded = false;
                        spellRangeCache.Clear();
                        CreateAndLoadDataFile();
                        AfterLoad();
                        loaded = true;
                    }

                });
            });
        }

        private void LoadOverrides()
        {
            spellRangeOverrideCache.Clear();

            if (File.Exists(overridePath))
            {
                try
                {
                    string data = File.ReadAllText(overridePath);
                    SpellRangeInfo[] fileData = JsonSerializer.Deserialize<SpellRangeInfo[]>(data);

                    foreach (var entry in fileData)
                    {
                        spellRangeOverrideCache.Add(entry.ID, entry);
                    }

                    foreach (var entry in spellRangeOverrideCache.Values)
                    {
                        if (string.IsNullOrEmpty(entry.PowerWords))
                        {
                            SpellDefinition spellD = SpellDefinition.FullIndexGetSpell(entry.ID);
                            if (spellD == SpellDefinition.EmptySpell)
                            {
                                SpellDefinition.TryGetSpellFromName(entry.Name, out spellD);
                            }

                            if (spellD != SpellDefinition.EmptySpell)
                            {
                                entry.PowerWords = spellD.PowerWords;
                            }
                        }
                        if (!string.IsNullOrEmpty(entry.PowerWords))
                        {
                            if (spellRangePowerWordCache.ContainsKey(entry.PowerWords))
                            {
                                spellRangePowerWordCache[entry.PowerWords] = entry;
                            }
                            else
                            {
                                spellRangePowerWordCache.Add(entry.PowerWords, entry);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public bool LoadFromString(string json)
        {
            Dictionary<int, SpellRangeInfo> oldCache = spellRangeCache;
            Dictionary<int, SpellRangeInfo> oldOverrides = spellRangeOverrideCache;
            Dictionary<string, SpellRangeInfo> oldPowerWords = spellRangePowerWordCache;
            bool wasLoaded = loaded;

            try
            {
                if (!TryParseConfiguration(json, out Dictionary<int, SpellRangeInfo> newCache))
                    return false;

                loaded = false;
                spellRangeCache = newCache;
                spellRangeOverrideCache = new Dictionary<int, SpellRangeInfo>();
                spellRangePowerWordCache = new Dictionary<string, SpellRangeInfo>();
                AfterLoad();
                loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                spellRangeCache = oldCache;
                spellRangeOverrideCache = oldOverrides;
                spellRangePowerWordCache = oldPowerWords;
                loaded = wasLoaded;
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        internal static bool TryParseConfiguration(string json, out Dictionary<int, SpellRangeInfo> result)
        {
            result = null;
            try
            {
                SpellRangeInfo[] entries = JsonSerializer.Deserialize<SpellRangeInfo[]>(json);
                if (entries == null || entries.Length == 0)
                    return false;

                var parsed = new Dictionary<int, SpellRangeInfo>(entries.Length);
                foreach (SpellRangeInfo entry in entries)
                {
                    if (entry == null || entry.ID < 0 || parsed.ContainsKey(entry.ID))
                        return false;

                    parsed.Add(entry.ID, entry);
                }

                result = parsed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AfterLoad()
        {
            spellRangePowerWordCache.Clear();
            foreach (var entry in spellRangeCache.Values)
            {
                if (string.IsNullOrEmpty(entry.PowerWords))
                {
                    SpellDefinition spellD = SpellDefinition.FullIndexGetSpell(entry.ID);
                    if (spellD == SpellDefinition.EmptySpell)
                    {
                        SpellDefinition.TryGetSpellFromName(entry.Name, out spellD);
                    }

                    if (spellD != SpellDefinition.EmptySpell)
                    {
                        entry.PowerWords = spellD.PowerWords;
                    }
                }
                if (!string.IsNullOrEmpty(entry.PowerWords))
                {
                    spellRangePowerWordCache.Add(entry.PowerWords, entry);
                }
            }
            LoadOverrides();
        }

        private void CreateAndLoadDataFile()
        {
            foreach (var entry in SpellsMagery.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsNecromancy.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsChivalry.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsBushido.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsNinjitsu.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsSpellweaving.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsMysticism.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }
            foreach (var entry in SpellsMastery.GetAllSpells)
            {
                spellRangeCache.Add(entry.Value.ID, SpellRangeInfo.FromSpellDef(entry.Value));
            }

            DelayedSave();
        }

        public void DelayedSave()
        {
            lock (saveLock)
            {
                hasPendingChanges = true;
                var options = new JsonSerializerOptions() { WriteIndented = true };
                pendingSaveJson = JsonSerializer.Serialize(spellRangeCache.Values.ToArray(), options);

                // Cancel existing timer if it's running
                saveTimer?.Dispose();

                saveTimer = new Timer(500) { AutoReset = false };
                saveTimer.Elapsed += (_,_) => PerformSave();
                saveTimer.Start();
            }
        }

        private void PerformSave()
        {
            string directory = Path.GetDirectoryName(savePath);
            string tempPath = savePath + ".tmp";
            lock (saveFileLock)
            {
                string fileData;
                lock (saveLock)
                {
                    if (!hasPendingChanges)
                        return;

                    fileData = pendingSaveJson;
                }

                try
                {
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    File.WriteAllText(tempPath, fileData);

                    if (File.Exists(savePath))
                        File.Replace(tempPath, savePath, null);
                    else
                        File.Move(tempPath, savePath);

                    lock (saveLock)
                    {
                        if (ReferenceEquals(fileData, pendingSaveJson))
                        {
                            hasPendingChanges = false;
                            pendingSaveJson = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Save failed: {e}");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to clean up temporary spell config '{tempPath}': {e}");
                    }
                }
            }
        }

        public void Save()
        {
            lock (saveLock)
            {
                saveTimer?.Dispose();
                saveTimer = null;
            }

            PerformSave();
        }
        #endregion

        private enum SpellDirection
        {
            EastWest,
            SouthNorth
        }

        public class SpellRangeInfo
        {
            public int ID { get; set; } = -1;
            public string Name { get; set; } = "";
            public string PowerWords { get; set; } = "";
            public int CursorSize { get; set; } = 0;
            public int CastRange { get; set; } = 1;
            public ushort Hue { get; set; } = 32;
            public ushort CursorHue { get; set; } = 10;
            public int MaxDuration { get; set; } = 10;
            public bool IsLinear { get; set; } = false;
            public double CastTime { get; set; } = 0.0;
            public bool ShowCastRangeDuringCasting { get; set; } = false;
            public bool FreezeCharacterWhileCasting { get; set; } = false;
            public bool ExpectTargetCursor { get; set; } = false;

            public static SpellRangeInfo FromSpellDef(SpellDefinition spell)
            {
                return new SpellRangeInfo() { ID = spell.ID, Name = spell.Name, PowerWords = spell.PowerWords };
            }
        }

        #region Cast Timer Bar


        public class CastTimerProgressBar : Gump
        {
            private Rectangle barBounds, barBoundsF;
            private Texture2D background;
            private Texture2D foreground;
            private Vector3 hue = ShaderHueTranslator.GetHueVector(0);


            public CastTimerProgressBar() : base(0, 0)
            {
                CanMove = false;
                AcceptMouseInput = false;
                CanCloseWithEsc = false;
                CanCloseWithRightClick = false;

                ref readonly var gi = ref Client.Game.Gumps.GetGump(0x0805);
                background = gi.Texture;
                barBounds = gi.UV;

                gi = ref Client.Game.Gumps.GetGump(0x0806);
                foreground = gi.Texture;
                barBoundsF = gi.UV;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (SpellVisualRangeManager.Instance.IsCastingWithoutTarget())
                {
                    SpellRangeInfo i = SpellVisualRangeManager.Instance.GetCurrentSpell();
                    if (i != null)
                    {
                        if (i.CastTime > 0)
                        {
                            if (background != null && foreground != null)
                            {
                                Mobile m = World.Player;
                                Client.Game.Animations.GetAnimationDimensions(
                                    m.AnimIndex,
                                    m.GetGraphicForAnimation(),
                                    0,
                                    0,
                                    m.IsMounted,
                                    0,
                                    out int centerX,
                                    out int centerY,
                                    out int width,
                                    out int height
                                );

                                WorldViewportGump vp = UIManager.GetGump<WorldViewportGump>();

                                x = vp.Location.X + (int)(m.RealScreenPosition.X - (m.Offset.X + 22 + 5));
                                y = vp.Location.Y + (int)(m.RealScreenPosition.Y - ((m.Offset.Y - m.Offset.Z) - (height + centerY + 15) + (m.IsGargoyle && m.IsFlying ? -22 : !m.IsMounted ? 22 : 0)));

                                batcher.Draw(background, new Rectangle(x, y, barBounds.Width, barBounds.Height), barBounds, hue);

                                double percent = (DateTime.Now - SpellVisualRangeManager.Instance.LastSpellTime).TotalSeconds / i.CastTime;

                                int widthFromPercent = (int)(barBounds.Width * percent);
                                widthFromPercent = widthFromPercent > barBounds.Width ? barBounds.Width : widthFromPercent; //Max width is the bar width

                                if (widthFromPercent > 0)
                                {
                                    batcher.DrawTiled(foreground, new Rectangle(x, y, widthFromPercent, barBoundsF.Height), barBoundsF, hue);
                                }

                                if (percent <= 0 && i.FreezeCharacterWhileCasting)
                                {
                                    World.Player.Flags &= ~Flags.Frozen;
                                }
                            }
                        }
                    }
                }
                return base.Draw(batcher, x, y);
            }
        }
        #endregion
    }

    internal readonly struct SpellVisualRangeImportRequest
    {
        internal SpellVisualRangeImportRequest(int generation, CancellationToken cancellationToken)
        {
            Generation = generation;
            CancellationToken = cancellationToken;
        }

        internal int Generation { get; }
        internal CancellationToken CancellationToken { get; }
    }

    internal sealed class SpellVisualRangeImportLifecycle
    {
        private readonly object _sync = new object();
        private CancellationTokenSource _cancellation;
        private int _generation;

        internal SpellVisualRangeImportRequest Begin()
        {
            lock (_sync)
            {
                CancelCurrent();
                _cancellation = new CancellationTokenSource();
                return new SpellVisualRangeImportRequest(++_generation, _cancellation.Token);
            }
        }

        internal bool IsCurrent(SpellVisualRangeImportRequest request)
        {
            lock (_sync)
            {
                return request.Generation == _generation
                    && _cancellation != null
                    && !request.CancellationToken.IsCancellationRequested;
            }
        }

        internal void Cancel()
        {
            lock (_sync)
            {
                ++_generation;
                CancelCurrent();
            }
        }

        private void CancelCurrent()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
