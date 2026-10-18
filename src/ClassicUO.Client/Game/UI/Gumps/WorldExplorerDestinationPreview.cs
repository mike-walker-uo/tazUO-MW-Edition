using System;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldExplorerDestinationPreview : Gump
    {
        private readonly Control _owner;
        private readonly Profile _profile = ProfileManager.CurrentProfile;

        private WorldExplorerDestinationPreview(Control owner, WorldExplorerPin pin) : base(0, 0)
        {
            _owner = owner;
            Width = 256; Height = 290; WantUpdateSize = false; AcceptMouseInput = false;
            CanMove = false;
            X = owner.ScreenCoordinateX + owner.Width + 8; Y = owner.ScreenCoordinateY;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 1f));
            Add(new Label(!string.IsNullOrWhiteSpace(pin.CustomName) ? pin.CustomName : pin.Name, true, CustomGumpThemeManager.TitleHue,
                224, font: 1, style: FontStyle.Cropped) { X = 16, Y = 14 });
            Add(new DestinationMap(pin.Location) { X = 32, Y = 44 });
            Add(new Label(TravelLocationManager.FacetName(pin.Location.Facet) + $"  {pin.Location.X}, {pin.Location.Y}", true,
                CustomGumpThemeManager.TextHue, 224, font: 1) { X = 16, Y = 246 });
            Add(new Label(pin.Location.Source, true, CustomGumpThemeManager.DimHue, 224, font: 1) { X = 16, Y = 266 });
            foreach (Control child in Children) child.AcceptMouseInput = false;
            SetInScreen();
        }

        internal static bool IsHovered(Control owner)
        {
            for (Control c = UIManager.MouseOverControl; c != null; c = c.Parent)
                if (c == owner) return true;
            return false;
        }

        internal static void Show(Control owner, WorldExplorerPin pin)
        {
            var existing = UIManager.GetGump<WorldExplorerDestinationPreview>();
            if (existing?._owner == owner && !existing.IsDisposed) return;
            existing?.Dispose();
            UIManager.Add(new WorldExplorerDestinationPreview(owner, pin));
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile || _owner.IsDisposed || !IsHovered(_owner))
            { Dispose(); return; }
            base.Update();
        }

        private sealed class DestinationMap : Control
        {
            private const int Size = 128;
            private readonly TravelDestinationLocation _location;
            private readonly uint[] _pixels = new uint[Size * Size];
            private readonly sbyte[] _z = new sbyte[Size * Size];
            private Texture2D _texture;
            private int _block;
            private bool _failed, _initialized;
            private readonly Label _status;

            internal DestinationMap(TravelDestinationLocation location)
            {
                _location = location; Width = 192; Height = 192; WantUpdateSize = false; AcceptMouseInput = false;
                for (int i = 0; i < _pixels.Length; i++) { _pixels[i] = 0xFF202020; _z[i] = sbyte.MinValue; }
                Add(_status = new Label("Loading terrain...", true, 0x0481, 176, font: 1) { X = 8, Y = 82 });
            }

            public override unsafe void Update()
            {
                base.Update();
                if (_failed || _texture != null) return;
                try
                {
                    var maps = MapLoader.Instance;
                    if (!_initialized)
                    {
                        if (_location.Facet < 0 || _location.Facet >= MapLoader.MAPS_COUNT) { _failed = true; _status.Text = "Map unavailable"; return; }
                        maps.LoadMap(_location.Facet, World.ClientFeatures.Flags.HasFlag(CharacterListFlags.CLF_UNLOCK_FELUCCA_AREAS));
                        _initialized = true;
                    }
                    var blocks = maps.BlockData[_location.Facet];
                    if (blocks == null) { _failed = true; _status.Text = "Map unavailable"; return; }
                    int startX = (_location.X - 64) & ~7, startY = (_location.Y - 64) & ~7;
                    var hues = HuesLoader.Instance;
                    for (int slice = 0; slice < 16 && _block < 17 * 17; slice++, _block++)
                    {
                        int bx = (startX >> 3) + _block % 17, by = (startY >> 3) + _block / 17;
                        if (bx < 0 || by < 0 || bx >= maps.MapBlocksSize[_location.Facet, 0] || by >= maps.MapBlocksSize[_location.Facet, 1]) continue;
                        int index = bx * maps.MapBlocksSize[_location.Facet, 1] + by;
                        if (index >= blocks.Length) continue;
                        ref IndexMap block = ref blocks[index];
                        if (block.MapAddress == 0) continue;
                        MapBlock* land = (MapBlock*)block.MapAddress;
                        MapCells* cells = (MapCells*)&land->Cells;
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                            {
                                int px = bx * 8 + x - _location.X + 64, py = by * 8 + y - _location.Y + 64;
                                if (px < 0 || py < 0 || px >= Size || py >= Size) continue;
                                int p = py * Size + px;
                                _pixels[p] = HuesHelper.Color16To32((ushort)(0x8000 | hues.GetRadarColorData(cells[y * 8 + x].TileID & 0x3FFF))) | 0xFF000000;
                                _z[p] = cells[y * 8 + x].Z;
                            }
                        StaticsBlock* statics = (StaticsBlock*)block.StaticAddress;
                        for (int s = 0; statics != null && s < block.StaticCount; s++)
                        {
                            var tile = statics[s];
                            if (tile.X >= 8 || tile.Y >= 8 || tile.Color == 0 || tile.Color == 0xFFFF || !GameObject.CanBeDrawn(tile.Color)) continue;
                            int px = bx * 8 + tile.X - _location.X + 64, py = by * 8 + tile.Y - _location.Y + 64;
                            if (px < 0 || py < 0 || px >= Size || py >= Size) continue;
                            int p = py * Size + px;
                            if (tile.Z < _z[p]) continue;
                            ushort color = tile.Hue == 0 ? hues.GetRadarColorData(tile.Color + 0x4000) : hues.GetHueColorRgba5551(16, tile.Hue);
                            _pixels[p] = HuesHelper.Color16To32((ushort)(0x8000 | color)) | 0xFF000000; _z[p] = tile.Z;
                        }
                    }
                    if (_block == 17 * 17)
                    {
                        _texture = new Texture2D(Client.Game.GraphicsDevice, Size, Size);
                        _texture.SetData(_pixels);
                        _status.IsVisible = false;
                    }
                }
                catch (Exception ex) { _texture?.Dispose(); _texture = null; _failed = true; _status.Text = "Map unavailable"; Log.Error("Destination map: " + ex); }
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                var hue = new Vector3(0, 0, 1);
                batcher.Draw(_texture ?? SolidColorTextureCache.GetTexture(new Color(32, 32, 32)), new Rectangle(x, y, Width, Height), hue);
                if (_texture != null)
                {
                    var dot = SolidColorTextureCache.GetTexture(Color.Yellow);
                    batcher.Draw(dot, new Rectangle(x + Width / 2 - 6, y + Height / 2 - 1, 13, 3), hue);
                    batcher.Draw(dot, new Rectangle(x + Width / 2 - 1, y + Height / 2 - 6, 3, 13), hue);
                }
                return base.Draw(batcher, x, y);
            }

            public override void Dispose() { _texture?.Dispose(); _texture = null; base.Dispose(); }
        }
    }
}
