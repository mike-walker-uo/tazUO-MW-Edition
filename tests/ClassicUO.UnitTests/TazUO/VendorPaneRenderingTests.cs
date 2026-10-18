using System;
using System.Reflection;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class VendorPaneRenderingTests
    {
        [Theory]
        [InlineData(typeof(VendorMarketWatchGump), "_watches")]
        [InlineData(typeof(VendorMarketWatchGump), "_results")]
        [InlineData(typeof(VendorPricingGump), "_summary")]
        public void Vendor_content_hosts_draw_new_rows_after_refresh(Type gumpType, string fieldName)
        {
            bool previousDebug = CUOEnviroment.Debug;
            try
            {
                CUOEnviroment.Debug = false;
                // Use each production field's declared container type without loading game fonts or textures.
                var field = gumpType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                var pane = (Control)Activator.CreateInstance(field.FieldType, new object[] { 0, 0, 200, 100 });
                var first = new DrawProbe { X = 4, Y = 6 };
                pane.Add(first);
                Assert.True(pane.Draw(null, 20, 30));
                Assert.Equal(1, first.Draws); Assert.Equal(24, first.LastX); Assert.Equal(36, first.LastY);

                pane.Clear(); pane.Children.RemoveAll(c => c.IsDisposed);
                var replacement = new DrawProbe(); pane.Add(replacement);
                Assert.True(first.IsDisposed);
                Assert.True(pane.Draw(null, 20, 30));
                Assert.Equal(1, first.Draws); Assert.Equal(1, replacement.Draws);
                pane.Dispose();
            }
            finally { CUOEnviroment.Debug = previousDebug; }
        }

        private sealed class DrawProbe : Control
        {
            internal int Draws, LastX, LastY;
            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            { Draws++; LastX = x; LastY = y; return true; }
        }
    }
}
