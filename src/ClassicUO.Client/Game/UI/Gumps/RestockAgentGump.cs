// TazUO addition: themed restock agent configuration and status gump.

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RestockAgentGump : Gump
    {
        private const int WIDTH = 900;
        private const int HEIGHT = 597;
        private const int LEFT_X = 24;
        private const int LEFT_WIDTH = 208;
        private const int LIST_X = 250;
        private const int LIST_WIDTH = 626;
        private const int SOURCE_SCAN_TIMEOUT_MS = 5000;

        private readonly Label _sourceName;
        private readonly Label _status;
        private readonly Label _listTitle;
        private readonly Label _readyBadge;
        private readonly Label _supplyCheck;
        private readonly Label _equipmentCheck;
        private readonly Label _durabilityCheck;
        private readonly Label _packCheck;
        private readonly VBoxContainer _itemsBox;
        private readonly List<RestockRow> _rows = new List<RestockRow>();
        private readonly Queue<uint> _sourceScanQueue = new Queue<uint>();
        private readonly HashSet<uint> _sourceGumpsToClose = new HashSet<uint>();
        private long _nextRefresh;
        private long _nextSourceOpenAt;
        private long _sourceOpenDeadline;
        private uint _currentSourceSerial;
        private int _sourceScanCount;
        private int _sourcesScanned;
        private bool _sourceScanActive;

        internal RestockAgentGump(bool announceReadiness = false) : base(0, 0)
        {
            X = ProfileManager.CurrentProfile?.RestockAgentPosition.X ?? 220;
            Y = ProfileManager.CurrentProfile?.RestockAgentPosition.Y ?? 120;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.RestockAgent, 0.96f));
            AddSurface(20, 10, WIDTH - 40, 52, 0.54f);
            AddSurface(727, 14, 113, 42, 0.48f);
            Add(_readyBadge = new Label("CHECKING", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                113, font: 1, align: TEXT_ALIGN_TYPE.TS_CENTER)
            {
                X = 727,
                Y = 26
            });
            NiceButton close = CreateButton(848, 20, 28, 28, "X", 99);
            close.SetTooltip("Close Restock Agent");
            Add(close);
            NiceButton loadouts = CreateButton(600, 20, 116, 28, "LOADOUTS", 9);
            loadouts.SetTooltip("Save, load, rename and update restock item profiles.");
            Add(loadouts);
            Add(new Label("RESTOCK AGENT", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                570, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Fill each target container to exact quantities from all selected sources.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 38
            });
            AddAccent(24, 61, WIDTH - 48);

            AddSurface(24, 72, WIDTH - 48, 66, 0.38f);
            Add(new Label("SOURCE CONTAINERS", true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent),
                480, font: 1)
            {
                X = 36,
                Y = 82
            });
            Add(_sourceName = new Label(RestockAgentManager.SourceDescription(), true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent), 530, font: 1,
                style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = 36,
                Y = 106
            });
            Add(CreateButton(548, 86, 86, 32, "Add source", 2));
            Add(CreateButton(640, 86, 86, 32, "Priority", 10));
            Add(CreateButton(732, 86, 68, 32, "Open", 3));
            Add(CreateButton(806, 86, 70, 32, "Clear", 8));

            Add(new Label("SUPPLY PACKS", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                LEFT_WIDTH, font: 1)
            {
                X = LEFT_X,
                Y = 158
            });
            Add(new Label("Click once to add missing entries.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), LEFT_WIDTH, font: 1)
            {
                X = LEFT_X,
                Y = 179
            });

            int presetY = 207;
            for (int i = 0; i < RestockAgentManager.Presets.Count; i++)
            {
                RestockPreset preset = RestockAgentManager.Presets[i];
                NiceButton button = CreateButton(LEFT_X, presetY, LEFT_WIDTH, 34,
                    preset.Name, 100 + i);
                button.SetTooltip($"Add {preset.Items.Length} configured item type(s)");
                Add(button);
                presetY += 40;
            }

            AddSurface(LEFT_X, 407, LEFT_WIDTH, 66, 0.34f);
            Add(new Label("SHARD SPECIFIC", true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent),
                LEFT_WIDTH - 16, font: 1)
            {
                X = LEFT_X + 8,
                Y = 416
            });
            Add(new Label("Tools, deeds, scrolls, food and pouches", true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent), LEFT_WIDTH - 16, font: 1)
            {
                X = LEFT_X + 8,
                Y = 437
            });
            Add(CreateButton(LEFT_X + 8, 451, LEFT_WIDTH - 16, 18,
                "Target custom item", 4));

            Add(new AlphaBlendControl(0.55f)
            {
                X = 240,
                Y = 157,
                Width = 1,
                Height = 306,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.RestockAgent)
            });

            Add(_listTitle = new Label("BACKPACK TARGETS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent), LIST_WIDTH, font: 1)
            {
                X = LIST_X,
                Y = 158
            });
            Add(new Label("Item", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 180, font: 1)
            {
                X = LIST_X + 48,
                Y = 181
            });
            Add(new Label("Have", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 48, font: 1)
            {
                X = LIST_X + 242,
                Y = 181
            });
            Add(new Label("Stock", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 48, font: 1)
            {
                X = LIST_X + 292,
                Y = 181
            });
            Add(new Label("Target", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 54, font: 1)
            {
                X = LIST_X + 342,
                Y = 181
            });
            Add(new Label("Destination", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 96, font: 1)
            {
                X = LIST_X + 402,
                Y = 181
            });
            Add(new Label("Hue", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 62, font: 1)
            {
                X = LIST_X + 510,
                Y = 181
            });

            AddSurface(LIST_X, 201, LIST_WIDTH, 260, 0.34f);
            var scroll = new ScrollArea(LIST_X + 4, 205, LIST_WIDTH - 8, 252, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            scroll.Add(_itemsBox = new VBoxContainer(scroll.Width - scroll.ScrollBarWidth() - 3, 0, 0));
            Add(scroll);

            Add(new Label("READINESS CHECK", true, FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 475
            });
            AddSurface(24, 495, WIDTH - 48, 54, 0.38f);
            AddReadinessHeading("SUPPLIES", 36);
            AddReadinessHeading("EQUIPMENT", 244);
            AddReadinessHeading("DURABILITY", 452);
            AddReadinessHeading("BACKPACK", 660);
            Add(_supplyCheck = CreateReadinessValue(36));
            Add(_equipmentCheck = CreateReadinessValue(244));
            Add(_durabilityCheck = CreateReadinessValue(452));
            Add(_packCheck = CreateReadinessValue(660));

            Add(CreateButton(LEFT_X, 557, 126, 30, "RESTOCK NOW", 1));
            Add(CreateButton(LEFT_X + 134, 557, 104, 30, "CHECK NOW", 6));
            Add(CreateButton(LEFT_X + 246, 557, 146, 30, "SET GEAR BASELINE", 7));
            Add(CreateButton(LEFT_X + 400, 557, 74, 30, "Clear", 5));
            Add(_status = new Label("Choose sources and add supply targets.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 360, font: 1,
                style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = 516,
                Y = 565
            });

            RebuildItems();
            RefreshReadiness(announceReadiness);
            BeginSourceScan();
            SetInScreen();
            UIManager.GetGump<RestockLoadoutGump>()?.Attach(this);
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 1)
            {
                if (_sourceScanActive)
                {
                    _status.Text = "Wait for the source stock scan to finish.";
                    return;
                }

                bool queued = RestockAgentManager.Run();
                _status.Text = queued
                    ? "Restock queued. Readiness updates as item moves complete."
                    : "No item moves were queued.";
            }
            else if (buttonID == 2)
            {
                PickSource();
            }
            else if (buttonID == 3)
            {
                OpenSource();
            }
            else if (buttonID == 4)
            {
                PickCustomItem();
            }
            else if (buttonID == 5)
            {
                RestockAgentManager.Settings.Items.Clear();
                RestockAgentManager.Save();
                RebuildItems();
                _status.Text = "All restock targets cleared.";
            }
            else if (buttonID == 6)
            {
                RefreshReadiness(true);
            }
            else if (buttonID == 7)
            {
                int count = RestockAgentManager.CaptureEquipmentBaseline();
                _status.Text = count > 0
                    ? $"Saved {count} equipped layer(s) as the readiness baseline."
                    : "Equipment baseline cleared because no equipment was found.";
                RefreshReadiness(false);
            }
            else if (buttonID == 8)
            {
                CancelSourceScan();
                RestockAgentManager.ClearSources();
                _sourceName.Text = RestockAgentManager.SourceDescription();
                _status.Text = "All source containers cleared.";
            }
            else if (buttonID == 9)
            {
                OpenLoadouts();
            }
            else if (buttonID == 10)
            {
                RestockSourcePriorityGump sources = UIManager.GetGump<RestockSourcePriorityGump>();
                if (sources == null) UIManager.Add(new RestockSourcePriorityGump(this));
                else sources.BringOnTop();
            }
            else if (buttonID == 99)
            {
                Dispose();
            }
            else if (buttonID >= 100)
            {
                int preset = buttonID - 100;
                int added = RestockAgentManager.AddPreset(preset);
                RebuildItems();
                _status.Text = added > 0
                    ? $"Added {added} item type(s) from {RestockAgentManager.Presets[preset].Name}."
                    : "That supply pack is already configured.";
            }
        }

        public override void Update()
        {
            base.Update();
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.RestockAgentPosition = Location;

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 750;
                _sourceName.Text = RestockAgentManager.SourceDescription();
                RestockCountSnapshot counts = RestockAgentManager.CreateCountSnapshot(
                    RestockAgentManager.Settings.Items);

                foreach (RestockRow row in _rows)
                {
                    row.RefreshCount(counts);
                }

                UpdateListTitle();
                RefreshReadiness(false);
            }

            if (_sourceScanActive)
            {
                UpdateSourceScan();
            }
        }

        internal void CheckReadiness(bool announce)
        {
            RefreshReadiness(announce);
        }

        internal void RefreshLoadoutState(string message = null)
        {
            RebuildItems();
            RefreshReadiness(false);
            BeginSourceScan();

            if (!string.IsNullOrWhiteSpace(message))
            {
                _status.Text = message;
            }
        }

        public override void Dispose()
        {
            CancelSourceScan();
            RestockAgentManager.Save();
            base.Dispose();
        }

        private async void PickSource()
        {
            _status.Text = "Target the source container.";
            uint serial = await TargetHelper.TargetAsync();

            if (IsDisposed || serial == 0)
            {
                return;
            }

            Item source = World.Items.Get(serial);

            if (source == null || source.IsDestroyed || !source.ItemData.IsContainer)
            {
                _status.Text = "Source must be a loaded container.";
                return;
            }

            if (!RestockAgentManager.AddSource(source))
            {
                _status.Text = "That source is already selected or is inside your backpack.";
                return;
            }
            _sourceName.Text = RestockAgentManager.SourceDescription();
            BeginSourceScan();
        }

        private async void PickCustomItem()
        {
            _status.Text = "Target an example item to add it.";
            uint serial = await TargetHelper.TargetAsync();

            if (IsDisposed || serial == 0)
            {
                return;
            }

            Item item = World.Items.Get(serial);

            if (item == null || item.IsDestroyed)
            {
                _status.Text = "Target a loaded item.";
                return;
            }

            if (RestockAgentManager.AddItem(item))
            {
                RebuildItems();
                _status.Text = "Custom item added with its exact hue.";
            }
            else
            {
                _status.Text = "That item and hue are already configured.";
            }
        }

        private void OpenSource()
        {
            if (RestockAgentManager.Settings.SourceSerials.Count == 0)
            {
                _status.Text = "No source containers are configured.";
                return;
            }

            CancelSourceScan();
            RestockAgentManager.OpenSources();
            _status.Text = "Opening all loaded source containers.";
        }

        private void BeginSourceScan()
        {
            CancelSourceScan();
            int loadedSources = 0;

            foreach (uint serial in RestockAgentManager.Settings.SourceSerials)
            {
                Item source = World.Items.Get(serial);

                if (source == null || source.IsDestroyed || !source.ItemData.IsContainer)
                {
                    continue;
                }

                loadedSources++;

                if (IsSourceGumpOpen(serial))
                {
                    continue;
                }

                _sourceScanQueue.Enqueue(serial);
                _sourceGumpsToClose.Add(serial);
            }

            _sourceScanCount = _sourceScanQueue.Count;
            _sourcesScanned = 0;

            if (_sourceScanCount == 0)
            {
                RefreshSourceCounts();

                if (RestockAgentManager.Settings.SourceSerials.Count > 0)
                {
                    _status.Text = loadedSources > 0
                        ? "Source stock is ready."
                        : "Selected source containers are not currently in range.";
                }

                return;
            }

            _sourceScanActive = true;
            _nextSourceOpenAt = (long)Time.Ticks;
            _status.Text = $"Reading stock from {_sourceScanCount} source container(s)…";
        }

        private void UpdateSourceScan()
        {
            long now = (long)Time.Ticks;

            if (_currentSourceSerial != 0)
            {
                bool opened = IsSourceGumpOpen(_currentSourceSerial);

                if (!opened && now < _sourceOpenDeadline)
                {
                    return;
                }

                if (opened)
                {
                    CloseSourceGump(_currentSourceSerial);
                    _sourceGumpsToClose.Remove(_currentSourceSerial);
                }

                _currentSourceSerial = 0;
                _sourceOpenDeadline = 0;
                _sourcesScanned++;
                _nextSourceOpenAt = now + Math.Max(500,
                    GlobalActionCooldown.CooldownDuration);
                RefreshSourceCounts();
            }

            if (_sourceScanQueue.Count > 0)
            {
                if (now < _nextSourceOpenAt || GlobalActionCooldown.IsOnCooldown)
                {
                    return;
                }

                _currentSourceSerial = _sourceScanQueue.Dequeue();
                _sourceOpenDeadline = now + SOURCE_SCAN_TIMEOUT_MS;
                GameActions.DoubleClick(_currentSourceSerial);
                GlobalActionCooldown.BeginCooldown();
                _status.Text = $"Reading source {_sourcesScanned + 1} of {_sourceScanCount}…";
                return;
            }

            if (_currentSourceSerial == 0)
            {
                _sourceScanActive = false;

                foreach (uint serial in _sourceGumpsToClose)
                {
                    CloseSourceGump(serial);
                }

                _sourceGumpsToClose.Clear();
                RefreshSourceCounts();
                _status.Text = $"Source scan finished: {_sourcesScanned} container(s) checked.";
            }
        }

        private void CancelSourceScan()
        {
            _sourceScanActive = false;
            _sourceScanQueue.Clear();
            _nextSourceOpenAt = 0;
            _sourceOpenDeadline = 0;
            _currentSourceSerial = 0;
            _sourceScanCount = 0;
            _sourcesScanned = 0;

            foreach (uint serial in _sourceGumpsToClose)
            {
                CloseSourceGump(serial);
            }

            _sourceGumpsToClose.Clear();
        }

        private void RefreshSourceCounts()
        {
            RestockCountSnapshot counts = RestockAgentManager.CreateCountSnapshot(
                RestockAgentManager.Settings.Items);

            foreach (RestockRow row in _rows)
            {
                row.RefreshCount(counts);
            }

            _sourceName.Text = RestockAgentManager.SourceDescription();
            RefreshReadiness(false);
        }

        private static bool IsSourceGumpOpen(uint serial)
        {
            ContainerGump container = UIManager.GetGump<ContainerGump>(serial);

            if (container != null && !container.IsDisposed)
            {
                return true;
            }

            GridContainer grid = UIManager.GetGump<GridContainer>(serial);
            return grid != null && !grid.IsDisposed;
        }

        private static void CloseSourceGump(uint serial)
        {
            ContainerGump container = UIManager.GetGump<ContainerGump>(serial);

            if (container != null && !container.IsDisposed)
            {
                container.Dispose();
            }

            GridContainer grid = UIManager.GetGump<GridContainer>(serial);

            if (grid != null && !grid.IsDisposed)
            {
                grid.Dispose();
            }
        }

        private void OpenLoadouts()
        {
            RestockLoadoutGump existing = UIManager.GetGump<RestockLoadoutGump>();

            if (existing == null || existing.IsDisposed)
            {
                UIManager.Add(new RestockLoadoutGump(this));
            }
            else
            {
                existing.Attach(this);
                existing.BringOnTop();
            }
        }

        private void RebuildItems()
        {
            _rows.Clear();
            _itemsBox.Clear();

            foreach (RestockEntry entry in RestockAgentManager.Settings.Items)
            {
                var row = new RestockRow(this, entry, _itemsBox.Width);
                _rows.Add(row);
                _itemsBox.Add(row);
            }

            if (_rows.Count == 0)
            {
                _itemsBox.Add(new Label("No targets configured. Add a supply pack or target an item.",
                    true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), _itemsBox.Width - 16, font: 1)
                {
                    X = 8,
                    Y = 12
                });
            }

            UpdateListTitle();
        }

        private void UpdateListTitle()
        {
            _listTitle.Text = $"RESTOCK TARGETS  •  {_rows.Count}  •  "
                + RestockAgentManager.ActiveLoadoutDescription;
        }

        private void RemoveEntry(RestockEntry entry)
        {
            RestockAgentManager.Remove(entry);
            RebuildItems();
            _status.Text = "Restock target removed.";
        }

        private void RefreshReadiness(bool announce)
        {
            ReadinessSnapshot snapshot = ReadinessCheckManager.Evaluate();
            _readyBadge.Text = snapshot.IsReady ? "READY" : "NOT READY";
            _readyBadge.Hue = snapshot.IsReady
                ? FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent)
                : (ushort)0x0021;

            if (snapshot.ActiveSupplyTargets == 0)
            {
                SetReadinessValue(_supplyCheck, "No targets configured", false);
            }
            else
            {
                SetReadinessValue(_supplyCheck,
                    $"{snapshot.ReadySupplyTargets}/{snapshot.ActiveSupplyTargets} targets ready",
                    snapshot.SuppliesReady);
            }

            if (snapshot.EquipmentBaselineCount == 0)
            {
                _equipmentCheck.Text = "Baseline not set";
                _equipmentCheck.Hue = FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
            }
            else if (snapshot.EquipmentReady)
            {
                SetReadinessValue(_equipmentCheck,
                    $"{snapshot.EquipmentBaselineCount} layers present", true);
            }
            else
            {
                string firstMissing = snapshot.MissingEquipmentLayers[0].ToString();
                SetReadinessValue(_equipmentCheck,
                    $"{snapshot.MissingEquipmentLayers.Count} missing: {firstMissing}", false);
            }

            if (snapshot.DurabilityItems == 0)
            {
                _durabilityCheck.Text = "No tracked durability";
                _durabilityCheck.Hue = FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
            }
            else if (snapshot.DurabilityReady)
            {
                SetReadinessValue(_durabilityCheck,
                    $"OK · lowest {snapshot.LowestDurability.Durabilty}/{snapshot.LowestDurability.MaxDurabilty}",
                    true);
            }
            else
            {
                SetReadinessValue(_durabilityCheck,
                    $"{snapshot.CriticalDurabilityItems} critical · lowest {snapshot.LowestDurability.Durabilty}/{snapshot.LowestDurability.MaxDurabilty}",
                    false);
            }

            string weight = snapshot.WeightMax > 0
                ? $"{snapshot.Weight}/{snapshot.WeightMax}"
                : $"{snapshot.Weight}/?";
            SetReadinessValue(_packCheck,
                $"W {weight} · {snapshot.BackpackItems}/{ReadinessCheckManager.BackpackItemLimit} items",
                snapshot.BackpackReady);

            if (!announce)
            {
                return;
            }

            if (snapshot.IsReady)
            {
                _status.Text = "Ready: supplies, gear, durability and backpack checks passed.";
                GameActions.Print("Readiness: READY. All configured checks passed.", 0x35);
                return;
            }

            var issues = new List<string>();

            if (!snapshot.SuppliesReady)
            {
                issues.Add(snapshot.ActiveSupplyTargets == 0
                    ? "no supply targets"
                    : $"{snapshot.ActiveSupplyTargets - snapshot.ReadySupplyTargets} supply shortage(s)");
            }

            if (!snapshot.EquipmentReady)
            {
                issues.Add("missing " + string.Join(", ", snapshot.MissingEquipmentLayers));
            }

            if (!snapshot.DurabilityReady)
            {
                issues.Add($"{snapshot.CriticalDurabilityItems} critical durability item(s)");
            }

            if (!snapshot.BackpackReady)
            {
                issues.Add("backpack capacity");
            }

            string summary = string.Join("; ", issues);
            _status.Text = summary;
            GameActions.Print($"Readiness: NOT READY — {summary}.", 0x21);
        }

        private void AddReadinessHeading(string text, int x)
        {
            Add(new Label(text, true, FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent), 166, font: 1)
            {
                X = x,
                Y = 501
            });
        }

        private static Label CreateReadinessValue(int x) => new Label(
            "Checking...", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 166, font: 1,
            style: FontStyle.BlackBorder | FontStyle.Cropped)
        {
            X = x,
            Y = 524
        };

        private static void SetReadinessValue(Label label, string text, bool ready)
        {
            label.Text = text;
            label.Hue = ready ? FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent) : (ushort)0x0021;
        }

        private void AddSurface(int x, int y, int width, int height, float alpha)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.RestockAgent, alpha));
        }

        private void AddAccent(int x, int y, int width)
        {
            Add(new AlphaBlendControl(0.72f)
            {
                X = x,
                Y = y,
                Width = width,
                Height = 1,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.RestockAgent)
            });
        }

        private static NiceButton CreateButton(int x, int y, int width, int height,
            string text, int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.RestockAgent);
        }

        private sealed class RestockRow : Control
        {
            private readonly RestockAgentGump _owner;
            private readonly RestockEntry _entry;
            private readonly Label _count;
            private readonly Label _stock;
            private readonly Label _detail;
            private readonly NiceButton _hue;
            private readonly NiceButton _destination;

            internal RestockRow(RestockAgentGump owner, RestockEntry entry, int width)
            {
                _owner = owner;
                _entry = entry;
                Width = width;
                Height = 58;
                WantUpdateSize = false;

                var surface = new AlphaBlendControl(0.34f)
                {
                    Width = width,
                    Height = 56,
                    BaseColor = Color.Black
                };
                Add(surface);

                var icon = new StaticPic(entry.Graphic, entry.Hue)
                {
                    CanMove = false,
                    AcceptMouseInput = false
                };

                if (!icon.IsDisposed && icon.Width > 0 && icon.Height > 0)
                {
                    double scale = Math.Min(1d, Math.Min(38d / icon.Width, 38d / icon.Height));
                    icon.ScaleWidthAndHeight(scale);
                    icon.X = 4 + (40 - icon.Width) / 2;
                    icon.Y = 8 + (38 - icon.Height) / 2;
                    Add(icon);
                }

                Add(new Label(entry.Name, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent),
                    188, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 48,
                    Y = 6
                });
                Add(_detail = new Label($"0x{entry.Graphic:X4}", true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                    188, font: 1)
                {
                    X = 48,
                    Y = 30
                });

                Add(_count = new Label("0", true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent),
                    48, font: 1, align: TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    X = 238,
                    Y = 18
                });

                Add(_stock = new Label("0", true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent),
                    48, font: 1, align: TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    X = 288,
                    Y = 18
                });

                var amountSurface = new AlphaBlendControl(0.65f)
                {
                    X = 343,
                    Y = 14,
                    Width = 58,
                    Height = 27,
                    BaseColor = Color.Black
                };
                Add(amountSurface);

                var amount = new AmountInput(this)
                {
                    X = 346,
                    Y = 17,
                    Width = 52,
                    Height = 20
                };
                amount.SetTooltip("Destination target quantity; zero disables this entry");
                Add(amount);

                Add(_destination = CreateRowButton(404, 14, 94,
                    RestockAgentManager.DestinationDescription(entry), 3));
                _destination.SetTooltip("Choose this item's destination container; target the backpack to reset");
                Add(_hue = CreateRowButton(504, 14, 64,
                    entry.MatchAnyHue ? "Any" : $"{entry.Hue:X4}", 1));
                _hue.SetTooltip("Toggle between any hue and this item's exact hue");
                Add(CreateRowButton(570, 14, 26, "X", 2));
                amount.SetText(entry.DesiredAmount.ToString());
                RefreshCount();
            }

            internal void RefreshCount()
            {
                int count = RestockAgentManager.CountInTarget(_entry);
                int sourceCount = RestockAgentManager.CountInSources(_entry);
                RefreshCount(count, sourceCount);
            }

            internal void RefreshCount(RestockCountSnapshot snapshot)
            {
                int count = snapshot.Target.TryGetValue(_entry, out int target) ? target : 0;
                int sourceCount = snapshot.Source.TryGetValue(_entry, out int source) ? source : 0;
                RefreshCount(count, sourceCount);
            }

            private void RefreshCount(int count, int sourceCount)
            {
                _count.Text = count.ToString();
                _stock.Text = sourceCount.ToString();
                _destination.SetText(RestockAgentManager.DestinationDescription(_entry));

                if (_entry.DesiredAmount == 0)
                {
                    _count.Hue = FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
                    _stock.Hue = FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
                    _detail.Text = $"Disabled · 0x{_entry.Graphic:X4}";
                    _detail.Hue = FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
                    return;
                }

                if (count >= _entry.DesiredAmount)
                {
                    _count.Hue = FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent);
                    _stock.Hue = sourceCount > 0
                        ? FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent)
                        : FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent);
                    _detail.Text = $"Ready · 0x{_entry.Graphic:X4}";
                    _detail.Hue = FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent);
                    return;
                }

                int deficit = _entry.DesiredAmount - count;
                bool sourceShort = sourceCount < deficit;
                _count.Hue = sourceShort ? (ushort)0x0035 : (ushort)0x0021;
                _stock.Hue = sourceShort ? (ushort)0x0021
                    : FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent);
                _detail.Text = sourceShort
                    ? $"Source short ({sourceCount}/{deficit}) · 0x{_entry.Graphic:X4}"
                    : $"Target short ({deficit}) · 0x{_entry.Graphic:X4}";
                _detail.Hue = _count.Hue;
            }

            public override void OnButtonClick(int buttonID)
            {
                if (buttonID == 1)
                {
                    _entry.MatchAnyHue = !_entry.MatchAnyHue;
                    _hue.SetText(_entry.MatchAnyHue ? "Any" : $"{_entry.Hue:X4}");
                    RestockAgentManager.Save();
                    RefreshCount();
                }
                else if (buttonID == 2)
                {
                    _owner.RemoveEntry(_entry);
                }
                else if (buttonID == 3)
                {
                    PickDestination();
                }
            }

            private async void PickDestination()
            {
                _owner._status.Text = "Target a container inside your backpack.";
                uint serial = await TargetHelper.TargetAsync();

                if (IsDisposed || _owner.IsDisposed || serial == 0)
                {
                    return;
                }

                Item target = World.Items.Get(serial);

                if (!RestockAgentManager.SetDestination(_entry, target))
                {
                    _owner._status.Text = "Destination must be the backpack or a loaded container inside it.";
                    return;
                }

                _destination.SetText(RestockAgentManager.DestinationDescription(_entry));
                _owner._status.Text = $"{_entry.Name} destination updated.";
                RefreshCount();
            }

            private static NiceButton CreateRowButton(int x, int y, int width, string text, int id)
            {
                return FeatureGumpArtwork.CreateButton(x, y, width, 26, text, id,
                    FeatureGumpArtworkKind.RestockAgent);
            }

            private sealed class AmountInput : StbTextBox
            {
                private readonly RestockRow _row;

                internal AmountInput(RestockRow row)
                    : base(1, ushort.MaxValue, 52, true, hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent))
                {
                    _row = row;
                    NumbersOnly = true;
                    TextChanged += OnTextChanged;
                }

                private bool _selectAllOnMouseUp;

                internal override void OnFocusEnter()
                {
                    _selectAllOnMouseUp = !IsFocused;
                    base.OnFocusEnter();

                    if (_selectAllOnMouseUp)
                    {
                        SelectAll();
                    }
                }

                protected override void OnMouseUp(int x, int y, MouseButtonType button)
                {
                    base.OnMouseUp(x, y, button);

                    if (button == MouseButtonType.Left && _selectAllOnMouseUp)
                    {
                        SelectAll();
                        _selectAllOnMouseUp = false;
                    }
                }

                private void OnTextChanged(object sender, EventArgs e)
                {
                    if (ushort.TryParse(Text, out ushort value))
                    {
                        _row._entry.DesiredAmount = value;
                        RestockAgentManager.Save();
                        _row.RefreshCount();
                    }
                }
            }
        }
    }
}
