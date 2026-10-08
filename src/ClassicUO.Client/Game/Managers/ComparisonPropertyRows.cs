using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Utility;
using ClassicUO.Assets;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal static class ComparisonPropertyRows
    {
        internal static Color HueSwatch(ushort hue) => hue == 0 || hue == 0xFFFF ? Color.White
            : new Color { PackedValue = HuesLoader.Instance.GetHueColorRgba8888(30, hue) };

        internal sealed class Row
        {
            internal string Name;
            internal string[] Values;
            internal bool Changed;
        }
        internal static List<Row> Build(ItemPropertiesData[] items)
        {
            var maps = new List<Dictionary<string, ItemPropertiesData.SinglePropertyData>>();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var map = new Dictionary<string, ItemPropertiesData.SinglePropertyData>(StringComparer.OrdinalIgnoreCase);
                var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in item.singlePropertyData)
                {
                    if (string.IsNullOrWhiteSpace(property.Name)) continue;
                    occurrences.TryGetValue(property.Name, out int count);
                    occurrences[property.Name] = ++count;
                    string key = property.Name + "#" + count;
                    map[key] = property; names[key] = property.Name;
                }
                maps.Add(map);
            }
            var rows = new List<Row>();
            foreach (var name in names)
            {
                var values = new string[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    if (!items[i].HasData) { values[i] = "Unknown (OPL pending)"; continue; }
                    if (!maps[i].TryGetValue(name.Key, out var property)) { values[i] = "Not listed (unknown)"; continue; }
                    string original = RegexHelper.GetRegex(@"/c\[[#a-zA-Z0-9]+\]", RegexOptions.IgnoreCase)
                        .Replace(property.OriginalString, "").Replace("/cd", "").Trim();
                    values[i] = property.FirstValue == double.MinValue ? "Present"
                        : original.StartsWith(property.Name, StringComparison.OrdinalIgnoreCase)
                            ? original.Substring(property.Name.Length).Trim() : original;
                    if (i == 0 && items.Length > 1 && maps[1].TryGetValue(name.Key, out var equipped)
                        && property.FirstValue != double.MinValue && equipped.FirstValue != double.MinValue)
                    {
                        double delta = property.FirstValue - equipped.FirstValue;
                        if (delta != 0) values[i] += " (" + (delta > 0 ? "+" : "") + delta.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                }
                rows.Add(new Row { Name = name.Value, Values = values,
                    Changed = values.Distinct(StringComparer.Ordinal).Count() > 1 });
            }
            return rows;
        }
    }
}
