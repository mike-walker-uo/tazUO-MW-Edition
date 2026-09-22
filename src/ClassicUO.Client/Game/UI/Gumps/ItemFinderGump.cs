// TazUO addition: universal item finder UI.

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using SDL3;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ItemFinderGump : Gump
    {
        private const int WIDTH = 780;
        private const int HEIGHT = 590;
        private const int GUIDE_X = 24;
        private const int GUIDE_WIDTH = 104;
        private const int RESULTS_X = 150;
        private const int RESULTS_WIDTH = 606;
        private const int RESULTS_PER_PAGE = 75;
        private const int CONTAINER_OPEN_INTERVAL_MS = 500;
        private const int CONTAINER_OPEN_TIMEOUT_MS = 5000;
        private const int SCAN_RESPONSE_GRACE_MS = 2500;

        private readonly FinderInput _input;
        private readonly Label _logic;
        private readonly Label _matchTitle;
        private readonly Label _pageLabel;
        private readonly Label _status;
        private readonly VBoxContainer _resultsBox;
        private readonly List<ItemFinderManager.SearchResult> _results =
            new List<ItemFinderManager.SearchResult>();
        private readonly HashSet<uint> _requestedProperties = new HashSet<uint>();
        private readonly HashSet<uint> _containersToClose = new HashSet<uint>();
        private readonly Queue<uint> _containersToScan = new Queue<uint>();

        private int _pendingProperties;
        private int _resultPage;
        private int _scanContainerCount;
        private int _scanContainersOpened;
        private long _refreshAt;
        private long _nextContainerOpenAt;
        private long _currentContainerDeadline;
        private long _finishScanAt;
        private uint _currentContainerSerial;
        private bool _areaScanActive;

        internal ItemFinderGump(string query = null) : base(0, 0)
        {
            X = ProfileManager.CurrentProfile?.ItemFinderPosition.X ?? 220;
            Y = ProfileManager.CurrentProfile?.ItemFinderPosition.Y ?? 130;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.ItemFinder, 0.96f));
            AddSurface(20, 10, WIDTH - 40, 50, 0.54f);
            Add(new Label("UNIVERSAL ITEM FINDER", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder),
                WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Scan reachable containers, then search the accumulated item catalog.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 38
            });
            NiceButton close = CreateButton(728, 16, 28, 26, "X", 99);
            close.SetTooltip("Close Item Finder");
            Add(close);
            NiceButton help = CreateButton(552, 16, 64, 26, "Help", 5);
            help.SetTooltip("Open Item Finder syntax and searchable property help.");
            Add(help);
            NiceButton catalog = CreateButton(466, 16, 80, 26, "Catalog", 6);
            catalog.SetTooltip("Review stored container locations, age and catalog contents.");
            Add(catalog);
            NiceButton savedSearches = CreateButton(622, 16, 100, 26, "Saved searches", 4);
            savedSearches.SetTooltip("Save, edit and run reusable Item Finder queries.");
            Add(savedSearches);
            AddAccent(24, 61, WIDTH - 48);

            AddSurface(24, 72, 492, 32, 0.68f, true);
            Add(_input = new FinderInput(this)
            {
                X = 31,
                Y = 78,
                Width = 478,
                Height = 22
            });
            Add(CreateButton(522, 72, 78, 32, "Search", 1));
            NiceButton scanArea = CreateButton(606, 72, 84, 32, "Scan area", 3);
            scanArea.SetTooltip("Scan loaded and reachable containers. Click again to cancel and close scan-opened containers.");
            Add(scanArea);
            Add(CreateButton(696, 72, 60, 32, "Clear", 2));

            Add(new Label("QUERY GUIDE", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH, font: 1)
            {
                X = GUIDE_X,
                Y = 125
            });
            Add(new Label("Examples", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH, font: 1)
            { X = GUIDE_X, Y = 151 });
            Add(CreateButton(GUIDE_X, 176, GUIDE_WIDTH, 32, "ALL", 10));
            Add(CreateButton(GUIDE_X, 214, GUIDE_WIDTH, 32, "NOT", 11));
            Add(CreateButton(GUIDE_X, 252, GUIDE_WIDTH, 32, "COUNT", 12));
            Add(new Label("Full syntax and\nproperties are in\nthe Help page.", true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH, font: 1)
            { X = GUIDE_X, Y = 302 });

            Add(new Label("Operators", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH, font: 1)
            {
                X = GUIDE_X,
                Y = 394
            });
            Add(new Label(">=   >   =   <   <=", true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder),
                GUIDE_WIDTH, font: 1)
            {
                X = GUIDE_X,
                Y = 419
            });
            Add(new Label("HCI DCI DI SSI\nLMC LRC FC FCR\nSDI MR", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH, font: 1)
            {
                X = GUIDE_X,
                Y = 444
            });

            Add(new AlphaBlendControl(0.55f)
            {
                X = 138,
                Y = 124,
                Width = 1,
                Height = 430,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
            });

            Add(_matchTitle = new Label("MATCHES", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder),
                RESULTS_WIDTH, font: 1)
            {
                X = RESULTS_X,
                Y = 125
            });
            Add(_logic = new Label("No active query", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder),
                RESULTS_WIDTH, font: 1)
            {
                X = RESULTS_X,
                Y = 146
            });

            AddSurface(RESULTS_X, 171, RESULTS_WIDTH, 369, 0.36f);
            var scroll = new ScrollArea(RESULTS_X + 4, 175, RESULTS_WIDTH - 8, 329, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            scroll.Add(_resultsBox = new VBoxContainer(scroll.Width - scroll.ScrollBarWidth() - 3, 0, 0));
            Add(scroll);
            Add(CreateButton(576, 508, 48, 28, "<", 20));
            Add(_pageLabel = new Label("1 / 1", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 84, font: 1,
                align: TEXT_ALIGN_TYPE.TS_CENTER)
            {
                X = 624,
                Y = 514
            });
            Add(CreateButton(708, 508, 48, 28, ">", 21));

            AddAccent(24, 564, WIDTH - 48);
            Add(_status = new Label("Scan this area to add reachable containers to the catalog.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 570
            });

            EventSink.OPLOnReceive += OnPropertiesReceived;
            ItemFinderManager.ScanReachable(_requestedProperties);
            SetQuery(query, !string.IsNullOrWhiteSpace(query));
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;
        internal string CurrentQuery => _input.Text;

        internal void SetQuery(string query, bool search = true)
        {
            _input.SetText(query ?? string.Empty);
            _input.CaretIndex = _input.Text.Length;
            FocusSearch();

            if (search && !string.IsNullOrWhiteSpace(query))
            {
                RunSearch(true);
            }
        }

        internal void FocusSearch()
        {
            UIManager.KeyboardFocusControl = _input;
            _input.SetKeyboardFocus();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1:
                    RunSearch(true);
                    break;
                case 2:
                    ClearSearch();
                    break;
                case 3:
                    if (_areaScanActive)
                    {
                        CancelAreaScan();
                        _status.Text = "Area scan cancelled; containers opened by the scan were closed.";
                    }
                    else ScanArea();
                    break;
                case 4:
                    OpenSavedSearches();
                    break;
                case 5:
                    OpenHelp();
                    break;
                case 6:
                    OpenCatalog();
                    break;
                case 10:
                    SetQuery("ring hci>=10 di>=20 ssi>=10");
                    break;
                case 11:
                    SetQuery("ring not:cursed");
                    break;
                case 12:
                    SetQuery("ring 2of(hci>=10,di>=20,ssi>=10)");
                    break;
                case 20:
                    if (_resultPage > 0)
                    {
                        _resultPage--;
                        RenderResults();
                    }
                    break;
                case 21:
                    if ((_resultPage + 1) * RESULTS_PER_PAGE < _results.Count)
                    {
                        _resultPage++;
                        RenderResults();
                    }
                    break;
                case 99:
                    Dispose();
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.ItemFinderPosition = Location;
            ItemFinderManager.UpdateHighlights();

            if (_refreshAt != 0 && Time.Ticks >= _refreshAt)
            {
                _refreshAt = 0;
                RunSearch();
            }

            if (_areaScanActive)
            {
                UpdateAreaScan();
            }
        }

        public override void Dispose()
        {
            EventSink.OPLOnReceive -= OnPropertiesReceived;
            ItemFinderManager.ClearHighlights();
            CancelAreaScan();

            if (UIManager.KeyboardFocusControl == _input)
            {
                UIManager.KeyboardFocusControl = null;
            }

            base.Dispose();
        }

        private void ClearSearch()
        {
            _input.SetText(string.Empty);
            _results.Clear();
            _resultsBox.Clear();
            _resultPage = 0;
            _pageLabel.Text = "1 / 1";
            _pendingProperties = 0;
            _requestedProperties.Clear();
            _logic.Text = "No active query";
            _matchTitle.Text = "MATCHES";
            _status.Text = $"Catalog contains {ItemFinderManager.CatalogCount} item(s). Scan again after moving.";
            FocusSearch();
        }

        private void OpenSavedSearches()
        {
            ItemFinderSavedSearchGump saved = UIManager.GetGump<ItemFinderSavedSearchGump>();

            if (saved == null)
            {
                UIManager.Add(new ItemFinderSavedSearchGump(this, _input.Text));
            }
            else
            {
                saved.Attach(this, _input.Text);
                saved.BringOnTop();
            }
        }

        private void OpenHelp()
        {
            ItemFinderHelpGump help = UIManager.GetGump<ItemFinderHelpGump>();

            if (help == null)
            {
                UIManager.Add(new ItemFinderHelpGump(this));
            }
            else
            {
                help.BringOnTop();
            }
        }

        private void OpenCatalog()
        {
            ItemFinderCatalogGump catalog = UIManager.GetGump<ItemFinderCatalogGump>();
            if (catalog == null) UIManager.Add(new ItemFinderCatalogGump(this));
            else catalog.BringOnTop();
        }

        private void ScanArea()
        {
            CancelAreaScan();
            var previouslyOpen = new HashSet<uint>();

            foreach (Gump gump in UIManager.Gumps)
            {
                if (!gump.IsDisposed && (gump is ContainerGump || gump is GridContainer))
                {
                    previouslyOpen.Add(gump.LocalSerial);
                }
            }

            List<uint> containers = ItemFinderManager.GetReachableContainerSerials();

            foreach (uint serial in containers)
            {
                if (!previouslyOpen.Contains(serial))
                {
                    _containersToScan.Enqueue(serial);
                    _containersToClose.Add(serial);
                }
            }

            ItemFinderManager.ScanReachable(_requestedProperties);
            RunSearch();
            _scanContainerCount = _containersToScan.Count;
            _scanContainersOpened = 0;

            if (_scanContainerCount == 0)
            {
                FinishAreaScan();
                return;
            }

            _areaScanActive = true;
            _nextContainerOpenAt = (long)Time.Ticks;
            _status.Text = $"Scanning {_scanContainerCount} reachable container(s) one at a time…";
        }

        private void UpdateAreaScan()
        {
            long now = (long)Time.Ticks;

            if (_currentContainerSerial != 0)
            {
                if (!IsContainerOpen(_currentContainerSerial)
                    && now < _currentContainerDeadline)
                {
                    return;
                }

                _currentContainerSerial = 0;
                _currentContainerDeadline = 0;
                _scanContainersOpened++;
                _nextContainerOpenAt = now + Math.Max(
                    CONTAINER_OPEN_INTERVAL_MS, GlobalActionCooldown.CooldownDuration);

                if (_containersToScan.Count == 0)
                {
                    _finishScanAt = now + SCAN_RESPONSE_GRACE_MS;
                }

                return;
            }

            if (_containersToScan.Count > 0)
            {
                if (now < _nextContainerOpenAt || GlobalActionCooldown.IsOnCooldown)
                {
                    return;
                }

                uint serial = _containersToScan.Dequeue();

                if (IsContainerOpen(serial))
                {
                    _containersToClose.Remove(serial);
                }
                else
                {
                    GameActions.DoubleClick(serial);
                    GlobalActionCooldown.BeginCooldown();
                    _currentContainerSerial = serial;
                    _currentContainerDeadline = now + CONTAINER_OPEN_TIMEOUT_MS;
                }

                _status.Text = $"Scanning container {_scanContainersOpened + 1} of {_scanContainerCount}…";

                if (_currentContainerSerial == 0)
                {
                    _scanContainersOpened++;
                    _nextContainerOpenAt = now + Math.Max(
                        CONTAINER_OPEN_INTERVAL_MS, GlobalActionCooldown.CooldownDuration);

                    if (_containersToScan.Count == 0)
                    {
                        _finishScanAt = now + SCAN_RESPONSE_GRACE_MS;
                    }
                }

                return;
            }

            if (_finishScanAt != 0 && now >= _finishScanAt)
            {
                FinishAreaScan();
            }
        }

        private void FinishAreaScan()
        {
            _areaScanActive = false;
            _nextContainerOpenAt = 0;
            _currentContainerDeadline = 0;
            _currentContainerSerial = 0;
            _finishScanAt = 0;

            ItemFinderManager.ScanReport scan = ItemFinderManager.ScanReachable(
                _requestedProperties, false, true);
            RunSearch();
            int closed = CloseScannedContainers();
            _status.Text = $"Container scan complete: {scan.NewItems} new item(s). "
                + $"Removed {scan.RemovedItems} missing item(s). Closed {closed} container(s). "
                + $"Catalog: {scan.TotalItems}.";
        }

        private void CancelAreaScan()
        {
            _areaScanActive = false;
            _containersToScan.Clear();
            _nextContainerOpenAt = 0;
            _currentContainerDeadline = 0;
            _currentContainerSerial = 0;
            _finishScanAt = 0;
            _scanContainerCount = 0;
            _scanContainersOpened = 0;
            CloseScannedContainers();
        }

        private static bool IsContainerOpen(uint serial)
        {
            ContainerGump container = UIManager.GetGump<ContainerGump>(serial);

            if (container != null && !container.IsDisposed)
            {
                return true;
            }

            GridContainer grid = UIManager.GetGump<GridContainer>(serial);
            return grid != null && !grid.IsDisposed;
        }

        private int CloseScannedContainers()
        {
            int closed = 0;

            foreach (uint serial in _containersToClose)
            {
                bool found = false;
                ContainerGump container = UIManager.GetGump<ContainerGump>(serial);

                if (container != null && !container.IsDisposed)
                {
                    container.Dispose();
                    found = true;
                }

                GridContainer grid = UIManager.GetGump<GridContainer>(serial);

                if (grid != null && !grid.IsDisposed)
                {
                    grid.Dispose();
                    found = true;
                }

                if (found)
                {
                    closed++;
                }
            }

            _containersToClose.Clear();
            return closed;
        }

        private void RunSearch(bool resetRequests = false)
        {
            if (string.IsNullOrWhiteSpace(_input.Text))
            {
                _input.SetText("*");
                _input.CaretIndex = _input.Text.Length;
            }

            if (resetRequests)
            {
                _requestedProperties.Clear();
                _resultPage = 0;
            }

            if (!ItemFinderManager.TrySearch(
                _input.Text,
                _requestedProperties,
                out ItemFinderManager.SearchReport report,
                out string error))
            {
                _pendingProperties = 0;
                _logic.Text = "QUERY ERROR";
                _matchTitle.Text = "MATCHES";
                _status.Text = error;
                _results.Clear();
                _resultsBox.Clear();
                return;
            }

            _results.Clear();
            _results.AddRange(report.Results);
            RenderResults();

            _pendingProperties = report.MissingProperties;
            _logic.Text = report.LogicSummary;
            _matchTitle.Text = $"MATCHES  •  {report.Results.Count}";
            _status.Text = report.MissingProperties > 0
                ? $"Loading properties for {report.MissingProperties} candidate item(s). Results update automatically."
                : report.Results.Count == 0
                    ? "No loaded item satisfies the complete query."
                    : $"{ItemFinderManager.CatalogCount} cataloged item(s). LOCATE marks, opens or tracks its container.";
        }

        private void RenderResults()
        {
            _resultsBox.Clear();
            int pageCount = Math.Max(1, (_results.Count + RESULTS_PER_PAGE - 1) / RESULTS_PER_PAGE);
            _resultPage = Math.Max(0, Math.Min(_resultPage, pageCount - 1));
            _pageLabel.Text = $"{_resultPage + 1} / {pageCount}";

            int first = _resultPage * RESULTS_PER_PAGE;
            int last = Math.Min(_results.Count, first + RESULTS_PER_PAGE);

            for (int i = first; i < last; i++)
            {
                _resultsBox.Add(new FinderResultRow(this, _results[i], i, _resultsBox.Width));
            }

            if (_results.Count == 0)
            {
                _resultsBox.Add(new Label("No matching items yet.", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), _resultsBox.Width - 12, font: 1)
                {
                    X = 8,
                    Y = 12
                });
            }
        }

        private void OpenResult(int index)
        {
            if (index >= 0 && index < _results.Count)
            {
                ItemFinderManager.Locate(_results[index]);
            }
        }

        private void OnPropertiesReceived(object sender, OPLEventArgs e)
        {
            _requestedProperties.Remove(e.Serial);

            if (_pendingProperties > 0 && !string.IsNullOrWhiteSpace(_input.Text))
            {
                _refreshAt = (long)Time.Ticks + 300;
            }
        }

        private void AddGuideCard(int y, int height, string title, string description,
            string example, int buttonID)
        {
            AddSurface(GUIDE_X, y, GUIDE_WIDTH, height, 0.34f);
            Add(new Label(title, true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.ItemFinder), GUIDE_WIDTH - 16, font: 1)
            {
                X = GUIDE_X + 8,
                Y = y + 7
            });
            Add(new Label(description, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder),
                GUIDE_WIDTH - 16, font: 1)
            {
                X = GUIDE_X + 8,
                Y = y + 27
            });
            NiceButton exampleButton = CreateButton(GUIDE_X + 8, y + height - 28,
                GUIDE_WIDTH - 16, 21, example, buttonID);
            exampleButton.SetTooltip(buttonID == 10
                ? "ring hci>=10 di>=20 ssi>=10"
                : buttonID == 11
                    ? "ring not:cursed\nProperty example: ring not:hci>=10"
                    : "ring 2of(hci>=10,di>=20,ssi>=10)");
            Add(exampleButton);
        }

        private void AddSurface(int x, int y, int width, int height, float alpha,
            bool input = false)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.ItemFinder, alpha, input));
        }

        private void AddAccent(int x, int y, int width)
        {
            Add(new AlphaBlendControl(0.72f)
            {
                X = x,
                Y = y,
                Width = width,
                Height = 1,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
            });
        }

        private static NiceButton CreateButton(int x, int y, int width, int height,
            string text, int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.ItemFinder);
        }

        private sealed class FinderResultRow : HitBox
        {
            private readonly ItemFinderGump _owner;
            private readonly int _index;
            private readonly AlphaBlendControl _surface;
            private readonly float _normalAlpha;

            internal FinderResultRow(ItemFinderGump owner, ItemFinderManager.SearchResult result,
                int index, int width) : base(0, 0, width, 52)
            {
                _owner = owner;
                _index = index;
                CanMove = false;
                Alpha = 0.08f;
                BackgroundColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder);

                Add(_surface = new AlphaBlendControl(0.34f)
                {
                    Width = width,
                    Height = 50,
                    BaseColor = Color.Black
                });
                _normalAlpha = _surface.Alpha;

                if (result.Graphic != 0)
                {
                    var icon = new StaticPic(result.Graphic, result.Hue)
                    {
                        CanMove = false,
                        AcceptMouseInput = false
                    };

                    if (!icon.IsDisposed && icon.Width > 0 && icon.Height > 0)
                    {
                        double scale = Math.Min(1d, Math.Min(36d / icon.Width, 36d / icon.Height));
                        icon.ScaleWidthAndHeight(scale);
                        icon.X = 5 + (38 - icon.Width) / 2;
                        icon.Y = 7 + (36 - icon.Height) / 2;
                        Add(icon);
                    }
                }

                string amount = result.Amount > 1
                    ? $"  ×{result.Amount}"
                    : string.Empty;
                Add(new Label(result.Name + amount, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder),
                    width - 172, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 48,
                    Y = 5
                });

                Add(new Label(string.IsNullOrEmpty(result.Properties) ? "Name/type match" : result.Properties,
                    true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.ItemFinder), width - 172, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 48,
                    Y = 27
                });

                Add(new Label(result.Location, true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 112, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped,
                    align: TEXT_ALIGN_TYPE.TS_RIGHT)
                {
                    X = width - 120,
                    Y = 5
                });
                Add(new Label("LOCATE  ›", true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder), 112, font: 1,
                    style: FontStyle.BlackBorder,
                    align: TEXT_ALIGN_TYPE.TS_RIGHT)
                {
                    X = width - 120,
                    Y = 27
                });

                SetTooltip($"Locate item\n{result.Name}\n{result.Location}\n\n{result.AllProperties}");
            }

            protected override void OnMouseEnter(int x, int y)
            {
                base.OnMouseEnter(x, y);
                _surface.Alpha = Math.Min(1f, _normalAlpha + 0.24f);
            }

            protected override void OnMouseExit(int x, int y)
            {
                base.OnMouseExit(x, y);
                _surface.Alpha = _normalAlpha;
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left)
                {
                    _owner.OpenResult(_index);
                }
            }
        }

        private sealed class FinderInput : StbTextBox
        {
            private readonly ItemFinderGump _owner;

            internal FinderInput(ItemFinderGump owner)
                : base(1, 220, 584, true, hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder))
            {
                _owner = owner;
            }

            protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
            {
                if (key == SDL.SDL_Keycode.SDLK_RETURN || key == SDL.SDL_Keycode.SDLK_KP_ENTER)
                {
                    _owner.RunSearch(true);
                    return;
                }

                base.OnKeyDown(key, mod);
            }
        }
    }
}
