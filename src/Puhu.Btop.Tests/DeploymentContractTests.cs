namespace Puhu.Btop.Tests;

/// <summary>
/// Guards the plugin's deployment contract. Puhu.Btop ships as a single DLL and the host
/// supplies every shared dependency (Akka, R3, Termina, ...). The plugin must therefore not
/// reference any assembly the host does not provide. The host references the "Servus" package
/// (Servus.dll) — it does NOT ship Servus.Akka.dll / Servus.Core.dll — so a reference to those
/// would make the released plugin fail to load with FileNotFoundException at actor creation and
/// the btop tab would silently show no data.
/// </summary>
public sealed class DeploymentContractTests
{
    [Theory(Timeout = 30000)]
    [InlineData("Servus.Akka")]
    [InlineData("Servus.Core")]
    public void Plugin_assembly_does_not_reference_host_absent_assembly(string forbidden)
    {
        var referenced = typeof(BtopPlugin).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(forbidden, referenced);
    }
}
