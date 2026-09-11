using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class VersionHistory : NineSliceGump
    {
        private static readonly string[] updateTexts =
        {
            "/c[white][0.21]/cd\n" +
            """
            - Replaced the original TazUO history with MW Edition release notes
            - Added a centered TazUO MW Edition GitHub link to the version-history gump
            - Updated the version-history title and version formatting
            """ +
            "\n",
            "/c[white][0.2]/cd\n" +
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
            "/c[white][0.1]/cd\n" +
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

        public VersionHistory() : base(0, 0, 400, 500, ModernUIConstants.ModernUIPanel, ModernUIConstants.ModernUIPanel_BoderSize, true, 200, 200)
        {
            CanCloseWithRightClick = true;
            CanMove = true;

            Build();

            CenterXInViewPort();
            CenterYInViewPort();
        }

        private void Build()
        {
            Clear();

            Positioner pos = new(13, 13);

            Add(pos.Position(TextBox.GetOne(Language.Instance.TazuoVersionHistory, TrueTypeLoader.EMBEDDED_FONT, 22, Color.White, TextBox.RTLOptions.DefaultCentered(Width))));

            Add(pos.Position(TextBox.GetOne(Language.Instance.CurrentVersion + CUOEnviroment.Version.ToString(2), TrueTypeLoader.EMBEDDED_FONT, 20, Color.Orange, TextBox.RTLOptions.DefaultCentered(Width))));

            _scrollArea = new ScrollArea(0, 0, Width - 26, Height - (pos.LastY + pos.LastHeight) - 32, true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            _vBoxContainer = new VBoxContainer(_scrollArea.Width - _scrollArea.ScrollBarWidth());
            _scrollArea.Add(_vBoxContainer);

            foreach (string s in updateTexts)
            {
                _vBoxContainer.Add(TextBox.GetOne(s, TrueTypeLoader.EMBEDDED_FONT, 15, Color.Orange, TextBox.RTLOptions.Default(_scrollArea.Width - _scrollArea.ScrollBarWidth())));
            }

            Add(pos.Position(_scrollArea));

            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUOWiki, "https://github.com/PlayTazUO/TazUO/wiki", Color.Orange, 15), 25, Height - 20));

            HttpClickableLink githubLink = new("TazUO MW Edition Github", "https://github.com/mike-walker-uo/tazUO-MW-Edition", Color.Orange, 15);
            Add(pos.PositionExact(githubLink, (Width - githubLink.Width) / 2, Height - 20));

            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUODiscord, "https://discord.gg/QvqzkB95G4", Color.Orange, 15), Width - 110, Height - 20));
        }

        protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
            Build();
        }
    }
}
