using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static ClassicUO.Game.UI.Gumps.WorldMapGump;

namespace ClassicUO.Game.Managers
{
    internal static class WorldMapMarkerCsv
    {
        internal static string Format(WMapMarker marker)
        {
            return string.Join(",", new[]
            {
                marker.X.ToString(CultureInfo.InvariantCulture),
                marker.Y.ToString(CultureInfo.InvariantCulture),
                marker.MapId.ToString(CultureInfo.InvariantCulture),
                marker.Name, marker.MarkerIconName, marker.ColorName,
                marker.ZoomIndex.ToString(CultureInfo.InvariantCulture)
            }.Select(Quote));
        }

        private static string Quote(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // Quoted fields may contain commas, doubled quotes and line breaks.
        // A malformed record is returned as null so the caller can report/skip it.
        internal static IEnumerable<string[]> Read(TextReader reader)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false, closed = false, invalid = false, started = false;
            int value;
            while ((value = reader.Read()) >= 0)
            {
                char c = (char)value;
                started = true;
                if (quoted)
                {
                    if (c != '"') field.Append(c);
                    else if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else { quoted = false; closed = true; }
                    continue;
                }

                if (c == ',' || c == '\r' || c == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    closed = false;
                    if (c == ',') continue;
                    if (c == '\r' && reader.Peek() == '\n') reader.Read();
                    if (fields.Count > 1 || fields[0].Length > 0)
                        yield return invalid ? null : fields.ToArray();
                    fields.Clear();
                    invalid = started = false;
                }
                else if (c == '"')
                {
                    if (field.Length == 0 && !closed) quoted = true;
                    else invalid = true;
                }
                else if (closed)
                {
                    if (c != ' ' && c != '\t') invalid = true;
                }
                else field.Append(c);
            }

            if (started)
            {
                fields.Add(field.ToString());
                yield return invalid || quoted ? null : fields.ToArray();
            }
        }
    }
}
