using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ContainerSearchQueryTests
    {
        [Theory]
        [InlineData(null, null, null, true)]
        [InlineData("   ", "Sword", "", true)]
        [InlineData("composite bow", "Exquisite Composite Bow", "", true)]
        [InlineData("composite bow", "Composite Longbow", "", false)]
        [InlineData("not present", "Sword", "", false)]
        [InlineData("not present", "Not Present", "", true)]
        [InlineData("gold or silver", "Gold or Silver", "", true)]
        [InlineData("swordfish", "Swordfish", "", true)]
        [InlineData("bow AND slayer", "Composite Bow", "Undead Slayer", true)]
        [InlineData("bow AND slayer", "Composite Bow", "Luck 100", false)]
        [InlineData("bow OR sword", "Exquisite Sword", null, true)]
        [InlineData("bow OR sword", "Robe", "", false)]
        [InlineData("NOT cursed", "Composite Bow", "Undead Slayer", true)]
        [InlineData("NOT cursed", "Composite Bow", "Cursed", false)]
        [InlineData("bow AND NOT cursed", "Composite Bow", "Undead Slayer", true)]
        [InlineData("bow AND NOT cursed", "Composite Bow", "Cursed", false)]
        [InlineData("bow OR sword AND NOT cursed", "Bow", "Cursed", true)]
        [InlineData("bow OR sword AND NOT cursed", "Sword", "Cursed", false)]
        [InlineData("(bow OR sword) AND NOT cursed", "Bow", "Cursed", false)]
        [InlineData("(bow OR sword) AND NOT cursed", "Sword", "Undead Slayer", true)]
        [InlineData("NOT(bow OR sword)", "Robe", "", true)]
        [InlineData("NOT(bow OR sword)", "Bow", "", false)]
        [InlineData("NOT NOT bow", "Bow", "", true)]
        [InlineData("((bow))", "Bow", "", true)]
        [InlineData("bow AND (slayer OR luck)", "Bow", "Luck 100", true)]
        [InlineData("bow AND (slayer OR luck)", "Robe", "Luck 100", false)]
        [InlineData("\"AND\"", "Sandals", "", true)]
        [InlineData("\"NOT cursed\"", "NOT cursed", "", true)]
        [InlineData("\"(Imbued)\" AND sword", "Sword", "(Imbued)", true)]
        [InlineData("\"hit fireball\" AND bow", "Bow", "Hit Fireball 50%", true)]
        [InlineData("BOW AND SLAYER", "Composite Bow", "Undead Slayer", true)]
        [InlineData("bow\tAND\nslayer", "Bow", "Undead Slayer", true)]
        public void Query_matches_phrases_and_boolean_expressions(string text, string name, string data, bool expected)
        {
            var query = ContainerSearchQuery.Parse(text);
            Assert.True(query.IsValid, query.Error);
            Assert.Equal(expected, query.Matches(name, data));
        }

        [Theory]
        [InlineData("AND")]
        [InlineData("OR sword")]
        [InlineData("NOT")]
        [InlineData("bow AND")]
        [InlineData("bow OR")]
        [InlineData("bow AND OR sword")]
        [InlineData("bow NOT cursed")]
        [InlineData("bow AND (slayer OR)")]
        [InlineData("(bow OR sword")]
        [InlineData("bow)")]
        [InlineData("()")]
        [InlineData("\"bow")]
        [InlineData("\"\"")]
        [InlineData("\"bow\" \"sword\"")]
        public void Incomplete_queries_report_errors_instead_of_matching_everything(string text)
        {
            var query = ContainerSearchQuery.Parse(text);
            Assert.False(query.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(query.Error));
            Assert.False(query.Matches("Bow", "Undead Slayer"));
        }

        [Theory]
        [InlineData("cursed", "Bow", null, false, "Pending")]
        [InlineData("NOT cursed", "Bow", null, false, "Pending")]
        [InlineData("NOT NOT cursed", "Bow", null, false, "Pending")]
        [InlineData("bow AND NOT cursed", "Bow", null, false, "Pending")]
        [InlineData("bow OR cursed", "Bow", null, false, "Match")]
        [InlineData("NOT cursed OR bow", "Bow", null, false, "Match")]
        [InlineData("NOT bow AND cursed", "Bow", null, false, "NoMatch")]
        [InlineData("NOT bow OR cursed", "Bow", null, false, "Pending")]
        [InlineData("NOT bow", "Bow", null, false, "NoMatch")]
        [InlineData("bow AND NOT cursed", "Bow", "", true, "Match")]
        [InlineData("bow AND NOT cursed", "Bow", "Cursed", true, "NoMatch")]
        [InlineData("NOT cursed", "Bow", "Cursed", true, "NoMatch")]
        [InlineData("NOT cursed", "Bow", "", true, "Match")]
        [InlineData("", null, null, false, "Match")]
        public void Missing_properties_stay_pending_without_losing_known_name_matches(string text, string name,
            string data, bool known, string expected)
        {
            var query = ContainerSearchQuery.Parse(text);
            Assert.Equal(expected, query.Evaluate(name, data, known).ToString());
        }
    }
}
