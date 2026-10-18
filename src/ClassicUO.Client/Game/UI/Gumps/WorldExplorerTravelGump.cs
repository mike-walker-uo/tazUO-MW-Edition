using System;
using System.IO;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldExplorerTravelGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly long _expires = (long)Time.Ticks + 3000;

        private WorldExplorerTravelGump(string destination, string method) : base(0, 0)
        {
            Width = 480; Height = 160; WantUpdateSize = false;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true; AcceptMouseInput = true;
            Add(new WorldExplorerTravelCard(_profile.WorldExplorerTravelStyle, destination, method, Width));
            SetTooltip(destination + "\n" + method);
            if (_profile.WorldExplorerTravelPosition is Point position)
                Location = position;
            else
            {
                CenterXInViewPort();
                var bounds = Client.Game.Scene.Camera.Bounds;
                Y = (int)((bounds.Y + bounds.Height) / UIManager.InterfaceScale) - Height - 45;
            }
            SetInScreen();
        }

        internal static void Show(string destination, string method)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            UIManager.GetGump<WorldExplorerTravelGump>()?.Dispose();
            UIManager.Add(new WorldExplorerTravelGump(destination, method));
        }

        internal static string MethodName(string kind, byte method) => kind == "portal" ? "Portal phrase"
            : method == 1 ? "Sacred Journey" : method == 2 ? "Recall (charges)" : "Recall";

        public override bool ShouldBeSaved => false;

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            if (IsDisposed || _profile != ProfileManager.CurrentProfile) return;
            _profile.WorldExplorerTravelPosition = Location;
            _profile.Save(ProfileManager.ProfilePath, false);
        }

        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile
                || Time.Ticks >= _expires && UIManager.DraggingControl != this)
            { Dispose(); return; }
            base.Update();
        }
    }

    internal sealed class WorldExplorerTravelCard : Control
    {
        internal static readonly string[] StyleNames =
            { "Runebook Parchment", "Moongate Brass", "Traveller's Map", "Runestone Plaque" };
        private static readonly string[] _files =
            { "runebook-parchment", "moongate-brass", "travellers-map", "runestone-plaque" };
        private static readonly Texture2D[] _textures = new Texture2D[4];
        private static readonly bool[] _failed = new bool[4];
        private readonly int _style;

        internal static int StyleIndex(byte style) => style < StyleNames.Length ? style : 1;
        internal static string ResourceName(int style) => "ClassicUO.Resources.WorldExplorerTravel." + _files[style] + ".png";

        internal WorldExplorerTravelCard(byte style, string destination, string method, int width)
        {
            _style = StyleIndex(style);
            Width = width; Height = width / 3; WantUpdateSize = false; AcceptMouseInput = false;
            ushort ink = _style == 0 || _style == 2 ? (ushort)0 : (ushort)0x0481;
            int textWidth = Width * 56 / 100;
            var heading = new Label("Travel to:", true, ink, textWidth, font: 1, align: TEXT_ALIGN_TYPE.TS_CENTER);
            var name = new Label(destination, true, ink, textWidth, font: 0,
                style: FontStyle.Cropped | (_style == 1 || _style == 3 ? FontStyle.BlackBorder : FontStyle.None), align: TEXT_ALIGN_TYPE.TS_CENTER);
            var detail = new Label(method, true, ink, textWidth, font: 1, align: TEXT_ALIGN_TYPE.TS_CENTER);
            int y = (Height - heading.Height - name.Height - detail.Height - 4) / 2;
            foreach (Label label in new[] { heading, name, detail })
            {
                label.X = Width * 27 / 100 + (textWidth - label.Width) / 2;
                label.Y = y; y += label.Height + 2;
                Add(label);
            }
        }

        private Texture2D GetTexture()
        {
            if (_failed[_style]) return null;
            if (_textures[_style] == null || _textures[_style].IsDisposed)
            {
                string resource = ResourceName(_style);
                try
                {
                    Log.Trace("World Explorer travel artwork: loading " + resource);
                    using (Stream stream = typeof(WorldExplorerTravelCard).Assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null) throw new FileNotFoundException("Missing embedded travel artwork", resource);
                        // Scale when drawing, avoiding FNA's native SDL surface resize/free path.
                        _textures[_style] = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                    }
                    int index = _style;
                    OptionalTextureCache.Register(_textures[index], () => _textures[index] = null);
                    Log.Trace("World Explorer travel artwork: loaded " + resource);
                }
                catch (Exception ex)
                {
                    _failed[_style] = true;
                    Log.Error("World Explorer travel artwork: " + resource + "\n" + ex);
                    return null;
                }
            }
            return _textures[_style];
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed) return false;
            Texture2D texture = GetTexture();
            if (texture != null)
            {
                OptionalTextureCache.Touch(texture);
                batcher.Draw(texture, new Rectangle(x, y, Width, Height), texture.Bounds, new Vector3(0, 0, 1));
            }
            else
            {
                Color fill = _style == 0 || _style == 2 ? new Color(215, 195, 150) : new Color(25, 30, 35);
                batcher.Draw(SolidColorTextureCache.GetTexture(fill), new Rectangle(x, y, Width, Height), new Vector3(0, 0, 1));
            }
            return base.Draw(batcher, x, y);
        }
    }
}
