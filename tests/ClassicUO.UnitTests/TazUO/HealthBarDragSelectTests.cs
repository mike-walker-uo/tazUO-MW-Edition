using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class HealthBarDragSelectTests
    {
        [Theory]
        [InlineData(HealthBarDragFilter.AllMobiles, 0xFE)]
        [InlineData(HealthBarDragFilter.Players, 0x46)]
        [InlineData(HealthBarDragFilter.FriendlyPlayers, 0x06)]
        [InlineData(HealthBarDragFilter.Guild, 0x04)]
        [InlineData(HealthBarDragFilter.HostileMobiles, 0x70)]
        [InlineData(HealthBarDragFilter.GreyAndHostileMobiles, 0x78)]
        [InlineData(HealthBarDragFilter.NeutralMobiles, 0x08)]
        public void FiltersMatchOnlyRequestedNotorieties(HealthBarDragFilter filter, int expectedMask)
        {
            for (int notoriety = 1; notoriety <= 7; notoriety++)
                Assert.Equal((expectedMask & (1 << notoriety)) != 0,
                    HealthBarDragSelect.Matches(filter, (NotorietyFlag)notoriety));
        }

        [Theory]
        [InlineData(false, false, false, 0)]
        [InlineData(true, false, false, 1)]
        [InlineData(false, true, false, 2)]
        [InlineData(false, false, true, 3)]
        [InlineData(true, true, false, 4)]
        [InlineData(true, false, true, 5)]
        [InlineData(false, true, true, 6)]
        [InlineData(true, true, true, 7)]
        public void AssignedChordTemporarilyOverridesPermanentFilter(bool ctrl, bool shift, bool alt, int modifier)
        {
            var profile = new Profile { DragSelectFilter = HealthBarDragFilter.Guild };
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.HostileMobiles, modifier);

            bool overridden = HealthBarDragSelect.TryGetOverride(profile, ctrl, shift, alt, out HealthBarDragFilter filter);

            Assert.Equal(modifier != 0, overridden);
            Assert.Equal(modifier != 0 ? HealthBarDragFilter.HostileMobiles : HealthBarDragFilter.Guild, filter);
            Assert.Equal(HealthBarDragFilter.Guild, profile.DragSelectFilter);
            Assert.False(HealthBarDragSelect.TryGetOverride(profile, false, false, false, out filter));
            Assert.Equal(HealthBarDragFilter.Guild, filter);
        }

        [Fact]
        public void AdditionalKeysDoNotActivateSingleKeyOverride()
        {
            var profile = new Profile { DragSelectFilter = HealthBarDragFilter.NeutralMobiles };
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.Players, 1);

            Assert.False(HealthBarDragSelect.TryGetOverride(profile, true, true, false, out HealthBarDragFilter filter));
            Assert.Equal(HealthBarDragFilter.NeutralMobiles, filter);
        }

        [Fact]
        public void ReassigningModifierMovesBindingAndPreservesOtherBindings()
        {
            var profile = new Profile();
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.Players, 1);
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.Guild, 2);
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.HostileMobiles, 1);

            Assert.Equal(0, profile.DragSelectFilterModifiers[(int)HealthBarDragFilter.Players]);
            Assert.Equal(2, profile.DragSelectFilterModifiers[(int)HealthBarDragFilter.Guild]);
            Assert.True(HealthBarDragSelect.TryGetOverride(profile, true, false, false, out HealthBarDragFilter filter));
            Assert.Equal(HealthBarDragFilter.HostileMobiles, filter);

            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.HostileMobiles, 0);
            Assert.False(HealthBarDragSelect.TryGetOverride(profile, true, false, false, out filter));
            Assert.Equal(2, profile.DragSelectFilterModifiers[(int)HealthBarDragFilter.Guild]);
        }

        [Fact]
        public void InvalidSavedSettingsNormalizeWithoutLosingValidBindings()
        {
            var profile = new Profile
            {
                DragSelectFilter = (HealthBarDragFilter)99,
                DragSelectFilterModifiers = new[] { 1, 1, -1, 8, 7 }
            };

            HealthBarDragSelect.NormalizeSettings(profile);

            Assert.Equal(HealthBarDragFilter.AllMobiles, profile.DragSelectFilter);
            Assert.Equal(new[] { 1, 0, 0, 0, 7, 0, 0 }, profile.DragSelectFilterModifiers);
        }

        [Fact]
        public void NullSavedModifiersDefaultToNoOverrides()
        {
            var profile = new Profile { DragSelectFilterModifiers = null };

            HealthBarDragSelect.NormalizeSettings(profile);

            Assert.Equal(new int[7], profile.DragSelectFilterModifiers);
        }

        [Fact]
        public void ProfileRoundTripPreservesFilterAndOverrides()
        {
            var profile = new Profile { DragSelectFilter = HealthBarDragFilter.GreyAndHostileMobiles };
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.AllMobiles, 3);
            HealthBarDragSelect.AssignModifier(profile, (int)HealthBarDragFilter.FriendlyPlayers, 4);

            string json = JsonSerializer.Serialize(profile, typeof(Profile), ProfileJsonContext.DefaultToUse);
            var restored = JsonSerializer.Deserialize(json, typeof(Profile), ProfileJsonContext.DefaultToUse) as Profile;

            Assert.NotNull(restored);
            Assert.Equal(profile.DragSelectFilter, restored.DragSelectFilter);
            Assert.Equal(profile.DragSelectFilterModifiers, restored.DragSelectFilterModifiers);
        }

        [Fact]
        public void ExistingProfileWithoutNewSettingsDefaultsToAllMobiles()
        {
            var profile = JsonSerializer.Deserialize("{}", typeof(Profile), ProfileJsonContext.DefaultToUse) as Profile;

            Assert.NotNull(profile);
            Assert.Equal(HealthBarDragFilter.AllMobiles, profile.DragSelectFilter);
            Assert.Equal(new int[7], profile.DragSelectFilterModifiers);
        }
    }
}
