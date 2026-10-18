using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Network;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class PluginMovementThreadTests : IDisposable
    {
        private static readonly MethodInfo _requestMove = typeof(Plugin).GetMethod(
            "RequestMove", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly Thread _gameThread = CUOEnviroment.GameThread;
        private readonly PlayerMobile _player = World.Player;
        private readonly Action[] _queued = MainThreadQueue.QueuedActions.ToArray();

        public PluginMovementThreadTests()
        {
            TestLogging.EnsureInitialized();
            MainThreadQueue.Reset();
            CUOEnviroment.GameThread = Thread.CurrentThread;
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, null);
        }

        [Fact]
        public void Worker_movement_waits_for_game_thread_and_returns_its_result()
        {
            Task<bool> request = Task.Run(() => RequestMove());
            try
            {
                Assert.True(SpinWait.SpinUntil(
                    () => !MainThreadQueue.QueuedActions.IsEmpty || request.IsCompleted,
                    TimeSpan.FromSeconds(5)));
                Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out Action move));
                Assert.False(request.IsCompleted);

                move();

                Assert.True(request.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(request.Result);
                Assert.Empty(MainThreadQueue.QueuedActions);
            }
            finally
            {
                // Release a waiting worker even if an assertion fails.
                while (MainThreadQueue.QueuedActions.TryDequeue(out Action action)) action();
            }
        }

        [Fact]
        public void Game_thread_movement_runs_directly_and_rejects_missing_player()
        {
            Assert.False(RequestMove());
            Assert.Empty(MainThreadQueue.QueuedActions);
        }

        private static bool RequestMove() =>
            (bool)_requestMove.Invoke(null, new object[] { 0, true });

        public void Dispose()
        {
            MainThreadQueue.Reset();
            CUOEnviroment.GameThread = _gameThread;
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _player);
            foreach (Action action in _queued) MainThreadQueue.EnqueueAction(action);
        }
    }
}
