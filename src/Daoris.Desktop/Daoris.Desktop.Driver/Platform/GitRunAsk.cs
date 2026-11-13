namespace Daoris.Driver;

/// <summary>
/// One git asked of <see cref="GitScheduler"/>: its folder, its repository (a queue's key), its wait, and its whole start, built before it queues.
/// </summary>
/// <param name="Repository">The repository's common directory, or the folder where it has none.</param>
/// <param name="Wait">What its run waits on.</param>
internal sealed record GitRunAsk(string Folder, string Repository, GitWait Wait)
{
    /// <summary>The whole start as built (the file, the folder, every argument, every variable, the runner and the bound): an ask takes a
    /// queued ask's answer only under the same key. Null never shares: a start with input, a streamed read, a held act.</summary>
    public string? Start { get; init; }

    /// <summary>Whether it may be ended once nobody waits: a read that takes no lock.</summary>
    public bool Endable { get; init; }

    /// <summary>Whether it may write: nothing queued behind it in its repository passes it, and it passes nothing queued ahead.</summary>
    public bool Writes { get; init; }

    /// <summary>The folder it removes (a working tree), admitted once the reads inside it have ended or been dropped.</summary>
    public string? Removes { get; init; }

    /// <summary>The working tree it makes (a <c>worktree add</c>): a removal of that tree waits for it as for a git inside it.</summary>
    public string? Makes { get; init; }

    public string? Input { get; init; }

    /// <summary>The start as <see cref="GitCommands"/> reads it, its input included, which its runner writes and the gate reads.</summary>
    public GitCommandRead? Command { get; init; }
}

/// <summary>
/// A git the scheduler answered as git's failure without running it to its end: its tree being removed (<c>going</c>), its
/// repository holding a git that outlived its kill (<c>stalled</c>), or a re-entry deeper than the places lent (<c>depth</c>).
/// </summary>
internal sealed class GitUnplacedException(string word, string message) : Exception(message)
{
    /// <summary>The word the failure is said by: <c>going</c>, <c>stalled</c> or <c>depth</c>.</summary>
    public string Word { get; } = word;

    internal static GitUnplacedException Going(string folder) =>
        new("going", $"the tree {Path.GetFileName(folder.TrimEnd('/', '\\'))} is being removed, so its read was not answered");

    internal static GitUnplacedException Stalled(string repository) =>
        new("stalled", $"git is stalled in {Path.GetFileName(repository.TrimEnd('/', '\\'))}: a git there outlived its kill, so the background's asks of it are not queued until it exits");

    internal static GitUnplacedException Depth() =>
        new("depth", "a git asked from a hook of a hook of a hook is deeper than the places lent to re-entries, so it was refused");
}
