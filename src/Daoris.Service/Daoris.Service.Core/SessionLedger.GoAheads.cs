namespace Daoris.Knowledge;

/// <summary>
/// A session asks the person for a go-ahead (KNOWUSE1a, D135 §2): held once on the ask its work is for, named by the act.
/// </summary>
public sealed partial class SessionLedger
{
    /// <summary>
    /// Keep a session's request for the person's go-ahead on an act outside its repository, on the ask its work is for:
    /// joined to the go-ahead already asked for that act, where the ask holds one, and asked anew where it holds none.
    /// </summary>
    /// <remarks>
    /// <para><b>The ask is derived, never passed</b>, as a word's is (DRIFT1a): an intake names its ask, and a driven
    /// session's quest was asked by one when its sender is <c>ask #id</c>, a chain step included. A quest one repository
    /// asked of another, and a conversation, are on no ask, and the answer says what to do instead.</para>
    ///
    /// <para><b>One go-ahead per act</b> (<see cref="GoAheadAct"/>): the same kind, the same place, and every word of what
    /// the earlier act touches. Where the words share some but cannot tell, the request is asked once more as its own,
    /// naming the near one, so the person's yes never covers an act they did not read. Among several it joins, the one
    /// whose words are most precise answers it.</para>
    /// </remarks>
    public async Task<GoAheadOutcome> AskGoAheadAsync(
        string sessionId, string? kind, string? on, string? act, string? why, DateTimeOffset now, CancellationToken ct = default)
    {
        var named = GoAheadAct.Kind(kind);
        if (named is null)
        {
            return new(GoAheadRefusal.BadKind,
                $"`{kind?.Trim()}` is no kind of act a go-ahead names: one of write, release, push, sign-in or run — a write is a "
                + "change to a system outside this repository (its configuration, its data, its entries), a release deploys or "
                + "publishes, a push sends commits or opens a pull request, a sign-in is credentials, and a run runs something "
                + "against a system's data. Nothing was asked.");
        }

        var where = GoAheadAct.Where(on);
        var touches = act?.Trim() ?? "";
        var reason = why?.Trim() ?? "";
        if (where.Length == 0) return new(GoAheadRefusal.Empty, "A go-ahead names where the act lands: production, development, or the system's name. Nothing was asked.");
        if (GoAheadAct.Words(touches, named, where).Count == 0)
        {
            return new(GoAheadRefusal.Empty, "A go-ahead names what it touches, in a few words: the configuration, the entries, the report. Nothing was asked.");
        }

        if (reason.Length == 0) return new(GoAheadRefusal.Empty, "A go-ahead says why the work needs it, and exactly what you would do. Nothing was asked.");
        if (where.Length > GoAheads.ActLimit || touches.Length > GoAheads.ActLimit || reason.Length > GoAheads.WordsLimit)
        {
            return new(GoAheadRefusal.TooMuch,
                $"A go-ahead names its act in at most {GoAheads.ActLimit} characters for where and for what, and says why in at "
                + $"most {GoAheads.WordsLimit}. Nothing was asked.");
        }

        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null || session.Origin is not null)
        {
            return new(GoAheadRefusal.NotFound, $"No session `{sessionId}` of this machine's.");
        }

        const string Instead =
            "so no go-ahead is held for it: say exactly what you need and why in your last message, commit what you have, "
            + "and end your turn with the quest still taken.";
        var quest = session.Quest is { } questId ? await quests.FindAsync(questId, ct).ConfigureAwait(false) : null;
        var askId = session.Ask ?? AskDesk.AskOf(quest?.From);
        if (askId is null || asks is null)
        {
            return new(GoAheadRefusal.NoAsk, (session.Quest, quest) switch
            {
                (null, _) => $"Session `{session.Id}` is a conversation on no ask, {Instead}",
                (_, null) => $"Session `{session.Id}` works quest `#{session.Quest}`, which is not held here, {Instead}",
                _ when askId is null => $"Session `{session.Id}` works quest `#{quest.Id}`, which `{quest.From}` asked rather than an ask, {Instead}",
                _ => $"Session `{session.Id}` is on ask `#{askId}`, which is not held here, {Instead}",
            });
        }

        var request = new GoAheadRequest(session.Id, session.Quest, now, reason);
        var (found, decided) = await asks.DecideGoAheadsAsync(askId, entries => Decide(entries, named, where, touches, request), now, ct)
            .ConfigureAwait(false);
        if (!found || decided is null)
        {
            return new(GoAheadRefusal.NoAsk, $"Session `{session.Id}` is on ask `#{askId}`, which is not held here, {Instead}");
        }

