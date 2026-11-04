namespace Daoris.Driver;

/// <summary>What one pass did, for the look's report, the machine log and the tests.</summary>
/// <param name="Armed">Whether a graph naming a plugin's node, a closure graph or a run bound to a graph version arms the runtime; false, the pass
/// read and wrote nothing.</param>
/// <param name="Evaluated">The units evaluated, by id.</param>
/// <param name="Said">What the pass says that no performed result does: a refused evaluation, a record that does not read.</param>
public sealed record NodeRuntimePass(bool Armed, IReadOnlyList<WorkPerformed> Performed, IReadOnlyList<string> Evaluated, IReadOnlyList<string> Said)
{
    public static NodeRuntimePass Idle { get; } = new(false, [], [], []);

    /// <summary>
    /// Whether the awaited set told differs from the one told before: the loop wakes once more, so a process the set now needs is started
    /// by the look's supervision and its start made, rather than at the look's deadline. The same set told again wakes nothing.
    /// </summary>
    public bool AwaitedChanged { get; init; }
}
