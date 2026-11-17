using System.Reflection;

namespace Daoris.Assistant.Tests;

/// <summary>
/// The package's shape: the core on the standard library and the ports alone, the fakes on the core, the nine ports the plan names,
/// and port records that carry names and states only, so a record never holds a type of the code behind the port and a second
/// package can promote a port's shape whole.
/// </summary>
public sealed class AssistantShapeTests
{
    private static readonly Assembly Core = typeof(IMachineFacts).Assembly;

    [Fact]
    public void The_core_references_the_standard_library_and_the_ports_alone()
    {
        var references = Core.GetReferencedAssemblies().Select(each => each.Name!).ToList();

        Assert.All(references, name => Assert.True(
            name is "System.Runtime" or "netstandard" or "Daoris.Ports" || name.StartsWith("System.", StringComparison.Ordinal),
            $"Daoris.Assistant references {name}"));
    }

    [Fact]
    public void The_fakes_reference_the_core_and_no_other_daoris_assembly_but_the_ports()
    {
        var references = typeof(ScriptedConversations).Assembly.GetReferencedAssemblies().Select(each => each.Name!).ToList();

        Assert.Contains("Daoris.Assistant", references);
        Assert.All(references.Where(name => name.StartsWith("Daoris.", StringComparison.Ordinal)), name => Assert.True(
            name is "Daoris.Assistant" or "Daoris.Ports", $"Daoris.Assistant.Testing references {name}"));
    }

    [Fact]
    public void The_ports_are_the_nine_the_plan_names()
    {
        var ports = Core.GetExportedTypes().Where(type => type.IsInterface && type.Namespace == "Daoris.Assistant.Ports").Select(type => type.Name).Order();

        Assert.Equal(
            ["IAssistantRecords", "ICheckouts", "IConversations", "IMachineFacts", "IPressRecords", "IReaderWalk", "IRoomFolder", "ITurnEvents", "IWindowPlaces"],
            ports);
    }

    [Fact]
    public void Every_port_record_carries_names_and_states_only()
    {
        var records = Core.GetExportedTypes().Where(type => type.Namespace == "Daoris.Assistant.Ports" && type.GetMethod("<Clone>$") is not null).ToList();

        Assert.NotEmpty(records);
        Assert.All(
            records.SelectMany(record => record.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => (record, property))),
            each => Assert.True(Allowed(each.property.PropertyType), $"{each.record.Name}.{each.property.Name} is a {each.property.PropertyType}"));
    }

    /// <summary>A name or a count (text, a flag, a number), a state (an enum of the ports), another port record, or a list or map of them.</summary>
    private static bool Allowed(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) return Allowed(inner);
        if (type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long)) return true;
        if (type.Namespace == "Daoris.Assistant.Ports" && (type.IsEnum || type.GetMethod("<Clone>$") is not null)) return true;
        if (!type.IsGenericType) return false;
        var shape = type.GetGenericTypeDefinition();
        return (shape == typeof(IReadOnlyList<>) || shape == typeof(IReadOnlyDictionary<,>)) && type.GetGenericArguments().All(Allowed);
    }
}
