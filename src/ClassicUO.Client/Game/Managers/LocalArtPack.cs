using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal static class LocalArtPack
    {
        // One record per static art/animation frame. Anchors are in replacement image pixels.
        internal sealed class Sprite
        {
            public Sprite() { }
            public int Graphic { get; set; }
            public int Body { get; set; } = -1;
            public int Group { get; set; }
            public int Direction { get; set; }
            public int Frame { get; set; }
            public string File { get; set; }
            public float Scale { get; set; } = 1f;
            public float AnchorX { get; set; }
            public float AnchorY { get; set; }
            public string HueMask { get; set; }
            internal Texture2D Texture;
            internal bool Failed;
        }
        private static readonly Dictionary<ushort, Sprite> Sprites = new Dictionary<ushort, Sprite>();
        private static readonly Dictionary<ulong, Sprite> Animations = new Dictionary<ulong, Sprite>();
        private static ulong AnimationKey(int body, int group, int direction, int frame) => ((ulong)(ushort)body << 24) | ((ulong)(byte)group << 16) | ((ulong)(byte)direction << 8) | (byte)frame;
        private static string _folder;
        private static bool _loaded;
        internal static void Reload() { _loaded = false; Sprites.Clear(); Animations.Clear(); }

        internal static bool TryGet(ushort graphic, out Sprite sprite)
        {
            sprite = null;
            return EnsureLoaded() && Sprites.TryGetValue(graphic, out sprite) && LoadSprite(sprite);
        }

        internal static bool TryGetAnimation(ushort body, byte group, byte direction, byte frame, out Sprite sprite)
        {
            sprite = null;
            return EnsureLoaded() && Animations.TryGetValue(AnimationKey(body, group, direction, frame), out sprite) && LoadSprite(sprite);
        }

        private static bool EnsureLoaded()
        {
            if (VisualBudget.Settings?.LocalArtPack != true) return false;
            string folder = VisualBudget.Settings.ArtPackFolder ?? "";
            if (string.IsNullOrWhiteSpace(folder)) return false;
            if (folder != _folder) { Reload(); _folder = folder; }
            if (!_loaded)
            {
                _loaded = true;
                try
                {
                    string path = Path.Combine(folder, "manifest.json");
                    if (!System.IO.File.Exists(path)) { Log.Warn("Art pack manifest missing: " + path); return false; }
                    var entries = JsonSerializer.Deserialize<List<Sprite>>(System.IO.File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (entries != null) foreach (Sprite entry in entries)
                        if (entry.Graphic >= 0 && entry.Graphic <= ushort.MaxValue && entry.Scale > 0 && entry.Scale <= 4
                            && !string.IsNullOrEmpty(entry.File) && Sprites.Count + Animations.Count < 4096)
                        {
                            if (entry.Body >= 0 && entry.Body <= ushort.MaxValue && entry.Group >= 0 && entry.Group <= 255
                                && entry.Direction >= 0 && entry.Direction <= 4 && entry.Frame >= 0 && entry.Frame <= 255)
                                Animations[AnimationKey(entry.Body, entry.Group, entry.Direction, entry.Frame)] = entry;
                            else if (entry.Body == -1) Sprites[(ushort)entry.Graphic] = entry;
                        }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException)
                { Log.Warn("Cannot load art pack: " + ex.Message); }
            }
            return true;
        }

        private static bool LoadSprite(Sprite sprite)
        {
            string folder = _folder;
            if (sprite.Failed) return false;
            if (sprite.Texture == null || sprite.Texture.IsDisposed)
            {
                Texture2D texture = null;
                try
                {
                    using (Stream stream = System.IO.File.OpenRead(AssetPath(folder, sprite.File))) texture = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                    if (texture.Width > 4096 || texture.Height > 4096) throw new InvalidDataException("Art pack sprite exceeds 4096px.");
                    if (!string.IsNullOrEmpty(sprite.HueMask))
                    {
                        using (Stream stream = System.IO.File.OpenRead(AssetPath(folder, sprite.HueMask)))
                        using (Texture2D mask = Texture2D.FromStream(Client.Game.GraphicsDevice, stream))
                        {
                            if (mask.Width != texture.Width || mask.Height != texture.Height) throw new InvalidDataException("Hue mask must match sprite dimensions.");
                            var pixels = new Color[texture.Width * texture.Height];
                            var maskPixels = new Color[pixels.Length];
                            texture.GetData(pixels); mask.GetData(maskPixels);
                            for (int i = 0; i < pixels.Length; i++)
                                if (maskPixels[i].R > 127 && maskPixels[i].A > 0)
                                { byte value = Math.Max(pixels[i].R, Math.Max(pixels[i].G, pixels[i].B)); pixels[i] = new Color(value, value, value, pixels[i].A); }
                                else if (pixels[i].A > 0 && pixels[i].R == pixels[i].G && pixels[i].R == pixels[i].B)
                                {
                                    // Partial hue tests exact grey; keep unmasked grey outside that test.
                                    Color color = pixels[i]; color.G = (byte)(color.G > 0 ? color.G - 1 : 1); pixels[i] = color;
                                }
                            texture.SetData(pixels);
                        }
                    }
                    Sprite owner = sprite;
                    sprite.Texture = texture;
                    OptionalTextureCache.Register(texture, () => owner.Texture = null);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
                { texture?.Dispose(); sprite.Failed = true; Log.Warn("Cannot load art pack sprite: " + ex.Message); return false; }
            }
            return true;
        }

        internal static string AssetPath(string folder, string name)
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, name));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Art pack asset must be inside its folder.");
            return path;
        }

        internal static Vector3 MaskHue(Sprite sprite, Vector3 hue)
        {
            if (sprite != null && !string.IsNullOrEmpty(sprite.HueMask) && hue.Y == ShaderHueTranslator.SHADER_HUED)
                hue.Y = ShaderHueTranslator.SHADER_PARTIAL_HUED;
            return hue;
        }

        internal static bool Draw(UltimaBatcher2D batcher, ushort graphic, int tileX, int tileY, Vector3 hue, float depth)
        {
            if (!TryGet(graphic, out Sprite sprite)) return false;
            batcher.Draw(sprite.Texture, new Vector2(tileX + 22 - sprite.AnchorX * sprite.Scale, tileY + 44 - sprite.AnchorY * sprite.Scale),
                sprite.Texture.Bounds, MaskHue(sprite, hue), 0f, Vector2.Zero, new Vector2(sprite.Scale), SpriteEffects.None, depth);
            return true;
        }
    }
}
