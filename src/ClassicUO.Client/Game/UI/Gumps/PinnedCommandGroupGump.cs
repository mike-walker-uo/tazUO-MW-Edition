#region license
// TazUO addition. Compact themed group of persistent command buttons.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PinnedCommandGroupGump : Gump
    {
        private const int PAD = 3;
        private const int BUTTON_HEIGHT = 21;

        internal int GroupId { get; }

        internal PinnedCommandGroupGump(int groupId, IReadOnlyList<string> commands) : base(0, 0)
        {
            GroupId = groupId;
            CanMove = true;
            CanCloseWithRightClick = false;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            int longest = 0;
            for (int i = 0; i < commands.Count; i++)
                longest = Math.Max(longest, commands[i]?.Length ?? 0);

            Width = Math.Max(76, Math.Min(230, 24 + longest * 7));
            Height = PAD * 2 + Math.Max(1, commands.Count) * BUTTON_HEIGHT;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.86f));

            for (int i = 0; i < commands.Count; i++)
            {
                string command = commands[i];
                var button = new NiceButton(PAD, PAD + i * BUTTON_HEIGHT, Width - PAD * 2, BUTTON_HEIGHT - 1,
                    ButtonAction.Activate, command, font: 1)
                {
                    IsSelectable = false,
                    DisplayBorder = true,
                    AlwaysShowBackground = true,
                    CanMove = true,
                    CanCloseWithRightClick = false
                };
                CustomGumpThemeManager.StyleButton(button);
                button.SetTooltip("Left-click: run command\nDrag: move; drop onto another pinned command to group\nRight-click: remove", 350);
                string captured = command;
                button.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                        PinnedCommandManager.Execute(captured);
                    else if (e.Button == MouseButtonType.Right)
                        PinnedCommandManager.Remove(GroupId, captured);
                };
                Add(button);
            }
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            PinnedCommandManager.UpdatePosition(GroupId, X, Y);
            PinnedCommandManager.TryMergeNearby(this);
        }
    }
}
