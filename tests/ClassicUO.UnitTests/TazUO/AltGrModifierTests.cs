using ClassicUO.Input;
using FluentAssertions;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class AltGrModifierTests
    {
        [Fact]
        public void RightAltIsReportedAsControlAltForPluginCompatibility()
        {
            SDL.SDL_Keymod normalized = Keyboard.NormalizeAltGr(SDL.SDL_Keymod.SDL_KMOD_RALT);

            (normalized & SDL.SDL_Keymod.SDL_KMOD_RALT)
                .Should().Be(SDL.SDL_Keymod.SDL_KMOD_RALT);
            (normalized & SDL.SDL_Keymod.SDL_KMOD_LCTRL)
                .Should().Be(SDL.SDL_Keymod.SDL_KMOD_LCTRL);
        }

        [Fact]
        public void LeftAltDoesNotGainControlModifier()
        {
            SDL.SDL_Keymod normalized = Keyboard.NormalizeAltGr(SDL.SDL_Keymod.SDL_KMOD_LALT);

            normalized.Should().Be(SDL.SDL_Keymod.SDL_KMOD_LALT);
        }
    }
}
