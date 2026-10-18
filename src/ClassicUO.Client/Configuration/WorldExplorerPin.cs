namespace ClassicUO.Configuration
{
    // Saved with the character profile. Name verifies the scanned destination;
    // CustomName changes only its displayed label.
    public sealed class WorldExplorerPin
    {
        public string Kind { get; set; } = string.Empty;
        public uint Serial { get; set; }
        public int Slot { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CustomName { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public ushort? ObservedHue { get; set; }
        public long LastScannedUtcTicks { get; set; }
        public string Phrase { get; set; } = string.Empty;
        public TravelDestinationLocation Location { get; set; }
        public int? FacetHint { get; set; }
    }

    public sealed class TravelDestinationLocation
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int? Z { get; set; }
        public int Facet { get; set; }
        public bool Approximate { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
