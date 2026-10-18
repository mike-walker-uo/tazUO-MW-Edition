using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility;

namespace ClassicUO.Configuration
{
    internal static class ProfileTransfer
    {
        internal sealed class Destination
        {
            internal string Path, Label, Server;
        }

        internal sealed class Snapshot
        {
            internal string SourcePath, Json;
            internal HashSet<ProfileTransferCategory> Categories;
            internal Dictionary<string, byte[]> Files;
        }

        internal sealed class Result
        {
            internal int ChangedFiles;
            internal string BackupPath;
        }

        internal static bool SamePath(string first, string second) =>
            string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        internal static List<Destination> FindDestinations(string root, string source)
        {
            var result = new List<Destination>();
            if (!Directory.Exists(root)) return result;
            foreach (string account in Directory.GetDirectories(root))
                foreach (string server in Directory.GetDirectories(account))
                    foreach (string character in Directory.GetDirectories(server))
                        if (File.Exists(Path.Combine(character, "profile.json")) && !SamePath(character, source))
                            result.Add(new Destination { Path = character, Server = Path.GetFileName(server),
                                Label = Path.GetFileName(character) + " / " + Path.GetFileName(server) + " / " + Path.GetFileName(account) });
            return result.OrderBy(d => d.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Called on the game thread: serialize live options and flush selected in-memory managers.
        internal static Snapshot Capture(HashSet<ProfileTransferCategory> categories)
        {
            Profile profile = ProfileManager.CurrentProfile;
            string path = ProfileManager.ProfilePath;
            var scene = Client.Game.GetScene<GameScene>();
            if (profile == null || scene == null || string.IsNullOrEmpty(path))
                throw new InvalidOperationException("Log in before transferring settings.");
            bool saveGumps = categories.Contains(ProfileTransferCategory.GumpLayout)
                || categories.Contains(ProfileTransferCategory.SkillButtons) || categories.Contains(ProfileTransferCategory.SpellIcons);
            if (saveGumps && !profile.SaveGumps(path))
                throw new IOException("Could not save the current gump layout.");
            if (categories.Contains(ProfileTransferCategory.Skills) && !saveGumps)
                SkillsGroupManager.Save();
            if (categories.Contains(ProfileTransferCategory.Macros))
            {
                scene.Macros.Save();
                SpellBarManager.Unload(); // Saves rows and hotkeys; does not unload runtime state.
            }
            if (categories.Contains(ProfileTransferCategory.InfoBar)) scene.InfoBars.Save();
            if (categories.Contains(ProfileTransferCategory.GridLayout))
            {
                foreach (var grid in UIManager.Gumps.OfType<GridContainer>()) grid.SaveGridLayout(false);
                GridContainerSaveData.Instance.Save();
            }
            if (categories.Contains(ProfileTransferCategory.Interface)) NameOverHeadManager.Save();
            if (categories.Contains(ProfileTransferCategory.Chat))
                if (!JsonHelper.SaveAndBackup(JournalFilterManager.Instance.Filters, Path.Combine(path, "journal_filters.json"), HashSetContext.Default.HashSetString))
                    throw new IOException("Could not save the current journal filters.");
            if (categories.Contains(ProfileTransferCategory.Combat))
            {
                BandageSettings.Save();
                SpellVisualRangeManager.Instance.Save();
            }
            if (categories.Contains(ProfileTransferCategory.Containers)) AutoLootManager.Instance.Save();
            if (categories.Contains(ProfileTransferCategory.Maps))
            {
                UIManager.GetGump<WorldMapGump>()?.SaveSettings();
                TileMarkerManager.Instance.Save();
            }
            if (categories.Contains(ProfileTransferCategory.Agents))
            {
                OrganizerAgent.Instance?.Save();
                RestockAgentManager.Save();
            }
            if (categories.Contains(ProfileTransferCategory.Enhancements)) TazUOFeatureSettings.Save();
            string json = ProfileTransferCatalog.JsonCategories.Values.Any(categories.Contains)
                ? JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile) : "{}";
            return SnapshotFrom(path, json, categories);
        }

        internal static Snapshot SnapshotFrom(string path, string json, HashSet<ProfileTransferCategory> categories)
        {
            if (categories.Count == 0) throw new InvalidOperationException("Select at least one settings category.");
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in ProfileTransferCatalog.Files.Where(p => categories.Contains(p.Key))
                .SelectMany(p => p.Value).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string file = Path.Combine(path, name);
                // Missing source files represent default/empty settings in this category.
                files.Add(name, File.Exists(file) ? File.ReadAllBytes(file) : null);
            }
            return new Snapshot { SourcePath = path, Json = json,
                Categories = new HashSet<ProfileTransferCategory>(categories), Files = files };
        }

