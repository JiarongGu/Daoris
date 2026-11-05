namespace Daoris.Driver;

/// <summary>
/// A native door's dialect as its adapter class reads and writes it (AGENTPROVIDER1 row d): the class's stream reader, new per session,
/// and its message and interrupt lines. Row k makes the reader its door's own over recorded frames; row t takes this bridge away.
/// </summary>
internal sealed class AdapterNativeDialect(ISessionAdapter adapter) : INativeDialect
{
    public IStreamMapper Mapper() =>
        adapter.StructuredOutput() ?? throw new DriverException($"the `{adapter.Name}` adapter maps no structured output.");

    public string FrameMessage(string text) => adapter.FrameMessage(text);

    public string? FrameInterrupt() => adapter.FrameInterrupt();
}
