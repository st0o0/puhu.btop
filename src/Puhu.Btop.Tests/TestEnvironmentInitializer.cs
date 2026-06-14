using System.Runtime.CompilerServices;

namespace Puhu.Btop.Tests;

internal static class TestEnvironmentInitializer
{
    /// <summary>
    /// Runs once before any test. Akka.Hosting.TestKit spins up a real <c>IHost</c> per test
    /// class; with config reload enabled each host registers file watchers, which on Linux CI
    /// can exhaust the inotify watch limit and hang the run. Disabling reload-on-change keeps
    /// the host lightweight and the run deterministic.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
}
