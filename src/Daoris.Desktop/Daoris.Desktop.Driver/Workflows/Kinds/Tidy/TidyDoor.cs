using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>A run's <c>tidy</c> to perform: its node, and the landing whose chain it ends, at the tip the landing recorded.</summary>
public sealed record TidyStart(string Unit, NodeDeclaration Node, string Repository, string Workspace, string Root, string Branch, string Tip);

/// <summary>The door a run's <c>tidy</c> is performed through.</summary>
public interface ITidyDoor : INodeDoor
{
    /// <summary>Starts the tidy off the pass, once per node and tip; false where one runs already or none may begin now.</summary>
    bool Start(TidyStart start);
}

/// <summary>
/// Performs a run's <c>tidy</c>: once the landing's pull request merged, each part of its chain (the session branch of every unit
/// the landing carries) is ended on the line. Its commits are kept on a recovery ref first, and its branch then goes through the journal's
/// delete (<see cref="JournaledDeletes"/>, the <c>ref.delete</c> act with its Undo), resting on that ref. What it ended is
/// written on the landing record (<see cref="ChainTidied"/>), the one judgement the keeper and the press read for those parts. It starts only
/// on a monitor's <c>when merged</c>, and where the request did not merge it ends nothing and fails nothing.
/// </summary>
/// <param name="home">The Daoris home whose landing and session records the tidy reads and writes.</param>
/// <param name="log">Told what a tidy came to; the machine log's line.</param>
/// <param name="runs">Whether a tidy may begin now: not while an update drains; null always may.</param>
/// <param name="performs">The work a started tidy does, a test's seam; null is <see cref="PerformAsync"/>.</param>
public sealed class TidyDoor(string home, Action<string>? log = null, Func<bool>? runs = null, Func<TidyStart, CancellationToken, Task>? performs = null) : ITidyDoor
{
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Start(TidyStart start)
    {
        if (runs is not null && !runs()) return false;
        var key = $"{start.Unit}\n{start.Node.Id}\n{start.Tip}";
        var gate = new TaskCompletionSource();
        if (!_running.TryAdd(key, gate.Task)) return false;
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await (performs?.Invoke(start, CancellationToken.None) ?? PerformAsync(start, CancellationToken.None)).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    log?.Invoke($"tidy  {start.Unit}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
                }
                finally
                {
                    _running.TryRemove(key, out _);
                    gate.TrySetResult();
                }
            });
        }

        return true;
    }

    /// <summary>Completes once every tidy started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => Task.WhenAll(_running.Values);

    /// <summary>The chain's parts ended and their branches removed, once its request merged; nothing otherwise.</summary>
    internal async Task PerformAsync(TidyStart start, CancellationToken ct)
    {
        var landings = new LandedBranches(home);
        if (landings.Of(start.Repository, start.Branch) is not { } landing || !string.Equals(landing.Tip, start.Tip, StringComparison.OrdinalIgnoreCase)) return;
        if (!PullRequestReading.IsMerged(landing) || TidyReading.Tidied(landing)) return;
        var trees = await TreeWorktrees.WorktreesAsync(start.Root, ct).ConfigureAwait(false);
        // The parts an earlier pass ended before one failed stand: their branches are gone now, so ending them again would leave them out.
        var before = landing.Tidied is { Partly: true } partly && string.Equals(partly.Tip, start.Tip, StringComparison.OrdinalIgnoreCase) ? partly.Parts : [];
        var parts = new List<TidiedPart>();
        foreach (var branch in PartsOf(landing))
        {
            if (before.FirstOrDefault(each => each.Branch == branch) is { } ended)
            {
                parts.Add(ended);
                continue;
            }

            try
            {
                if (await EndAsync(start, branch, trees, ct).ConfigureAwait(false) is { } part) parts.Add(part);
            }
            catch (Exception) when (parts.Count > 0)
            {
                try
                {
                    landings.Tidied(start.Repository, start.Branch, new ChainTidied(start.Tip, DateTimeOffset.UtcNow) { Parts = parts, Partly = true });
                }
                catch (Exception writing) when (writing is IOException or UnauthorizedAccessException)
                {
                    log?.Invoke($"tidy  {start.Unit}: the parts of `{start.Branch}`'s chain ended so far could not be written ({writing.Message.TrimEnd().TrimEnd('.')}).");
                }

                throw;
            }
        }

        landings.Tidied(start.Repository, start.Branch, new ChainTidied(start.Tip, DateTimeOffset.UtcNow) { Parts = parts });
        log?.Invoke($"tidy  {start.Unit}: {parts.Count(part => part.Kept is null)} of {parts.Count} part(s) of `{start.Branch}`'s chain went, "
                    + "each kept on a recovery ref.");
    }

    /// <summary>The session branches of the units the landing carries: its chain's parts, as the session record names them.</summary>
    private IEnumerable<string> PartsOf(LandedBranch landing) =>
        new SessionBranches(home).All()
            .Where(each => string.Equals(each.Repository, landing.Repository, StringComparison.OrdinalIgnoreCase)
                           && each.Unit is { } unit && landing.Units.Contains(unit, StringComparer.Ordinal))
            .Select(each => each.Branch)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// One part ended: its tip kept on a recovery ref in an act of the journal's, then its branch deleted through the journal resting on that
    /// ref, never one a tree has checked out (git's own delete refuses that, and the journal's does not ask). Null where its branch is gone already.
    /// </summary>
    private async Task<TidiedPart?> EndAsync(TidyStart start, string branch, IReadOnlyDictionary<string, string> trees, CancellationToken ct)
    {
        var full = $"refs/heads/{branch}";
        var (stands, at, _) = await WorkingTree.GitAsync(start.Root, ["rev-parse", "--verify", "--quiet", full], ct).ConfigureAwait(false);
        if (stands != 0 || !WorkingTree.IsCommitId(at.Trim())) return null;
        var tip = at.Trim();
        // Kept whether or not the branch goes: a part whose branch stays is ended all the same, and the press removes it on that record once
        // its tree is gone, resting on this ref.
        var (kept, why) = await RecoveryKeeps.KeepAsync(home, start.Root, start.Repository, branch, tip).ConfigureAwait(false);
        if (kept is null) return new TidiedPart(branch, tip) { Kept = $"its commits could not be kept on a recovery ref ({why})" };
        if (trees.GetValueOrDefault(branch) is { } tree) return new TidiedPart(branch, tip) { KeptOn = kept, Kept = $"it is checked out at {tree}" };
        var gone = await JournaledDeletes.DeleteAsync(home, start.Root, start.Repository, branch, tip, [], _ => LateAsync(start.Root, branch, tip), new RecoveryName(kept, false))
            .ConfigureAwait(false);
        return new TidiedPart(branch, tip) { KeptOn = kept, Act = gone.Act, Kept = gone.Kept ?? (gone.Said is { } said ? $"git would not delete it: {said}" : null) };
    }

    /// <summary>A tree that checked the branch out as it went puts it back as it stood; null goes on.</summary>
    private static async Task<string?> LateAsync(string root, string branch, string tip)
    {
        Dictionary<string, string> trees;
        try
        {
            trees = await TreeWorktrees.WorktreesAsync(root, CancellationToken.None).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            trees = new() { [branch] = "a tree git could not list" };
        }

        if (trees.GetValueOrDefault(branch) is not { } tree) return null;
        var back = await RefTransaction.RestoreDeleted($"refs/heads/{branch}", tip).RunAsync(root, CancellationToken.None).ConfigureAwait(false);
        return back.Done ? $"it was checked out at {tree} as it went, so it was put back where it was"
            : $"it was checked out at {tree} as it went, and could not be put back ({back.Said})";
    }
}
