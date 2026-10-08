using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal readonly struct GroundDropLocation
    {
        internal readonly ushort X, Y;
        internal readonly sbyte Z;
        internal readonly uint Container;
        internal GroundDropLocation(ushort x, ushort y, sbyte z, uint container)
        { X = x; Y = y; Z = z; Container = container; }
    }

    internal static class GroundDropPreview
    {
        internal const uint WorldContainer = 0xFFFFFFFF;
        internal static sbyte SurfaceZ(sbyte z, byte height, bool surface) =>
            unchecked((sbyte)(z + (surface && height != 0xFF ? height : 0)));

        // Shared by the preview and the actual drop packet; no new server-side rules.
        internal static bool TryResolve(GameObject target, ushort heldGraphic, out GroundDropLocation drop)
        {
            drop = default;
            if (target == null || target.IsDestroyed || target.Distance > Constants.DRAG_ITEMS_DISTANCE) return false;
            if (target is Entity entity)
            {
                if (target is Mobile || target is Item container && container.ItemData.IsContainer)
                { drop = new GroundDropLocation(0xFFFF, 0xFFFF, 0, entity.Serial); return true; }
                if (!(target is Item item) || !(item.ItemData.IsSurface || item.ItemData.IsStackable && item.Graphic == heldGraphic)) return false;
                drop = new GroundDropLocation(item.X, item.Y, SurfaceZ(item.Z, item.ItemData.Height, item.ItemData.IsSurface),
                    item.ItemData.IsSurface ? WorldContainer : item.Serial);
            }
            else if (target is Land || target is Static || target is Multi)
            {
                sbyte z = target.Z;
                if (!(target is Land))
                {
                    ref StaticTiles data = ref TileDataLoader.Instance.StaticData[target.Graphic];
                    z = SurfaceZ(z, data.Height, data.IsSurface);
                }
                drop = new GroundDropLocation(target.X, target.Y, z, WorldContainer);
            }
            else return false;
            return !(drop.Container == WorldContainer && drop.X == 0 && drop.Y == 0);
        }

        internal static void Draw(UltimaBatcher2D batcher)
        {
            if (ProfileManager.CurrentProfile?.GroundDropPreview != true || !UIManager.IsMouseOverWorld || World.Player == null) return;
            ItemHold held = Client.Game.GameCursor.ItemHold;
            if (!held.Enabled || held.IsFixedPosition || held.IsGumpTexture || TargetManager.IsTargeting || UIManager.IsDragging) return;
            GameObject target = SelectedObject.Object as GameObject;
            GroundDropLocation drop;
            Point anchor;
            bool valid;
            if (Keyboard.Ctrl)
            {
                anchor = World.Player.RealScreenPosition;
                anchor.X += 22; anchor.Y += 18; // Player tile + (1,0,1), matching Ctrl drop.
                drop = new GroundDropLocation((ushort)(World.Player.X + 1), World.Player.Y, unchecked((sbyte)(World.Player.Z + 1)), 0);
                valid = true;
            }
            else
            {
                if (target == null || target is Mobile || target is Item container && container.ItemData.IsContainer) return;
                valid = TryResolve(target, held.Graphic, out drop);
                if (valid && drop.Container != WorldContainer) return; // Existing stack, rather than ground placement.
                anchor = target.RealScreenPosition;
                if (valid) anchor.Y -= (drop.Z - target.Z) * 4;
            }
            ref readonly var art = ref Client.Game.Arts.GetArt(held.DisplayedGraphic);
            if (art.Texture == null) return;
            Vector3 hue = ShaderHueTranslator.GetHueVector(held.Hue, held.IsPartialHue, 0.45f);
            var rect = new Rectangle(anchor.X + 22 - art.UV.Width / 2, anchor.Y + 44 - art.UV.Height, art.UV.Width, art.UV.Height);
            // Direct batch drawing never participates in mouse selection or scene object lists.
            batcher.Draw(art.Texture, rect, art.UV, hue);
            Vector3 markerHue = ShaderHueTranslator.GetHueVector(0, false, 0.75f);
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(valid ? Color.PaleGreen : Color.IndianRed),
                anchor.X + 12, anchor.Y + 30, 20, 10, markerHue);
        }
    }
}
