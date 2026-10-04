using System;
using ClassicUO.Utility;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class ValueStringBuilderReplaceTests
    {
        [Theory]
        [InlineData("Cast {power}", "{power}", "In Flam", "Cast In Flam")]
        [InlineData("Cast {spell}", "{spell}", "Heal", "Cast Heal")]
        [InlineData("Cast {spell}", "{spell}", "Greater Heal", "Cast Greater Heal")]
        public void Replacing_spell_placeholders_preserves_prefix(string text, string oldValue, string newValue, string expected)
        {
            var builder = new ValueStringBuilder(stackalloc char[32]);
            builder.Append(text);
            builder.Replace(oldValue.AsSpan(), newValue.AsSpan());
            Assert.Equal(expected, builder.ToString());
        }

        [Theory]
        [InlineData("abc", "123", "prefix-123-suffix")]
        [InlineData("abc", "1", "prefix-1-suffix")]
        [InlineData("abc", "12345", "prefix-12345-suffix")]
        public void Replacement_in_a_range_uses_absolute_match_position(string oldValue, string newValue, string expected)
        {
            var builder = new ValueStringBuilder(stackalloc char[64]);
            builder.Append("prefix-abc-suffix");
            builder.Replace(oldValue.AsSpan(), newValue.AsSpan(), 5, 6);
            Assert.Equal(expected, builder.ToString());
        }
    }
}
