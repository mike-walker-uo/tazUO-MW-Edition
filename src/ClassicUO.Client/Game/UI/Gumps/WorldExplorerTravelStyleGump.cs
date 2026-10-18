using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldExplorerTravelStyleGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;

        internal WorldExplorerTravelStyleGump() : base(0, 0)
        {
            Width = 800; Height = 470; WantUpdateSize = false;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true; AcceptMouseInput = true;
            Build(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        private void Build()
        {
            Clear();
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.98f));
            Add(new Label("WORLD EXPLORER  •  TRAVEL STYLE", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 20, Y = 14 });
            Add(new Label("Choose the destination popup artwork. Saved for this character.", true,
                CustomGumpThemeManager.TextHue, 760, font: 1) { X = 20, Y = 38 });
            for (int i = 0; i < WorldExplorerTravelCard.StyleNames.Length; i++)
            {
                int x = 20 + i % 2 * 394, y = 64 + i / 2 * 186;
                bool selected = i == WorldExplorerTravelCard.StyleIndex(_profile.WorldExplorerTravelStyle);
                Add(new Label(WorldExplorerTravelCard.StyleNames[i], true, CustomGumpThemeManager.TextHue, 366, font: 1) { X = x, Y = y });
                Add(new WorldExplorerTravelCard((byte)i, "Yew Bank", "Recall", 366) { X = x, Y = y + 26 });
                var use = new NiceButton(x + 256, y + 154, 110, 28, ButtonAction.Activate,
                    selected ? "Selected" : "Use") { IsSelectable = false, ButtonParameter = i + 1 };
                CustomGumpThemeManager.StyleDataButton(use);
                Add(use);
            }
            Add(new Label("Appears for 3 seconds after a travel request. Right-click / Esc closes it.", true,
                CustomGumpThemeManager.DimHue, 760, font: 1) { X = 20, Y = 444 });
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID < 1 || buttonID > WorldExplorerTravelCard.StyleNames.Length) return;
            _profile.WorldExplorerTravelStyle = (byte)(buttonID - 1);
            _profile.Save(ProfileManager.ProfilePath, false);
            Build();
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            base.Update();
        }
    }
}
