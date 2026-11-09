namespace Daoris.Driver;

/// <summary>What the loop's resolvers reach the sessions through: the service's records, the say door, and this machine's processes.</summary>
/// <param name="Records">Every session record the service keeps.</param>
/// <param name="Say">Words told to a session under a key, as Daoris's own: the conversation goes on with them at the next look.</param>
/// <param name="Running">Whether a process of the session runs here.</param>
/// <param name="Stop">The session's process ended for the driver's reason (the session's stop door); false where none runs here.</param>
public sealed record ResolverSessions(
    Func<CancellationToken, Task<IReadOnlyCollection<PriorSession>>> Records,
    Func<string, string, string, CancellationToken, Task<SayAnswer>> Say,
    Func<string, bool> Running,
    Func<string, Noted, bool> Stop)
{
    /// <summary>How often a wait reads the session again.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long a stop waits for the process to exit before it reads the tree as let go regardless.</summary>
    public TimeSpan StopWait { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// The loop's resolvers: the tree's newest session, its conversation going on. The ask is said through the say door under the row's ask
/// key, which the session's going on carries into its reserved tree; its turn has ended once its record holds that word no longer and nothing
/// of it runs. The stop goes through the session's stop door as the driver's, so the session is asked again rather than read as the person's stop.
/// </summary>
public sealed class SessionResolvers(ResolverSessions sessions) : IResolvers
{
    /// <summary>What a stop of the resolver's turn writes on its record.</summary>
    public static Noted Stopped => Noted.Of(EndNoteCodes.EndedResolveAsk,
        "Stopped by this machine's driver: the workflow's ask to settle a conflict in its tree ended. What it changed is kept for the next ask.");

    /// <inheritdoc/>
    public async Task<(string? Session, string? Why)> SessionAsync(TreeHandOff row, CancellationToken ct)
    {
        if (await NewestAsync(row.Tree, ct).ConfigureAwait(false) is not { } record) return (null, "No session worked in its tree here.");
        return Declined(record) ? (null, $"`{record.Session}` may not be asked: the person stopped or declined it.") : (record.Session, null);
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(TreeHandOff row, string session, string words, Func<bool> lapsed, CancellationToken ct)
    {
        if (!(await sessions.Say(session, words, row.Ask, ct).ConfigureAwait(false)).Kept) return ResolverEnds.Untold;
        while (true)
        {
            if (lapsed()) return ResolverEnds.Lapsed;
            var record = (await sessions.Records(ct).ConfigureAwait(false)).FirstOrDefault(each => each.Session == session);
            if (record is null || Declined(record)) return ResolverEnds.Declined;
            if (!record.Running && !sessions.Running(session) && !(record.Said ?? []).Any(word => word.Key == row.Ask)) return ResolverEnds.Ended;
            var left = row.Until - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero) return ResolverEnds.TimedOut;
            await Task.Delay(left < sessions.Poll ? left : sessions.Poll, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(TreeHandOff row, CancellationToken ct)
    {
        if (row.Session is not { } session || !sessions.Stop(session, Stopped)) return;
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(sessions.StopWait);
        try
        {
            while (sessions.Running(session)) await Task.Delay(sessions.Poll, bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Its process tree is ended already; a process that outlives the bound is no longer its turn.
        }
    }

    /// <summary>The newest record of this machine whose tree is <paramref name="tree"/>.</summary>
    private async Task<PriorSession?> NewestAsync(string tree, CancellationToken ct)
    {
        var key = TreeHandOffs.KeyOf(tree);
        return (await sessions.Records(ct).ConfigureAwait(false))
            .Where(record => !record.Teammate && record.Tree is { Length: > 0 } at && TreeHandOffs.KeyOf(at) == key)
            .MaxBy(record => record.Created ?? DateTimeOffset.MinValue);
    }

    /// <summary>Stopped by the person or declined: a stop of its own <c>resolve</c>'s is neither.</summary>
    private static bool Declined(PriorSession record) =>
        string.Equals(record.State, "declined", StringComparison.OrdinalIgnoreCase)
        || (record.PersonStopped && !(record.NoteParts ?? []).Any(part => part.Code == EndNoteCodes.EndedResolveAsk.Code));
}
