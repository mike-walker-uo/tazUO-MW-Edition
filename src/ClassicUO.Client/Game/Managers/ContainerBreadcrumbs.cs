using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers
{
    internal static class ContainerBreadcrumbs
    {
        internal readonly struct Step
        {
            internal readonly uint Serial;
            internal readonly string Name;
            internal readonly bool Available;
            internal Step(uint serial, string name, bool available)
            { Serial = serial; Name = name; Available = available; }
        }

        internal static List<Step> Build(uint serial)
        {
            var result = new List<Step>();
            var seen = new HashSet<uint>();
            while (SerialHelper.IsItem(serial) && seen.Add(serial) && result.Count < 32)
            {
                Item item = World.Items.Get(serial);
                if (item != null && !item.IsDestroyed)
                {
                    result.Add(new Step(serial, GridContainer.GridSlotManager.GetItemName(item) ?? "Container", true));
                    serial = item.Container;
                }
                else if (ItemFinderManager.TryGetKnownParent(serial, out uint parent, out string name))
                {
                    result.Add(new Step(serial, (name ?? "Container") + " [last-known]", false));
                    serial = parent;
                }
                else
                {
                    result.Add(new Step(serial, $"Container 0x{serial:X8} [last-known]", false));
                    break;
                }
            }
            result.Reverse();
            return result;
        }

        internal static void Open(uint serial)
        {
            Item item = World.Items.Get(serial);
            if (item == null || item.IsDestroyed || !item.ItemData.IsContainer)
            { GameActions.Print("That parent container is not currently available.", 0x35); return; }
            Gump existing = (Gump)UIManager.GetGump<GridContainer>(serial) ?? UIManager.GetGump<ContainerGump>(serial);
            if (existing != null && !existing.IsDisposed) { existing.SetInScreen(); existing.BringOnTop(); }
            else GameActions.DoubleClick(serial);
        }
    }

    internal sealed class ContainerBreadcrumbLabel : Label
    {
        private readonly uint _serial;
        internal ContainerBreadcrumbLabel(uint serial, string text, ushort hue, int width = 0)
            : base(text, true, hue, width, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
        {
            _serial = serial; AcceptMouseInput = true;
            SetTooltip(text + "\nClick to navigate known parent containers. Right-click closes the gump.");
        }
        internal void RefreshPath()
        {
            string path = Path(_serial);
            if (Text == path) return;
            Text = path;
            SetTooltip(path + "\nClick to navigate known parent containers. Right-click closes the gump.");
        }
        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                var menu = new ContextMenuControl();
                foreach (var step in ContainerBreadcrumbs.Build(_serial))
                {
                    uint serial = step.Serial;
                    menu.Add(step.Name, () => ContainerBreadcrumbs.Open(serial));
                }
                menu.Show();
                return;
            }
            base.OnMouseUp(x, y, button);
        }
        internal static string Path(uint serial) =>
            string.Join(" › ", ContainerBreadcrumbs.Build(serial).Select(step => step.Name));
    }
}
