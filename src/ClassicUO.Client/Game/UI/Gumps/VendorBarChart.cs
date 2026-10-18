using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorBarChart : Control
    {
        internal sealed class Bar
        {
            internal string Name, Detail;
            internal decimal? Value;
        }

        internal VendorBarChart(string title, IReadOnlyList<Bar> values, int width, int rowHeight = 28)
        {
            Width = width; Height = 30 + values.Count * rowHeight; WantUpdateSize = false;
            Add(new Label(title, true, 0x0481, width, font: 1, style: FontStyle.Cropped));
            decimal max = values.Where(v => v.Value.HasValue).Select(v => Math.Max(0, v.Value.Value)).DefaultIfEmpty(0).Max();
            int caption = width / 3, graph = width - caption - 100;
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i]; int y = 28 + i * rowHeight;
                var label = new Label(value.Name, true, 0x0481, caption - 8, font: 1, style: FontStyle.Cropped) { Y = y };
                label.SetTooltip(value.Detail ?? value.Name); Add(label);
                Add(new AlphaBlendControl(1) { X = caption, Y = y + 3, Width = graph, Height = 12, BaseColor = new Color(17, 44, 32) });
                if (max > 0 && value.Value > 0)
                {
                    var bar = new AlphaBlendControl(1) { X = caption, Y = y + 3, Width = Math.Max(1, (int)(graph * value.Value.Value / max)), Height = 12,
                        BaseColor = new Color(181, 143, 66), AcceptMouseInput = true };
                    bar.SetTooltip(value.Detail ?? value.Name); Add(bar);
                }
                Add(new Label(value.Value?.ToString("N2", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.') ?? "unknown",
                    true, 0x0481, 96, font: 1, style: FontStyle.Cropped) { X = caption + graph + 4, Y = y });
            }
        }
    }
}
