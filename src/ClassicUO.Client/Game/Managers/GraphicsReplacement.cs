using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClassicUO.Game.Managers
{
    internal static class GraphicsReplacement
    {
        private static Dictionary<ushort, GraphicChangeFilter> graphicChangeFilters = new Dictionary<ushort, GraphicChangeFilter>();
        public static Dictionary<ushort, GraphicChangeFilter> GraphicFilters { get { return graphicChangeFilters; } }
        private static HashSet<ushort> quickLookup = new HashSet<ushort>();
        public static void Load()
        {
            if (File.Exists(GetSavePath()))
            {
                try
                {
                    graphicChangeFilters = JsonSerializer.Deserialize<Dictionary<ushort, GraphicChangeFilter>>(File.ReadAllText(GetSavePath()));
                    foreach (var filter in graphicChangeFilters)
                        quickLookup.Add(filter.Key);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public static void Save()
        {
            if (graphicChangeFilters.Count > 0)
            {
                try
                {
                    File.WriteAllText(GetSavePath(), JsonSerializer.Serialize<Dictionary<ushort, GraphicChangeFilter>>(graphicChangeFilters));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to save mobile graphic change filter. {e.Message}");
                }
                graphicChangeFilters.Clear();
                quickLookup.Clear();
            }
            else
            {
                if (File.Exists(GetSavePath()))
                    File.Delete(GetSavePath());
            }
        }

        public static void Replace(ushort graphic, ref ushort newgraphic, ref ushort hue)
        {
            if (quickLookup.Contains(graphic))
            {
                var filter = graphicChangeFilters[graphic];
                newgraphic = filter.ReplacementGraphic;
                if (filter.NewHue != ushort.MaxValue)
                    hue = filter.NewHue;
            }
        }

        public static void ReplaceHue(ushort graphic, ref ushort hue)
        {
            if (quickLookup.Contains(graphic))
            {
                var filter = graphicChangeFilters[graphic];
                if (filter.NewHue != ushort.MaxValue)
                    hue = filter.NewHue;
            }
        }

        public static void ResetLists()
        {
            Dictionary<ushort, GraphicChangeFilter> newList = new Dictionary<ushort, GraphicChangeFilter>();
            quickLookup.Clear();

            foreach (var item in graphicChangeFilters)
            {
                newList.Add(item.Value.OriginalGraphic, item.Value);
                quickLookup.Add(item.Value.OriginalGraphic);
            }
            graphicChangeFilters = newList;
        }

        public static GraphicChangeFilter NewFilter(ushort originalGraphic, ushort newGraphic, ushort newHue = ushort.MaxValue)
        {
            if (!graphicChangeFilters.ContainsKey(originalGraphic))
            {
                GraphicChangeFilter f;
                graphicChangeFilters.Add(originalGraphic, f = new GraphicChangeFilter()
                {
                    OriginalGraphic = originalGraphic,
                    ReplacementGraphic = newGraphic,
                    NewHue = newHue
                });

                quickLookup.Add(originalGraphic);
                return f;

            }
            return null;
        }

        public static void DeleteFilter(ushort originalGraphic)
        {
            if (graphicChangeFilters.ContainsKey(originalGraphic))
                graphicChangeFilters.Remove(originalGraphic);

            if (quickLookup.Contains(originalGraphic))
                quickLookup.Remove(originalGraphic);
        }

        private static string GetSavePath()
        {
            return Path.Combine(CUOEnviroment.ExecutablePath, "Data", "MobileReplacementFilter.json");
        }
    }

    internal class GraphicChangeFilter
    {
        public ushort OriginalGraphic { get; set; }
        public ushort ReplacementGraphic { get; set; }
        public ushort NewHue { get; set; } = ushort.MaxValue;
    }
}