using System.Collections.Generic;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class ForcedTooltipManager
    {
        //This class is inteded to help generate tooltips for server before tooltips existed.

        private static Dictionary<uint, long> _requestedSingleClick = new Dictionary<uint, long>();
        private static long DELAY = 500;
        private const uint UPDATE_DELAY = 1500;
        public static void RequestName(uint serial)
        {
            if (World.ClientFeatures.TooltipsEnabled) return;

            if (_requestedSingleClick.ContainsKey(serial))
                if (Time.Ticks < _requestedSingleClick[serial])
                    return;

            if (World.OPL.TryGetRevision(serial, out var revision) && revision > Time.Ticks) return;

            _requestedSingleClick[serial] = Time.Ticks + DELAY;
            GameActions.SingleClick(serial);
        }

        public static bool IsObjectTextRequested(Entity parent, string text, ushort hue)
        {
            if (parent == null || World.ClientFeatures.TooltipsEnabled) return false;

            if (!_requestedSingleClick.ContainsKey(parent.Serial)) return false;

            if (Time.Ticks > _requestedSingleClick[parent.Serial])
            {
                _requestedSingleClick.Remove(parent.Serial);
                return false;
            }

            if ((World.OPL.TryGetRevision(parent.Serial, out uint rev) && rev >= Time.Ticks) && World.OPL.TryGetNameAndData(parent.Serial, out var name, out var data))
            {
                if (!string.IsNullOrEmpty(name) && name != text) //Item has a name, but it doesn't match what was sent. This could be another line of data.
                {

                    if ((!string.IsNullOrEmpty(data) && data.IndexOf(text) < 0) || string.IsNullOrEmpty(data)) //Annoying IndexOf returns 0 is string is string.Empty
                    {
                        World.OPL.Add(parent.Serial, Time.Ticks + UPDATE_DELAY, name, data + "\n" + text, 0);
                    }
                }
                else if (string.IsNullOrEmpty(name)) //Name is empty
                {
                    World.OPL.Add(parent.Serial, Time.Ticks + UPDATE_DELAY, text, string.Empty, 0);
                }
            }
            else
            {
                World.OPL.Add(parent.Serial, Time.Ticks + UPDATE_DELAY, text, string.Empty, 0);
            }
            return true;
        }
    }
}
