namespace Daoris.Driver;

/// <summary>
/// The look's tidy's hold on a repository for one tree's removal (<see cref="TreeLock.TryReplaying"/>): taken around the git writes
/// that remove one tree or branch, never across the walk that judges them. Either held, with the sessions asked again under it
/// (<see cref="AskAsync"/>), or refused, with what the tree is kept for and how it says so. Made in the flow that holds it, never inside
/// an async method of its own, since a flow's hold is carried to the writes under it by the flow's own context.
/// </summary>
internal sealed class TidyHold(
    TreeLock? held, string code, string said, Func<Task<(IReadOnlySet<string>? Busy, string? Why)>>? sessions = null) : IDisposable
{
    private IReadOnlySet<string>? _busy;
    private readonly System.Diagnostics.Stopwatch _since = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Whether the repository is held.</summary>
    public bool Held => held is not null;

    /// <summary>The trees sessions running or waiting name, asked under the hold; null until <see cref="AskAsync"/> answers.</summary>
    public IReadOnlySet<string>? Busy => _busy;

    /// <summary><see cref="TidyKept.Starting"/> where the lock is refused, <see cref="TidyKept.Sessions"/> where the service does not answer.</summary>
    public string Code { get; private set; } = code;

    /// <summary>What a tree kept for the refusal says.</summary>
    public string Said { get; private set; } = said;

    /// <summary>Asks which sessions run, under the hold; false, with the words, where it is refused or the service did not answer.</summary>
    public async Task<bool> AskAsync()
    {
        if (held is null || sessions is null) return false;
        var (busy, why) = await sessions().ConfigureAwait(false);
        if (busy is null)
        {
            (Code, Said) = (TidyKept.Sessions, why ?? "the sessions running were not said");
            return false;
        }

        _busy = busy;
        return true;
    }

    public void ReadsDone() => SetAsideFaults.Current?.HoldRead?.Invoke(_since.Elapsed);

    public void Dispose() => held?.Dispose();
}
