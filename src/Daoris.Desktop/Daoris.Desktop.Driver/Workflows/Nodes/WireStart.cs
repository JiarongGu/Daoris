using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// A start the evaluation proposes (ready → active), for its caller to apply through <see cref="NodeLifecycle.Apply"/> when it observes,
/// dispatches, offers the task or waits (PERFORM1, HUMAN1, WAITUNIT1). Its move names the binding and the run (FABRIC-WIRE1e); it carries
/// the trigger firings it consumes, for the record writer.
/// </summary>
public sealed record WireStart(string Node, NodeMove Move)
{
    public IReadOnlyList<WireFiring> Firings { get; init; } = [];
}
