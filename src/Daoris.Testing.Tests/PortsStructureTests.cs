using System.Reflection;
using Daoris.Ports.Ambient;
using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Testing.Tests;

public sealed class PortsStructureTests
{
    [Fact]
    public void Ports_references_the_standard_library_alone()
    {
        var references = typeof(IGit).Assembly.GetReferencedAssemblies().Select(each => each.Name!).ToList();

        Assert.All(references, name => Assert.True(
            name == "System.Runtime" || name == "netstandard" || name.StartsWith("System.", StringComparison.Ordinal),
            $"Daoris.Ports references {name}"));
    }

    [Fact]
    public void Testing_references_the_ports_and_no_adapter()
    {
        var references = typeof(ScriptedGit).Assembly.GetReferencedAssemblies().Select(each => each.Name!).ToList();

        Assert.Contains("Daoris.Ports", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Daoris.Adapters.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_ambient_ports_are_unset_until_a_root_sets_them_once()
    {
        Assert.False(AmbientPorts.IsSet);
        Assert.Throws<InvalidOperationException>(AmbientPorts.Read);

        var clock = new MovedClock();
        AmbientPorts.Set(new PortSet { Clock = clock });

        Assert.Same(clock, AmbientPorts.Read().Clock);
        Assert.Throws<InvalidOperationException>(() => AmbientPorts.Set(new PortSet { Clock = clock }));
    }
}
