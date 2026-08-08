using System;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ProfileDataStoreTests
    {
        [Theory]
        [InlineData("../profile.json")]
        [InlineData("folder/settings.tsv")]
        public void RejectsPathsOutsideActiveProfile(string path)
        {
            Action act = () => ProfileDataStore.GetPath(path);
            act.Should().Throw<ArgumentException>();
        }
    }
}
