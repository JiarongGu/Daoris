namespace Daoris.Driver;

/// <summary>The step that chose a start's account, and the one that chose it among the rest where the first passed one.</summary>
public sealed record WalkChoice(WalkStep Step, WalkStep? Rest = null)
{
    /// <summary>
    /// The account <see cref="Step"/> weighed the one the start ran on against, where it is near or pace: near names
    /// the account passed, pace the one it is ahead of, so the start's first line can say what that account said.
    /// </summary>
    public string? Over { get; init; }

    /// <summary>The account <see cref="Rest"/> weighed it against, where that is near or pace.</summary>
    public string? RestOver { get; init; }
}
