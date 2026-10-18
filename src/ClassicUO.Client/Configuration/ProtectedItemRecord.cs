using System.Collections.Generic;

namespace ClassicUO.Configuration
{
    public sealed class ProtectedItemRecord
    {
        public uint Serial { get; set; }
        public string Name { get; set; } = "";
        public List<uint> Ancestors { get; set; } = new List<uint>();
    }
}
