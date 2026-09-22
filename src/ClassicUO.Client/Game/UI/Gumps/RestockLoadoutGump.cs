// TazUO addition: named restock item loadouts.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RestockLoadoutGump : Gump
    {
        private const int WIDTH = 650;
        private const int HEIGHT = 500;

        private RestockAgentGump _owner;
        private readonly StbTextBox _nameInput;
        private readonly VBoxContainer _loadoutBox;
        private readonly Label _loadoutTitle;
        private readonly Label _working;
        private readonly Label _status;
        private int _selectedIndex = -1;
        private long _nextRefresh;

        internal RestockLoadoutGump(RestockAgentGump owner) : base(0, 0)
        {
            _owner = owner;
            X = owner?.X + 36 ?? 250;
            Y = owner?.Y + 36 ?? 150;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.RestockAgent, 0.97f));
            AddSurface(20, 10, WIDTH - 40, 50, 0.54f);
            Add(new Label("RESTOCK AGENT  •  LOADOUTS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Named item lists are saved separately for this character.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 38
            });
            Add(CreateButton(WIDTH - 52, 16, 28, 26, "X", 99));
            AddAccent(24, 61, WIDTH - 48);

            Add(new Label("NAME", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                60, font: 1)
            {
                X = 24,
                Y = 80
            });
            AddSurface(82, 72, 250, 32, 0.68f, true);
            Add(_nameInput = new StbTextBox(1, 40, 236, true,
                hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent))
            {
                X = 89,
                Y = 79,
                Width = 236,
                Height = 22
            });
            Add(_working = new Label(string.Empty, true,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent),
                280, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = 346,
                Y = 80
            });

            Add(CreateButton(24, 114, 92, 28, "Save new", 1));
            NiceButton update = CreateButton(122, 114, 132, 28, "Update selected", 2);
            update.SetTooltip("Overwrite the selected loadout with the Restock Agent's current targets and name.");
            Add(update);
            Add(CreateButton(260, 114, 78, 28, "Clear", 3));
            Add(_status = new Label("Load a profile, edit targets in Restock Agent, then update it.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                288, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
            {
                X = 350,
                Y = 120
            });
            AddAccent(24, 153, WIDTH - 48);

            Add(_loadoutTitle = new Label("SAVED LOADOUTS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent),
                WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 168
            });
            AddSurface(24, 192, WIDTH - 48, 244, 0.36f);
            var scroll = new ScrollArea(28, 196, WIDTH - 56, 236, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            scroll.Add(_loadoutBox = new VBoxContainer(
                scroll.Width - scroll.ScrollBarWidth() - 3, 0, 0));
            Add(scroll);

            AddAccent(24, 451, WIDTH - 48);
            Add(new Label(
                "LOAD restores targets, destinations and source priority. EDIT selects it for updating.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 458
            });

            RefreshRows();
            RefreshWorkingLabel();
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;

        internal void Attach(RestockAgentGump owner)
        {
            _owner = owner;
            RefreshRows();
            RefreshWorkingLabel();
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 1)
            {
                SaveNew();
            }
            else if (buttonID == 2)
            {
                UpdateSelected();
            }
            else if (buttonID == 3)
            {
                ClearEditor();
            }
            else if (buttonID >= 1000 && buttonID < 2000)
            {
                Load(buttonID - 1000, false);
            }
            else if (buttonID >= 2000 && buttonID < 3000)
            {
                Load(buttonID - 2000, true);
            }
            else if (buttonID >= 3000 && buttonID < 4000)
            {
                Delete(buttonID - 3000);
            }
            else if (buttonID == 99)
            {
                Dispose();
            }
        }

        public override void Update()
        {
            base.Update();

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 500;
                string previous = _working.Text;
                RefreshWorkingLabel();

                if (!string.Equals(previous, _working.Text, StringComparison.Ordinal))
                {
                    RefreshRows();
                }
            }
        }

        public override void Dispose()
        {
            if (UIManager.KeyboardFocusControl == _nameInput)
            {
                UIManager.KeyboardFocusControl = null;
            }

            base.Dispose();
        }

        private void SaveNew()
        {
            if (!RestockAgentManager.AddLoadout(_nameInput.Text))
            {
                _status.Text = "Enter a unique loadout name.";
                return;
            }

            _selectedIndex = RestockAgentManager.Loadouts.Count - 1;
            string name = RestockAgentManager.Loadouts[_selectedIndex].Name;
            _nameInput.SetText(name);
            _status.Text = $"Saved {name} with {RestockAgentManager.Settings.Items.Count} target(s).";
            RefreshOwner($"Saved restock loadout: {name}.");
            RefreshRows();
            RefreshWorkingLabel();
        }

        private void UpdateSelected()
        {
            if (_selectedIndex < 0)
            {
                _status.Text = "Choose Edit on a saved loadout first.";
                return;
            }

            if (!RestockAgentManager.UpdateLoadout(_selectedIndex, _nameInput.Text))
            {
                _status.Text = "Enter a unique name for the selected loadout.";
                return;
            }

            string name = RestockAgentManager.Loadouts[_selectedIndex].Name;
            _nameInput.SetText(name);
            _status.Text = $"Updated {name} from the current Restock Agent targets.";
            RefreshOwner($"Updated restock loadout: {name}.");
            RefreshRows();
            RefreshWorkingLabel();
        }

        private void Load(int index, bool edit)
        {
            IReadOnlyList<RestockLoadout> loadouts = RestockAgentManager.Loadouts;

            if (index < 0 || index >= loadouts.Count
                || !RestockAgentManager.LoadLoadout(index))
            {
                _status.Text = "The selected loadout could not be loaded.";
                return;
            }

            string name = RestockAgentManager.Loadouts[index].Name;

            if (edit)
            {
                _selectedIndex = index;
                _nameInput.SetText(name);
                _status.Text = $"Editing {name}. Change targets in Restock Agent, then choose Update selected.";
            }
            else
            {
                _selectedIndex = -1;
                _nameInput.SetText(string.Empty);
                _status.Text = $"Loaded {name}.";
            }

            RefreshOwner($"Loaded restock loadout: {name}.");
            RefreshRows();
            RefreshWorkingLabel();

            if (edit && _owner != null && !_owner.IsDisposed)
            {
                _owner.BringOnTop();
            }
        }

        private void Delete(int index)
        {
            if (!RestockAgentManager.DeleteLoadout(index))
            {
                _status.Text = "The selected loadout could not be deleted.";
                return;
            }

            if (_selectedIndex == index)
            {
                _selectedIndex = -1;
                _nameInput.SetText(string.Empty);
            }
            else if (_selectedIndex > index)
            {
                _selectedIndex--;
            }

            _status.Text = "Loadout deleted. Current restock targets were kept.";
            RefreshOwner();
            RefreshRows();
            RefreshWorkingLabel();
        }

        private void ClearEditor()
        {
            _selectedIndex = -1;
            _nameInput.SetText(string.Empty);
            _status.Text = "Editor cleared. Current restock targets were kept.";
            UIManager.KeyboardFocusControl = _nameInput;
            _nameInput.SetKeyboardFocus();
        }

        private void RefreshOwner(string message = null)
        {
            if (_owner != null && !_owner.IsDisposed)
            {
                _owner.RefreshLoadoutState(message);
            }
        }

        private void RefreshWorkingLabel()
        {
            _working.Text = $"CURRENT: {RestockAgentManager.ActiveLoadoutDescription}"
                + $"  •  {RestockAgentManager.Settings.Items.Count} target(s)";
        }

        private void RefreshRows()
        {
            _loadoutBox.Clear();
            IReadOnlyList<RestockLoadout> loadouts = RestockAgentManager.Loadouts;
            _loadoutTitle.Text = $"SAVED LOADOUTS  •  {loadouts.Count}";

            for (int i = 0; i < loadouts.Count; i++)
            {
                _loadoutBox.Add(new LoadoutRow(loadouts[i], i, _loadoutBox.Width));
            }

            if (loadouts.Count == 0)
            {
                _loadoutBox.Add(new Label(
                    "No loadouts yet. Configure targets, enter a name, and choose Save new.",
                    true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                    _loadoutBox.Width - 16, font: 1)
                {
                    X = 8,
                    Y = 12
                });
            }
        }

        private void AddSurface(int x, int y, int width, int height, float alpha,
            bool input = false)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.RestockAgent, alpha, input));
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

        private sealed class LoadoutRow : Control
        {
            internal LoadoutRow(RestockLoadout loadout, int index, int width)
            {
                Width = width;
                Height = 58;
                WantUpdateSize = false;
                CanMove = false;

                Add(new AlphaBlendControl(0.34f)
                {
                    Width = width,
                    Height = 56,
                    BaseColor = Color.Black
                });
                Add(new Label(loadout.Name, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent),
                    width - 202, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 6
                });

                bool active = string.Equals(
                    RestockAgentManager.Settings.ActiveLoadoutName,
                    loadout.Name, StringComparison.OrdinalIgnoreCase);
                string detail = $"{loadout.Items?.Count ?? 0} target(s)  •  "
                    + $"{loadout.SourceSerials?.Count ?? 0} ordered source(s)";

                if (active)
                {
                    detail += RestockAgentManager.ActiveLoadoutDescription.EndsWith(
                        " • modified", StringComparison.Ordinal)
                        ? "  •  ACTIVE, MODIFIED"
                        : "  •  ACTIVE";
                }

                Add(new Label(detail, true,
                    active
                        ? FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.RestockAgent)
                        : FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent),
                    width - 202, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 31
                });

                Add(CreateButton(width - 190, 14, 58, 28, "Load", 1000 + index));
                Add(CreateButton(width - 126, 14, 58, 28, "Edit", 2000 + index));
                NiceButton delete = CreateButton(width - 60, 14, 28, 28, "X", 3000 + index);
                delete.SetTooltip("Delete this saved loadout; current targets remain unchanged.");
                Add(delete);
            }
        }
    }
}
