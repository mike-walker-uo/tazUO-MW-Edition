using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ShortlistProfileTests
    {
        [Fact]
        public void New_options_round_trip_and_default_to_existing_layouts()
        {
            var defaults = new Profile();
            Assert.Equal(0, defaults.StatusLayout); Assert.False(defaults.GroundDropPreview);
            Assert.False(defaults.BackpackSections.Enabled); Assert.False(defaults.WorldMapMarkerNamesAtAnyZoom);
            Assert.Empty(defaults.Condition_KeepExisting);
            var profile = new Profile { StatusLayout = 2, GroundDropPreview = true, CounterBarShowHotkeys = true,
                WorldMapMarkerNamesAtAnyZoom = true, Condition_KeepExisting = new List<bool> { true, false } };
            profile.BackpackSections.Enabled = true;
            profile.BackpackSections.Rules = new List<GridSectionRule> { new GridSectionRule
                { Name = "My gems", Match = GridSectionMatch.Graphic, Value = 0xF10, Hue = 0x35 } };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            Profile restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(2, restored.StatusLayout); Assert.True(restored.GroundDropPreview); Assert.True(restored.CounterBarShowHotkeys);
            Assert.True(restored.WorldMapMarkerNamesAtAnyZoom); Assert.Equal(new[] { true, false }, restored.Condition_KeepExisting);
            Assert.True(restored.BackpackSections.Enabled); Assert.Equal("My gems", restored.BackpackSections.Rules[0].Name);
            Assert.Equal(0xF10, restored.BackpackSections.Rules[0].Value); Assert.False(restored.CorpseSections.Enabled);
            Assert.False(restored.ContainerSections.Enabled);
        }

        [Fact]
        public void Old_container_JSON_inherits_category_sections_and_preserves_locked_slots()
        {
            var entry = JsonSerializer.Deserialize("{\"s\":42,\"ls\":{\"99\":{\"s\":99,\"k\":true,\"sl\":7}}}",
                GridContainerSerializerContext.Default.GridContainerEntry);
            Assert.Equal(-1, entry.SectionsOverride);
            Assert.True(entry.Slots[99].Locked); Assert.Equal(7, entry.Slots[99].Slot);
        }

        [Fact]
        public void Section_rules_are_ordered_and_backpack_and_corpse_configs_are_independent()
        {
            var config = new GridSectionsConfig { Rules = new List<GridSectionRule>
            {
                new GridSectionRule { Name = "Silver", Match = GridSectionMatch.NameContains, Text = "silver" },
                new GridSectionRule { Name = "Rings", Match = GridSectionMatch.EquipmentLayer, Value = (int)Layer.Ring }
            } };
            var data = new StaticTiles { Flags = TileFlag.Wearable, Layer = (byte)Layer.Ring };
            Assert.Equal(0, config.Group(data, 10, "SILVER ring")); Assert.Equal(1, config.Group(data, 10, "gold ring"));
            Assert.Equal(2, config.Group(default, 20, "ruby"));
            var profile = new Profile(); profile.BackpackSections.Rules[0].Name = "Pack weapons";
            Assert.Equal("Weapons", profile.CorpseSections.Rules[0].Name);
        }

        [Theory]
        [InlineData(0xF7A, true)] [InlineData(0xF8D, true)] [InlineData(0xF78, true)]
        [InlineData(0xF10, false)] [InlineData(0xF91, false)]
        public void Reagent_sections_exclude_unrelated_art(int graphic, bool expected) =>
            Assert.Equal(expected, new GridSectionRule { Match = GridSectionMatch.Reagents }.Matches(default, (ushort)graphic, ""));

        [Fact]
        public void Keep_existing_matches_rule_identity_without_mixing_same_label_rules()
        {
            Assert.True(CoolDownBarManager.MatchesRule("rule-a", "Heal", "rule-a", "Heal"));
            Assert.False(CoolDownBarManager.MatchesRule("rule-a", "Heal", "rule-b", "Heal"));
            Assert.False(CoolDownBarManager.MatchesRule("rule-a", "Heal", null, "Heal"));
            Assert.True(CoolDownBarManager.MatchesRule(null, "Heal", null, "Heal"));
        }

        [Fact]
        public void Old_cooldown_rules_gain_distinct_persisted_ids()
        {
            var profile = new Profile { Condition_Hue = new List<ushort> { 42, 42 },
                Condition_Label = new List<string> { "Heal", "Heal" },
                Condition_Trigger = new List<string> { "heal", "heal" },
                Condition_Duration = new List<int> { 10, 20 }, Condition_Type = new List<int> { 0, 0 } };
            profile.EnsureCooldownRuleIds();
            Assert.Equal(2, profile.Condition_Ids.Count);
            Assert.NotEqual(profile.Condition_Ids[0], profile.Condition_Ids[1]);
            string first = profile.Condition_Ids[0], second = profile.Condition_Ids[1];
            profile.EnsureCooldownRuleIds();
            Assert.Equal(first, profile.Condition_Ids[0]);
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(second, restored.Condition_Ids[1]);
        }

        [Theory]
        [InlineData(50, 100, 50)] [InlineData(-1, 100, 0)] [InlineData(150, 100, 100)] [InlineData(1, 0, 0)]
        public void Compact_status_bars_clamp_missing_or_unusual_stat_values(int value, int maximum, int expected) =>
            Assert.Equal(expected, StatusGumpCompact.FillWidth(100, value, maximum));

        [Fact]
        public void Map_label_collision_bounds_match_the_drawn_edge_clamping()
        {
            Rectangle bounds = WorldMapGump.MarkerLabelBounds(new Point(499, 399), new Vector2(100, 20), new Rectangle(0, 0, 500, 400));
            Assert.Equal(new Rectangle(390, 353, 104, 24), bounds);
            Assert.True(bounds.Intersects(WorldMapGump.MarkerLabelBounds(new Point(495, 390), new Vector2(100, 20), new Rectangle(0, 0, 500, 400))));
        }
    }
}
