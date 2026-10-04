using System.Globalization;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ItemPropertyCultureTests
    {
        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Decimal_item_properties_have_same_value_under_each_OS_locale(string culture)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var property = new ItemPropertiesData.SinglePropertyData("Weapon Speed 1.5 -2.25");
                Assert.Equal(1.5, property.FirstValue);
                Assert.Equal(-2.25, property.SecondValue);
            }
            finally { CultureInfo.CurrentCulture = original; }
        }
    }
}
