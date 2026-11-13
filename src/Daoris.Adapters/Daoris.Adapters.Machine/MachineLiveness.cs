using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>The machine's answer to whether a process the application once started still runs, as the root hands it.</summary>
public sealed class MachineLiveness : IProcessLiveness
{
    public static MachineLiveness Shared { get; } = new();

    public bool Runs(int id, DateTimeOffset created) => ProcessLiveness.Alive(id, created.UtcTicks);
}
