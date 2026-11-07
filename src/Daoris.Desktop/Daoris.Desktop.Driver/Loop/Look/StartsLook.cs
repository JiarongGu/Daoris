namespace Daoris.Driver;

/// <summary>
/// Every planned start and every intake due begun at once, each to its record open or to what it came to, never waiting for a session
/// to end: what an earlier look let go of, said first as that look would have said it, and this look's own outcome after it. It leaves
/// the look's <see cref="StartsOutcome"/> for the passes after it and the report.
/// </summary>
[LookPass("start", LookPhase.Planned, "intakes")]
internal sealed class StartsLook(IStartsWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var state = round.State;
        var events = round.Events;
        var starts = state.Starts;

        // 🔴 One run per record the person's words wait on: a run that took a record up moves it to working a moment after the look
        // planned it, so a look in between still reads it waiting and plans it again, and a second run's move to working is refused
        // (working → working) and fails the session the first is resuming. Claimed here for the run's whole life, so a later look
        // begins nothing on it, and says why. After the intakes are counted, so the slot the record holds is counted as the plan counted it.
        var goingOn = new Dictionary<string, string>(StringComparer.Ordinal);
        var claimed = new List<Consideration>();
        foreach (var start in starts)
        {
            if (start is { GoesOn: true, Resumes: { } record })
            {
                if (!wiring.Runs.TryGoOn(record.Session))
                {
                    events.Add(wiring.Lines.Progress("going-on", $"going on  session {record.Session} (#{start.Quest.Id} → {start.Quest.To}): "
                        + "a run of this machine's already goes on with the person's words in it."));
                    continue;
                }

                goingOn[start.Quest.Id] = record.Session;
            }

            claimed.Add(start);
        }

        var beginning = new BegunStarts(wiring, events, goingOn);
        var running = new ClaimedStart(wiring, state.Snapshot.Repositories, goingOn);
        var config = wiring.Config;
        foreach (var start in claimed)
        {
            // The agent it runs on: a handed quest's own, which the held-wait report names its cooling by.
            var started = new StartBegun(
                start.Quest.Id, null, start.Quest.To, start.Workspace,
                StartPlan.AgentFor(
                    Driver.StartWork(start.Quest), config, start.Resumes, new HandedQuests(wiring.Home).Read(start.Quest.Id)?.Agent,
                    wiring.CatalogueForReport(), Driver.PlaceOf(start)), start);
            beginning.Begin(
                started, new StartReservation(start.Quest.Id, null, start.Quest.To, TreeGoneBackInto(start)),
                (own, opened) => running.RunAsync(start, own, opened, ct));
        }

        foreach (var ask in state.Intakes)
        {
            var started = new StartBegun(
                null, ask.Id, $"ask #{ask.Id}", ask.Workspace,
                StartPlan.AgentFor(ParticipantWork.Intake, config, catalogue: wiring.CatalogueForReport(), place: new RolePlace(ask.Workspace)));
            beginning.Begin(
                started, new StartReservation(null, ask.Id, $"ask #{ask.Id}", null), (own, opened) => wiring.RunIntakeAsync(ask, own, opened, ct));
        }

        var begun = await beginning.SettledAsync(ct).ConfigureAwait(false);

        // What the starts an earlier look let go of came to since, said first; this look's own outcome for the same quest is said after
        // it, and stands.
        IReadOnlyList<StartCame> outcomes = [.. wiring.Runs.TakeCame(), .. begun];
        var said = Fold(outcomes, events, state.Concluded);
        state.Outcome = said with { Waits = AccountWaits.Of(outcomes, wiring.Context.Harnesses, wiring.Service) };
    }

    /// <summary>
    /// The tree a start goes back into, where its repository opens trees: a further part's, a resume's or a carry-on's. A first start
    /// grows its own, and in a repository whose sessions run in its root, the root is held by its repository.
    /// </summary>
    private string? TreeGoneBackInto(Consideration start) =>
        wiring.Config.OpensOwnTree(start.Quest.To) ? start.Entry?.Into?.Tree ?? start.Resumes?.Tree : null;

    private StartsOutcome Fold(IReadOnlyList<StartCame> outcomes, List<string> events, List<SessionEnded> concluded)
    {
        var progressed = false;
        var heldAt = new Dictionary<string, string>(StringComparer.Ordinal);
        var signedOutAt = new Dictionary<string, SignedOutAccounts>(StringComparer.Ordinal);
        var untrusted = new List<TrustHold>();
        foreach (var came in outcomes)
        {
            untrusted.AddRange(came.Untrusted);
            if (came.Came is null)
            {
                // Null is a session opened, and running on: its ending joins a later look's report.
                progressed = true;
                if (came.Started.Quest is { } opened)
                {
                    heldAt.Remove(opened);
                    signedOutAt.Remove(opened);
                }

                // A line a limit cut off says that it went on, once, as its own news, not its end's; or, its own account ready again,
                // that it resumed its own conversation.
                if (LimitedStart.LineOf(came.Started) is { } wentOn)
                {
                    var (code, line) = wiring.LimitResumed.TryRemove(wentOn.Quest!, out _)
                        ? (LookSaid.LimitResumed, LimitedStart.ResumedLine)
                        : (LookSaid.LimitWentOn, LimitedStart.WentOnLine);
                    events.Add(wiring.Lines.News("start", wentOn with { Code = code }, $"start  #{wentOn.Quest}: {line}"));
                }

                continue;
            }

            // A start held on its accounts waits on the person (its row); one held otherwise is tried at a later look. A line a limit
            // cut off, held on cooling accounts, says it waits as its own news, once, the band saying the time.
            if (came.Came.Cooling is not null && LimitedStart.LineOf(came.Started) is { } limitWaits)
            {
                events.Add(wiring.Lines.News("start", limitWaits, came.Came.Line));
            }
            else
            {
                var startClass = came.Came is { Cooling: not null } or { SignedOut: not null } ? LookClass.Wait
                    : came.Came is { Held: not null, Ended: null } ? LookClass.Retry
                    : LookClass.Progress;
                events.Add(wiring.Lines.Of(startClass, "start", came.Came.Line));
            }

            progressed |= came.Came.Opened;
            if (came.Came.Ended is not null) concluded.Add(came.Came.Ended);
            if (came.Started.Quest is { } quest && came.Came.Held is not null) heldAt[quest] = came.Came.Held;
            if (came.Started.Quest is { } passed && came.Came.SignedOut is not null) signedOutAt[passed] = came.Came.SignedOut;
        }

        return new StartsOutcome(progressed, heldAt, signedOutAt, untrusted, []);
    }
}
