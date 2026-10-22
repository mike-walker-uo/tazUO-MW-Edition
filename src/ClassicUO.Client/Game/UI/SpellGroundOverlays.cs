using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI
{
    // Reuse confirmed-use timestamps; ground illumination has no separate buff or cast tracking.
    public static partial class OnslaughtDeliveryEffect
    {
        internal static bool DrawGround(UltimaBatcher2D batcher) =>
            SpellGroundGlow.DrawPlayer(batcher, SpellAbilityEffectId.Onslaught, _triggeredAt, LIFETIME_MS);
    }

    public static partial class ConsecrateWeaponEffect
    {
        internal static bool DrawGround(UltimaBatcher2D batcher) =>
            SpellGroundGlow.DrawPlayer(batcher, SpellAbilityEffectId.ConsecrateWeapon, _triggeredAt, LIFETIME_MS);
    }

    public static partial class DivineFuryEffect
    {
        internal static bool DrawGround(UltimaBatcher2D batcher) =>
            SpellGroundGlow.DrawPlayer(batcher, SpellAbilityEffectId.DivineFury, _triggeredAt, LIFETIME_MS);
    }

    public static partial class SacredJourneyEffect
    {
        internal static bool DrawGround(UltimaBatcher2D batcher) =>
            SpellGroundGlow.DrawPlayer(batcher, SpellAbilityEffectId.SacredJourney, _triggeredAt, LIFETIME_MS);
    }

    public static partial class AbilityOverheadEffect
    {
        internal static bool DrawGround(UltimaBatcher2D batcher) =>
            SpellAbilityEffectSettings.TryMap(_kind, out SpellAbilityEffectId id)
            && SpellGroundGlow.DrawPlayer(batcher, id, _triggeredAt, LIFETIME_MS);
    }
}
