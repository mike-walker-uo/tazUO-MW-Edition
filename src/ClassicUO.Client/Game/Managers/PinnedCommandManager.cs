#region license
// TazUO addition. Persistent movable command buttons created by the command palette.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    internal static class PinnedCommandManager
    {
        private const string FILENAME = "pinned_commands.tsv";
        private const int SNAP_DISTANCE = 12;

        internal sealed class Group
        {
            internal int Id;
            internal int X;
            internal int Y;
            internal readonly List<string> Commands = new List<string>();
        }

        private static readonly List<Group> _groups = new List<Group>();
        private static bool _loaded;
        private static int _nextId = 1;

        internal static void Load()
        {
            ResetForProfile();
            _loaded = true;

            foreach (string line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 4 ||
                    !int.TryParse(fields[0], out int id) ||
                    !int.TryParse(fields[1], out int x) ||
                    !int.TryParse(fields[2], out int y))
                {
                    continue;
                }

                string command;
                try
                {
                    command = Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));
                }
                catch (FormatException)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(command)) continue;

                Group group = Find(id);
                if (group == null)
                {
                    group = new Group { Id = id, X = x, Y = y };
                    _groups.Add(group);
                }

                group.Commands.Add(command.Trim());
                if (id >= _nextId) _nextId = id + 1;
            }

            ShowAll();
        }

        internal static void ResetForProfile()
        {
            foreach (Gump gump in UIManager.Gumps)
            {
                if (gump is PinnedCommandGroupGump pinned) pinned.Dispose();
            }

            _groups.Clear();
            _loaded = false;
            _nextId = 1;
        }

        internal static void Pin(string commandLine)
        {
            EnsureLoaded();
            commandLine = Normalize(commandLine);
            if (commandLine.Length <= 1) return;

            foreach (Group group in _groups)
            {
                if (group.Commands.Exists(c => string.Equals(c, commandLine, StringComparison.OrdinalIgnoreCase)))
                {
                    GameActions.Print($"{commandLine} is already pinned.", 0x35);
                    return;
                }
            }

            int offset = _groups.Count % 8;
            var added = new Group
            {
                Id = _nextId++,
                X = 220 + offset * 18,
                Y = 80 + offset * 24
            };
            added.Commands.Add(commandLine);
            _groups.Add(added);
            Show(added);
            Save();
        }

        internal static void Execute(string commandLine)
        {
            commandLine = Normalize(commandLine);
            string body = commandLine.TrimStart('-').Trim();
            string[] args = body.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0) return;

            if (!CommandManager.Commands.ContainsKey(args[0]))
            {
                GameActions.Print($"Pinned command -{args[0]} is no longer registered.", 0x21);
                return;
            }

            CommandManager.Execute(args[0], args);
        }

        internal static void Remove(int groupId, string commandLine)
        {
            Group group = Find(groupId);
            if (group == null) return;

            group.Commands.RemoveAll(c => string.Equals(c, commandLine, StringComparison.OrdinalIgnoreCase));
            DisposeGump(groupId);

            if (group.Commands.Count == 0)
            {
                _groups.Remove(group);
            }
            else
            {
                Show(group);
            }

            Save();
        }

        internal static void UpdatePosition(int groupId, int x, int y)
        {
            Group group = Find(groupId);
            if (group == null) return;
            group.X = x;
            group.Y = y;
            Save();
        }

        internal static bool Undock(int groupId, string commandLine, int x, int y)
        {
            Group source = Find(groupId);

            if (source == null || source.Commands.Count <= 1)
            {
                return false;
            }

            int commandIndex = source.Commands.FindIndex(command => string.Equals(
                command, commandLine, StringComparison.OrdinalIgnoreCase));

            if (commandIndex < 0)
            {
                return false;
            }

            string command = source.Commands[commandIndex];
            source.Commands.RemoveAt(commandIndex);

            var detached = new Group
            {
                Id = _nextId++,
                X = x,
                Y = y
            };
            detached.Commands.Add(command);
            _groups.Add(detached);

            DisposeGump(source.Id);
            Show(source);
            Show(detached);
            Save();
            return true;
        }

        internal static void TryMergeNearby(PinnedCommandGroupGump source)
        {
            if (source == null || source.IsDisposed) return;

            PinnedCommandGroupGump target = null;
            foreach (Gump gump in UIManager.Gumps)
            {
                if (!(gump is PinnedCommandGroupGump candidate) ||
                    candidate == source || candidate.IsDisposed)
                {
                    continue;
                }

                bool near = source.X <= candidate.X + candidate.Width + SNAP_DISTANCE &&
                            source.X + source.Width + SNAP_DISTANCE >= candidate.X &&
                            source.Y <= candidate.Y + candidate.Height + SNAP_DISTANCE &&
                            source.Y + source.Height + SNAP_DISTANCE >= candidate.Y;
                if (near)
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null) return;

            Group sourceGroup = Find(source.GroupId);
            Group targetGroup = Find(target.GroupId);
            if (sourceGroup == null || targetGroup == null) return;

            foreach (string command in sourceGroup.Commands)
            {
                if (!targetGroup.Commands.Exists(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)))
                    targetGroup.Commands.Add(command);
            }

            targetGroup.X = target.X;
            targetGroup.Y = target.Y;
            _groups.Remove(sourceGroup);
            source.Dispose();
            target.Dispose();
            Show(targetGroup);
            Save();
        }

        internal static void RefreshGumps()
        {
            if (!_loaded) return;
            DisposeAllGumps();
            ShowAll();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        private static Group Find(int id) => _groups.Find(g => g.Id == id);

        private static string Normalize(string commandLine)
        {
            commandLine = (commandLine ?? string.Empty).Trim();
            return commandLine.StartsWith("-", StringComparison.Ordinal) ? commandLine : "-" + commandLine;
        }

        private static void ShowAll()
        {
            foreach (Group group in _groups) Show(group);
        }

        private static void Show(Group group)
        {
            UIManager.Add(new PinnedCommandGroupGump(group.Id, group.Commands)
            {
                X = group.X,
                Y = group.Y
            });
        }

        private static void DisposeGump(int groupId)
        {
            foreach (Gump gump in UIManager.Gumps)
            {
                if (gump is PinnedCommandGroupGump pinned && pinned.GroupId == groupId)
                    pinned.Dispose();
            }
        }

        private static void DisposeAllGumps()
        {
            foreach (Gump gump in UIManager.Gumps)
            {
                if (gump is PinnedCommandGroupGump pinned) pinned.Dispose();
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, writer =>
            {
                foreach (Group group in _groups)
                {
                    foreach (string command in group.Commands)
                    {
                        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(command));
                        writer.WriteLine($"{group.Id}\t{group.X}\t{group.Y}\t{encoded}");
                    }
                }
            });
        }
    }
}
