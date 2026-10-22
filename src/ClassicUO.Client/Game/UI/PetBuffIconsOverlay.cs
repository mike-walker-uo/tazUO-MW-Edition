using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI
{
    internal static class PetBuffIconsOverlay
    {
        private const int Cell = 24, PerRow = 8, MaxIcons = 16;
        private static RenderedText _unknown;

        internal static void Reset() { _unknown?.Destroy(); _unknown = null; }

        internal static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!PetBuffMonitor.Enabled || ProfileManager.CurrentProfile?.PetBuffOverheadIcons != true || PetBuffMonitor.Current.Pets.Count == 0) return;
            var camera = Client.Game.Scene.Camera;
            Rectangle bounds = camera.Bounds;
            if (!batcher.ClipBegin(bounds.X, bounds.Y, bounds.Width, bounds.Height)) return;
            try
            {
                uint now = Time.Ticks;
                Vector3 neutral = ShaderHueTranslator.GetHueVector(0, false, 1f, true);
                var dark = SolidColorTextureCache.GetTexture(new Color(12, 16, 20, 230));
                var confirmed = SolidColorTextureCache.GetTexture(new Color(85, 195, 170));
                var inferred = SolidColorTextureCache.GetTexture(new Color(230, 170, 65));
                foreach (PetBuffMonitor.Pet pet in PetBuffMonitor.Current.Pets.Values)
                {
                    var mobile = World.Mobiles.Get(pet.Serial);
                    if (mobile == null || mobile.IsDestroyed || mobile.IsDead || mobile.Distance > World.ClientViewRange || mobile.FrameInfo.Y == 0) continue;
                    int count = 0;
                    foreach (PetBuffMonitor.Status status in pet.Statuses.Values) if (status.ShowIcon(now) && !status.Expired(now)) count++;
                    count = Math.Min(count, MaxIcons);
                    if (count == 0) continue;
                    Point head = mobile.RealScreenPosition;
                    head.X += 22 + (int)mobile.Offset.X;
                    float bodyScale = BodyScaleManager.Get(mobile.GetGraphicForAnimation(), mobile);
                    head.Y += (mobile.IsMounted ? 0 : 22) + (int)(mobile.Offset.Y - mobile.Offset.Z - mobile.FrameInfo.Y * bodyScale) - 12;
                    head = camera.WorldToScreen(head); head.X += bounds.X; head.Y += bounds.Y;
                    int rows = (count + PerRow - 1) / PerRow, index = 0;
                    foreach (PetBuffMonitor.Status status in pet.Statuses.Values)
                    {
                        if (index >= count) break;
                        if (!status.ShowIcon(now) || status.Expired(now)) continue;
                        int rowCount = Math.Min(PerRow, count - index / PerRow * PerRow);
                        int x = head.X - rowCount * Cell / 2 + index % PerRow * Cell;
                        int y = head.Y - rows * Cell + index / PerRow * Cell;
                        bool observed = status.Source == PetBuffMonitor.Evidence.Observed;
                        batcher.Draw(observed ? inferred : confirmed, new Rectangle(x, y, Cell - 1, Cell - 1), neutral);
                        batcher.Draw(dark, new Rectangle(x + 1, y + 1, Cell - 3, Cell - 3), neutral);
                        ushort graphic = PetBuffMonitor.IconGraphic(status.Kind);
                        if (graphic != 0)
                        {
                            ref readonly var gump = ref Client.Game.Gumps.GetGump(graphic);
                            if (gump.Texture != null)
                                batcher.Draw(gump.Texture, new Rectangle(x + 2, y + 2, Cell - 5, Cell - 5), gump.UV,
                                    ShaderHueTranslator.GetHueVector(0, false, observed ? 0.8f : 1f, true));
                        }
                        if (observed || graphic == 0)
                        {
                            if (_unknown == null) _unknown = RenderedText.Create("?", 0x0481, 1, style: FontStyle.BlackBorder);
                            _unknown.Draw(batcher, x + Cell - 10, y + Cell - 17);
                        }
                        index++;
                    }
                }
            }
            finally { batcher.ClipEnd(); }
        }
    }
}
