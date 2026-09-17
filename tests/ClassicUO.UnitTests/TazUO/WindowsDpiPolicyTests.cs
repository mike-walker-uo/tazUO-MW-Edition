using ClassicUO;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WindowsDpiPolicyTests
    {
        [Fact]
        public void DefaultsToWindowsSystemScaling()
        {
            WindowsDpiPolicy.ResolveAwareness(false, null)
                .Should().Be(WindowsDpiPolicy.SystemScaledAwareness);
        }

        [Fact]
        public void NativeDpiUsesPerMonitorV2Awareness()
        {
            WindowsDpiPolicy.ResolveAwareness(true, null)
                .Should().Be(WindowsDpiPolicy.NativeAwareness);
        }

        [Fact]
        public void PreservesExplicitEnvironmentOverride()
        {
            WindowsDpiPolicy.ResolveAwareness(false, "system")
                .Should().Be("system");
        }

        [Fact]
        public void NativeDpiOverridesEnvironmentSetting()
        {
            WindowsDpiPolicy.ResolveAwareness(true, "unaware")
                .Should().Be(WindowsDpiPolicy.NativeAwareness);
        }
    }
}
