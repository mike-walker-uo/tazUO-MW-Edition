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

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ClassicUO.Game.UI.Gumps
{
    internal class NameOverheadGump : Gump
    {
        // Centralised hue resolver: guild-substring override (TazUO) wins over notoriety default.
        private static ushort ResolveNameHue(Entity entity)
        {
            ushort defaultHue = entity is Mobile m
                ? Notoriety.GetHue(m.NotorietyFlag)
                : (ushort)0x0481;
            if (entity != null && Managers.GuildHueMap.TryResolveHue(entity.Serial, out ushort gh))
                return gh;
            return defaultHue;
        }

        private static bool IsProtectedItem(Item item)
        {
            if (item == null || !item.OnGround)
                return false;

            if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
            {
                if (!string.IsNullOrEmpty(name)
                    && (name.IndexOf("[locked down]", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("[secured]", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("[secure]", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;

                if (!string.IsNullOrEmpty(data))
                {
                    bool notSecured = data.IndexOf("unsecure", StringComparison.OrdinalIgnoreCase) >= 0
                        || data.IndexOf("not secure", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool notLockedDown = data.IndexOf("not locked down", StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((!notLockedDown && data.IndexOf("locked down", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!notSecured && data.IndexOf("secure", StringComparison.OrdinalIgnoreCase) >= 0))
                        return true;

                    if (notSecured || notLockedDown)
                        return false;
                }
            }

            // The movable/weight heuristic cannot confirm a house lockdown.
            // Keep unknown items in the "unlocked" search rather than hiding a decay risk.
            return false;
        }

        private bool MatchesSearch(Entity entity)
        {
            string search = NameOverHeadManager.Search?.Trim();
            if (string.IsNullOrEmpty(search))
                return true;

            if (search.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
                return entity is Item floorItem && floorItem.OnGround && !IsProtectedItem(floorItem);

            if (!string.IsNullOrEmpty(entity.Name)
                && entity.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(_text?.Text)
                && _text.Text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return World.OPL.TryGetNameAndData(entity.Serial, out string name, out string data)
                && ((!string.IsNullOrEmpty(name)
                        && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(data)
                        && data.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private AlphaBlendControl _background;
        private Point _lockedPosition,
            _lastLeftMousePositionDown;
        private bool _positionLocked,
            _leftMouseIsDown,
            _isLastTarget,
            _needsNameUpdate;
        private TextBox _text;
        private Texture2D _borderColor = SolidColorTextureCache.GetTexture(Color.Black);
        private CustomGumpTheme _appliedTheme;
        private byte _appliedOpacity;
        private bool _themeApplied;
        private Vector2 _textDrawOffset = Vector2.Zero;
        private static int currentHeight = 22;
        private static readonly int COLLISION_SPACING = 8;

        public static int CurrentHeight
        {
            get
            {
                if (NameOverHeadManager.IsShowing)
                {
                    return currentHeight;
                }

                return 0;
            }
            private set
            {
                currentHeight = value;
            }
        }

        public new UILayer LayerOrder {
            get
            {
                if (IsFocused)
                    return UILayer.Default;

                return UILayer.Under;
            }
            set { }
        }

        public NameOverheadGump(uint serial) : base(serial, 0)
        {
            CanMove = false;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;

            Entity entity = World.Get(serial);

            if (entity == null)
            {
                Dispose();
                return;
            }

            if (entity is Item item && item.OnGround)
                World.OPL.Contains(item.Serial);

            _text = TextBox.GetOne(string.Empty, ProfileManager.CurrentProfile.NamePlateFont, ProfileManager.CurrentProfile.NamePlateFontSize, ResolveNameHue(entity), TextBox.RTLOptions.DefaultCenterStroked());

            SetTooltip(entity);

            BuildGump();
            SetName();
        }

        public bool SetName()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity == null)
            {
                return false;
            }

            _text ??= TextBox.GetOne(string.Empty, ProfileManager.CurrentProfile.NamePlateFont, ProfileManager.CurrentProfile.NamePlateFontSize, ResolveNameHue(entity), TextBox.RTLOptions.DefaultCenterStroked());

            if (entity is Item item)
            {
                if (!World.OPL.TryGetNameAndData(item, out string t, out _))
                {
                    t = string.Empty;
                    _needsNameUpdate = true;
                    if (!item.IsCorpse && item.Amount > 1)
                    {
                        t = item.Amount.ToString() + ' ';
                    }

                    if (string.IsNullOrEmpty(item.ItemData.Name))
                    {
                        t += ClilocLoader.Instance.GetString(1020000 + item.Graphic, true, t);
                    }
                    else
                    {
                        t += StringHelper.CapitalizeAllWords(
                            StringHelper.GetPluralAdjustedString(
                                item.ItemData.Name,
                                item.Amount > 1
                            )
                        );
                    }
                }
                else
                {
                    _needsNameUpdate = false;
                }

                if (string.IsNullOrEmpty(t))
                {
                    return false;
                }

                _text.Text = t;

                Width = _background.Width = Math.Max(60, _text.Width) + 4;
                Height = _background.Height = CurrentHeight = Math.Max(Constants.OBJECT_HANDLES_GUMP_HEIGHT, _text.Height) + 4;
                _textDrawOffset.X = (Width - _text.Width - 4) >> 1;
                _textDrawOffset.Y = (Height - _text.Height) >> 1;
                WantUpdateSize = false;

                return true;
            }

            if (!string.IsNullOrEmpty(entity.Name))
            {
                _text.Text = entity.Name;

                Width = _background.Width = Math.Max(60, _text.Width) + 4;
                Height = _background.Height = Math.Max(Constants.OBJECT_HANDLES_GUMP_HEIGHT, _text.Height) + 4;
                _textDrawOffset.X = (Width - _text.Width - 4) >> 1;
                _textDrawOffset.Y = (Height - _text.Height) >> 1;
                WantUpdateSize = false;

                return true;
            }

            return false;
        }

        private void BuildGump()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity == null)
            {
                Dispose();

                return;
            }

            Add
            (
                _background = new AlphaBlendControl(ProfileManager.CurrentProfile.NamePlateOpacity / 100f)
                {
                    WantUpdateSize = false,
                    Hue = ResolveNameHue(entity)
                }
            );
        }

        private void UpdateThemeSurface()
        {
            CustomGumpTheme theme = CustomGumpThemeManager.Current;
            byte opacity = ProfileManager.CurrentProfile.NamePlateOpacity;
            if (_themeApplied && _appliedTheme == theme && _appliedOpacity == opacity)
                return;

            CustomGumpThemeManager.ApplyDataSurface(_background, opacity / 100f, false);
            _appliedTheme = theme;
            _appliedOpacity = opacity;
            _themeApplied = true;
        }

        protected override void CloseWithRightClick()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity != null)
            {
                entity.ObjectHandlesStatus = ObjectHandlesStatus.CLOSED;
            }

            base.CloseWithRightClick();
        }

        private void DoDrag()
        {
            var delta = Mouse.Position - _lastLeftMousePositionDown;

            if (
                Math.Abs(delta.X) <= Constants.MIN_GUMP_DRAG_DISTANCE
                && Math.Abs(delta.Y) <= Constants.MIN_GUMP_DRAG_DISTANCE
            )
            {
                return;
            }

            _leftMouseIsDown = false;
            _positionLocked = false;

            Entity entity = World.Get(LocalSerial);

            if (entity is Mobile || entity is Item it && it.IsDamageable)
            {
                if (UIManager.IsDragging)
                {
                    return;
                }

                BaseHealthBarGump gump = UIManager.GetGump<BaseHealthBarGump>(LocalSerial);
                gump?.Dispose();

                if (ProfileManager.CurrentProfile.CustomBarsToggled)
                {
                    Rectangle rect = new Rectangle(
                        0,
                        0,
                        HealthBarGumpCustom.HPB_WIDTH,
                        HealthBarGumpCustom.HPB_HEIGHT_SINGLELINE
                    );

                    UIManager.Add(
                        gump = new HealthBarGumpCustom(entity)
                        {
                            X = Mouse.Position.X - (rect.Width >> 1),
                            Y = Mouse.Position.Y - (rect.Height >> 1)
                        }
                    );
                }
                else
                {
                    ref readonly var gumpInfo = ref Client.Game.Gumps.GetGump(0x0804);

                    UIManager.Add(
                        gump = new HealthBarGump(entity)
                        {
                            X = Mouse.LClickPosition.X - (gumpInfo.UV.Width >> 1),
                            Y = Mouse.LClickPosition.Y - (gumpInfo.UV.Height >> 1)
                        }
                    );
                }

                UIManager.AttemptDragControl(gump, true);
            }
            else if (entity != null)
            {
                GameActions.PickUp(LocalSerial, 0, 0);

                //if (entity.Texture != null)
                //    GameActions.PickUp(LocalSerial, entity.Texture.Width >> 1, entity.Texture.Height >> 1);
                //else
                //    GameActions.PickUp(LocalSerial, 0, 0);
            }
        }

        protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                if (SerialHelper.IsMobile(LocalSerial))
                {
                    if (World.Player.InWarMode)
                    {
                        GameActions.Attack(LocalSerial);
                    }
                    else
                    {
                        GameActions.DoubleClick(LocalSerial);
                    }
                }
                else
                {
                    if (!GameActions.OpenCorpse(LocalSerial))
                    {
                        GameActions.DoubleClick(LocalSerial);
                    }
                }

                return true;
            }

            return false;
        }

        protected override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                _lastLeftMousePositionDown = Mouse.Position;
                _leftMouseIsDown = true;
            }

            base.OnMouseDown(x, y, button);
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                _leftMouseIsDown = false;

                if (!Client.Game.GameCursor.ItemHold.Enabled)
                {
                    if (
                        UIManager.IsDragging
                        || Math.Max(Math.Abs(Mouse.LDragOffset.X), Math.Abs(Mouse.LDragOffset.Y))
                            >= 1
                    )
                    {
                        _positionLocked = false;

                        return;
                    }
                }

                if (TargetManager.IsTargeting)
                {
                    switch (TargetManager.TargetingState)
                    {
                        case CursorTarget.Internal:
                        case CursorTarget.Position:
                        case CursorTarget.Object:
                        case CursorTarget.Grab:
                        case CursorTarget.SetGrabBag:
                            TargetManager.Target(LocalSerial);
                            Mouse.LastLeftButtonClickTime = 0;

                            break;

                        case CursorTarget.SetTargetClientSide:
                            TargetManager.Target(LocalSerial);
                            Mouse.LastLeftButtonClickTime = 0;
                            UIManager.Add(new InspectorGump(World.Get(LocalSerial)));

                            break;

                        case CursorTarget.HueCommandTarget:
                            CommandManager.OnHueTarget(World.Get(LocalSerial));

                            break;
                    }
                }
                else
                {
                    if (
                        Client.Game.GameCursor.ItemHold.Enabled
                        && !Client.Game.GameCursor.ItemHold.IsFixedPosition
                    )
                    {
                        uint drop_container = 0xFFFF_FFFF;
                        bool can_drop = false;
                        ushort dropX = 0;
                        ushort dropY = 0;
                        sbyte dropZ = 0;

                        Entity obj = World.Get(LocalSerial);

                        if (obj != null)
                        {
                            can_drop = obj.Distance <= Constants.DRAG_ITEMS_DISTANCE;

                            if (can_drop)
                            {
                                if (obj is Item it && it.ItemData.IsContainer || obj is Mobile)
                                {
                                    dropX = 0xFFFF;
                                    dropY = 0xFFFF;
                                    dropZ = 0;
                                    drop_container = obj.Serial;
                                }
                                else if (
                                    obj is Item it2
                                    && (
                                        it2.ItemData.IsSurface
                                        || it2.ItemData.IsStackable
                                            && it2.DisplayedGraphic
                                                == Client.Game.GameCursor.ItemHold.DisplayedGraphic
                                    )
                                )
                                {
                                    dropX = obj.X;
                                    dropY = obj.Y;
                                    dropZ = obj.Z;

                                    if (it2.ItemData.IsSurface)
                                    {
                                        dropZ += (sbyte)(
                                            it2.ItemData.Height == 0xFF ? 0 : it2.ItemData.Height
                                        );
                                    }
                                    else
                                    {
                                        drop_container = obj.Serial;
                                    }
                                }
                            }
                            else
                            {
                                Client.Game.Audio.PlaySound(0x0051);
                            }

                            if (can_drop)
                            {
                                if (drop_container == 0xFFFF_FFFF && dropX == 0 && dropY == 0)
                                {
                                    can_drop = false;
                                }

                                if (can_drop)
                                {
                                    GameActions.DropItem(
                                        Client.Game.GameCursor.ItemHold.Serial,
                                        dropX,
                                        dropY,
                                        dropZ,
                                        drop_container
                                    );
                                }
                            }
                        }
                    }
                    else if (!DelayedObjectClickManager.IsEnabled)
                    {
                        DelayedObjectClickManager.Set(
                            LocalSerial,
                            Mouse.Position.X,
                            Mouse.Position.Y,
                            Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK
                        );
                    }
                }
            }

            base.OnMouseUp(x, y, button);
        }

        protected override void OnMouseOver(int x, int y)
        {
            if (_leftMouseIsDown)
            {
                DoDrag();
            }

            if (!_positionLocked && SerialHelper.IsMobile(LocalSerial))
            {
                Mobile m = World.Mobiles.Get(LocalSerial);

                if (m == null)
                {
                    Dispose();

                    return;
                }

                _positionLocked = true;

                Client.Game.Animations.GetAnimationDimensions(
                    m.AnimIndex,
                    m.GetGraphicForAnimation(),
                    /*(byte) m.GetDirectionForAnimation()*/
                    0,
                    /*Mobile.GetGroupForAnimation(m, isParent:true)*/
                    0,
                    m.IsMounted,
                    /*(byte) m.AnimIndex*/
                    0,
                    out int centerX,
                    out int centerY,
                    out int width,
                    out int height
                );

                _lockedPosition.X = (int)(m.RealScreenPosition.X + m.Offset.X + 22 + 5);

                _lockedPosition.Y = (int)(
                    m.RealScreenPosition.Y
                    + (m.Offset.Y - m.Offset.Z)
                    - (height + centerY + 15)
                    + (
                        m.IsGargoyle && m.IsFlying
                            ? -22
                            : !m.IsMounted
                                ? 22
                                : 0
                    )
                );
            }

            base.OnMouseOver(x, y);
        }

        protected override void OnMouseExit(int x, int y)
        {
            _positionLocked = false;
            base.OnMouseExit(x, y);
        }

        private static readonly List<(uint Serial, Rectangle Bounds)> _placedNameplates =
            new List<(uint Serial, Rectangle Bounds)>();
        private static readonly Dictionary<(int X, int Y), List<int>> _placementCells =
            new Dictionary<(int X, int Y), List<int>>();
        private const int PLACEMENT_CELL_SHIFT = 7; // 128-pixel cells
        private static uint _placementTick;

        private static bool IntersectsPlacedNameplate(Rectangle bounds, uint serial)
        {
            for (int cellY = bounds.Top >> PLACEMENT_CELL_SHIFT; cellY <= (bounds.Bottom - 1) >> PLACEMENT_CELL_SHIFT; cellY++)
            {
                for (int cellX = bounds.Left >> PLACEMENT_CELL_SHIFT; cellX <= (bounds.Right - 1) >> PLACEMENT_CELL_SHIFT; cellX++)
                {
                    if (!_placementCells.TryGetValue((cellX, cellY), out List<int> indices))
                        continue;

                    foreach (int index in indices)
                    {
                        var placed = _placedNameplates[index];
                        if (placed.Serial != serial && bounds.Intersects(placed.Bounds))
                            return true;
                    }
                }
            }

            return false;
        }

        private static void IndexPlacement(Rectangle bounds, int index)
        {
            for (int cellY = bounds.Top >> PLACEMENT_CELL_SHIFT; cellY <= (bounds.Bottom - 1) >> PLACEMENT_CELL_SHIFT; cellY++)
            {
                for (int cellX = bounds.Left >> PLACEMENT_CELL_SHIFT; cellX <= (bounds.Right - 1) >> PLACEMENT_CELL_SHIFT; cellX++)
                {
                    if (!_placementCells.TryGetValue((cellX, cellY), out List<int> indices))
                    {
                        indices = new List<int>();
                        _placementCells.Add((cellX, cellY), indices);
                    }

                    indices.Add(index);
                }
            }
        }

        private Point AdjustPositionToAvoidOverlap(int originalX, int originalY, int layoutHeight,
            Rectangle viewport)
        {
            if (!ProfileManager.CurrentProfile.NamePlateAvoidOverlap)
                return new Point(originalX, originalY);

            if (_placementTick != Time.Ticks)
            {
                _placedNameplates.Clear();
                foreach (List<int> indices in _placementCells.Values)
                    indices.Clear();
                _placementTick = Time.Ticks;
            }

            int stepX = Math.Max(32, (Width + COLLISION_SPACING) / 2);
            int stepY = layoutHeight + COLLISION_SPACING;
            for (int ring = 0; ring <= 8; ring++)
            {
                for (int row = -ring; row <= ring; row++)
                {
                    for (int column = -ring; column <= ring; column++)
                    {
                        if (Math.Max(Math.Abs(row), Math.Abs(column)) != ring)
                            continue;

                        int x = originalX + column * stepX;
                        int y = originalY + row * stepY;
                        if (x < viewport.X || y < viewport.Y
                            || x + Width > viewport.Right || y + layoutHeight > viewport.Bottom)
                            continue;

                        Rectangle bounds = new Rectangle(x - COLLISION_SPACING / 2,
                            y - COLLISION_SPACING / 2,
                            Width + COLLISION_SPACING, layoutHeight + COLLISION_SPACING);
                        if (!IntersectsPlacedNameplate(bounds, LocalSerial))
                        {
                            RememberPlacement(bounds);
                            return new Point(x, y);
                        }
                    }
                }
            }

            RememberPlacement(new Rectangle(originalX, originalY, Width, layoutHeight));
            return new Point(originalX, originalY);
        }

        private void RememberPlacement(Rectangle bounds)
        {
            for (int i = 0; i < _placedNameplates.Count; i++)
            {
                if (_placedNameplates[i].Serial == LocalSerial)
                {
                    _placedNameplates[i] = (LocalSerial, bounds);
                    IndexPlacement(bounds, i);
                    return;
                }
            }

            _placedNameplates.Add((LocalSerial, bounds));
            IndexPlacement(bounds, _placedNameplates.Count - 1);
        }

        public override void Update()
        {
            base.Update();

            Entity entity = World.Get(LocalSerial);

            if (
                entity == null
                || entity.IsDestroyed
                || entity.ObjectHandlesStatus == ObjectHandlesStatus.NONE
                || entity.ObjectHandlesStatus == ObjectHandlesStatus.CLOSED
            )
            {
                Dispose();
            }
            else
            {
                if (entity == TargetManager.LastTargetInfo.Serial)
                {
                    if (!_isLastTarget) //Only set this if it was not already last target
                    {
                        _borderColor = SolidColorTextureCache.GetTexture(Color.Red);
                        _text.Hue = ResolveNameHue(entity);
                        _isLastTarget = true;
                    }
                }
                else
                {
                    if (_isLastTarget)//If we make it here, it is no longer the last target so we update colors and set this to false.
                    {
                        _borderColor = SolidColorTextureCache.GetTexture(Color.Black);
                        _text.Hue = ResolveNameHue(entity);
                        _isLastTarget = false;
                    }
                }

                if (_needsNameUpdate)
                {
                    SetName();
                }
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed)
            {
                return false;
            }

            bool _isMobile = false;
            double _hpPercent = 1;
            IsVisible = true;
            if (SerialHelper.IsMobile(LocalSerial))
            {
                Mobile m = World.Mobiles.Get(LocalSerial);

                if (m == null)
                {
                    Dispose();

                    return false;
                }

                if (!MatchesSearch(m))
                {
                    IsVisible = false;
                    return true;
                }

                _isMobile = true;
                _hpPercent = (double)m.Hits / (double)m.HitsMax;

                IsVisible = true;
                if (ProfileManager.CurrentProfile.NamePlateHideAtFullHealth && _hpPercent >= 1)
                {
                    if (ProfileManager.CurrentProfile.NamePlateHideAtFullHealthInWarmode)
                    {
                        if (World.Player.InWarMode)
                        {
                            IsVisible = false;
                            return false;
                        }

                    }
                    else
                    {
                        IsVisible = false;
                        return false;
                    }

                }

                if (_positionLocked)
                {
                    x = _lockedPosition.X;
                    y = _lockedPosition.Y;
                }
                else
                {
                    Client.Game.Animations.GetAnimationDimensions(
                        m.AnimIndex,
                        m.GetGraphicForAnimation(),
                        /*(byte) m.GetDirectionForAnimation()*/
                        0,
                        /*Mobile.GetGroupForAnimation(m, isParent:true)*/
                        0,
                        m.IsMounted,
                        /*(byte) m.AnimIndex*/
                        0,
                        out int centerX,
                        out int centerY,
                        out int width,
                        out int height
                    );

                    x = (int)(m.RealScreenPosition.X + m.Offset.X + 22 + 5);
                    y = (int)(
                        m.RealScreenPosition.Y
                        + (m.Offset.Y - m.Offset.Z)
                        - (height + centerY + 15)
                        + (
                            m.IsGargoyle && m.IsFlying
                                ? -22
                                : !m.IsMounted
                                    ? 22
                                    : 0
                        )
                    );
                }
            }
            else if (SerialHelper.IsItem(LocalSerial))
            {
                Item item = World.Items.Get(LocalSerial);

                if (item == null)
                {
                    Dispose();
                    return false;
                }

                if (!MatchesSearch(item))
                {
                    IsVisible = false;
                    return true;
                }

                var bounds = Client.Game.Arts.GetRealArtBounds(item.Graphic);

                x = item.RealScreenPosition.X + (int)item.Offset.X + 22 + 5;
                y =
                    item.RealScreenPosition.Y
                    + (int)(item.Offset.Y - item.Offset.Z)
                    + (bounds.Height >> 1);
            }

            Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

            Point p = Client.Game.Scene.Camera.WorldToScreen(new Point(x, y));
            x = p.X - (Width >> 1);
            y = p.Y - (Height);// >> 1);

            var camera = Client.Game.Scene.Camera;
            x += camera.Bounds.X;
            y += camera.Bounds.Y;

            if (x < camera.Bounds.X || x + Width > camera.Bounds.Right)
            {
                return false;
            }

            if (y < camera.Bounds.Y || y + Height > camera.Bounds.Bottom)
            {
                return false;
            }

            int layoutHeight = Height;
            if (ProfileManager.CurrentProfile.NamePlateHealthBar && _isMobile)
            {
                Mobile mobile = World.Mobiles.Get(LocalSerial);
                if (mobile is PlayerMobile || World.Party.Contains(mobile.Serial))
                    layoutHeight += 20;
            }

            var adjustedPos = AdjustPositionToAvoidOverlap(x, y, layoutHeight, camera.Bounds);
            x = adjustedPos.X;
            y = adjustedPos.Y;

            X = x;
            Y = y;

            UpdateThemeSurface();
            CustomGumpTheme theme = CustomGumpThemeManager.Current;
            _background.IsVisible = !(CustomGumpThemeManager.IsArtTheme(theme)
                && CustomThemeArt.DrawNameplateFill(batcher, x, y, Width, Height, theme,
                    ProfileManager.CurrentProfile.NamePlateOpacity / 100f));
            base.Draw(batcher, x, y);
            bool protectedItem = SerialHelper.IsItem(LocalSerial)
                && IsProtectedItem(World.Items.Get(LocalSerial));
            if (protectedItem)
                batcher.Draw(SolidColorTextureCache.GetTexture(Color.Black),
                    new Rectangle(x, y, Width, Height),
                    ShaderHueTranslator.GetHueVector(0, false,
                        ProfileManager.CurrentProfile.NamePlateOpacity / 100f * 0.60f));
            int plateY = y;

            if (ProfileManager.CurrentProfile.NamePlateHealthBar && _isMobile)
            {
                Mobile m = World.Mobiles.Get(LocalSerial);
                var isPlayer = m is PlayerMobile;
                var isInParty = World.Party.Contains(m.Serial);

                var _alpha = ProfileManager.CurrentProfile.NamePlateHealthBarOpacity / 100f;
                DrawResourceBar(batcher, m, x, y, Height / (isPlayer || isInParty ? 3 : 1), m =>
                {
                    var hpPercent = (double)m.Hits / (double)m.HitsMax;
                    var _baseHue = hpPercent switch
                    {
                        1 => (m is PlayerMobile || World.Party.Contains(m.Serial)) ? 0x0058 : Notoriety.GetHue(m.NotorietyFlag),
                        > .8 => 0x0058,
                        > .4 => 0x0030,
                        _ => 0x0021
                    };
                    Vector3 hueVec = ShaderHueTranslator.GetHueVector(_baseHue, false, _alpha);

                    if (m.IsPoisoned)
                    {
                        hueVec = ShaderHueTranslator.GetHueVector(63, false, _alpha);
                    }
                    else if (m.IsYellowHits || m.IsParalyzed)
                    {
                        hueVec = ShaderHueTranslator.GetHueVector(353, false, _alpha);
                    }
                    return (hueVec, hpPercent);
                }, out var nY);

                if (m is PlayerMobile || isInParty)
                {
                    DrawResourceBar(batcher, m, x, nY, Height / 3, m =>
                    {
                        var mpPercent = (double)m.Mana / (double)m.ManaMax;
                        var _baseHue = mpPercent switch
                        {
                            > .6 => 0x0058,
                            > .2 => 0x0030,
                            _ => 0x0021
                        };
                        Vector3 hueVec = ShaderHueTranslator.GetHueVector(_baseHue, false, _alpha);
                        return (hueVec, mpPercent);
                    }, out nY);

                    DrawResourceBar(batcher, m, x, nY, Height / 3, m =>
                    {
                        var spPercent = (double)m.Stamina / (double)m.StaminaMax;
                        var _baseHue = spPercent switch
                        {
                            > .8 => 0x0058,
                            > .5 => 0x0030,
                            _ => 0x0021
                        };
                        Vector3 hueVec = ShaderHueTranslator.GetHueVector(_baseHue, false, _alpha);
                        return (hueVec, spPercent);
                    }, out nY);
                    y += 20;
                }
            }

            hueVector.Z = ProfileManager.CurrentProfile.NamePlateBorderOpacity / 100f;
            Texture2D nameplateBorder = _isLastTarget
                ? _borderColor
                : SolidColorTextureCache.GetTexture(protectedItem
                    ? new Color(94, 97, 101)
                    : CustomGumpThemeManager.CompactBorderColor);
            batcher.DrawRectangle(nameplateBorder, x, plateY, Width, Height, hueVector);

            return _text.Draw(batcher, (int)(x + 2 + _textDrawOffset.X), (int)(y + 2 + _textDrawOffset.Y));
        }

        private void DrawResourceBar(UltimaBatcher2D batcher, Mobile m, int x, int y, int height, Func<Mobile, (Vector3, double)> getHueVector, out int nY)
        {
            var data = getHueVector == null ? (ShaderHueTranslator.GetHueVector(0x0058), 0) : getHueVector(m);
            batcher.DrawRectangle
            (
                _borderColor,
                x,
                y,
                Width,
                height,
                ShaderHueTranslator.GetHueVector(0)
            );
            batcher.Draw
            (
                SolidColorTextureCache.GetTexture(Color.White),
                new Vector2(x + 1, y + 1),
                new Rectangle(x, y, Math.Min((int)((Width - 1) * data.Item2), Width - 1), height),
                data.Item1
            );
            nY = y + height;
        }

        public override void Dispose()
        {
            _text?.Dispose();
            base.Dispose();
        }
    }
}