        var (refusal, goAhead, join, near) = decided.Value;
        if (refusal is { } full) return new(GoAheadRefusal.TooMuch, full);
        return new(GoAheadRefusal.None, Said(askId, goAhead!, join, near), askId, goAhead, join);
    }

    /// <summary>
    /// The decision over the ask's go-aheads as stored: join the most precise one naming the act, else ask it anew, naming
    /// the first whose words shared some where they could not tell.
    /// </summary>
    private static (IReadOnlyList<(System.Text.Json.JsonElement Raw, GoAhead? Read)>? Next, (string? Refusal, GoAhead? GoAhead, GoAheadJoin Join, GoAhead? Near)? Result)
        Decide(IReadOnlyList<(System.Text.Json.JsonElement Raw, GoAhead? Read)> entries, string kind, string on, string act, GoAheadRequest request)
    {
        var held = entries.Select(entry => entry.Read).OfType<GoAhead>().ToList();
        var same = held
            .Where(goAhead => GoAheadAct.Match(goAhead, kind, on, act) == GoAheadMatch.Same)
            .OrderByDescending(goAhead => GoAheadAct.Words(goAhead.Act, goAhead.Kind, goAhead.On).Count)
            .ThenBy(goAhead => goAhead.Number)
            .FirstOrDefault();
        if (same is not null)
        {
            // The same session asking again is not a second request of its own: its first is the record.
            if (same.Asked.Any(earlier => earlier.Session == request.Session)) return (null, (null, same, GoAheadJoin.Joined, null));
            var joined = same with { Asked = [.. same.Asked, request] };
            return ([.. entries.Select(entry => entry.Read?.Number == same.Number ? (entry.Raw, joined) : entry)], (null, joined, GoAheadJoin.Joined, null));
        }

        if (held.Count >= GoAheads.Most)
        {
            return (null, ($"This ask holds {GoAheads.Most} go-aheads already, the most one ask holds: name what you need in your "
                + "last message instead, and end your turn with the quest still taken. Nothing was asked.", null, GoAheadJoin.New, null));
        }

        var near = held.FirstOrDefault(goAhead => GoAheadAct.Match(goAhead, kind, on, act) == GoAheadMatch.Unclear);
        var asked = new GoAhead(GoAheads.Highest(entries) + 1, kind, on, act) { Asked = [request], Near = near?.Number };
        return ([.. entries, (default, asked)], (null, asked, near is null ? GoAheadJoin.New : GoAheadJoin.Unmatched, near));
    }

    /// <summary>What the session is told: what it asked, or the go-ahead it joined and that go-ahead's answer, verbatim.</summary>
    private static string Said(string askId, GoAhead goAhead, GoAheadJoin join, GoAhead? near)
    {
        const string Stop =
            " Say in your last message that the work waits on it, by its number, commit what you have, and end your turn with "
            + "the quest still taken, or carry on with what does not need it. The person answers it on the ask, and every "
            + "session on the ask is handed their answer.";
        return join switch
        {
            GoAheadJoin.New => $"Asked as go-ahead {goAhead.Number} on ask `#{askId}`: {goAhead.Named}.{Stop}",
            GoAheadJoin.Unmatched =>
                $"The words could not tell whether this is go-ahead {near!.Number} on ask `#{askId}` ({near.Named}): they share "
                + "some of its words without naming all of them, so it could not match it, and it is asked once more as go-ahead "
                + $"{goAhead.Number}: {goAhead.Named}. Where it is the same act, ask in its words next time and it joins.{Stop}",
            _ => goAhead.State switch
            {
                GoAheadState.Approved =>
                    $"Go-ahead {goAhead.Number} on ask `#{askId}` already asks for this ({goAhead.Named}), and the person approved it "
                    + $"{GoAheads.When(goAhead.Answer!.At)}{Saying(goAhead.Answer)}. It is not asked again: do the act as approved, "
                    + "and no further.",
                GoAheadState.Refused =>
                    $"Go-ahead {goAhead.Number} on ask `#{askId}` already asks for this ({goAhead.Named}), and the person refused it "
                    + $"{GoAheads.When(goAhead.Answer!.At)}{Saying(goAhead.Answer)}. It is not asked again: do not do it, and where the "
                    + "work cannot finish without it, say so in your closing note.",
                _ =>
                    $"Go-ahead {goAhead.Number} on ask `#{askId}` already asks for this ({goAhead.Named}), first asked by session "
                    + $"`{goAhead.Asked[0].Session}` {GoAheads.When(goAhead.Asked[0].At)}, and the person has not answered it yet. It is "
                    + $"not asked again: name go-ahead {goAhead.Number} in your last message rather than listing it as a new question.",
            },
        };
    }

    private static string Saying(GoAheadAnswer answer) => answer.Words is { Length: > 0 } words ? $", saying: \"{words}\"" : "";
}
