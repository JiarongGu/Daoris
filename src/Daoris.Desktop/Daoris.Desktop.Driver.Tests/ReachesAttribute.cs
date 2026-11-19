namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a test class runs that its files do not name, said for the reached checks (<c>tools/process-reach.mjs</c> reads it from the
/// text; nothing here reads it at run time): an area of a project the tests compile against (<c>Daoris.Desktop.Driver/Agents</c>),
/// or a path from the checkout's root of a project they compile none of (<c>src/Daoris.Service/Daoris.Service.Mcp</c>) or a data folder (<c>examples/plugins</c>), reached by a
/// change to that path alone. <see cref="Why"/> is required.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ReachesAttribute(params string[] areas) : Attribute
{
    public IReadOnlyList<string> Areas { get; } = areas;

    public string Why { get; init; } = "";
}
