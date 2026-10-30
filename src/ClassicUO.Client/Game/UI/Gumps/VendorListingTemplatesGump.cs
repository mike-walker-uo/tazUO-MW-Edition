using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorListingTemplatesGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly Gump _listing;
        private StbTextBox _name, _price, _description;
        private ScrollArea _list;
        private Label _status;

        private VendorListingTemplatesGump(Gump listing) : base(0, 0)
        {
            _listing = listing;
            if (_profile.VendorListingTemplates == null) _profile.VendorListingTemplates = new System.Collections.Generic.List<VendorListingTemplate>();
            Width = 680; Height = 510; WantUpdateSize = false; CanMove = true; AcceptMouseInput = true;
            CanCloseWithRightClick = true; CanCloseWithEsc = true;
            Add(new VendorGumpArtwork(Width, Height));
            Add(new Label("NAMED LISTING TEMPLATES", true, 0x0481, font: 1) { X = 28, Y = 22 });
            Add(new Label("Name", true, 0x0481, font: 1) { X = 28, Y = 66 });
            _name = Input(110, 62, 536, 80);
            Add(new Label("Price", true, 0x0481, font: 1) { X = 28, Y = 102 });
            _price = Input(110, 98, 536, 60);
            Add(new Label("Description", true, 0x0481, font: 1) { X = 28, Y = 138 });
            _description = Input(28, 164, 618, 1000, 84); _description.Multiline = true;
            Add(new VendorLocalButton(28, 264, 145, "Save / update", Save));
            Add(new VendorLocalButton(183, 264, 145, "Copy listing", CopyListing));
            Add(new VendorLocalButton(338, 264, 145, "Apply to listing", Apply));
            Add(new VendorLocalButton(493, 264, 153, "Delete named", Delete));
            _list = new ScrollArea(28, 308, 620, 130, true); Add(_list);
            _status = new Label("Templates fill fields only. Review sale status and server-calculated fees before listing.",
                true, 0x0386, 620, font: 1) { X = 28, Y = 454 }; Add(_status);
            RefreshList(); if (listing != null) CopyListing(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open(Gump listing)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            UIManager.GetGump<VendorListingTemplatesGump>()?.Dispose();
            UIManager.Add(new VendorListingTemplatesGump(listing));
        }

        internal static void OpenForRestock(VendorLedgerManager.RestockGroup group)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null || group == null) return;
            UIManager.GetGump<VendorListingTemplatesGump>()?.Dispose();
            var gump = new VendorListingTemplatesGump(null);
            string name = System.Text.RegularExpressions.Regex.Replace(group.ItemName, @"^\d+\s+", "").Trim();
            var template = gump._profile.VendorListingTemplates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            gump._name.SetText(name);
            if (template != null)
            {
                gump._price.SetText(template.Price); gump._description.SetText(template.Description);
                gump._status.Text = "Saved template loaded. Open the listing window and apply; review before listing.";
            }
            else if (group.Prices.Median.HasValue)
            {
                gump._price.SetText(Math.Round(group.Prices.Median.Value, 0, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture));
                gump._status.Text = "Median sale price suggested for the whole bundle. Open listing and apply; review before listing.";
            }
            else gump._status.Text = "Enter a price. Open the listing window and apply; review before listing.";
            UIManager.Add(gump);
        }

        private StbTextBox Input(int x, int y, int width, int length, int height = 30)
        {
            Add(new AlphaBlendControl(1) { X = x, Y = y, Width = width, Height = height, BaseColor = new Microsoft.Xna.Framework.Color(22, 21, 19) });
            var input = new StbTextBox(1, length, width - 12, true, hue: 0x0481) { X = x + 6, Y = y + 4, Width = width - 12, Height = height - 8 };
            Add(input); return input;
        }
        private static bool IsListing(Gump gump) => gump != null && !gump.IsDisposed && gump.IsVisible && gump.IsFromServer
            && VendorLedgerManager.Recognize(string.Join("\n", VendorLedgerManager.Cells(gump).Select(c => c.Text))) == VendorGumpKind.Listing;

        private StbTextBox[] ListingFields()
        {
            Gump listing = _listing;
            if (listing != null && !IsListing(listing)) return Array.Empty<StbTextBox>();
            if (listing == null)
            {
                var candidates = UIManager.Gumps.Where(IsListing).Take(2).ToArray();
                if (candidates.Length != 1) return Array.Empty<StbTextBox>();
                listing = candidates[0];
            }
            return listing.Children.OfType<StbTextBox>().Where(t => t.IsFromServer && !t.IsDisposed)
                .OrderBy(t => t.Y).ThenBy(t => t.X).ToArray();
        }

        private void CopyListing()
        {
            var fields = ListingFields();
            if (fields.Length != 2) { _status.Text = "Open one Set Price and Description window, then try again."; return; }
            _price.SetText(fields[0].Text); _description.SetText(fields[1].Text);
        }
        private void Apply()
        {
            var fields = ListingFields();
            if (fields.Length != 2) { _status.Text = "Open one Set Price and Description window, then try again."; return; }
            fields[0].SetText(_price.Text); fields[1].SetText(_description.Text);
            _status.Text = "Fields filled. Sale checkboxes unchanged. Calculate fees, then review and List item yourself.";
        }
        private void Save()
        {
            string name = _name.Text.Trim();
            if (name.Length == 0) { _status.Text = "Enter a template name."; return; }
            var template = _profile.VendorListingTemplates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                if (_profile.VendorListingTemplates.Count >= 64) { _status.Text = "Template limit reached (64). Delete an unused template first."; return; }
                _profile.VendorListingTemplates.Add(template = new VendorListingTemplate());
            }
            template.Name = name; template.Price = _price.Text; template.Description = _description.Text;
            _profile.Save(ProfileManager.ProfilePath, false); RefreshList(); _status.Text = "Saved " + name + ".";
        }
        private void Delete()
        {
            _profile.VendorListingTemplates.RemoveAll(t => string.Equals(t.Name, _name.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            _profile.Save(ProfileManager.ProfilePath, false); RefreshList(); _status.Text = "Deleted the named template, if present.";
        }
        private void RefreshList()
        {
            _list.Clear(); int y = 0;
            foreach (var template in _profile.VendorListingTemplates.OrderBy(t => t.Name))
            {
                var saved = template;
                _list.Add(new VendorLocalButton(0, y, 585, saved.Name + " / " + saved.Price, () =>
                { _name.SetText(saved.Name); _price.SetText(saved.Price); _description.SetText(saved.Description); }));
                y += 34;
            }
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            base.Update();
        }
    }
}
