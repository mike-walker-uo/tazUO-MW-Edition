using System.Text.RegularExpressions;
using ClassicUO.Utility;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class RegexHelperTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Cache_preserves_options_independent_of_request_order(bool insensitiveFirst)
        {
            string pattern = insensitiveFirst ? "^AuditOptionA$" : "^AuditOptionB$";
            RegexHelper.GetRegex(pattern, insensitiveFirst ? RegexOptions.IgnoreCase : RegexOptions.None);
            Regex sensitive = RegexHelper.GetRegex(pattern, RegexOptions.None);
            Regex insensitive = RegexHelper.GetRegex(pattern, RegexOptions.IgnoreCase);
            Assert.False(sensitive.IsMatch(pattern.Substring(1, pattern.Length - 2).ToLowerInvariant()));
            Assert.True(insensitive.IsMatch(pattern.Substring(1, pattern.Length - 2).ToLowerInvariant()));
            Assert.NotSame(sensitive, insensitive);
            Assert.Same(sensitive, RegexHelper.GetRegex(pattern, RegexOptions.Compiled));
        }
    }
}
