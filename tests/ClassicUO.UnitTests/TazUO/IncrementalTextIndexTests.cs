using System;
using System.Linq;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class IncrementalTextIndexTests
    {
        [Fact]
        public void Changed_item_replaces_old_terms_without_losing_unchanged_items()
        {
            var index = new IncrementalTextIndex();
            index.Update(1, "Ruby Ring Hit Chance Increase 10");
            index.Update(2, "Sapphire Ring Hit Chance Increase 15");
            index.Update(1, "Emerald Bracelet Mana Regeneration 2");
            Assert.Empty(index.Candidates(new[] { "ruby" }));
            Assert.Equal(new uint[] { 1 }, index.Candidates(new[] { "emerald", "mana" }).OrderBy(id => id));
            Assert.Equal(new uint[] { 2 }, index.Candidates(new[] { "ring", "hit chance" }).OrderBy(id => id));
        }

        [Fact]
        public void Substrings_and_phrases_prune_without_changing_the_final_matcher()
        {
            var index = new IncrementalTextIndex();
            index.Update(1, "Greater Heal potion");
            index.Update(2, "Greater Refresh potion");
            index.Update(3, "Heal scroll");
            Assert.Equal(new uint[] { 1 }, index.Candidates(new[] { "greater heal", "poti" }));
            Assert.Equal(new uint[] { 1, 3 }, index.Candidates(new[] { "HEAL" }).OrderBy(id => id));
            Assert.Empty(index.Candidates(new[] { "greater heal", "refresh" }));
        }

        [Fact]
        public void Remove_and_profile_clear_remove_every_posting()
        {
            var index = new IncrementalTextIndex();
            index.Update(1, "Nightshade"); index.Update(2, "Nightshade");
            index.Remove(1);
            Assert.Equal(new uint[] { 2 }, index.Candidates(new[] { "night" }));
            index.Clear();
            Assert.Empty(index.Candidates(new[] { "night" }));
            index.Update(1, "Garlic");
            Assert.Equal(new uint[] { 1 }, index.Candidates(new[] { "garlic" }));
        }

        [Theory]
        [InlineData("hi")]
        [InlineData("Änne")]
        [InlineData("宝石")]
        public void Queries_unsafe_for_pruning_fall_back_to_the_existing_evaluator(string term)
        {
            Assert.Null(new IncrementalTextIndex().Candidates(new[] { term }));
            Assert.Null(new IncrementalTextIndex().Candidates(Array.Empty<string>()));
        }

        [Fact]
        public void Known_ASCII_terms_can_prune_a_query_also_containing_short_or_Unicode_terms()
        {
            var index = new IncrementalTextIndex(); index.Update(1, "Änne's Ruby Ring"); index.Update(2, "Ruby Bracelet");
            Assert.Equal(new uint[] { 1 }, index.Candidates(new[] { "Änne", "ring", "hi" }));
        }
    }
}
