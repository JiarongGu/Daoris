namespace Daoris.Driver;

/// <summary>An open go-ahead on <paramref name="Ask"/> as a role's listing reads it: the newest live session that asked it, in <paramref name="Repository"/>.</summary>
public sealed record OpenGoAhead(string Ask, GoAheadView GoAhead, string Repository, string Session);

/// <summary>
/// The open go-aheads the <c>goahead</c> kind may list, read only where it could list one: every look and every terminal's
/// poll asks it, so it reads the service once at most, and not at all where no role could answer a go-ahead.
/// </summary>
public static class GoAheadsOpen
{
    /// <summary>
    /// The open go-aheads a role may be listed for, each with the newest live session that asked it and its repository. Nothing is read where
    /// no appointed role a terminal or an agent holds is granted <c>goahead.answer</c>, or no live session is on an ask; else the open asks
    /// are read in one list (<paramref name="asks"/>), and an ended session names no subject.
    /// </summary>
    public static async Task<IReadOnlyList<OpenGoAhead>> ReadAsync(
        IReadOnlyList<RoleRecord> appointed, IReadOnlyList<PriorSession> records, Func<CancellationToken, Task<IReadOnlyList<AskView>>> asks, CancellationToken ct)
    {
        if (!appointed.Any(Answers)) return [];
        var live = records.Where(record => !SessionStates.IsEnded(record.State)).ToList();
        var on = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ask in live.Select(record => RolesWorkOrders.Current.AskOfEntry(record.Entry)).OfType<string>()) on.TryAdd(ask.TrimStart('#'), ask);
        if (on.Count == 0) return [];
        var open = new List<OpenGoAhead>();
        foreach (var ask in await asks(ct).ConfigureAwait(false))
        {
            if (!on.TryGetValue(ask.Id.TrimStart('#'), out var named)) continue;
            foreach (var goAhead in (ask.GoAheads ?? []).Where(each => each.Answer is null))
            {
                // The newest asker still live names the subject: an ended session waits on nothing.
                var asker = goAhead.Asked.Reverse().Select(request => live.LastOrDefault(each => each.Session == request.Session))
                    .FirstOrDefault(each => each?.Repository is not null);
                if (asker is not null) open.Add(new OpenGoAhead(named, goAhead, asker.Repository!, asker.Session));
            }
        }

        return open;
    }

    // A role that could answer a go-ahead: held by a terminal or an agent (the person's rules answer none) and granted the act.
    private static bool Answers(RoleRecord role) =>
        role.Hold is { Holder: RoleHolders.Terminal or RoleHolders.Agent } hold && hold.Acts.Contains(GoAheadAuthority.Act, StringComparer.Ordinal);
}
