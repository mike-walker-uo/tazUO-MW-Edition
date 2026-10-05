using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    // These fixtures temporarily replace client-owned global session state.
    [CollectionDefinition("Client session regression", DisableParallelization = true)]
    public class ClientSessionRegressionCollection { }
}
