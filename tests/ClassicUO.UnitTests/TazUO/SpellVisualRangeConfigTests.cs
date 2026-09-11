using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class SpellVisualRangeConfigTests
    {
        [Fact]
        public void Accepts_unique_nonnegative_spell_ids()
        {
            const string json = "[{\"ID\":1,\"Name\":\"Clumsy\"},{\"ID\":2,\"Name\":\"Create Food\"}]";

            SpellVisualRangeManager.TryParseConfiguration(json, out var result).Should().BeTrue();
            result.Keys.Should().BeEquivalentTo(1, 2);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("[{\"ID\":-1}]")]
        [InlineData("[{\"ID\":1},{\"ID\":1}]")]
        [InlineData("not json")]
        public void Rejects_invalid_config_without_partial_result(string json)
        {
            SpellVisualRangeManager.TryParseConfiguration(json, out var result).Should().BeFalse();
            result.Should().BeNull();
        }

        [Fact]
        public void New_import_cancels_and_supersedes_previous_request()
        {
            var lifecycle = new SpellVisualRangeImportLifecycle();
            SpellVisualRangeImportRequest oldRequest = lifecycle.Begin();

            SpellVisualRangeImportRequest currentRequest = lifecycle.Begin();

            oldRequest.CancellationToken.IsCancellationRequested.Should().BeTrue();
            lifecycle.IsCurrent(oldRequest).Should().BeFalse();
            lifecycle.IsCurrent(currentRequest).Should().BeTrue();
        }

        [Fact]
        public void Session_reset_cancels_current_import()
        {
            var lifecycle = new SpellVisualRangeImportLifecycle();
            SpellVisualRangeImportRequest request = lifecycle.Begin();

            lifecycle.Cancel();

            request.CancellationToken.IsCancellationRequested.Should().BeTrue();
            lifecycle.IsCurrent(request).Should().BeFalse();
        }
    }
}
