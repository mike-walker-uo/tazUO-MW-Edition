// TazUO addition: compact feature launcher attached to the player's paperdoll.

using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PaperDollFeatureToolsGump : Gump
    {
        private const int WIDTH = 142;
        private readonly Gump _owner;
        private long _nextRefresh;
        private bool _lastCollapsed;
        private int _lastAlertCount = -1;
        private bool _lastReady;

        internal PaperDollFeatureToolsGump(Gump owner) : base(0, 0)
        {
            _owner = owner;
            CanMove = false;
            CanCloseWithRightClick = false;
            CanBeLocked = false;
            AcceptMouseInput = true;
            Build();
            UpdatePosition();
        }

        public override bool ShouldBeSaved => false;

        public override void Update()
        {
            if (_owner == null || _owner.IsDisposed || World.Player == null)
            {
                Dispose();
                return;
            }

            IsVisible = _owner.IsVisible
                && !(_owner is PaperDollGump paperdoll && paperdoll.IsMinimized);

            if (!IsVisible)
                return;

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 1000;
                int alerts = AlertCenterManager.History.Count(entry => entry.Active);
                bool ready = ReadinessCheckManager.Evaluate().IsReady;
                bool collapsed = ProfileManager.CurrentProfile?.PaperdollToolsCollapsed ?? false;

                if (alerts != _lastAlertCount || ready != _lastReady || collapsed != _lastCollapsed)
                    Build();
            }

            UpdatePosition();
            base.Update();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1: Open<WorldExplorerGump>(() => new WorldExplorerGump()); break;
                case 2: Open<ItemFinderGump>(() => new ItemFinderGump()); break;
                case 3: Open<AlertCenterGump>(() => new AlertCenterGump()); break;
                case 4: Open<RestockAgentGump>(() => new RestockAgentGump()); break;
                case 5: Open<EquipmentGuruGump>(() => new EquipmentGuruGump()); break;
                case 9:
                    if (ProfileManager.CurrentProfile != null)
                    {
                        ProfileManager.CurrentProfile.PaperdollToolsCollapsed =
                            !ProfileManager.CurrentProfile.PaperdollToolsCollapsed;
                        Build();
                    }
                    break;
            }
        }

        private void Build()
        {
            Clear();
            bool collapsed = ProfileManager.CurrentProfile?.PaperdollToolsCollapsed ?? false;
            int alerts = AlertCenterManager.History.Count(entry => entry.Active);
            bool ready = ReadinessCheckManager.Evaluate().IsReady;
            _lastCollapsed = collapsed;
            _lastAlertCount = alerts;
            _lastReady = ready;
            Width = collapsed ? 30 : WIDTH;
            Height = collapsed ? 28 : 164;

            Add(FeatureGumpArtwork.CreateSurface(0, 0, Width, Height,
                FeatureGumpArtworkKind.RestockAgent, 0.94f));
            Add(Button(4, 3, collapsed ? 22 : 24, 22, collapsed ? "+" : "−", 9,
                collapsed ? "Show feature tools" : "Hide feature tools"));

            if (collapsed)
                return;

            Add(new Label("TOOLS", true, 0x0481, 104, font: 1) { X = 34, Y = 7 });
            Add(Button(5, 30, 132, 24, "World Explorer", 1));
            Add(Button(5, 56, 132, 24, "Item Finder", 2));
            Add(Button(5, 82, 132, 24, alerts == 0 ? "Alerts" : $"Alerts  ({alerts})", 3));
            Add(Button(5, 108, 132, 24, ready ? "Restock  • READY" : "Restock  • CHECK", 4));
            Add(Button(5, 134, 132, 24, "Equipment Guru", 5));
        }

        private NiceButton Button(int x, int y, int width, int height, string text, int id,
            string tooltip = null)
        {
            NiceButton button = FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.RestockAgent);
            if (!string.IsNullOrEmpty(tooltip)) button.SetTooltip(tooltip);
            return button;
        }

        private void UpdatePosition()
        {
            int right = _owner.X + _owner.Width + 3;
            X = System.Math.Max(0, right + Width <= Client.Game.Window.ClientBounds.Width
                ? right
                : _owner.X - Width - 3);
            Y = System.Math.Max(0, System.Math.Min(
                _owner.Y + 24,
                Client.Game.Window.ClientBounds.Height - Height));
        }

        private static void Open<T>(System.Func<T> factory) where T : Gump
        {
            T existing = UIManager.GetGump<T>();
            if (existing != null && !existing.IsDisposed)
            {
                existing.BringOnTop();
                return;
            }
            UIManager.Add(factory());
        }
    }
}