        internal static string Merge(string source, string destination, ISet<ProfileTransferCategory> categories)
        {
            using (var from = JsonDocument.Parse(source))
            using (var to = JsonDocument.Parse(destination))
            using (var buffer = new MemoryStream())
            {
                if (from.RootElement.ValueKind != JsonValueKind.Object || to.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Profile settings must be a JSON object.");
                var values = to.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                foreach (var property in from.RootElement.EnumerateObject())
                {
                    if (property.Name == "username" || property.Name == "server_name" || property.Name == "character_name"
                        || property.Name == "protected_items") continue;
                    var category = ProfileTransferCatalog.JsonCategories.TryGetValue(property.Name, out var found)
                        ? found : ProfileTransferCategory.Other;
                    if (categories.Contains(category)) values[property.Name] = property.Value;
                }
                using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    foreach (var value in values)
                    {
                        writer.WritePropertyName(value.Key);
                        value.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        // Worker-thread disk operation; no access to active profile, managers or UI.
        internal static Result Apply(Snapshot snapshot, string destination)
        {
            if (SamePath(snapshot.SourcePath, destination)) throw new InvalidOperationException("The source profile cannot be a destination.");
            string profileFile = Path.Combine(destination, "profile.json");
            if (!File.Exists(profileFile)) throw new FileNotFoundException("Destination profile no longer exists.", profileFile);
            var changes = new Dictionary<string, byte[]>(snapshot.Files, StringComparer.OrdinalIgnoreCase);
            if (changes.TryGetValue("gumps.xml", out var gumps))
            {
                string gumpsPath = Path.Combine(destination, "gumps.xml");
                changes["gumps.xml"] = MergeGumps(gumps, File.Exists(gumpsPath) ? File.ReadAllBytes(gumpsPath) : null, snapshot.Categories);
            }
            if (ProfileTransferCatalog.JsonCategories.Values.Any(snapshot.Categories.Contains))
                changes.Add("profile.json", Encoding.UTF8.GetBytes(Merge(snapshot.Json, File.ReadAllText(profileFile), snapshot.Categories)));
            var before = new Dictionary<string, byte[]>();
            foreach (string name in changes.Keys.ToArray())
            {
                string path = Path.Combine(destination, name);
                byte[] old = File.Exists(path) ? File.ReadAllBytes(path) : null;
                byte[] updated = changes[name];
                if (old == null && updated == null || old != null && updated != null && old.SequenceEqual(updated)) changes.Remove(name);
                else before.Add(name, old);
            }
            var result = new Result { ChangedFiles = changes.Count };
            if (changes.Count == 0) return result;
            result.BackupPath = Path.Combine(destination, "transfer-backups", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(result.BackupPath);
            foreach (var original in before)
                if (original.Value != null) File.WriteAllBytes(Path.Combine(result.BackupPath, original.Key), original.Value);
            File.WriteAllLines(Path.Combine(result.BackupPath, "created-files.txt"), before.Where(p => p.Value == null).Select(p => p.Key));
            var applied = new List<string>();
            try
            {
                foreach (var change in changes)
                {
                    WriteAtomic(Path.Combine(destination, change.Key), change.Value);
                    applied.Add(change.Key);
                }
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                foreach (string name in applied.AsEnumerable().Reverse())
                    try { WriteAtomic(Path.Combine(destination, name), before[name]); }
                    catch (Exception restoreFailure) { failures.Add(restoreFailure); }
                throw new AggregateException("Transfer failed. Backup: " + result.BackupPath, failures);
            }
            return result;
        }

        private static byte[] MergeGumps(byte[] source, byte[] destination, ISet<ProfileTransferCategory> categories)
        {
            if (categories.Contains(ProfileTransferCategory.GumpLayout) && categories.Contains(ProfileTransferCategory.SkillButtons)
                && categories.Contains(ProfileTransferCategory.SpellIcons)) return source;
            if (source == null && destination == null) return null;
            var from = ReadGumps(source);
            var to = ReadGumps(destination);
            FilterGumps(from, categories, true);
            FilterGumps(to, categories, false);
            foreach (XmlElement element in from.DocumentElement.ChildNodes.OfType<XmlElement>())
                if (element.Name == "gump" || element.Name == "anchored_group_gump")
                    to.DocumentElement.AppendChild(to.ImportNode(element, true));
            using (var buffer = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
                    to.Save(writer);
                return buffer.ToArray();
            }
        }

        private static XmlDocument ReadGumps(byte[] bytes)
        {
            var document = new XmlDocument { XmlResolver = null };
            if (bytes == null) document.LoadXml("<gumps />");
            else
                using (var stream = new MemoryStream(bytes))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document.Load(reader);
            if (document.DocumentElement?.Name != "gumps") throw new InvalidDataException("Expected a gumps.xml layout.");
            return document;
        }

        private static void FilterGumps(XmlDocument document, ISet<ProfileTransferCategory> categories, bool keepSelected)
        {
            var root = document.DocumentElement;
            foreach (XmlElement gump in root.SelectNodes("gump | anchored_group_gump/gump").OfType<XmlElement>().ToArray())
            {
                int.TryParse(gump.GetAttribute("type"), out int type);
                var category = (GumpType)type == GumpType.SkillButton ? ProfileTransferCategory.SkillButtons
                    : (GumpType)type == GumpType.SpellButton ? ProfileTransferCategory.SpellIcons : ProfileTransferCategory.GumpLayout;
                if (categories.Contains(category) != keepSelected) gump.ParentNode.RemoveChild(gump);
            }
            // Keep matrix coordinates and dimensions, including holes left by an unselected category.
            foreach (XmlElement group in root.SelectNodes("anchored_group_gump").OfType<XmlElement>().ToArray())
                if (group.SelectNodes("gump").Count == 0) root.RemoveChild(group);
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            if (bytes == null) { File.Delete(path); return; }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
