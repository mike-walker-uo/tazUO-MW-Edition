using ClassicUO.Game.Managers;
using ClassicUO.Input;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class FocusAndTransferTests
    {
        [Fact]
        public void Focus_loss_clears_modifiers_but_the_next_real_key_event_can_set_them()
        {
            try
            {
                Keyboard.OnKeyDown(new SDL.SDL_KeyboardEvent { mod = SDL.SDL_Keymod.SDL_KMOD_LCTRL | SDL.SDL_Keymod.SDL_KMOD_LSHIFT | SDL.SDL_Keymod.SDL_KMOD_LALT });
                Assert.True(Keyboard.Ctrl && Keyboard.Shift && Keyboard.Alt);
                Keyboard.ResetModifiers();
                Assert.False(Keyboard.Ctrl || Keyboard.Shift || Keyboard.Alt);
                Keyboard.OnKeyDown(new SDL.SDL_KeyboardEvent { mod = SDL.SDL_Keymod.SDL_KMOD_LSHIFT });
                Assert.True(Keyboard.Shift);
                Assert.False(Keyboard.Ctrl || Keyboard.Alt);
            }
            finally { Keyboard.ResetModifiers(); }
        }
        [Fact]
        public void Compact_progress_includes_skipped_items_but_excludes_finished_queues()
        {
            var previous = QueuedOperations.Recent.ToArray();
            try
            {
                QueuedOperations.Recent.Clear();
                QueuedOperations.Recent.Add(new QueuedOperation("Move") { Total = 20, Sent = 5, Skipped = 2, SubmissionComplete = true });
                QueuedOperations.Recent.Add(new QueuedOperation("Move") { Total = 10, Sent = 1 });
                QueuedOperations.Recent.Add(new QueuedOperation("Done") { Total = 100, Sent = 100, SubmissionComplete = true });
                QueuedOperations.Recent.Add(new QueuedOperation("Cancelled") { Total = 100, Cancelled = true });
                Assert.Equal(2, QueuedOperations.ActiveProgress(out int completed, out int total));
                Assert.Equal(8, completed);
                Assert.Equal(30, total);
            }
            finally { QueuedOperations.Recent.Clear(); QueuedOperations.Recent.AddRange(previous); }
        }
    }
}
