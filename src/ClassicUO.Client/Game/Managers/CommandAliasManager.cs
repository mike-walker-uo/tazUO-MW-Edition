#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// User-defined command aliases. `-alias <newname> <existing>` registers
    /// a new chat command that invokes the existing handler. Persisted to
    /// `{ProfilePath}/aliases.tsv` and replayed at Initialize.
    /// </summary>
    public static class CommandAliasManager
    {
        private const string FILENAME = "aliases.tsv";
        private static readonly Dictionary<string, string> _aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyDictionary<string, string> All => _aliases;

        public static void Load()
        {
            UnloadBindings();
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                Bind(bits[0], bits[1], false);
            }
        }

        public static void ResetForProfile() => UnloadBindings();

        private static void UnloadBindings()
        {
            foreach (string name in _aliases.Keys) CommandManager.UnRegister(name);
            _aliases.Clear();
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _aliases) sw.WriteLine(kv.Key + "\t" + kv.Value);
            });
        }

        public static void Bind(string newName, string existing, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(existing)) return;
            newName = newName.Trim(); existing = existing.Trim();
            if (!CommandManager.Commands.TryGetValue(existing, out var handler))
            {
                GameActions.Print($"Unknown command '{existing}'.", 0x21);
                return;
            }
            if (CommandManager.Commands.ContainsKey(newName) && !_aliases.ContainsKey(newName))
            {
                GameActions.Print($"Cannot replace built-in command '{newName}'.", 0x21);
                return;
            }
            _aliases[newName] = existing;
            CommandManager.Register(newName, handler);
            CommandMetadata.Synchronize(CommandManager.Commands.Keys);
            if (persist) { Save(); GameActions.Print($"Alias -{newName} → -{existing}.", 0x35); }
        }

        public static void Remove(string name)
        {
            if (_aliases.Remove(name?.Trim() ?? string.Empty))
            {
                CommandManager.UnRegister(name);
                CommandMetadata.Synchronize(CommandManager.Commands.Keys);
                Save();
                GameActions.Print($"Alias -{name} removed.", 0x21);
            }
        }
    }
}
