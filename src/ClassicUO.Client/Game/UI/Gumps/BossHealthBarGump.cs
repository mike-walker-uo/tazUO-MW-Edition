using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class BossHealthBarGump : Gump
    {
        private const int CleanHeightPixels = 76;
        private const int OrnateHeightPixels = 112;
        private const int MinimumWidth = 280;
        private const int MinimumOrnateWidth = 360;
        private const int MaximumWidth = 900;
        private const int OrnateCapWidth = 64;
        private const int OrnateCrestWidth = 208;
        private const int AutoShowRange = 18;

        // Boss names and display-name variants from https://uoalive.com/wiki/TAAGS,
        // https://uoalive.com/wiki/Champion_Spawns, https://uoalive.com/wiki/Peerless,
        // https://uoalive.com/wiki/Doom, https://uoalive.com/wiki/Shadowguard
        // and ServUO pub57 Scripts/Mobiles/Bosses.
        // The client receives scaled creature hits, so wire HitsMax cannot identify bosses.
        private static readonly HashSet<string> KnownBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abyssal infernal", "abyssmal horror", "abscess", "adrian", "andros",
            "anon", "anon the mage", "barracoon", "barracoon the piper",
            "charybdis", "chiikkaha the toothed", "chief paroxysmus", "coil",
            "cora the sorceress", "corgul the soulbinder", "crimson dragon",
            "dark father", "darknight creeper", "demon knight", "dragon turtle",
            "dread horn", "fleshrenderer", "gnaw", "grim", "grobu",
            "harrower", "ilhenir", "impaler", "irk", "juo'nar",
            "juo'nar the lich",
            "khal ankur", "lady melisande", "lord oaks", "malefic",
            "master theophilus", "medusa", "mephitis", "meraktus", "miasma",
            "monstrous interred grizzle", "moug-guur", "navrey", "navrey night eyes",
            "navrey night-eyes", "neira", "neira the necromancer", "night terror",
            "niporailem", "osiredon", "ozymandias", "ozymandias the lord of castle barataria",
            "pirate champion", "primeval lich",
            "putrefier", "pyre", "rend", "rikktor", "semidar", "serado",
            "serado the awakened", "shadow knight", "shadowlord",
            "shimmering effusion", "silvani", "slasher of veils", "stygian dragon",
            "swoop", "szavetra", "travesty", "true harrower", "twaulo",
            "tyball's shadow", "virtuebane", "virulent"
        };
        private static readonly HashSet<uint> DismissedBosses = new HashSet<uint>();

        private static Texture2D _ornateFrame;
        private readonly bool _automatic;

        private readonly HitBox _targetArea;
        private readonly NiceButton _optionsButton;
        private readonly Label _nameLabel;
        private readonly Label _percentLabel;
        private readonly Label _statusLabel;

        private static bool UseArtStyle => ProfileManager.CurrentProfile?.BossHealthBarOrnate == true;

        private static ushort FrameHue
        {
            get
            {
                switch (CustomGumpThemeManager.Current)
                {
                    case CustomGumpTheme.BritannianChronicle:
                    case CustomGumpTheme.MarinersChart:
                        return 0x0455;
                    case CustomGumpTheme.MoonglowArcane:
                    case CustomGumpTheme.TerMurRelic:
                    case CustomGumpTheme.Celestial:
                        return 0x0058;
                    case CustomGumpTheme.UOAlive:
                    case CustomGumpTheme.Heartwood:
                        return 0x044E;
                    case CustomGumpTheme.UOAlive2:
                        return 0x0058;
                    case CustomGumpTheme.Dungeon:
                    case CustomGumpTheme.Blackthorn:
                    case CustomGumpTheme.Doom:
                    case CustomGumpTheme.BloodOath:
                        return 0x0021;
                    case CustomGumpTheme.Exodus:
                        return 0x0455;
                    default:
                        return CustomGumpThemeManager.GetPanelHue(CustomGumpThemeManager.Current);
                }
            }
        }

        public override bool ShouldBeSaved => false;
        public override GumpType GumpType => GumpType.None;

        internal static void AutoTick()
        {
            if (!World.InGame || World.Player == null || ProfileManager.CurrentProfile == null)
                return;

            DismissedBosses.RemoveWhere(serial =>
                !(World.Get(serial) is Mobile mobile) || mobile.IsDestroyed || mobile.Distance > AutoShowRange);

            BossHealthBarGump existing = UIManager.GetGump<BossHealthBarGump>();
            if (existing != null && !existing.IsDisposed)
            {
                if (!existing._automatic)
                    return;
                if (IsTrackableAutoBoss(World.Get(existing.LocalSerial) as Mobile))
                    return;
                existing.Dispose();
            }

            Mobile closest = null;
            foreach (Mobile mobile in World.Mobiles.Values)
            {
                if (IsAutoBoss(mobile) && !DismissedBosses.Contains(mobile.Serial)
                    && (closest == null || mobile.Distance < closest.Distance))
                    closest = mobile;
            }

            if (closest != null)
                UIManager.Add(new BossHealthBarGump(closest.Serial, true));
        }

        internal static void ResetAuto() => DismissedBosses.Clear();

        private static bool IsAutoBoss(Mobile mobile)
        {
            return IsTrackableAutoBoss(mobile)
                && KnownBosses.Contains(MobHpTable.Normalize(mobile.Name) ?? string.Empty);
        }

        private static bool IsTrackableAutoBoss(Mobile mobile)
        {
            return mobile != null && !mobile.IsDestroyed && !mobile.IsDead
                && !mobile.IsPlayer && mobile != World.Player && !mobile.IsRenamable
                && mobile.NotorietyFlag != NotorietyFlag.Invulnerable
                && mobile.Distance <= AutoShowRange
                && !FriendsListManager.Instance.IsFriend(mobile.Serial)
                && !FriendsListManager.Instance.IsFriend(mobile.Name);
        }

        public static void Toggle(uint serial)
        {
            if (!(World.Get(serial) is Mobile mobile) || mobile.IsDead || mobile.IsPlayer || mobile == World.Player)
                return;

            BossHealthBarGump existing = UIManager.GetGump<BossHealthBarGump>();
            if (existing != null)
            {
                bool sameMobile = existing.LocalSerial == serial;
                existing.Dispose();
                if (sameMobile)
                {
                    DismissedBosses.Add(serial);
                    return;
                }
            }

            DismissedBosses.Remove(serial);
            UIManager.Add(new BossHealthBarGump(serial));
        }

        private BossHealthBarGump(uint serial, bool automatic = false) : base(serial, 0)
        {
            _automatic = automatic;
            CanMove = true;
            CanCloseWithRightClick = false;
            WantUpdateSize = false;
            int minimumWidth = UseArtStyle ? MinimumOrnateWidth : MinimumWidth;
            Width = Math.Max(minimumWidth, Math.Min(MaximumWidth, ProfileManager.CurrentProfile.BossHealthBarWidth));
            Height = UseArtStyle ? OrnateHeightPixels : CleanHeightPixels;
            ProfileManager.CurrentProfile.BossHealthBarWidth = Width;

            Point saved = ProfileManager.CurrentProfile.BossHealthBarPosition;
            if (saved == Point.Zero)
                CenterAtTop();
            else
                Location = saved;

            Add(_targetArea = new HitBox(0, 0, Width, Height, "Click to select; drag to move", 0f) { CanMove = true });
            _targetArea.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtonType.Left && Math.Max(Math.Abs(Mouse.LDragOffset.X), Math.Abs(Mouse.LDragOffset.Y)) < 2)
                    SelectBoss();
            };

            Add(_nameLabel = new Label(string.Empty, true, 0x0481, font: 1, style: FontStyle.BlackBorder) { Y = 5 });
            Add(_percentLabel = new Label(string.Empty, true, 0x0481, font: 1, style: FontStyle.BlackBorder) { Y = 32 });
            Add(_statusLabel = new Label(string.Empty, true, 0x0035, font: 1, style: FontStyle.BlackBorder) { Y = 54 });

            Add(_optionsButton = new NiceButton(Width - 64, 4, 58, 16, ButtonAction.Activate, "Options", unicode: false, font: 1)
            {
                ButtonParameter = 1,
                IsSelectable = false
            });
            _optionsButton.SetTooltip("Boss bar style, width, position, and unpin");
            CustomGumpThemeManager.StyleButton(_optionsButton);

            GameActions.RequestMobileStatus(serial);
            UpdateLabels();
        }

        public override void Update()
        {
            if (!World.InGame || World.Player == null || ProfileManager.CurrentProfile == null || !(World.Get(LocalSerial) is Mobile mobile) || mobile.IsDestroyed || mobile.IsDead)
            {
                Dispose();
                return;
            }

            base.Update();
            UpdateLabels();
        }

        internal void ApplyTheme()
        {
            if (CustomGumpThemeManager.Current == CustomGumpTheme.Minimal
                || CustomGumpThemeManager.Current == CustomGumpTheme.TazUO)
            {
                _optionsButton.ArtStyle = false;
                _optionsButton.AlwaysShowBackground = false;
                _optionsButton.DisplayBorder = false;
                _optionsButton.TextLabel.Hue = 0x0481;
            }
            else
                CustomGumpThemeManager.StyleButton(_optionsButton);
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (!(World.Get(LocalSerial) is Mobile mobile))
                return;

            bool ornate = UseArtStyle;
            if (ornate && Width < MinimumOrnateWidth)
            {
                Width = MinimumOrnateWidth;
                _targetArea.Width = Width;
                _optionsButton.X = Width - 64;
            }
            Height = ornate ? OrnateHeightPixels : CleanHeightPixels;
            _targetArea.Height = Height;
            _nameLabel.Y = ornate ? 27 : 5;
            _percentLabel.Y = ornate ? 48 : 32;
            _statusLabel.Y = ornate ? 91 : 54;
            _optionsButton.Y = ornate ? 90 : 4;

            string name = mobile.Name ?? string.Empty;
            int nameLimit = ornate ? 20 : Math.Max(12, (Width - 120) / 8);
            if (name.Length > nameLimit)
                name = name.Substring(0, nameLimit - 1) + "…";

            if (_nameLabel.Text != name)
                _nameLabel.Text = name;
            _nameLabel.X = (Width - _nameLabel.Width) / 2;

            string percent = mobile.HitsMax > 0
                ? $"{Math.Max(0, Math.Min(100, mobile.Hits * 100 / mobile.HitsMax))}%"
                : "?";
            if (_percentLabel.Text != percent)
                _percentLabel.Text = percent;
            _percentLabel.X = Width - _percentLabel.Width - (ornate ? 72 : 17);

            string status = mobile.IsPoisoned ? "Poisoned" : string.Empty;
            if (mobile.IsParalyzed)
                status += (status.Length > 0 ? "  |  " : string.Empty) + "Paralyzed";
            if (mobile.IsYellowHits)
                status += (status.Length > 0 ? "  |  " : string.Empty) + "Yellow hits";
            if (_statusLabel.Text != status)
                _statusLabel.Text = status;
            _statusLabel.X = (Width - _statusLabel.Width) / 2;
        }

        private void SelectBoss()
        {
            if (!(World.Get(LocalSerial) is Mobile mobile) || mobile.IsDestroyed)
                return;

            if (TargetManager.IsTargeting)
                TargetManager.Target(LocalSerial);
            else
            {
                TargetManager.SelectedTarget = LocalSerial;
                TargetManager.LastTargetInfo.SetEntity(LocalSerial);
            }

            Mouse.CancelDoubleClick = true;
            Mouse.LastLeftButtonClickTime = 0;
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID != 1)
                return;

            var menu = new ContextMenuControl();
            menu.Add("Clean style", () =>
            {
                ProfileManager.CurrentProfile.BossHealthBarOrnate = false;
                UpdateLabels();
            });
            menu.Add("Ornate style", () =>
            {
                ProfileManager.CurrentProfile.BossHealthBarOrnate = true;
                ResizeBossBar(Width);
            });
            menu.Add("Narrower", () => ResizeBossBar(Width - 50));
            menu.Add("Wider", () => ResizeBossBar(Width + 50));
            menu.Add("Center at top", () =>
            {
                CenterAtTop();
                ProfileManager.CurrentProfile.BossHealthBarPosition = Location;
            });
            menu.Add("Unpin", () =>
            {
                DismissedBosses.Add(LocalSerial);
                Dispose();
            });
            menu.Show();
        }

        private void ResizeBossBar(int width)
        {
            int minimumWidth = UseArtStyle ? MinimumOrnateWidth : MinimumWidth;
            int newWidth = Math.Max(minimumWidth, Math.Min(MaximumWidth, width));
            X -= (newWidth - Width) / 2;
            Width = newWidth;
            _targetArea.Width = newWidth;
            _optionsButton.X = newWidth - 64;
            ProfileManager.CurrentProfile.BossHealthBarWidth = newWidth;
            ProfileManager.CurrentProfile.BossHealthBarPosition = Location;
            UpdateLabels();
        }

        private void CenterAtTop()
        {
            WorldViewportGump viewport = UIManager.GetGump<WorldViewportGump>();
            X = viewport != null ? viewport.X + (viewport.Width - Width) / 2 : (Client.Game.Window.ClientBounds.Width - Width) / 2;
            Y = viewport != null ? viewport.Y + 12 : 18;
        }

        protected override void OnMove(int x, int y)
        {
            base.OnMove(x, y);
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.BossHealthBarPosition = Location;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed || !IsVisible || ProfileManager.CurrentProfile == null)
                return false;

            bool ornate = UseArtStyle;
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            if (ornate)
            {
                Texture2D ornateFrame = GetOrnateFrame();
                if (ornateFrame != null)
                {
                    DrawOrnateFrame(batcher, ornateFrame, x, y,
                        ShaderHueTranslator.GetHueVector(FrameHue, false, 1f));

                    if (World.Get(LocalSerial) is Mobile boss && boss.HitsMax > 0)
                    {
                        int fillWidth = Math.Max(0, Math.Min(Width - 140,
                            (int)((long)(Width - 140) * boss.Hits / boss.HitsMax)));
                        if (fillWidth > 0)
                        {
                            batcher.Draw(SolidColorTextureCache.GetTexture(new Color(117, 13, 23)),
                                new Rectangle(x + 70, y + 52, fillWidth, 10), hue);
                            batcher.Draw(SolidColorTextureCache.GetTexture(new Color(215, 62, 57)),
                                new Rectangle(x + 70, y + 52, fillWidth, 2), hue);
                            batcher.Draw(SolidColorTextureCache.GetTexture(new Color(174, 29, 35)),
                                new Rectangle(x + 70, y + 54, fillWidth, 5), hue);
                            batcher.Draw(SolidColorTextureCache.GetTexture(new Color(69, 7, 18)),
                                new Rectangle(x + 70, y + 60, fillWidth, 2), hue);
                        }
                    }

                    return base.Draw(batcher, x, y);
                }
            }

            Texture2D frame = SolidColorTextureCache.GetTexture(new Color(93, 98, 107));
            Texture2D panel = SolidColorTextureCache.GetTexture(new Color(12, 12, 18, 225));
            Texture2D track = SolidColorTextureCache.GetTexture(new Color(45, 10, 15));
            Texture2D fill = SolidColorTextureCache.GetTexture(new Color(190, 31, 38));

            batcher.Draw(frame, new Rectangle(x, y, Width, Height), hue);
            batcher.Draw(panel, new Rectangle(x + 2, y + 2, Width - 4, Height - 4), hue);
            batcher.Draw(track, new Rectangle(x + 14, y + 31, Width - 28, 21), hue);

            if (World.Get(LocalSerial) is Mobile mobile && mobile.HitsMax > 0)
            {
                int fillWidth = Math.Max(0, Math.Min(Width - 32, (int)((long)(Width - 32) * mobile.Hits / mobile.HitsMax)));
                if (fillWidth > 0)
                    batcher.Draw(fill, new Rectangle(x + 16, y + 33, fillWidth, 17), hue);
            }

            return base.Draw(batcher, x, y);
        }

        private static Texture2D GetOrnateFrame()
        {
            if (_ornateFrame == null)
            {
                using (Stream stream = typeof(BossHealthBarGump).Assembly.GetManifestResourceStream(
                    "ClassicUO.Resources.BossHealthBar.ornate-frame.png"))
                    _ornateFrame = stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
            }

            return _ornateFrame;
        }

        private void DrawOrnateFrame(UltimaBatcher2D batcher, Texture2D frame, int x, int y, Vector3 hue)
        {
            const int sourceY = 100;
            const int sourceHeight = 460;
            int railWidth = (Width - 2 * OrnateCapWidth - OrnateCrestWidth) / 2;
            int rightRailWidth = Width - 2 * OrnateCapWidth - OrnateCrestWidth - railWidth;

            batcher.Draw(frame, new Rectangle(x, y, OrnateCapWidth, 90),
                new Rectangle(0, sourceY, 300, sourceHeight), hue);
            batcher.Draw(frame, new Rectangle(x + OrnateCapWidth, y, railWidth, 90),
                new Rectangle(300, sourceY, 300, sourceHeight), hue);
            batcher.Draw(frame, new Rectangle(x + OrnateCapWidth + railWidth, y, OrnateCrestWidth, 90),
                new Rectangle(600, sourceY, 972, sourceHeight),
                CustomGumpThemeManager.Current == CustomGumpTheme.UOAlive2
                    ? ShaderHueTranslator.GetHueVector(0x048D, false, 1f) : hue);
            batcher.Draw(frame, new Rectangle(x + OrnateCapWidth + railWidth + OrnateCrestWidth, y, rightRailWidth, 90),
                new Rectangle(1572, sourceY, 300, sourceHeight), hue);
            batcher.Draw(frame, new Rectangle(x + Width - OrnateCapWidth, y, OrnateCapWidth, 90),
                new Rectangle(1872, sourceY, 300, sourceHeight), hue);
        }
    }
}
