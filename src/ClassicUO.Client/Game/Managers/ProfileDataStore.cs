#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Shared, profile-scoped persistence for TazUO feature data. Writes go to
    /// a temporary file and replace the prior file only after a complete flush.
    /// </summary>
    public static class ProfileDataStore
    {
        public static string GetPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (Path.IsPathRooted(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Profile data filename must not contain a path.", nameof(fileName));
            if (string.IsNullOrEmpty(ProfileManager.ProfilePath)) return null;
            return Path.Combine(ProfileManager.ProfilePath, fileName);
        }

        public static string[] ReadAllLines(string fileName)
        {
            string path = GetPath(fileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Array.Empty<string>();

            try
            {
                return File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                Report(fileName, "read", ex);
                return Array.Empty<string>();
            }
        }

        public static string ReadAllText(string fileName)
        {
            string path = GetPath(fileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Report(fileName, "read", ex);
                return null;
            }
        }

        public static bool WriteAllLines(string fileName, IEnumerable<string> lines)
        {
            return Write(fileName, writer =>
            {
                if (lines == null) return;
                foreach (string line in lines) writer.WriteLine(line ?? string.Empty);
            });
        }

        public static bool WriteAllText(string fileName, string text)
        {
            return Write(fileName, writer => writer.Write(text ?? string.Empty));
        }

        public static bool Write(string fileName, Action<StreamWriter> write)
        {
            string path = GetPath(fileName);
            if (string.IsNullOrEmpty(path) || write == null) return false;
            string tempPath = path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    write(writer);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, path + ".bak1", true);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (Exception ex)
            {
                Report(fileName, "write", ex);
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return false;
            }
        }

        private static void Report(string fileName, string operation, Exception ex)
        {
            string feature = $"Persistence:{fileName}";
            FeatureDiagnostics.RecordFailure(feature + ":" + operation, ex);
        }
    }
}
