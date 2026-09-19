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
        public string Phrase { get; set; } = string.Empty;
    }
}
