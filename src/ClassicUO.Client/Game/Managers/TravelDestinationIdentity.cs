using System;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal static class TravelDestinationIdentity
    {
        internal static string NameKey(WorldExplorerPin pin) => (pin.Name ?? "").Trim().ToUpperInvariant();
        internal static string SourceKey(WorldExplorerPin pin) => $"{pin.Kind}:{pin.Serial:X8}:{pin.Slot}:" + (pin.Phrase ?? "").ToUpperInvariant();
        internal static string Observation(WorldExplorerPin pin)
        {
            string date = pin.LastScannedUtcTicks > 0 && pin.LastScannedUtcTicks <= DateTime.MaxValue.Ticks
                ? new DateTime(pin.LastScannedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("g") : "unknown";
            return $"Source 0x{pin.Serial:X8} / slot {pin.Slot + 1} / observed hue "
                + (pin.ObservedHue.HasValue ? $"0x{pin.ObservedHue.Value:X4}" : "unknown")
                + "\nLast scan: " + date + " / facet unknown";
        }
    }
}
