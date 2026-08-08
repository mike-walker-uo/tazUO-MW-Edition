using System;
using System.Linq;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class SpellAbilityEffectSettingsTests
    {
        [Fact]
        public void Catalog_contains_every_effect_once()
        {
            SpellAbilityEffectId[] ids =
                SpellAbilityEffectSettings.Entries
                    .Select(entry => entry.Id)
                    .ToArray();

            ids.Should().OnlyHaveUniqueItems();
            ids.Should().BeEquivalentTo(
                Enum.GetValues(typeof(SpellAbilityEffectId))
                    .Cast<SpellAbilityEffectId>()
            );
        }

        [Fact]
        public void Catalog_entries_have_visible_labels()
        {
            SpellAbilityEffectSettings.Entries.Should().OnlyContain(
                entry =>
                    !string.IsNullOrWhiteSpace(entry.Name)
                    && !string.IsNullOrWhiteSpace(entry.Category)
            );
        }

        [Theory]
        [InlineData(0, 8)]
        [InlineData(20, 28)]
        [InlineData(125, 127)]
        [InlineData(127, 127)]
        public void Preview_target_is_raised_and_clamped(
            int groundZ,
            int expectedZ)
        {
            SpellAbilityEffectSettings
                .GetRaisedPreviewZ((sbyte)groundZ)
                .Should()
                .Be((sbyte)expectedZ);
        }
    }
}
