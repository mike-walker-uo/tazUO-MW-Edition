using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using ClassicUO.Configuration;
using ClassicUO.Game.UI;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    // Optional ambience only: preserves combat/targeting cues and never downloads assets.
    internal static class LocalSoundscapePack
    {
        private static Dictionary<AmbienceOverlay.AmbientBiome, byte[]> _samples = new();
        private static string _folder, _profile;
        private static int _generation;
        internal static int Revision { get; private set; }
        internal static string Status { get; private set; } = "Native UO ambience";
        internal static void Reload() { _generation++; _folder = null; _samples.Clear(); Revision++; }
        internal static void Reset() { Reload(); _profile = null; Status = "Native UO ambience"; }
        internal static bool TryGet(AmbienceOverlay.AmbientBiome biome, out byte[] pcm) => _samples.TryGetValue(biome, out pcm);
        internal static void Update(bool enabled, string folder)
        {
            folder = enabled ? folder ?? "" : "";
            string profile = ProfileManager.ProfilePath;
            if (_folder == folder && _profile == profile) return;
            _folder = folder; _profile = profile; _samples.Clear(); Revision++;
            int generation = ++_generation;
            if (string.IsNullOrWhiteSpace(folder)) { Status = "Native UO ambience"; return; }
            Status = "Loading local soundscape pack…";
            Task.Run(() =>
            {
                if (generation != Volatile.Read(ref _generation)) return;
                var samples = new Dictionary<AmbienceOverlay.AmbientBiome, byte[]>();
                string status;
                try
                {
                    string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string manifest = Path.Combine(root, "manifest.json");
                    if (new FileInfo(manifest).Length > 32768) throw new InvalidDataException("Manifest exceeds 32 KiB.");
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                    string name = doc.RootElement.GetProperty("name").GetString();
                    string license = doc.RootElement.GetProperty("license").GetString();
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(license)) throw new InvalidDataException("Pack must declare name and license.");
                    var regions = doc.RootElement.GetProperty("regions");
                    foreach (AmbienceOverlay.AmbientBiome biome in Enum.GetValues(typeof(AmbienceOverlay.AmbientBiome)))
                    {
                        if (generation != Volatile.Read(ref _generation)) return;
                        if (!regions.TryGetProperty(biome.ToString(), out var value)) continue;
                        try
                        {
                            string relative = value.GetString();
                            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Sample paths must be relative.");
                            string path = Path.GetFullPath(Path.Combine(root, relative));
                            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Sample escapes pack folder.");
                            using var stream = File.OpenRead(path);
                            if (stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("WAV exceeds 2 MiB.");
                            var wav = new byte[(int)stream.Length];
                            int read = 0;
                            while (read < wav.Length) { int n = stream.Read(wav, read, wav.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                            samples[biome] = DecodeWave(wav);
                        }
                        catch (Exception ex) when (Expected(ex)) { /* Missing/corrupt region uses original UO sound. */ }
                    }
                    status = name + " / license: " + license + " / " + samples.Count + "/7 local regions; others use native UO";
                }
                catch (Exception ex) when (Expected(ex)) { status = "Soundscape fallback: " + ex.Message; }
                MainThreadQueue.EnqueueAction(() =>
                {
                    if (generation != _generation) return;
                    _samples = samples; Status = status; Revision++;
                    Log.Info("Soundscape pack: " + status);
                });
            });
        }
        private static bool Expected(Exception ex) => ex is IOException || ex is UnauthorizedAccessException
            || ex is ArgumentException || ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is NotSupportedException;

        // FNA's existing native sound path consumes 22050 Hz mono signed 16-bit PCM.
        internal static byte[] DecodeWave(byte[] wav)
        {
            if (wav == null || wav.Length < 12 || Id(wav, 0) != "RIFF" || Id(wav, 8) != "WAVE") throw new InvalidDataException("Expected RIFF WAVE.");
            bool format = false; int dataAt = 0, dataLength = 0;
            int declared = BitConverter.ToInt32(wav, 4);
            if (declared < 4 || declared > wav.Length - 8) throw new InvalidDataException("Truncated RIFF.");
            int end = declared + 8;
            for (int at = 12; at <= end - 8;)
            {
                int size = BitConverter.ToInt32(wav, at + 4);
                if (size < 0 || size > end - at - 8) throw new InvalidDataException("Invalid WAV chunk.");
                string id = Id(wav, at); int start = at + 8;
                if (id == "fmt ")
                {
                    format = size >= 16 && BitConverter.ToUInt16(wav, start) == 1
                        && BitConverter.ToUInt16(wav, start + 2) == 1 && BitConverter.ToInt32(wav, start + 4) == 22050
                        && BitConverter.ToInt32(wav, start + 8) == 44100 && BitConverter.ToUInt16(wav, start + 12) == 2
                        && BitConverter.ToUInt16(wav, start + 14) == 16;
                }
                else if (id == "data") { dataAt = start; dataLength = size; }
                at = start + size + (size & 1);
            }
            if (!format || dataLength < 2 || dataLength > 30 * 44100 || (dataLength & 1) != 0) throw new InvalidDataException("Use PCM16 mono 22050 Hz, maximum 30 seconds.");
            var pcm = new byte[dataLength]; Array.Copy(wav, dataAt, pcm, 0, dataLength); return pcm;
        }
        private static string Id(byte[] data, int at) => System.Text.Encoding.ASCII.GetString(data, at, 4);
    }
}
