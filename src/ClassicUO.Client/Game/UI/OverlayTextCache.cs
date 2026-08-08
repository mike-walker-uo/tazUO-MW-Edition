#region license
// TazUO addition. Overlays used to call TextBox.GetOne(...)+Dispose() every
// frame — a full rich-text layout per call. This cache keeps one TextBox per
// call site (slot) and only relayouts when the string/color actually changes.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI
{
    public static class OverlayTextCache
    {
        private sealed class Slot
        {
            public string Text;
            public Color Color;
            public float Size;
            public TextBox Tb;
        }

        private static readonly Dictionary<string, Slot> _slots =
            new Dictionary<string, Slot>(StringComparer.Ordinal);

        /// <summary>
        /// Returns a cached TextBox for this slot key; relayouts only when
        /// text/size/color changed. Do NOT Dispose the returned instance.
        /// </summary>
        public static TextBox Get(string key, string text, float size, Color color, int? width = null)
        {
            if (!_slots.TryGetValue(key, out var s))
            {
                s = new Slot();
                _slots[key] = s;
            }

            if (s.Tb == null || s.Tb.IsDisposed
                || s.Size != size || s.Color != color
                || !string.Equals(s.Text, text, StringComparison.Ordinal))
            {
                s.Tb?.Dispose();
                s.Tb = TextBox.GetOne(text, Assets.TrueTypeLoader.EMBEDDED_FONT, size, color,
                    new TextBox.RTLOptions { Width = width });
                s.Text = text;
                s.Size = size;
                s.Color = color;
            }

            return s.Tb;
        }
    }
}
