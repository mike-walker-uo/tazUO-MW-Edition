using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class VersionHistory : NineSliceGump
    {
        // Add the new release notes here whenever CUOEnviroment.Version changes.
        private static readonly string[] updateTexts =
        {
            "[0.5]\n" +
            """
            - Added custom gump themes and improved decorative-border spacing and macro-button readability
            - Added World Explorer with rune and rune-book scanning, pinned destinations, and travel options
            - Added per-area gump opacity commands and controls
            - Improved Razor Enhanced hotkey labels on macro buttons
            - Added a Windows system-DPI default with an optional native-DPI mode
            - Moved themed toast alerts to the top center with a movable, resizable anchor
            - Added low-tithing toast alerts for Chivalry users
            """ +
            "\n",
            "[0.4]\n" +
            """
            - Updated the graphics and input stack from SDL2 to pinned SDL3/FNA while retaining .NET Framework 4.7.2 and Razor Enhanced compatibility
            - Added native graphics runtime details to crash diagnostics
            - Restored AltGr as Ctrl+Alt for Razor Enhanced hotkeys
            - Preserved Ctrl-click pinned backpack and grid-container items in their selected cells across restarts
            - Made corpse-container and Grid Loot positions persist reliably across restarts
            - Fixed a logout crash while grid-container layouts were being saved
            - Bundled the official TazUO fonts and restored Chakra Petch
            """ +
            "\n",
            "[0.3]\n" +
            """
            - Added nearby player-speech history with session clearing
            - Added nearby-speech filters for the player and owned pets
            - Removed the speech-composer field from nearby speech history
            - Added adjustable utility and chat gump background opacity while preserving container opacity
            - Added separate corpse-container and buff/debuff-bar opacity controls
            - Added durability-gump opacity and an absolute 0-100% hover-opacity target
            - Consolidated individual gump-opacity controls on a dedicated options page
            - Showed a gump's real opacity while Alt is held for opacity adjustment
            - Excluded classic and modern paperdolls from hover-opacity changes
            - Added slayer/equipment-bar opacity and collapse controls
            - Extended Alt+mouse-wheel opacity adjustment through modern gump child controls
            - Preserved Alt-scroll opacity when script gumps rebuild their controls
            - Fixed zero-opacity corpse grid slots and buff-bar hover flickering
            - Applied buff-bar hover opacity consistently across all rows without fading icons or text
            - Saved the last corpse-container position across client restarts
            - Matched the durability-gump background to the selected custom theme
            - Added freshness-aware tithing display and low-tithing warnings for Chivalry users
            - Added value-based performance HUD colors
            - Kept Perf HUD ping values green through 150 ms
            - Wrapped long Perf HUD stall diagnostics within the panel
            - Improved login-screen alignment for the MW Edition logo and version information
            - Made the base -speechhistory command directly pinnable in the command palette
            - Added an expanded performance HUD with frame time, 1% low FPS, ping/jitter history, network queue, GC, and stall diagnostics
            - Added profile and gump-layout recovery snapshots with an in-game recovery gump
            - Added safe graphics startup mode with the -safegraphics argument
            - Added startup checks for missing or incomplete Ultima Online data
            - Added visible surface-aware footstep effects for water, snow, grass, sand, mud, dirt, wood, stone, mines, and dungeons
            - Added mount-aware boot, hoof, claw, and large-creature tracks with surface particles
            - Added selectable blood, shadow, arcane, fire, frost, poison, holy, necromantic, lightning, petal, leaf, rune, stardust, ethereal, rainbow, and lava movement trails
            - Replaced generated fantasy-trail symbols with varied native UO artwork
            - Reworked inconsistent fantasy trails with larger coherent native-UO effect frames and removed tree artwork from Falling Leaves
            - Increased Fire, Poison, and Lava Cracks trails to normal effect size
            - Added the -trailfx gump for trail style, intensity, lifetime, surface-track, and particle controls
            - Preserved the trail-effects gump position when changing its options
            - Disabled blood splatter, blood trail, and blood ground decals by default
            - Reduced unnecessary feature-settings disk writes
            - Added event-handler fault isolation so one optional feature cannot interrupt other handlers
            - Added incoming network queue limits to prevent runaway memory use during stalls

            Bug fixes
            - Fixed startup recovery when global settings cannot be loaded
            - Fixed disabled movement trails continuing to collect positions
            - Fixed legacy blood-trail settings remaining enabled after an upgrade
            - Fixed footstep decals disappearing when the full ambience overlay is disabled
            - Fixed Alt+mouse-wheel and hover opacity changes fading gump text
            - Fixed zero-opacity gumps retaining a forced 10% background
            - Fixed corpse grid slots ignoring corpse-container opacity
            - Fixed buff-bar hover opacity flickering
            """ +
            "\n",
            "[0.21]\n" +
            """
            - Replaced the original TazUO history with MW Edition release notes
            - Added a centered TazUO MW Edition GitHub link to the version-history gump
            - Updated the version-history title and version formatting
            """ +
            "\n",
            "[0.2]\n" +
            """
            - Improved client performance and reduced render-loop allocations
            - Optimized network processing and improved reconnect stability
            - Added a session-log option; logging is disabled by default
            - Added main-thread hang diagnostics and safer log handling
            - Improved malformed cliloc and PNG artwork error handling
            - Improved external artwork loading and caching
            - Added low-durability equipment highlighting
            - Improved skill-gain diagnostics
            - Disabled Legion/Python scripting while retaining its implementation
            - Removed built-in Discord, anonymous metrics, update notifier, and UOAssist compatibility
            - Added automated Windows tests, publishing, and release artifacts

            Bug fixes
            - Fixed quoted text parsing
            - Fixed settings initialization when the executable path is unavailable
            - Added packet, configuration, durability, and hang-diagnostic regression tests
            """ +
            "\n",
            "[0.1]\n" +
            """
            - Initial TazUO MW Edition release based on TazUO 4.5.22.0
            - Added TazUO MW Edition branding and version information
            - Added the MW Edition logo to the login screen
            - Added the TazUO MW Edition GitHub link to the login screen
            """ +
            "\n"
        };

        private ScrollArea _scrollArea;
        private VBoxContainer _vBoxContainer;
        private CustomGumpTheme _theme;

        public VersionHistory() : base(0, 0, 400, 500, ModernUIConstants.ModernUIPanel, ModernUIConstants.ModernUIPanel_BoderSize, true, 200, 200)
        {
            CanCloseWithRightClick = true;
            CanMove = true;
            _theme = CustomGumpThemeManager.Current;

            Build();

            CenterXInViewPort();
            CenterYInViewPort();
        }

        private void Build()
        {
            Clear();
            DrawNineSliceBackground = _theme == CustomGumpTheme.TazUO;
            if (!DrawNineSliceBackground)
            {
                AlphaBlendControl background = new(0.95f)
                {
                    Width = Width,
                    Height = Height,
                    ArtPanel = true
                };
                CustomGumpThemeManager.ApplyDataSurface(background, 0.95f);
                Add(background);
            }

            int inset = CustomThemeArt.ContentInset;
            int contentWidth = Width - 26 - inset * 2;
            Color textColor = _theme == CustomGumpTheme.BritannianChronicle ? Color.Black : Color.Orange;
            Color titleColor = _theme == CustomGumpTheme.BritannianChronicle || _theme == CustomGumpTheme.Classic
                ? Color.Black : Color.White;
            Positioner pos = new(13 + inset, 13);
            pos.Y += inset;

            Add(pos.Position(TextBox.GetOne(Language.Instance.TazuoVersionHistory, TrueTypeLoader.EMBEDDED_FONT, 22, titleColor, TextBox.RTLOptions.DefaultCentered(contentWidth))));

            Add(pos.Position(TextBox.GetOne(Language.Instance.CurrentVersion + CUOEnviroment.Version.ToString(2), TrueTypeLoader.EMBEDDED_FONT, 20, textColor, TextBox.RTLOptions.DefaultCentered(contentWidth))));

            int footerTop = Height - 43 - inset;
            _scrollArea = new ScrollArea(0, 0, contentWidth, System.Math.Max(1, footerTop - pos.Y - 6), true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            _vBoxContainer = new VBoxContainer(_scrollArea.Width - _scrollArea.ScrollBarWidth());
            _scrollArea.Add(_vBoxContainer);

            foreach (string s in updateTexts)
            {
                _vBoxContainer.Add(TextBox.GetOne(s, TrueTypeLoader.EMBEDDED_FONT, 15, textColor, TextBox.RTLOptions.Default(_scrollArea.Width - _scrollArea.ScrollBarWidth())));
            }

            Add(pos.Position(_scrollArea));

            Color linkColor = _theme == CustomGumpTheme.BritannianChronicle ? new Color(95, 50, 20) : Color.Orange;
            int linkY = Height - 22 - inset;
            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUOWiki, "https://github.com/PlayTazUO/TazUO/wiki", linkColor, 15), 13 + inset, footerTop));

            HttpClickableLink githubLink = new("TazUO MW Edition Github", "https://github.com/mike-walker-uo/tazUO-MW-Edition", linkColor, 15);
            Add(pos.PositionExact(githubLink, (Width - githubLink.Width) / 2, linkY));

            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUODiscord, "https://discord.gg/QvqzkB95G4", linkColor, 15), Width - 110 - inset, footerTop));
        }

        public override void Update()
        {
            base.Update();
            if (_theme != CustomGumpThemeManager.Current)
            {
                _theme = CustomGumpThemeManager.Current;
                Build();
            }
        }

        protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
            Build();
        }
    }
}
