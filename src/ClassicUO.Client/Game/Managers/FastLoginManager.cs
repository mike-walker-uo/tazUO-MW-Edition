using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Scenes;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal static class FastLoginManager
    {
        internal const int SlotCount = 5;
        private static Store _store;
        private static Settings _currentLogin;
        private static int _startupSlot = -1;
        internal static bool UsingStartupSnapshot { get; private set; }
        internal static Store Slots => _store ?? (_store = new Store(Path.Combine(CUOEnviroment.ExecutablePath, "Data", "FastLogin")));

        internal sealed class Store
        {
            private readonly string _directory;
            private readonly Settings[] _slots = new Settings[SlotCount];
            private readonly string[] _errors = new string[SlotCount];
            internal Store(string directory) { _directory = directory; }
            internal string FilePath(int slot)
            {
                if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
                return Path.Combine(_directory, "slot-" + (slot + 1) + ".json");
            }
            internal Settings Get(int slot) => _slots[slot];
            internal string Error(int slot) => _errors[slot];
            internal void Reload()
            {
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    _slots[slot] = null; _errors[slot] = null;
                    string path = FilePath(slot);
                    try
                    {
                        if (!File.Exists(path)) continue;
                        var value = JsonSerializer.Deserialize(File.ReadAllText(path), typeof(Settings), SettingsJsonContext.RealDefault) as Settings;
                        if (!Valid(value)) throw new InvalidDataException();
                        _slots[slot] = value;
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException
                        || ex is JsonException || ex is NotSupportedException || ex is FormatException
                        || ex is ArgumentException || ex is OverflowException)
                    { _errors[slot] = "Slot could not be read. Clear it or save it again."; }
                }
            }
            internal void Save(int slot, Settings value)
            {
                string path = FilePath(slot);
                if (!Valid(value)) throw new InvalidDataException("Incomplete fast login slot.");
                Settings snapshot = Clone(value);
                string json = JsonSerializer.Serialize(snapshot, typeof(Settings), SettingsJsonContext.RealDefault);
                Directory.CreateDirectory(_directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, json);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                _slots[slot] = snapshot; _errors[slot] = null;
            }
            internal void Clear(int slot)
            {
                File.Delete(FilePath(slot));
                _slots[slot] = null; _errors[slot] = null;
            }
        }

        internal static bool Valid(Settings value) => value != null && !string.IsNullOrWhiteSpace(value.Username)
            && !string.IsNullOrEmpty(value.Password) && !string.IsNullOrWhiteSpace(value.IP) && value.Port != 0
            && !string.IsNullOrWhiteSpace(value.LastServerName) && !string.IsNullOrWhiteSpace(value.FastLoginCharacter);

        internal static Settings Clone(Settings source) => JsonSerializer.Deserialize(
            JsonSerializer.Serialize(source, typeof(Settings), SettingsJsonContext.Default), typeof(Settings), SettingsJsonContext.RealDefault) as Settings;

        // Only connection values are applied in an already running client. Assets, plugins and global
        // preferences remain those loaded at startup. Slot files change only through explicit Save.
        internal static void CopyConnection(Settings source, Settings target)
        {
            target.Username = source.Username; target.Password = source.Password;
            target.IP = source.IP; target.Port = source.Port; target.Encryption = source.Encryption;
            target.LastServerName = source.LastServerName; target.LastServerNum = source.LastServerNum;
            target.FastLoginCharacter = source.FastLoginCharacter;
        }

        internal static bool Compatible(Settings slot, Settings runtime) =>
            string.Equals(slot.ClientVersion, runtime.ClientVersion, StringComparison.OrdinalIgnoreCase) && slot.ShardType == runtime.ShardType;

        internal static bool Prepare(int slot, out Settings settings, out string error)
        {
            settings = null; error = null;
            if (slot < 0 || slot >= SlotCount) { error = "Fast login slot must be between 1 and 5."; return false; }
            Settings saved = Slots.Get(slot);
            if (saved == null) { error = Slots.Error(slot) ?? "Empty slot. Log in normally, then use -fastlogin to save a character."; return false; }
            if (!Compatible(saved, Settings.GlobalSettings))
            { error = "Slot uses another client version or shard setup. Save it again with this client."; return false; }
            settings = Clone(saved);
            return true;
        }

        internal static void CaptureSuccessfulLogin(LoginScene scene)
        {
            _currentLogin = null;
            try
            {
                string character = scene.SelectedCharacterName ?? ProfileManager.CurrentProfile?.CharacterName;
                if (string.IsNullOrEmpty(character)) return;
                Settings snapshot = Clone(Settings.GlobalSettings);
                CopyConnection(scene.ConnectionSettings, snapshot);
                snapshot.Username = LoginScene.Account;
                snapshot.Password = Crypter.Encrypt(scene.Password);
                snapshot.LastServerName = World.ServerName;
                snapshot.LastServerNum = (ushort)(scene.ServerIndex + 1);
                snapshot.FastLoginCharacter = character;
                snapshot.AutoLogin = snapshot.SaveAccount = true;
                if (Valid(snapshot)) _currentLogin = snapshot;
            }
            catch (Exception ex)
            { Log.Warn("Fast login capture unavailable: " + ex.GetType().Name); }
        }

        internal static Settings ReconnectSettings(string account) => _currentLogin != null
            && string.Equals(_currentLogin.Username, account, StringComparison.Ordinal) ? Clone(_currentLogin) : null;

        internal static bool CanSave => World.InGame && _currentLogin != null
            && ProfileManager.CurrentProfile?.Username == _currentLogin.Username
            && ProfileManager.CurrentProfile?.CharacterName == _currentLogin.FastLoginCharacter
            && World.ServerName == _currentLogin.LastServerName;

        internal static bool SaveCurrent(int slot, out string error)
        {
            error = null;
            if (!CanSave) { error = "Log in successfully before saving a character."; return false; }
            try
            {
                Settings snapshot = Clone(Settings.GlobalSettings);
                CopyConnection(_currentLogin, snapshot);
                snapshot.AutoLogin = snapshot.SaveAccount = true;
                Slots.Save(slot, snapshot);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException
                || ex is JsonException || ex is NotSupportedException)
            { error = "Could not save slot. Check access to Data/FastLogin."; return false; }
        }

        internal static int FindCharacter(string[] characters, string requested)
        {
            if (characters == null) return -1;
            for (int i = 0; i < characters.Length; i++)
                if (!string.IsNullOrEmpty(characters[i]) && (string.IsNullOrEmpty(requested)
                    || string.Equals(characters[i], requested, StringComparison.Ordinal))) return i;
            return -1;
        }

        internal static void SetStartupSlot(string value)
        { _startupSlot = int.TryParse(value, out int number) && number >= 1 && number <= SlotCount ? number - 1 : -2; }
        internal static void ApplyStartupSettingsPath()
        {
            UsingStartupSnapshot = false;
            if (_startupSlot < 0) return;
            Slots.Reload();
            if (Slots.Get(_startupSlot) == null) return;
            Settings.CustomSettingsFilepath = Slots.FilePath(_startupSlot);
            UsingStartupSnapshot = true;
        }
        internal static Settings StartupSettings() => UsingStartupSnapshot ? Clone(Slots.Get(_startupSlot)) : null;
        internal static bool ConsumeStartupSlot(out int slot)
        { slot = _startupSlot; _startupSlot = -1; return slot != -1; }

        internal static string ShortcutTarget(string executable, int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return "\"" + executable + "\" -fastlogin " + (slot + 1);
        }
    }
}
