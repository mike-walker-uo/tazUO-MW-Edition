using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class EquipmentSwitcherTests : IDisposable
    {
        private readonly StaticTiles[] _oldTiles = TileDataLoader._staticData;
        private readonly Dictionary<uint, ItemProperty> _properties = (Dictionary<uint, ItemProperty>)typeof(ObjectPropertiesListManager)
            .GetField("_itemsProperties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(World.OPL);
        private readonly Dictionary<uint, ItemProperty> _oldProperties = new();
        private const uint SERIAL = 0x4FFE0101;

        public EquipmentSwitcherTests()
        {
            TileDataLoader._staticData = new StaticTiles[2];
            TileDataLoader._staticData[1] = new StaticTiles { Name = "Sword" };
            _properties.TryGetValue(SERIAL, out var old); _oldProperties[SERIAL] = old;
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("swo", true)]
        [InlineData("unknown", false)]
        public void Search_matches_names_before_properties_arrive(string query, bool expected)
        {
            _properties.Remove(SERIAL);
            var item = new Item { Serial = SERIAL, Graphic = 1, Name = "Exquisite Sword", Hue = 77, Container = 42 };
            Assert.Equal(expected, PaperDollBackpackEquipmentGump.MatchesSearch(item, query));
            Assert.Equal((ushort)77, item.Hue); Assert.Equal(42u, item.Container);
            Assert.False(PaperDollBackpackEquipmentGump.MatchesSearch(null, query));
        }

        [Fact]
        public void Search_refreshes_from_later_tooltip_data_and_matches_slayer_or_property()
        {
            var item = new Item { Serial = SERIAL, Graphic = 1 };
            _properties.Remove(SERIAL);
            Assert.False(PaperDollBackpackEquipmentGump.MatchesSearch(item, "undead"));
            _properties[SERIAL] = new ItemProperty { Name = "Exquisite Sword", Data = "Undead Slayer\nHit Fireball 50%" };
            Assert.True(PaperDollBackpackEquipmentGump.MatchesSearch(item, "UNDEAD"));
            Assert.True(PaperDollBackpackEquipmentGump.MatchesSearch(item, "fireball"));
            Assert.True(PaperDollBackpackEquipmentGump.MatchesSearch(item, "exquisite"));
        }

        [Fact]
        public void Background_hue_uses_first_configured_slayer_and_supports_talisman_slayers()
        {
            var profile = new Profile { SlayerBarDefaultHue = 9, SlayerBarSlayerHues = new Dictionary<string, ushort>
                { ["Demon Slayer"] = 33, ["Undead Slayer"] = 55, ["Wolf Slayer"] = 77 } };
            Assert.Equal((ushort)33, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "demon slayer\nUndead Slayer"));
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Undead Slayer\nDemon Slayer"));
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Slayer: Undead"));
            Assert.Equal((ushort)77, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "*Wolf Slayer*\r\nLuck 100"));
            Assert.Equal((ushort)9, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Damage Increase 10%"));
            profile.SlayerBarSlayerHues["Undead Slayer"] = 0;
            Assert.Equal((ushort)0, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Undead Slayer"));
            profile.SlayerBarSlayerHues = null;
            Assert.Equal((ushort)9, PaperDollBackpackEquipmentGump.BackgroundHue(profile, null));
        }

        [Theory]
        [InlineData("Silver")]
        [InlineData("silver slayer")]
        [InlineData("Slayer: Silver")]
        [InlineData("*SILVER*\r")]
        public void Silver_uses_undead_hue_and_retains_tooltip_order(string silver)
        {
            var profile = new Profile { SlayerBarDefaultHue = 9, SlayerBarSlayerHues = new Dictionary<string, ushort>
                { ["Undead Slayer"] = 55, ["Demon Slayer"] = 33 } };
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, silver));
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, silver + "\nDemon Slayer"));
            Assert.Equal((ushort)33, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Demon Slayer\n" + silver));
            profile.SlayerBarSlayerHues["Undead Slayer"] = 0;
            Assert.Equal((ushort)0, PaperDollBackpackEquipmentGump.BackgroundHue(profile, silver));
            Assert.Equal("Undead Slayer", PaperDollBackpackEquipmentGump.CanonicalSlayer(
                Assert.Single(PaperDollBackpackEquipmentGump.SlayerNames(silver))));
        }

        [Fact]
        public void Shared_undead_rule_takes_precedence_over_older_silver_override()
        {
            var profile = new Profile { SlayerBarDefaultHue = 9, SlayerBarSlayerHues = new Dictionary<string, ushort>
                { ["SILVER SLAYER"] = 77, ["UNDEAD SLAYER"] = 55 } };
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Silver"));
            Assert.Equal((ushort)55, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Undead Slayer"));
            profile.SlayerBarSlayerHues.Remove("UNDEAD SLAYER");
            Assert.Equal((ushort)77, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Undead Slayer"));
            Assert.Equal((ushort)77, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Silver"));
            profile.SlayerBarSlayerHues.Clear();
            Assert.Equal((ushort)9, PaperDollBackpackEquipmentGump.BackgroundHue(profile, "Silver"));
            Assert.Empty(PaperDollBackpackEquipmentGump.SlayerNames("Silver Ring\nSilver 100"));
        }

        [Theory]
        [InlineData(false, 7, 0, 5, 6)]
        [InlineData(true, 7, 0, 5, 6)]
        [InlineData(false, 19, 1, 12, 23)]
        [InlineData(true, 19, 1, 12, 23)]
        [InlineData(false, 0, 0, 0, 0)]
        [InlineData(true, 0, 0, 0, 0)]
        public void Rotated_cells_stay_inside_content_bounds_without_overlap(bool horizontal, int weapons, int shields, int talismans, int books)
        {
            int[] counts = { weapons, shields, talismans, books };
            Point size = PaperDollBackpackEquipmentGump.ContentSize(counts, horizontal);
            var rectangles = new List<Rectangle>();
            int offset = 0;
            foreach (int count in counts)
            {
                if (count == 0) continue;
                for (int i = 0; i < count; i++)
                {
                    Point point = PaperDollBackpackEquipmentGump.CellPosition(i, offset, horizontal);
                    var cell = new Rectangle(point.X, point.Y, 26, 26);
                    Assert.True(cell.X >= 0 && cell.Y >= 0 && cell.Right <= size.X && cell.Bottom <= size.Y);
                    Assert.DoesNotContain(rectangles, previous => previous.Intersects(cell));
                    rectangles.Add(cell);
                    Point rotated = PaperDollBackpackEquipmentGump.CellPosition(i, offset, !horizontal);
                    Assert.Equal(new Point(point.Y, point.X), rotated);
                }
                offset += ((count + 8) / 9) * 30 + 4;
            }
            Assert.Equal(counts.Sum(), rectangles.Count);
        }

        [Theory]
        [InlineData("0", true, 0)]
        [InlineData("1150", true, 1150)]
        [InlineData("0x047E", true, 1150)]
        [InlineData(" 0X047e ", true, 1150)]
        [InlineData("3001", false, 0)]
        [InlineData("65536", false, 0)]
        [InlineData("-1", false, 0)]
        [InlineData("bad", false, 0)]
        public void Hue_input_accepts_decimal_and_prefixed_hex_with_range_check(string text, bool valid, int hue)
        {
            Assert.Equal(valid, EquipmentSwitcherOptionsGump.ParseHue(text, 3000, out ushort actual));
            Assert.Equal((ushort)hue, actual);
        }

        [Fact]
        public void Detached_switcher_survives_its_paperdoll_releasing_ownership()
        {
            var owner = (PaperDollGump)FormatterServices.GetUninitializedObject(typeof(PaperDollGump));
            var bar = (PaperDollBackpackEquipmentGump)FormatterServices.GetUninitializedObject(typeof(PaperDollBackpackEquipmentGump));
            typeof(PaperDollBackpackEquipmentGump).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bar, new Profile { SlayerBarDetached = true });
            var ownerField = typeof(PaperDollBackpackEquipmentGump).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic);
            ownerField.SetValue(bar, owner);
            bar.ReleaseOwner(owner);
            Assert.Null(ownerField.GetValue(bar));
            Assert.False(bar.IsDisposed);
            Assert.True(bar.Detached);
        }

        [Fact]
        public void Position_orientation_and_hues_round_trip_and_are_included_in_settings_transfer()
        {
            var profile = new Profile { SlayerBarDetached = true, SlayerBarLocked = true, SlayerBarHorizontal = true, SlayerBarPosition = new Point(53, 75),
                SlayerBarDefaultHue = 99, SlayerBarSlayerHues = new Dictionary<string, ushort> { ["Undead Slayer"] = 1150 } };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(restored.SlayerBarDetached); Assert.True(restored.SlayerBarHorizontal);
            Assert.True(restored.SlayerBarLocked);
            Assert.Equal(profile.SlayerBarPosition, restored.SlayerBarPosition);
            Assert.Equal((ushort)99, restored.SlayerBarDefaultHue);
            Assert.Equal((ushort)1150, PaperDollBackpackEquipmentGump.BackgroundHue(restored, "undead slayer"));
            Assert.Equal(ProfileTransferCategory.GumpLayout, ProfileTransferCatalog.JsonCategories["slayer_bar_position"]);
            Assert.Equal(ProfileTransferCategory.Interface, ProfileTransferCatalog.JsonCategories["slayer_bar_slayer_hues"]);
            Assert.Equal(ProfileTransferCategory.GumpLayout, ProfileTransferCatalog.JsonCategories["slayer_bar_locked"]);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void Position_lock_only_allows_dragging_unlocked_detached_bars(bool detached, bool locked, bool movable)
        {
            var profile = new Profile { SlayerBarDetached = detached, SlayerBarLocked = locked };
            Assert.Equal(movable, PaperDollBackpackEquipmentGump.CanMoveDetached(profile));
        }

        [Fact]
        public void Legend_explanation_identifies_winning_slayer_and_explicit_theme_override()
        {
            var profile = new Profile { SlayerBarDefaultHue = 9, SlayerBarSlayerHues = new Dictionary<string, ushort>
                { ["Demon Slayer"] = 33, ["Undead Slayer"] = 55 } };
            string data = "Undead Slayer\nDemon Slayer";
            var rule = PaperDollBackpackEquipmentGump.BackgroundRule(profile, data);
            Assert.Equal("Undead Slayer", rule.Key); Assert.Equal((ushort)55, rule.Value);
            string explanation = PaperDollBackpackEquipmentGump.HueExplanation(profile, data);
            Assert.Contains("Undead Slayer / 0x0037", explanation);
            Assert.Contains("Slayers: Undead Slayer, Demon Slayer", explanation);
            profile.SlayerBarSlayerHues["Undead Slayer"] = 0;
            Assert.Contains("Undead Slayer / theme", PaperDollBackpackEquipmentGump.HueExplanation(profile, data));
            Assert.Contains("Default / unmatched equipment / 0x0009", PaperDollBackpackEquipmentGump.HueExplanation(profile, "Luck 100"));
        }

        public void Dispose()
        {
            TileDataLoader._staticData = _oldTiles;
            foreach (var entry in _oldProperties)
                if (entry.Value == null) _properties.Remove(entry.Key); else _properties[entry.Key] = entry.Value;
        }
    }
}
