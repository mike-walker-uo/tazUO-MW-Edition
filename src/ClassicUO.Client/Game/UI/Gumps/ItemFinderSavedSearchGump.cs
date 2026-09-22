// TazUO addition: saved Item Finder searches and query examples.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ItemFinderSavedSearchGump : Gump
    {
        private const int WIDTH = 760;
        private const int HEIGHT = 560;
        private const int SAVED_WIDTH = 350;

        private static readonly SearchExample[] _examples =
        {
            new SearchExample(
                "Balanced ring",
                "ring hci>=10 di>=20 ssi>=10",
                "Every listed bonus must meet its minimum."),
            new SearchExample(
                "Flexible ring",
                "ring 2of(hci>=10,di>=20,ssi>=10)",
                "Any two of the three bonuses may match."),
            new SearchExample(
                "Clean caster item",
                "lmc>=8 lrc>=15 not:cursed",
                "Requires both caster bonuses and excludes Cursed."),
            new SearchExample(
                "Uncursed weapon",
                "weapon di>=40 not:cursed",
                "Text and property filters can be combined.")
        };

        private ItemFinderGump _finder;
        private readonly StbTextBox _nameInput;
        private readonly StbTextBox _queryInput;
        private readonly VBoxContainer _savedBox;
        private readonly Label _savedTitle;
        private readonly Label _status;
        private int _selectedIndex = -1;

        internal ItemFinderSavedSearchGump(ItemFinderGump finder, string currentQuery)
            : base(0, 0)
        {
            _finder = finder;
            X = finder?.X + 18 ?? 240;
            Y = finder?.Y + 18 ?? 150;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.ItemFinder, 0.97f));
            AddSurface(20, 10, WIDTH - 40, 50, 0.54f);
            Add(new Label("ITEM FINDER  •  SAVED SEARCHES", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Keep reusable queries for this character, or start from an example.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 38
            });
            Add(CreateButton(708, 16, 28, 26, "X", 99));
            AddAccent(24, 61, WIDTH - 48);

            Add(new Label("NAME", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), 60, font: 1)
            {
                X = 24,
                Y = 79
            });
            AddSurface(83, 72, 205, 32, 0.68f, true);
            Add(_nameInput = CreateInput(90, 79, 191, 50));

            Add(new Label("QUERY", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), 60, font: 1)
            {
                X = 305,
                Y = 79
            });
            AddSurface(362, 72, 374, 32, 0.68f, true);
            Add(_queryInput = CreateInput(369, 79, 360, 220));
            _queryInput.SetText(currentQuery ?? string.Empty);

            Add(CreateButton(24, 113, 92, 27, "Save new", 1));
            NiceButton update = CreateButton(122, 113, 128, 27, "Update selected", 2);
            update.SetTooltip("Replace the selected saved search with the editor values.");
            Add(update);
            Add(CreateButton(256, 113, 82, 27, "Clear", 3));
            Add(_status = new Label("Select Edit to change a saved search.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 390, font: 1)
            {
                X = 350,
                Y = 119
            });
            AddAccent(24, 150, WIDTH - 48);

            Add(_savedTitle = new Label("SAVED SEARCHES", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), SAVED_WIDTH, font: 1)
            {
                X = 24,
                Y = 166
            });
            AddSurface(24, 190, SAVED_WIDTH, 329, 0.36f);
            var savedScroll = new ScrollArea(28, 194, SAVED_WIDTH - 8, 321, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            savedScroll.Add(_savedBox = new VBoxContainer(
                savedScroll.Width - savedScroll.ScrollBarWidth() - 3, 0, 0));
            Add(savedScroll);

            Add(new AlphaBlendControl(0.55f)
            {
                X = 384,
                Y = 164,
                Width = 1,
                Height = 355,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
            });
            Add(new Label("EXAMPLES", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), 352, font: 1)
            {
                X = 398,
                Y = 166
            });

            for (int i = 0; i < _examples.Length; i++)
            {
                AddExample(398, 190 + i * 82, 338, _examples[i], i);
            }

            AddAccent(24, 534, WIDTH - 48);
            Add(new Label("RUN executes immediately. LOAD copies an example into the editor so it can be changed or saved.",
                true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 540
            });

            RefreshSavedRows();
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;

        internal void Attach(ItemFinderGump finder, string currentQuery)
        {
            _finder = finder;

            if (_selectedIndex < 0 && !string.IsNullOrWhiteSpace(currentQuery))
            {
                _queryInput.SetText(currentQuery);
            }
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
                RunSaved(buttonID - 1000);
            }
            else if (buttonID >= 2000 && buttonID < 3000)
            {
                EditSaved(buttonID - 2000);
            }
            else if (buttonID >= 3000 && buttonID < 4000)
            {
                DeleteSaved(buttonID - 3000);
            }
            else if (buttonID >= 4000 && buttonID < 5000)
            {
                LoadExample(buttonID - 4000);
            }
            else if (buttonID >= 5000 && buttonID < 6000)
            {
                RunQuery(_examples[buttonID - 5000].Query);
            }
            else if (buttonID == 99)
            {
                Dispose();
            }
        }

        public override void Dispose()
        {
            if (UIManager.KeyboardFocusControl == _nameInput
                || UIManager.KeyboardFocusControl == _queryInput)
            {
                UIManager.KeyboardFocusControl = null;
            }

            base.Dispose();
        }

        private void SaveNew()
        {
            if (string.IsNullOrWhiteSpace(_nameInput.Text)
                || string.IsNullOrWhiteSpace(_queryInput.Text))
            {
                _status.Text = "Enter both a name and query.";
                return;
            }

            if (!ItemFinderManager.AddSavedSearch(_nameInput.Text, _queryInput.Text))
            {
                _status.Text = "That name already exists or the search could not be saved.";
                return;
            }

            _selectedIndex = -1;
            _status.Text = "Search saved for this character.";
            RefreshSavedRows();
        }

        private void UpdateSelected()
        {
            if (_selectedIndex < 0)
            {
                _status.Text = "Select Edit on a saved search first.";
                return;
            }

            if (!ItemFinderManager.UpdateSavedSearch(
                _selectedIndex, _nameInput.Text, _queryInput.Text))
            {
                _status.Text = "Enter unique name and a non-empty query.";
                return;
            }

            _status.Text = "Saved search updated.";
            RefreshSavedRows();
        }

        private void ClearEditor()
        {
            _selectedIndex = -1;
            _nameInput.SetText(string.Empty);
            _queryInput.SetText(string.Empty);
            _status.Text = "Editor cleared.";
            UIManager.KeyboardFocusControl = _nameInput;
            _nameInput.SetKeyboardFocus();
        }

        private void RunSaved(int index)
        {
            IReadOnlyList<ItemFinderManager.SavedSearch> searches = ItemFinderManager.SavedSearches;

            if (index >= 0 && index < searches.Count)
            {
                RunQuery(searches[index].Query);
            }
        }

        private void EditSaved(int index)
        {
            IReadOnlyList<ItemFinderManager.SavedSearch> searches = ItemFinderManager.SavedSearches;

            if (index < 0 || index >= searches.Count)
            {
                return;
            }

            _selectedIndex = index;
            _nameInput.SetText(searches[index].Name);
            _queryInput.SetText(searches[index].Query);
            _status.Text = $"Editing: {searches[index].Name}";
            UIManager.KeyboardFocusControl = _nameInput;
            _nameInput.SetKeyboardFocus();
        }

        private void DeleteSaved(int index)
        {
            if (!ItemFinderManager.DeleteSavedSearch(index))
            {
                _status.Text = "The saved search could not be deleted.";
                return;
            }

            if (_selectedIndex == index)
            {
                ClearEditor();
            }
            else if (_selectedIndex > index)
            {
                _selectedIndex--;
            }

            _status.Text = "Saved search deleted.";
            RefreshSavedRows();
        }

        private void LoadExample(int index)
        {
            if (index < 0 || index >= _examples.Length)
            {
                return;
            }

            _selectedIndex = -1;
            _nameInput.SetText(_examples[index].Name);
            _queryInput.SetText(_examples[index].Query);
            _status.Text = "Example loaded. Edit it or save it as a new search.";
        }

        private void RunQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            if (_finder == null || _finder.IsDisposed)
            {
                _finder = new ItemFinderGump(query);
                UIManager.Add(_finder);
            }
            else
            {
                _finder.SetQuery(query);
                _finder.BringOnTop();
            }

            Dispose();
        }

        private void RefreshSavedRows()
        {
            _savedBox.Clear();
            IReadOnlyList<ItemFinderManager.SavedSearch> searches = ItemFinderManager.SavedSearches;
            _savedTitle.Text = $"SAVED SEARCHES  •  {searches.Count}";

            for (int i = 0; i < searches.Count; i++)
            {
                _savedBox.Add(new SavedSearchRow(searches[i], i, _savedBox.Width));
            }

            if (searches.Count == 0)
            {
                _savedBox.Add(new Label("No saved searches yet. Name the current query and choose Save new.",
                    true, FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder),
                    _savedBox.Width - 16, font: 1)
                {
                    X = 8,
                    Y = 12
                });
            }
        }

        private void AddExample(int x, int y, int width, SearchExample example, int index)
        {
            AddSurface(x, y, width, 74, 0.34f);
            Add(new Label(example.Name, true,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.ItemFinder), width - 16, font: 1)
            {
                X = x + 8,
                Y = y + 6
            });
            Add(new Label(example.Explanation, true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder), width - 16, font: 1,
                style: FontStyle.Cropped)
            {
                X = x + 8,
                Y = y + 25
            });
            Add(new Label(example.Query, true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), width - 112, font: 1,
                style: FontStyle.Cropped)
            {
                X = x + 8,
                Y = y + 49
            });
            Add(CreateButton(x + width - 98, y + 46, 44, 22, "Load", 4000 + index));
            Add(CreateButton(x + width - 50, y + 46, 42, 22, "Run", 5000 + index));
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

        private static StbTextBox CreateInput(int x, int y, int width, int maxLength)
        {
            return new StbTextBox(1, maxLength, width, true,
                hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder))
            {
                X = x,
                Y = y,
                Width = width,
                Height = 22
            };
        }

        private static NiceButton CreateButton(int x, int y, int width, int height,
            string text, int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.ItemFinder);
        }

        private sealed class SavedSearchRow : Control
        {
            internal SavedSearchRow(ItemFinderManager.SavedSearch search, int index, int width)
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
                Add(new Label(search.Name, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder), width - 122, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 5
                });
                Add(new Label(search.Query, true,
                    FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.ItemFinder), width - 122, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                {
                    X = 8,
                    Y = 29
                });
                Add(CreateButton(width - 112, 5, 42, 22, "Run", 1000 + index));
                Add(CreateButton(width - 66, 5, 42, 22, "Edit", 2000 + index));
                NiceButton remove = CreateButton(width - 31, 31, 23, 20, "X", 3000 + index);
                remove.SetTooltip("Delete this saved search");
                Add(remove);
            }
        }

        private sealed class SearchExample
        {
            internal SearchExample(string name, string query, string explanation)
            {
                Name = name;
                Query = query;
                Explanation = explanation;
            }

            internal string Name { get; }
            internal string Query { get; }
            internal string Explanation { get; }
        }
    }
}
