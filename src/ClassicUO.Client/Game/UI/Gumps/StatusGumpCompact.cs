using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class StatusGumpCompact : StatusGumpBase
    {
        private readonly Label _name, _stats, _weight;
        private readonly Label[] _values = new Label[3];
        private readonly Rectangle[] _bars = new Rectangle[3];
        private readonly bool _vertical;

        internal StatusGumpCompact()
        {
            AcceptMouseInput = true;
            CanCloseWithRightClick = true; CanCloseWithEsc = true;
            _vertical = ProfileManager.CurrentProfile.StatusLayout == 2;
            Width = _vertical ? 190 : 370;
            Height = _vertical ? 178 : 122;
            WantUpdateSize = false;
            _point = new Point(Width - 20, Height - 20);
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.9f));
            Add(_name = new Label("", true, CustomGumpThemeManager.TitleHue, Width - 34, font: 1) { X = 12, Y = 8, AcceptMouseInput = false });
            string[] names = { "HP", "Mana", "Stam" };
            for (int i = 0; i < 3; i++)
            {
                int x = _vertical ? 12 : 12 + i * 116;
                int y = _vertical ? 35 + i * 30 : 36;
                _bars[i] = new Rectangle(x, y + 18, _vertical ? 166 : 106, 5);
                Add(_values[i] = new Label("", true, CustomGumpThemeManager.TextHue, _bars[i].Width, font: 1)
                { X = x, Y = y, AcceptMouseInput = false });
                _values[i].SetTooltip(names[i] + " current / maximum");
            }
            int statY = _vertical ? 127 : 69;
            Add(_stats = new Label("", true, CustomGumpThemeManager.TextHue, Width - 24, font: 1) { X = 12, Y = statY, AcceptMouseInput = false });
            Add(_weight = new Label("", true, CustomGumpThemeManager.TextHue, Width - 24, font: 1) { X = 12, Y = statY + 21, AcceptMouseInput = true });
            var minimize = new Label("-", true, CustomGumpThemeManager.TitleHue, font: 1) { X = _point.X, Y = _point.Y };
            minimize.SetTooltip("Minimize to health bar");
            Add(minimize);
            var close = new NiceButton(Width - 24, 6, 18, 20, ButtonAction.Default, "X") { IsSelectable = false, ButtonParameter = 99 };
            Add(close);
            SetTooltip("Alt+click: status layouts. Right-click: close.");
            ContextMenu = new ContextMenuControl();
            ContextMenu.Add("Horizontal status", () => ChangeLayout(1));
            ContextMenu.Add("Vertical status", () => ChangeLayout(2));
            ContextMenu.Add("Full status", () => ChangeLayout(0));
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left && Keyboard.Alt && !TargetManager.IsTargeting)
            {
                ContextMenu.Show();
                return;
            }
            base.OnMouseUp(x, y, button);
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99) Dispose();
            else base.OnButtonClick(buttonID);
        }

        internal static void ChangeLayout(int layout)
        {
            StatusGumpBase old = GetStatusGump();
            Point position = old?.Location ?? ProfileManager.CurrentProfile.StatusGumpPosition;
            ProfileManager.CurrentProfile.StatusLayout = layout;
            ProfileManager.CurrentProfile.Save(ProfileManager.ProfilePath, false);
            if (old != null)
            {
                old.Dispose();
                UIManager.Add(AddStatusGump(position.X, position.Y));
            }
        }

        internal static int FillWidth(int width, int current, int maximum) =>
            maximum <= 0 ? 0 : (int)(width * Math.Max(0, Math.Min(1.0, (double)current / maximum)));

        public override void Update()
        {
            if (!World.InGame || World.Player == null) { Dispose(); return; }
            base.Update();
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 100;
            var player = World.Player;
            _name.Text = player.Name ?? string.Empty;
            _values[0].Text = $"HP {player.Hits}/{player.HitsMax}";
            _values[1].Text = $"Mana {player.Mana}/{player.ManaMax}";
            _values[2].Text = $"Stam {player.Stamina}/{player.StaminaMax}";
            _stats.Text = $"Str {player.Strength}  Dex {player.Dexterity}  Int {player.Intelligence}";
            _weight.Text = _vertical ? $"Weight {player.Weight}/{player.WeightMax}" :
                $"Weight {player.Weight}/{player.WeightMax}   Gold {player.Gold}   Pets {player.Followers}/{player.FollowersMax}";
            _weight.SetTooltip($"Gold {player.Gold}\nFollowers {player.Followers}/{player.FollowersMax}");
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (!base.Draw(batcher, x, y) || World.Player == null) return false;
            DrawBar(batcher, x, y, 0, World.Player.Hits, World.Player.HitsMax, Color.IndianRed);
            DrawBar(batcher, x, y, 1, World.Player.Mana, World.Player.ManaMax, Color.RoyalBlue);
            DrawBar(batcher, x, y, 2, World.Player.Stamina, World.Player.StaminaMax, Color.Goldenrod);
            return true;
        }

        private void DrawBar(UltimaBatcher2D batcher, int x, int y, int index, int value, int maximum, Color color)
        {
            Rectangle rect = _bars[index]; rect.Offset(x, y);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0);
            batcher.Draw(SolidColorTextureCache.GetTexture(Color.DarkSlateGray), rect, hue);
            rect.Width = FillWidth(rect.Width, value, maximum);
            if (rect.Width > 0) batcher.Draw(SolidColorTextureCache.GetTexture(color), rect, hue);
        }
    }
}
