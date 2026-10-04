using System.Collections.Generic;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.Configuration
{
    public class CooldownConditionTests
    {
        [Theory]
        [InlineData("hue")]
        [InlineData("label")]
        [InlineData("duration")]
        [InlineData("trigger")]
        [InlineData("type")]
        public void Condition_count_never_exceeds_shortest_persisted_list(string shortened)
        {
            var profile = new Profile
            {
                Condition_Hue = new List<ushort> { 1, 2 },
                Condition_Label = new List<string> { "first", "second" },
                Condition_Duration = new List<int> { 1, 2 },
                Condition_Trigger = new List<string> { "first", "second" },
                Condition_Type = new List<int> { 0, 0 }
            };
            Assert.Equal(2, profile.CoolDownConditionCount);
            switch (shortened)
            {
                case "hue": profile.Condition_Hue.RemoveAt(1); break;
                case "label": profile.Condition_Label.RemoveAt(1); break;
                case "duration": profile.Condition_Duration.RemoveAt(1); break;
                case "trigger": profile.Condition_Trigger.RemoveAt(1); break;
                case "type": profile.Condition_Type.RemoveAt(1); break;
            }
            Assert.Equal(1, profile.CoolDownConditionCount);
            profile.Condition_Trigger = null;
            Assert.Equal(0, profile.CoolDownConditionCount);
        }
    }
}
