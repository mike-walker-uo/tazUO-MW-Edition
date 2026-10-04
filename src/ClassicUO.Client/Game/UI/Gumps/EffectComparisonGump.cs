using System;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EffectComparisonGump : Gump
    {
        private readonly SpellAbilityEffectEntry[] _entries = SpellAbilityEffectSettings.Entries.Where(e =>
            SpellAbilityEffectSettings.TryMap(e.Id, out CombatVisualKind combat) && combat != CombatVisualKind.StaminaDrain && combat != CombatVisualKind.BattleLust
            || SpellAbilityEffectSettings.TryMap(e.Id, out EnhancedSpellVisualKind spell) && spell != EnhancedSpellVisualKind.DeathRay
            || SpellAbilityEffectSettings.TryMap(e.Id, out HitAreaElement area) || e.Id == SpellAbilityEffectId.Lightning).ToArray();
        private int _selected, _quality = 2;
        private uint _born;
        private GameEffect _effect;
        private readonly EffectManager _manager = new EffectManager();

        internal EffectComparisonGump() : base(0, 0)
        {
            X = 120; Y = 120; Width = 760; Height = 480; CanMove = true; CanCloseWithRightClick = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("Classic / enhanced effect comparison", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 18, Y = 15 });
            Add(new BaseOptionsGump.ComboBoxWithLabel("Effect", 60, 310, _entries.Select(e => e.Name).ToArray(), 0,
                (index, _) => { _selected = index; Restart(); }, true) { X = 18, Y = 48 });
            Add(new BaseOptionsGump.ComboBoxWithLabel("Quality", 65, 160, new[] { "Low", "Medium", "High" }, 2,
                (index, _) => _quality = index) { X = 445, Y = 48 });
            Add(new Label("Classic UO", true, CustomGumpThemeManager.TextHue, font: 1) { X = 150, Y = 108 });
            Add(new Label("Enhanced", true, CustomGumpThemeManager.TextHue, font: 1) { X = 510, Y = 108 });
            Add(new Label("Local preview. No casts, packets or world effects. Effect detail sliders apply to the enhanced side.",
                true, CustomGumpThemeManager.DimHue, 710, font: 1) { X = 18, Y = 435 });
            Restart(); SetInScreen();
        }

        private void Restart()
        {
            _effect?.Destroy(); _effect = null; _born = Time.Ticks;
            SpellAbilityEffectId id = _entries[_selected].Id;
            using (EffectPresentation.For(id, _quality))
            {
                if (id == SpellAbilityEffectId.Lightning)
                    _effect = new LightningEffect(_manager, 0, 0, 0, 0, 0, true);
                else if (SpellAbilityEffectSettings.TryMap(id, out EnhancedSpellVisualKind spell))
                    _effect = new EnhancedSpellVisualEffect(_manager, 0, 0, 0, 0, spell);
                else if (SpellAbilityEffectSettings.TryMap(id, out CombatVisualKind combat))
                    _effect = new CombatVisualEffect(_manager, 0, 0, 0, 0, combat);
                else if (SpellAbilityEffectSettings.TryMap(id, out HitAreaElement area))
                    _effect = new HitAreaEffect(_manager, 0, 0, 0, 0, 0, 0, 800, 0, area);
            }
        }

        public override void Update()
        {
            base.Update();
            if (Time.Ticks - _born > (_entries[_selected].Id == SpellAbilityEffectId.Lightning ? 650u : 2000u)) Restart();
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            SpellAbilityEffectId id = _entries[_selected].Id;
            ushort graphic = ClassicGraphic(id);
            uint age = Time.Ticks - _born;
            ushort animated = id == SpellAbilityEffectId.Lightning ? (ushort)(graphic + Math.Min(9u, age / 40u)) : ClassicFrame(graphic, age);
            ref readonly var sprite = ref (id == SpellAbilityEffectId.Lightning
                ? ref Client.Game.Gumps.GetGump(animated) : ref Client.Game.Arts.GetArt(animated));
            if (batcher.ClipBegin(x + 18, y + 140, 350, 275))
            {
                if (sprite.Texture != null)
                    batcher.Draw(sprite.Texture, new Vector2(x + 195, y + 295), sprite.UV, ShaderHueTranslator.GetHueVector(ClassicHue(id)),
                        0f, new Vector2(sprite.UV.Width / 2f, id == SpellAbilityEffectId.Lightning ? sprite.UV.Height : sprite.UV.Height / 2f), Vector2.One, SpriteEffects.None, 0);
                batcher.ClipEnd();
            }
            if (_effect is LightningEffect lightning) lightning.AnimIndex = (byte)Math.Min(9u, age / 40u);
            if (!batcher.ClipBegin(x + 390, y + 140, 350, 275)) return true;
            using (EffectPresentation.For(id, _quality))
            {
                if (_effect != null) _effect.Draw(batcher, x + 530, y + 280, 0);
                else if (SpellAbilityEffectSettings.TryMap(id, out AbilityOverheadKind kind))
                    AbilityOverheadEffect.DrawSymbol(batcher, new Vector2(x + 555, y + 265), (Time.Ticks - _born) / 2000f,
                        1f, 1f, kind);
            }
            batcher.SetBlendState(null);
            batcher.ClipEnd();
            return true;
        }

        internal static ushort ClassicGraphic(SpellAbilityEffectId id)
        {
            switch (id)
            {
                case SpellAbilityEffectId.Lightning: return 0x4E20;
                case SpellAbilityEffectId.MagicArrow: return 0x36E4;
                case SpellAbilityEffectId.Fireball: return 0x36D4;
                case SpellAbilityEffectId.EnergyBolt: return 0x379F;
                case SpellAbilityEffectId.Explosion: return 0x36BD;
                case SpellAbilityEffectId.NetherBolt: return 0x36D4;
                case SpellAbilityEffectId.EagleStrike: return 0x407A;
                case SpellAbilityEffectId.Bombard: return 0x1363;
                case SpellAbilityEffectId.WordOfDeath: return 0x0F5F;
                case SpellAbilityEffectId.MeteorSwarm: return 0x36D4;
                case SpellAbilityEffectId.Thunderstorm: return 0x1B6C;
                case SpellAbilityEffectId.NetherCyclone: return 0x375A;
                case SpellAbilityEffectId.Wildfire: return 0x398C;
                case SpellAbilityEffectId.HailStorm: return 0x3779;
                case SpellAbilityEffectId.LowerAttack:
                case SpellAbilityEffectId.LowerDefense: return 0x37BE;
                case SpellAbilityEffectId.Harm: return 0x374A;
                case SpellAbilityEffectId.ManaDrain: return 0x3789;
                case SpellAbilityEffectId.HitFireArea:
                case SpellAbilityEffectId.HitColdArea:
                case SpellAbilityEffectId.HitPoisonArea:
                case SpellAbilityEffectId.HitEnergyArea: return 0x3779;
                default: return 0x373A;
            }
        }
        private static unsafe ushort ClassicFrame(ushort graphic, uint age)
        {
            var frame = AnimDataLoader.Instance.CalculateCurrentGraphic(graphic);
            return frame.FrameCount == 0 ? graphic : (ushort)(graphic + frame.FrameData[(age / Math.Max(100u, frame.FrameInterval * 100u)) % frame.FrameCount]);
        }
        private static ushort ClassicHue(SpellAbilityEffectId id)
        {
            switch (id)
            {
                case SpellAbilityEffectId.NetherBolt:
                case SpellAbilityEffectId.NetherCyclone: return 0x49B;
                case SpellAbilityEffectId.MeteorSwarm: return 9502;
                case SpellAbilityEffectId.WordOfDeath: return 0x22;
                case SpellAbilityEffectId.Thunderstorm: return 0x481;
                case SpellAbilityEffectId.HailStorm: return 0x64;
                case SpellAbilityEffectId.Harm: return 1154;
                case SpellAbilityEffectId.LowerAttack: return 0x0B;
                case SpellAbilityEffectId.LowerDefense: return 0x24;
                case SpellAbilityEffectId.HitFireArea: return 1160;
                case SpellAbilityEffectId.HitColdArea: return 2100;
                case SpellAbilityEffectId.HitPoisonArea: return 1166;
                case SpellAbilityEffectId.HitEnergyArea: return 120;
                default: return 0;
            }
        }
        public override void Dispose() { _effect?.Destroy(); _manager.Clear(); base.Dispose(); }
    }
}
