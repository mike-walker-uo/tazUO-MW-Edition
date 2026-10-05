using System;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class FeatureDiagnosticsTests : IDisposable
    {
        public FeatureDiagnosticsTests()
        {
            TestLogging.EnsureInitialized();
            FeatureDiagnostics.ResetSession();
        }

        public void Dispose()
        {
            FeatureDiagnostics.ResetSession();
            // Notices from this fixture must not reach a later session.
            while (MainThreadQueue.QueuedActions.TryDequeue(out var action)) action();
        }

        [Fact]
        public void New_session_restores_a_feature_isolated_after_three_failures()
        {
            for (int i = 0; i < 3; i++)
                FeatureDiagnostics.RecordFailure("Test feature", new InvalidOperationException("test"));
            Assert.True(FeatureDiagnostics.IsDisabled("test FEATURE"));

            FeatureDiagnostics.ResetSession();

            Assert.False(FeatureDiagnostics.IsDisabled("Test feature"));
            Assert.Empty(FeatureDiagnostics.Snapshot());
        }

        [Fact]
        public void Old_disable_notice_does_not_print_into_a_new_session()
        {
            for (int i = 0; i < 3; i++)
                FeatureDiagnostics.RecordFailure("Test feature", new InvalidOperationException("test"));
            Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out var notice));
            FeatureDiagnostics.ResetSession();

            // There is no GameController in this fixture; a stale GameActions.Print would throw.
            notice();
            Assert.Empty(FeatureDiagnostics.Snapshot());
        }
    }
}
