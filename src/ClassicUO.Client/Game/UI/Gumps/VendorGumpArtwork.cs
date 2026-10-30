using System;
using System.IO;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorGumpArtwork : Control
    {
        private static Texture2D _texture;
        private static bool _failed;
        private static Texture2D _buttonTexture;
        private static bool _buttonFailed;

        internal VendorGumpArtwork(int width, int height)
        { Width = width; Height = height; WantUpdateSize = false; AcceptMouseInput = false; }

        private static Texture2D Texture()
        {
            if (_failed) return null;
            if (_texture != null && !_texture.IsDisposed) return _texture;
            try
            {
                using (var stream = typeof(VendorGumpArtwork).Assembly.GetManifestResourceStream("ClassicUO.Resources.VendorLedger.royal-panel.png"))
                {
                    if (stream == null) throw new FileNotFoundException("Missing vendor ledger artwork");
                    _texture = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                }
                OptionalTextureCache.Register(_texture, () => _texture = null);
                return _texture;
            }
            catch (Exception ex) { _failed = true; Log.Error("Vendor artwork: " + ex); return null; }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            var texture = Texture();
            if (texture == null)
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(10, 30, 23)), new Rectangle(x, y, Width, Height), new Vector3(0, 0, 1));
            else
            {
                OptionalTextureCache.Touch(texture);
                int edge = Math.Min(28, Math.Min(Width, Height) / 4), sourceEdge = 320;
                int[] sx = { 0, sourceEdge, texture.Width - sourceEdge }, sy = { 0, sourceEdge, texture.Height - sourceEdge };
                int[] sw = { sourceEdge, texture.Width - 2 * sourceEdge, sourceEdge }, sh = { sourceEdge, texture.Height - 2 * sourceEdge, sourceEdge };
                int[] dx = { x, x + edge, x + Width - edge }, dy = { y, y + edge, y + Height - edge };
                int[] dw = { edge, Width - 2 * edge, edge }, dh = { edge, Height - 2 * edge, edge };
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        batcher.Draw(texture, new Rectangle(dx[col], dy[row], dw[col], dh[row]),
                            new Rectangle(sx[col], sy[row], sw[col], sh[row]), new Vector3(0, 0, 1));
            }
            return base.Draw(batcher, x, y);
        }

        internal static void Enhance(Gump gump)
        {
            if (!gump.IsFromServer || ProfileManager.CurrentProfile == null) return;
            VendorSearchWatchManager.ObserveGump(gump);
            string raw = gump.PacketGumpText ?? "";
            if (raw.IndexOf("vendor", StringComparison.OrdinalIgnoreCase) < 0 && raw.IndexOf("Set Price", StringComparison.OrdinalIgnoreCase) < 0
                && raw.IndexOf("Shop Name", StringComparison.OrdinalIgnoreCase) < 0
                && !gump.Children.Any(c => c.Y <= 80 && (VendorLedgerManager.Text(c).IndexOf("vendor", StringComparison.OrdinalIgnoreCase) >= 0
                    || VendorLedgerManager.Text(c).IndexOf("Set Price", StringComparison.OrdinalIgnoreCase) >= 0))) return;
            var cells = VendorLedgerManager.Cells(gump);
            var kind = VendorLedgerManager.Recognize(string.Join("\n", cells.Select(c => c.Text)));
            if (kind == VendorGumpKind.None) return;
            var record = VendorLedgerManager.ObserveGump(gump, kind, cells);
            gump.IsVisible = true;
            gump.SuppressPersistence = false;
            if (!ProfileManager.CurrentProfile.IllustratedVendorGumps) return;
            if (kind == VendorGumpKind.History)
            {
                if (VendorRefreshManager.OwnsHistory(gump)) { gump.IsVisible = false; gump.SuppressPersistence = true; return; }
                if (record != null && VendorLedgerManager.ParseSales(cells) != null)
                {
                    gump.IsVisible = false;
                    gump.SuppressPersistence = true;
                    VendorLedgerGump.Open(record, 1, gump);
                }
                else Log.Warn("Vendor ledger: unsupported sales table; retaining original gump.\n" + gump.PacketGumpText);
                return; // Unknown table layouts keep their original appearance and controls.
            }
            var original = gump.Children.Where(c => !c.IsDisposed).ToArray();
            int width = Math.Max(200, original.Max(c => c.X + c.Width)) + 12;
            int height = Math.Max(100, original.Max(c => c.Y + c.Height)) + 40;
            foreach (Control child in original)
            {
                if (child is GumpPicBase || child is AlphaBlendControl) child.IsVisible = false;
                if (child is ResizePic && !original.OfType<StbTextBox>().Any(t => t.X >= child.X && t.Y >= child.Y
                    && t.X < child.X + child.Width && t.Y < child.Y + child.Height)) child.IsVisible = false;
                if (child is Label label && label.Hue <= 1) label.Hue = 0x0481;
                if (child is CroppedText cropped && cropped.Hue <= 1) cropped.Hue = 0x0481;
            }
            gump.Insert(0, new VendorGumpArtwork(width, height));
            string[] captions = kind == VendorGumpKind.Owner
                ? new[] { "See goods", "Customize", "Rename Shop", "Rename Vendor", "Open Paperdoll", "Collect Gold", "Deposit Gold", "Dismiss Vendor", "View Sales History", "CANCEL" }
                : new[] { "LIST ITEM", "CANCEL LISTING", "CALCULATE FEES", "CLEAR" };
            foreach (string caption in captions)
            {
                var action = VendorRefreshManager.Action(gump, caption);
                var label = original.FirstOrDefault(c => VendorLedgerManager.Text(c).Equals(caption, StringComparison.OrdinalIgnoreCase));
                if (action == null || label == null) continue;
                action.IsVisible = label.IsVisible = false;
                int id = action.ButtonID;
                gump.Add(new VendorLocalButton(action.X - 2, Math.Min(action.Y, label.Y) - 2,
                    Math.Max(100, label.X + label.Width - action.X + 12), caption, () => gump.OnButtonClick(id)));
            }
            if (kind == VendorGumpKind.Owner && record != null)
                gump.Add(new VendorLocalButton(30, height - 34, 190, "Vendor ledger", () => VendorLedgerGump.Open(record)));
            if (kind == VendorGumpKind.Listing && original.OfType<StbTextBox>().Count() == 2)
                gump.Add(new VendorLocalButton(30, height - 34, 190, "Listing templates", () => VendorListingTemplatesGump.Open(gump)));
            if (width >= 540)
            {
                if (kind == VendorGumpKind.Listing)
                    gump.Add(new VendorLocalButton(230, height - 34, 140, "Pricing / fees", () => VendorPricingGump.Open(listing: gump)));
                else gump.Add(new VendorLocalButton(230, height - 34, 140, "Vendor Search", () => VendorSearchWatchManager.OpenSearch()));
                gump.Add(new VendorLocalButton(380, height - 34, 140, "Market watch", VendorMarketWatchGump.Open));
                if (kind == VendorGumpKind.Listing && width >= 690)
                    gump.Add(new VendorLocalButton(530, height - 34, 140, "Vendor Search", () => VendorSearchWatchManager.OpenSearch()));
            }
        }

        internal static void DrawButton(UltimaBatcher2D batcher, int x, int y, int width, int height, bool hover)
        {
            if (!_buttonFailed && (_buttonTexture == null || _buttonTexture.IsDisposed))
                try
                {
                    using (var stream = typeof(VendorGumpArtwork).Assembly.GetManifestResourceStream("ClassicUO.Resources.VendorLedger.royal-button.png"))
                    {
                        if (stream == null) throw new FileNotFoundException("Missing vendor button artwork");
                        _buttonTexture = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                    }
                    OptionalTextureCache.Register(_buttonTexture, () => _buttonTexture = null);
                }
                catch (Exception ex) { _buttonFailed = true; Log.Error("Vendor button artwork: " + ex); }
            if (_buttonTexture != null && !_buttonTexture.IsDisposed)
            {
                OptionalTextureCache.Touch(_buttonTexture);
                // Keep the generated green material, without repeating ornate gold end caps on every action.
                var source = new Rectangle(_buttonTexture.Width / 5, _buttonTexture.Height / 4,
                    _buttonTexture.Width * 3 / 5, _buttonTexture.Height / 3);
                batcher.Draw(_buttonTexture, new Rectangle(x + 1, y + 1, width - 2, height - 2), source, new Vector3(0, 0, 1));
            }
            else batcher.Draw(SolidColorTextureCache.GetTexture(new Color(17, 53, 36)), new Rectangle(x, y, width, height), new Vector3(0, 0, 1));
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(hover ? new Color(168, 139, 79) : new Color(91, 77, 49)),
                x, y, width - 1, height - 1, new Vector3(0, 0, 1));
        }
    }

    internal sealed class VendorLocalButton : NiceButton
    {
        private readonly Action _click;
        internal VendorLocalButton(int x, int y, int width, string text, Action click)
            : base(x, y, width, 24, ButtonAction.Activate, text, hue: 0x0481, font: 1)
        {
            _click = click; IsSelectable = false;
        }
        public override void OnButtonClick(int buttonID) => _click();
        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            VendorGumpArtwork.DrawButton(batcher, x, y, Width, Height, MouseIsOver);
            return base.Draw(batcher, x, y);
        }
    }
}
