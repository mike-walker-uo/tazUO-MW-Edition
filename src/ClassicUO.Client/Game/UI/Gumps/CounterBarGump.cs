#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Linq;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal class CounterBarGump : Gump
    {
        private AlphaBlendControl _background;

        public static CounterBarGump CurrentCounterBarGump { get; private set; }

        private int _rows,
            _columns,
            _rectSize;

        //private bool _isVertical;

        public CounterBarGump() : base(0, 0)
        {
            CurrentCounterBarGump = this;
        }

        public CounterBarGump(
            int x,
            int y,
            int rectSize = 30,
            int rows = 1,
            int columns = 1 /*, bool vertical = false*/
        ) : base(0, 0)
        {
            X = x;
            Y = y;

            if (rectSize < 30)
            {
                rectSize = 30;
            }
            else if (rectSize > 80)
            {
                rectSize = 80;
            }

            if (rows < 1)
            {
                rows = 1;
            }

            if (columns < 1)
            {
                columns = 1;
            }

            _rows = rows;
            _columns = columns;
            _rectSize = rectSize;
            //_isVertical = vertical;

            BuildGump();

            CurrentCounterBarGump = this;

            IsLocked = ProfileManager.CurrentProfile.CounterGumpLocked;
            CanCloseWithRightClick = false;
        }

        public override GumpType GumpType => GumpType.CounterBar;

        // Shared inventory scan: walks the player's worn-container tree once per ~100ms
        // and aggregates totals for every (graphic, hue) referenced by current CounterItems.
        // Without this, each CounterItem walked the tree independently — O(N*M).
        private static long _scanTime;
        private static readonly Dictionary<long, CounterScanEntry> _scan = new Dictionary<long, CounterScanEntry>();

        internal struct CounterScanEntry
        {
            public int Amount;
            public Item TooltipItem;
        }

        private static long ScanKey(ushort graphic, ushort hue) => ((long)graphic << 16) | hue;

        internal static bool TryGetScanEntry(ushort graphic, ushort hue, out CounterScanEntry entry)
        {
            RefreshScan();
            return _scan.TryGetValue(ScanKey(graphic, hue), out entry);
        }

        private static void RefreshScan()
        {
            if (Time.Ticks < _scanTime)
                return;
            _scanTime = (long)Time.Ticks + 100;

            _scan.Clear();
            CounterBarGump cbg = CurrentCounterBarGump;
            if (cbg == null || cbg.IsDisposed)
                return;

            // Seed dict with keys-of-interest.
            foreach (Control c in cbg.Children)
            {
                if (c is CounterItem ci && ci.Graphic != 0 && ci.SpellID == default && ci.Action == null)
                {
                    long k = ScanKey(ci.Graphic, ci.Hue);
                    if (!_scan.ContainsKey(k))
                        _scan[k] = default;
                }
            }
            if (_scan.Count == 0)
                return;

            long[] keys = _scan.Keys.ToArray();
            for (Item bag = (Item)World.Player.Items; bag != null; bag = (Item)bag.Next)
            {
                if (!bag.ItemData.IsContainer || bag.IsEmpty || bag.Layer < Layer.OneHanded || bag.Layer > Layer.Legs) continue;
                var snapshot = InventoryCounts.Get(bag);
                foreach (long key in keys)
                {
                    ushort graphic = (ushort)(key >> 16), hue = (ushort)key;
                    var entry = _scan[key];
                    entry.Amount += snapshot.Count(graphic, hue, rawAmounts: true);
                    entry.TooltipItem = snapshot.Sample(graphic, hue) ?? entry.TooltipItem;
                    _scan[key] = entry;
                }
            }
        }

        private void BuildGump()
        {
            CanMove = true;
            AcceptMouseInput = true;
            AcceptKeyboardInput = false;
            WantUpdateSize = false;
            Width = _rectSize * _columns + 1;
            Height = _rectSize * _rows + 1;

            Add(_background = new AlphaBlendControl(0.7f) { Width = Width, Height = Height, ArtPanel = true });
            ApplyTheme();

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    Add(
                        new CounterItem(
                            col * _rectSize + 2,
                            row * _rectSize + 2,
                            _rectSize - 4,
                            _rectSize - 4
                        )
                    );
                }
            }
        }

        internal void ApplyTheme()
        {
            CustomGumpThemeManager.ApplyDataSurface(_background, 0.75f);
        }

        public void SetLayout(int size, int rows, int columns)
        {
            bool ok = false;

            //if (_isVertical != isvertical)
            //{
            //    _isVertical = isvertical;
            //    int temp = _rows;
            //    _rows = _columns;
            //    _columns = temp;
            //    ok = true;
            //}

            if (rows > 30)
            {
                rows = 30;
            }

            if (columns > 30)
            {
                columns = 30;
            }

            if (size < 30)
            {
                size = 30;
            }
            else if (size > 80)
            {
                size = 80;
            }

            if (_rectSize != size)
            {
                ok = true;
                _rectSize = size;
            }

            if (rows < 1)
            {
                rows = 1;
            }

            if (_rows != rows)
            {
                ok = true;
                _rows = rows;
            }

            if (columns < 1)
            {
                columns = 1;
            }

            if (_columns != columns)
            {
                ok = true;
                _columns = columns;
            }

            if (ok)
            {
                ApplyLayout();
            }
        }

        private void ApplyLayout()
        {
            Width = _rectSize * _columns + 1;
            Height = _rectSize * _rows + 1;

            _background.Width = Width;
            _background.Height = Height;

            CounterItem[] items = GetControls<CounterItem>();

            int[] indices = new int[items.Length];

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    int index = /*_isVertical ? col * _rows + row :*/
                        row * _columns + col;

                    if (index < items.Length)
                    {
                        CounterItem c = items[index];

                        c.X = col * _rectSize + 2;
                        c.Y = row * _rectSize + 2;
                        c.Width = _rectSize - 4;
                        c.Height = _rectSize - 4;

                        c.SetGraphic(c.Graphic, c.Hue, c.IsGumpIcon);

                        indices[index] = -1;
                    }
                    else
                    {
                        Add(
                            new CounterItem(
                                col * _rectSize + 2,
                                row * _rectSize + 2,
                                _rectSize - 4,
                                _rectSize - 4
                            )
                        );
                    }
                }
            }

            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];

                if (index >= 0 && index < items.Length)
                {
                    items[i].Parent = null;

                    items[i].Dispose();
                }
            }

            SetInScreen();
        }

        public CounterItem GetCounterItem(int index)
        {
            CounterItem[] items = GetControls<CounterItem>();

            if (items == null)
            {
                return null;
            }

            if (index >= 0 && items.Length > index)
            {
                return items[index];
            }

            return null;
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            base.OnMouseUp(x, y, button);

            if (button == MouseButtonType.Left)
            {
                if (Keyboard.Alt && Keyboard.Ctrl)
                {
                    IsLocked = ProfileManager.CurrentProfile.CounterGumpLocked = !IsLocked;
                }
            }
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);

            writer.WriteAttributeString("rows", _rows.ToString());
            writer.WriteAttributeString("columns", _columns.ToString());
            writer.WriteAttributeString("rectsize", _rectSize.ToString());

            IEnumerable<CounterItem> controls = FindControls<CounterItem>();

            writer.WriteStartElement("controls");

            foreach (CounterItem control in controls)
            {
                writer.WriteStartElement("control");
                writer.WriteAttributeString("graphic", control.Graphic.ToString());
                writer.WriteAttributeString("hue", control.Hue.ToString());
                if (control.SpellID != default)
                    writer.WriteAttributeString("spellid", control.SpellID.ToString());
                if (control.Action != null) writer.WriteAttributeString("action", control.Action.Serialize());
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);

            _rows = int.Parse(xml.GetAttribute("rows"));
            _columns = int.Parse(xml.GetAttribute("columns"));
            _rectSize = int.Parse(xml.GetAttribute("rectsize"));

            BuildGump();

            XmlElement controlsXml = xml["controls"];

            if (controlsXml != null)
            {
                CounterItem[] items = GetControls<CounterItem>();
                int index = 0;

                foreach (XmlElement controlXml in controlsXml.GetElementsByTagName("control"))
                {
                    if (index < items.Length)
                    {
                        bool isGump = false;
                        if (ClientAction.TryParse(controlXml.GetAttribute("action"), out ClientAction action))
                        {
                            items[index].SetAction(action);
                            index++;
                            continue;
                        }
                        if (controlXml.HasAttribute("spellid"))
                        {
                            items[index].SpellID = int.Parse(controlXml.GetAttribute("spellid"));
                            isGump = true;
                        }

                        items[index]?.SetGraphic(
                            ushort.Parse(controlXml.GetAttribute("graphic")),
                            ushort.Parse(controlXml.GetAttribute("hue")),
                            isGump
                        );
                        index++;
                    }
                    else
                    {
                        Log.Error(ResGumps.IndexOutOfbounds);
                    }
                }
            }

            IsEnabled = IsVisible = ProfileManager.CurrentProfile.CounterBarEnabled;
            IsLocked = ProfileManager.CurrentProfile.CounterGumpLocked;
        }

        protected override void OnLockedChanged()
        {
            base.OnLockedChanged();
            CanCloseWithRightClick = false;
        }

        public override void Dispose()
        {
            if (CurrentCounterBarGump == this)
            {
                CurrentCounterBarGump = null;
            }
            base.Dispose();
        }

        public class CounterItem : Control
        {
            private int _amount;
            private int _lastDisplayedAmount = -1;

            private readonly ImageWithText _image;
            private uint _time;
            private const uint HIGHLIGHT_DURATION = 1000;
            private uint _endHighlight;
            private bool _highlight;
            private readonly Label _hotkeyLabel;
            private string _lastHotkey;
            private ClientAction _cellAction;
            internal ClientAction Action { get; private set; }
            internal bool IsGumpIcon { get; private set; }

            public CounterItem(int x, int y, int w, int h)
            {
                AcceptMouseInput = true;
                WantUpdateSize = false;
                CanMove = true;
                CanCloseWithRightClick = false;

                X = x;
                Y = y;
                Width = w;
                Height = h;

                _image = new ImageWithText();
                Add(_image);
                Add(_hotkeyLabel = new Label("", true, 0x35, 0, 1, FontStyle.BlackBorder)
                { X = 2, Y = 0, AcceptMouseInput = false });

                ContextMenu = new ContextMenuControl();
                ContextMenu.Add(ResGumps.UseObject, Use);
                ContextMenu.Add(ResGumps.Remove, RemoveItem);
                ContextMenu.Add("Set spell", GenSpellList());
                ContextMenu.Add("Assign shortcut", () => ActionShortcutGump.Open(CellAction()));
                ContextMenu.Add("Show shortcut labels", () =>
                    ProfileManager.CurrentProfile.CounterBarShowHotkeys = !ProfileManager.CurrentProfile.CounterBarShowHotkeys);
            }

            public ushort Graphic { get; private set; }

            public ushort Hue { get; private set; }

            private int _spellId;
            public int SpellID
            {
                get => _spellId;
                set { _spellId = value; if (value != 0) Action = null; SetTooltip((string)null); }
            }

            private ClientAction CellAction()
            {
                if (_cellAction != null) return _cellAction;
                int index = Array.IndexOf((Parent as CounterBarGump)?.GetControls<CounterItem>() ?? Array.Empty<CounterItem>(), this);
                return index < 0 ? null : _cellAction = new ClientAction(ClientActionKind.CounterCell, index, "Counter cell " + (index + 1));
            }

            internal void SetAction(ClientAction action)
            {
                SpellID = 0;
                Action = action;
                ushort icon = 0x082C;
                if (action.Kind == ClientActionKind.PrimaryAbility || action.Kind == ClientActionKind.SecondaryAbility)
                {
                    int ability = ((byte)World.Player.Abilities[action.Kind == ClientActionKind.PrimaryAbility ? 0 : 1] & 0x7F) - 1;
                    if (ability >= 0 && ability < AbilityData.Abilities.Length) icon = AbilityData.Abilities[ability].Icon;
                }
                else if (action.Kind == ClientActionKind.Macro)
                    icon = MacroManager.TryGetMacroManager()?.FindMacro(action.Name)?.Graphic ?? icon;
                SetGraphic(icon, 0, true);
                SetTooltip(action.Name);
            }

            protected override void OnMouseDown(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Right && !Keyboard.Alt)
                {
                    ContextMenu = new ContextMenuControl();
                    ContextMenu.Add(ResGumps.UseObject, Use);
                    ContextMenu.Add(ResGumps.Remove, RemoveItem);
                    ContextMenu.Add("Set spell", GenSpellList());
                    var skills = new List<ContextMenuItemEntry>();
                    foreach (Skill skill in World.Player.Skills.Where(skill => skill?.IsClickable == true))
                        skills.Add(new ContextMenuItemEntry(skill.Name, () => SetAction(new ClientAction(ClientActionKind.Skill, skill.Index, skill.Name))));
                    ContextMenu.Add("Set skill", skills);
                    ContextMenu.Add("Primary ability", () => SetAction(new ClientAction(ClientActionKind.PrimaryAbility, 0, "Primary weapon ability")));
                    ContextMenu.Add("Secondary ability", () => SetAction(new ClientAction(ClientActionKind.SecondaryAbility, 0, "Secondary weapon ability")));
                    var macros = new List<ContextMenuItemEntry>();
                    foreach (Macro macro in MacroManager.TryGetMacroManager().GetAllMacros().Where(m => !(m.Items is MacroObjectString first && first.Code == MacroType.ContextAction)))
                        macros.Add(new ContextMenuItemEntry(macro.Name, () => SetAction(new ClientAction(ClientActionKind.Macro, 0, macro.Name))));
                    ContextMenu.Add("Set macro", macros);
                    var loadouts = new List<ContextMenuItemEntry>();
                    foreach (string name in QuickLoadoutManager.Names)
                        loadouts.Add(new ContextMenuItemEntry(name, () => SetAction(new ClientAction(ClientActionKind.Loadout, 0, name))));
                    ContextMenu.Add("Set loadout", loadouts);
                    ContextMenu.Add("Assign shortcut", () => ActionShortcutGump.Open(CellAction()));
                    ContextMenu.Add("Show shortcut labels", () => ProfileManager.CurrentProfile.CounterBarShowHotkeys = !ProfileManager.CurrentProfile.CounterBarShowHotkeys);
                }
                base.OnMouseDown(x, y, button);
            }

            public void SetGraphic(ushort graphic, ushort hue, bool isGumpIcon = false)
            {
                if (Graphic != graphic || Hue != hue || IsGumpIcon != isGumpIcon) _lastDisplayedAmount = -1;
                IsGumpIcon = isGumpIcon;
                _image.ChangeGraphic(graphic, hue, isGumpIcon);
                if (Action != null) _image.SetAmount(Action.Name.Substring(0, Math.Min(3, Action.Name.Length)));

                if (graphic == 0)
                {
                    return;
                }

                Graphic = graphic;
                Hue = hue;
            }

            public void RemoveItem()
            {
                _image?.ChangeGraphic(0, 0);
                _amount = 0;
                _lastDisplayedAmount = -1;
                Graphic = 0;
                SpellID = default;
                Action = null;
                SetTooltip((string)null);
                _image.SetAmount(string.Empty);
            }

            public void Use()
            {
                if (!World.InGame || World.Player == null) return;
                if (Graphic != 0 && ProfileManager.CurrentProfile.CounterBarHighlightOnUse)
                { _highlight = true; _endHighlight = Time.Ticks + HIGHLIGHT_DURATION; }
                if (Action != null) { Action.Execute(); return; }
                if (Graphic == 0)
                {
                    return;
                }

                if (SpellID != default)
                {
                    GameActions.CastSpell(SpellID);
                    return;
                }

                Item backpack = World.Player.FindItemByLayer(Layer.Backpack);

                if (backpack == null)
                {
                    return;
                }

                Item item = backpack.FindItem(Graphic, Hue);

                if (item != null)
                {
                    GameActions.DoubleClick(item);
                }
            }

            public List<ContextMenuItemEntry> GenSpellList()
            {
                List<ContextMenuItemEntry> list = new List<ContextMenuItemEntry>();

                ContextMenuItemEntry entry = new ContextMenuItemEntry("Magery");
                foreach (var spell in SpellsMagery.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Necromancy");
                foreach (var spell in SpellsNecromancy.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Chivalry");
                foreach (var spell in SpellsChivalry.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Bushido");
                foreach (var spell in SpellsBushido.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Ninjitsu");
                foreach (var spell in SpellsNinjitsu.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Spellweaving");
                foreach (var spell in SpellsSpellweaving.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Mysticism");
                foreach (var spell in SpellsMysticism.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);


                entry = new ContextMenuItemEntry("Mastery");
                foreach (var spell in SpellsMastery.GetAllSpells.Values)
                    entry.Add(new ContextMenuItemEntry(spell.Name, () =>
                    {
                        SetGraphic((ushort)(spell.GumpIconSmallID), 0, true);
                        SpellID = spell.ID;
                    }));
                list.Add(entry);
                return list;
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left)
                {
                    if (Keyboard.Alt && Keyboard.Ctrl)
                        if(Parent is Gump pg)
                        {
                            Log.Trace(pg.GetType().ToString());
                            pg.IsLocked = ProfileManager.CurrentProfile.CounterGumpLocked = !pg.IsLocked;
                        }
                    if (Client.Game.GameCursor.ItemHold.Enabled)
                    {
                        SpellID = 0; Action = null;
                        SetGraphic(
                            Client.Game.GameCursor.ItemHold.Graphic,
                            Client.Game.GameCursor.ItemHold.Hue
                        );

                        GameActions.DropItem(
                            Client.Game.GameCursor.ItemHold.Serial,
                            Client.Game.GameCursor.ItemHold.X,
                            Client.Game.GameCursor.ItemHold.Y,
                            0,
                            Client.Game.GameCursor.ItemHold.Container
                        );
                    }
                    else if (ProfileManager.CurrentProfile.CastSpellsByOneClick)
                    {
                        Use();
                        return;
                    }
                }
                else if (button == MouseButtonType.Right && Keyboard.Alt && Graphic != 0)
                {
                    RemoveItem();

                    return;
                }

                base.OnMouseUp(x, y, button);
            }

            protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
            {
                if (
                    button == MouseButtonType.Left
                    && !ProfileManager.CurrentProfile.CastSpellsByOneClick
                )
                {
                    Use();
                }

                return true;
            }

            public override void Update()
            {
                base.Update();

                if (Parent != null && Parent.IsEnabled && _time < Time.Ticks)
                {
                    _time = Time.Ticks + 100;
                    ClientAction cell = ProfileManager.CurrentProfile.CounterBarShowHotkeys ? CellAction() : null;
                    Macro shortcut = cell == null ? null : MacroManager.TryGetMacroManager()?.FindMacro(cell.ShortcutName);
                    string key = ActionShortcutGump.BindingLabel(shortcut);
                    if (_lastHotkey != key) { _hotkeyLabel.Text = _lastHotkey = key; }
                    if (Action != null)
                    {
                        if (Action.Kind == ClientActionKind.PrimaryAbility || Action.Kind == ClientActionKind.SecondaryAbility)
                        {
                            int slot = Action.Kind == ClientActionKind.PrimaryAbility ? 0 : 1;
                            int ability = ((byte)World.Player.Abilities[slot] & 0x7F) - 1;
                            if (ability >= 0 && ability < AbilityData.Abilities.Length && Graphic != AbilityData.Abilities[ability].Icon)
                                SetGraphic(AbilityData.Abilities[ability].Icon, 0, true);
                            _image.SetAmount(((byte)World.Player.Abilities[slot] & 0x80) != 0 ? "ON" : "");
                        }
                        return;
                    }
                    if (SpellID != default)
                    {
                        if (Tooltip == null)
                            SetTooltip(SpellDefinition.FullIndexGetSpell(SpellID).Name);
                        return;
                    }

                    if (Graphic == 0)
                    {
                        _image.SetAmount(string.Empty);
                    }
                    else
                    {
                        // Shared inventory scan — see CounterBarGump.RefreshScan.
                        if (CounterBarGump.TryGetScanEntry(Graphic, Hue, out var entry))
                        {
                            _amount = entry.Amount;
                            if (entry.TooltipItem != null)
                                SetTooltip(entry.TooltipItem);
                        }
                        else
                        {
                            _amount = 0;
                        }

                        if (ProfileManager.CurrentProfile.CounterBarHighlightOnUse && _lastDisplayedAmount > _amount)
                        {
                            _highlight = true;
                            _endHighlight = Time.Ticks + HIGHLIGHT_DURATION;
                        }

                        if (_amount != _lastDisplayedAmount)
                        {
                            if (ProfileManager.CurrentProfile.CounterBarDisplayAbbreviatedAmount &&
                                _amount >= ProfileManager.CurrentProfile.CounterBarAbbreviatedAmount)
                            {
                                _image.SetAmount(StringHelper.IntToAbbreviatedString(_amount));
                            }
                            else
                            {
                                _image.SetAmount(_amount.ToString());
                            }
                            _lastDisplayedAmount = _amount;
                        }
                    }
                }
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                base.Draw(batcher, x, y);

                Texture2D color = SolidColorTextureCache.GetTexture(
                    MouseIsOver
                        ? Color.Yellow
                        : ProfileManager.CurrentProfile.CounterBarHighlightOnAmount
                        && _amount < ProfileManager.CurrentProfile.CounterBarHighlightAmount
                        && Graphic != 0 && Action == null && SpellID == 0
                            ? Color.Red
                            : CustomGumpThemeManager.CompactBorderColor
                );

                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

                if (_highlight && Time.Ticks > _endHighlight)
                {
                    _highlight = false;
                }

                if (_highlight)
                {
                    hueVector.Z = ((float)_endHighlight - (float)Time.Ticks) / (float)HIGHLIGHT_DURATION;
                    batcher.Draw(SolidColorTextureCache.GetTexture(Color.Yellow), new Rectangle(x, y, Width, Height), hueVector);
                }

                hueVector.Z = 1;

                batcher.DrawRectangle(color, x, y, Width, Height, hueVector);

                return true;
            }

            private class ImageWithText : Control
            {
                private readonly Label _label;
                private ushort _graphic;
                private ushort _hue;
                private bool _partial;
                private bool _isGumpGraphic;

                public ImageWithText()
                {
                    CanMove = true;
                    WantUpdateSize = true;
                    AcceptMouseInput = false;

                    _label = new Label("", true, 0x35, 0, 1, FontStyle.BlackBorder)
                    {
                        X = 2,
                        Y = Height - 15,
                        AcceptMouseInput = false
                    };

                    Add(_label);
                }

                public void ChangeGraphic(ushort graphic, ushort hue, bool isGumpGraphic = false)
                {
                    _isGumpGraphic = isGumpGraphic;
                    if (graphic != 0)
                    {
                        _graphic = graphic;
                        _hue = hue;
                        _partial = isGumpGraphic ? false : TileDataLoader.Instance.StaticData[graphic].IsPartialHue;
                        _label.Y = Parent.Height - 15;
                    }
                    else
                    {
                        _graphic = 0;
                    }
                    if (_isGumpGraphic)
                        _label.Text = string.Empty;
                }

                public override void Update()
                {
                    base.Update();

                    if (Parent != null)
                    {
                        Width = Parent.Width;
                        Height = Parent.Height;
                    }
                }

                public override bool Draw(UltimaBatcher2D batcher, int x, int y)
                {
                    if (_graphic != 0)
                    {
                        ref readonly var artInfo = ref Client.Game.Arts.GetArt(_graphic);
                        if (_isGumpGraphic)
                            artInfo = ref Client.Game.Gumps.GetGump(_graphic);

                        var rect = _isGumpGraphic ? artInfo.UV : Client.Game.Arts.GetRealArtBounds(_graphic);

                        Vector3 hueVector = ShaderHueTranslator.GetHueVector(_hue, _partial, 1f, _isGumpGraphic);

                        Point originalSize = new Point(Width, Height);
                        Point point = new Point();

                        if (rect.Width < Width)
                        {
                            originalSize.X = rect.Width;
                            point.X = (Width >> 1) - (originalSize.X >> 1);
                        }

                        if (rect.Height < Height)
                        {
                            originalSize.Y = rect.Height;
                            point.Y = (Height >> 1) - (originalSize.Y >> 1);
                        }

                        if (_isGumpGraphic)
                            batcher.Draw(
                                artInfo.Texture,
                                new Rectangle(x + point.X, y + point.Y, originalSize.X, originalSize.Y),
                                new Rectangle(
                                    artInfo.UV.X,
                                    artInfo.UV.Y,
                                    rect.Width,
                                    rect.Height
                                ),
                                hueVector
                            );
                        else
                            batcher.Draw(
                                artInfo.Texture,
                                new Rectangle(x + point.X, y + point.Y, originalSize.X, originalSize.Y),
                                new Rectangle(
                                    artInfo.UV.X + rect.X,
                                    artInfo.UV.Y + rect.Y,
                                    rect.Width,
                                    rect.Height
                                ),
                                hueVector
                            );
                    }

                    return base.Draw(batcher, x, y);
                }

                public void SetAmount(string amount)
                {
                    _label.Text = amount;
                }

                public string GetText()
                {
                    return _label?.Text ?? "";
                }
            }
        }
    }
}
