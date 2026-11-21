using static Daoris.Driver.DriverBrainstorms;

namespace Daoris.Driver;

/// <summary>A brainstorm's one pass, and a meeting's turn run as one, on its owner's slots, clock and turns.</summary>
internal sealed class BrainstormPasses(DriverBrainstorms owner, LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    private TimeProvider Clock => owner.Clock;

    private TimeSpan Tick => owner.Tick;

    private bool TrySlot() => owner.TrySlot();

    private void FreeSlot() => owner.FreeSlot();

    private Task<BrainstormTurnRun> TurnAsync(BrainstormTurn turn, CancellationToken ct) => owner.TurnAsync(turn, ct);

    /// <summary>
    /// One pass: a slot under the cap, waited for until <paramref name="until"/>; then its turn in a fresh
    /// room within its own minutes from its slot, cut at <paramref name="until"/>; its answer validated by <paramref name="check"/>,
    /// and where that failed its one repair, handed the codes, within the same minutes. A limit starts it once more instead, as that
    /// repair; a crash is a turn with no answer; the person's stop ends it. Its record is written at each turn's start, so a restart
    /// knows what ran, and whatever it came to. A meeting's turn is one such pass, run by
    /// <paramref name="run"/>: a meeting's runner, never the brainstorm's.
    /// </summary>
    /// <param name="run">How its turns run; null for a brainstorm's: its test stand-in, else <see cref="TurnAsync"/>.</param>
    /// <param name="workspace">The workspace its turns ask their accounts for (<see cref="BrainstormTurn.Workspace"/>); null for the machine's.</param>
    internal async Task<(BrainstormPassRecord Record, T? Valid)> PassAsync<T>(
        string id, string pass, string agent, string role, IReadOnlyList<RoomFile> packet, string instruction, byte[]? proposals,
        Func<string?, (T? Valid, IReadOnlyList<string> Codes)> check, BrainstormFiles files, TimeSpan own, DateTimeOffset until,
        CancellationToken stop, CancellationToken ct, Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? run = null,
        ActorEffective? entry = null, string? workspace = null)
        where T : class
    {
        run ??= _context.BrainstormTurns ?? TurnAsync;
        var family = AgentFamily.Of(_context.Adapters, agent);
        var maker = family.Maker ?? family.Owner;
        string? account = null;
        // The accounts this voice passed for its model: its one bounded restart leaves them out, never a cool-off.
        var passedAccounts = new List<string>();
        (BrainstormPassRecord, T?) Ended(string state, IReadOnlyList<string> codes, int turns, T? valid = null, string? why = null)
        {
            var prior = files.ReadPasses(id).FirstOrDefault(each => each.By.Pass == pass);
            var record = new BrainstormPassRecord(new BrainstormBy(pass, maker, agent, account), role, state, codes, turns)
            {
                Actor = "brainstorm", Choice = entry?.From ?? "before", Asked = entry?.Asked ?? ActorAsked.Before(agent), Ran = prior?.Ran, Why = why,
            };
            files.WritePass(id, record);
            return (record, valid);
        }

        // The slot is waited for until the deadline; false where the person's stop or the deadline came first, said by the caller.
        async Task<bool> SlotAsync()
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (stop.IsCancellationRequested) return false;
                // The deadline first: a slot let go at the deadline starts nothing.
                if (Clock.GetUtcNow() >= until) return false;
                if (TrySlot()) return true;
                try
                {
                    await Task.Delay(Tick, Clock, stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // The person's stop: said at the loop's top.
                }
            }
        }

        if (!await SlotAsync().ConfigureAwait(false))
        {
            return stop.IsCancellationRequested ? Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], 0) : Ended(BrainstormPassRecord.Unavailable, [CapFull], 0);
        }

        var holding = true;
        try
        {
            var ownEnd = Clock.GetUtcNow() + own;
            var end = ownEnd < until ? ownEnd : until;
            var cutCode = until < ownEnd ? Deadline : TimedOut;
            IReadOnlyList<string> codes = [];
            var restart = false;
            var waits = 0;
            // A valid answer a turn gave before its limit: the pass's, on the account that gave it, where the retry ends with none.
            (T Valid, string? Account)? soFar = null;
            (BrainstormPassRecord, T?) Short(string state, IReadOnlyList<string> said, int turns, string? why = null)
            {
                if (soFar is not { } kept) return Ended(state, said, turns, why: why);
                account = kept.Account;
                return Ended(BrainstormPassRecord.Answered, BrainstormPassRecord.CodesOf(kept.Valid), turns, kept.Valid);
            }

            for (var turns = 1; turns <= 2; turns++)
            {
                ct.ThrowIfCancellationRequested();
                if (stop.IsCancellationRequested) return Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], turns - 1);
                var left = end - Clock.GetUtcNow();
                if (left <= TimeSpan.Zero) return Short(BrainstormPassRecord.Failed, [.. codes, cutCode], turns - 1);

                var turn = turns == 1 ? pass : $"{pass}r";
                var said = turns == 1 || restart ? instruction : BrainstormInstruction.Repair(instruction, codes);
                Ended(BrainstormPassRecord.Running, [], turns);
                var room = files.OpenRoom(id, turn, packet, said, proposals);
                using var limit = new CancellationTokenSource(left, Clock);
                using var window = CancellationTokenSource.CreateLinkedTokenSource(stop, limit.Token);
                BrainstormTurnRun ran;
                var cut = false;
                try
                {
                    ran = await run(
                        new BrainstormTurn(id, turn, agent, room, said, files.AnswerPath(id, turn), Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)))
                        {
                            Entry = entry, Passed = passedAccounts.Count > 0 ? [.. passedAccounts] : null, Workspace = workspace,
                        },
                        window.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (window.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    cut = true;
                    ran = new BrainstormTurnRun(true, null, null, $"stopped  brainstorm turn {turn}");
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // A crash ends the turn with no answer: its pass alone fails, after its repair, never the brainstorm.
                    ran = new BrainstormTurnRun(true, null, Crashed, $"crashed  brainstorm turn {turn}: {error.Message}");
                }

                account = ran.Account ?? account;
                if (!ran.Ran && ran.Code == StartPasses.Cooling && ran.Lifts is { } lifts && lifts < until && waits < MostWaits)
                {
                    // the voice's accounts all cool, and the first lifts before the deadline: the pass waits for it, its slot
                    // let go meanwhile, and its own minutes start when it runs. A cool-off that outlasts the deadline is not waited for.
                    waits++;
                    FreeSlot();
                    holding = false;
                    try
                    {
                        var wait = lifts - Clock.GetUtcNow();
                        if (wait > TimeSpan.Zero) await Task.Delay(wait, Clock, stop).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // The person's stop: said at the loop's top.
                    }

                    var again = false;
                    if (!stop.IsCancellationRequested)
                    {
                        if (!await SlotAsync().ConfigureAwait(false))
                        {
                            return stop.IsCancellationRequested
                                ? Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], turns)
                                : Short(BrainstormPassRecord.Unavailable, [StartPasses.Cooling, CapFull], turns);
                        }

                        again = true;
                    }

                    holding = again;
                    ownEnd = Clock.GetUtcNow() + own;
                    end = ownEnd < until ? ownEnd : until;
                    cutCode = until < ownEnd ? Deadline : TimedOut;
                    turns--;
                    continue;
                }

                if (!ran.Ran)
                {
                    // The walk's sentence is kept on the record and written to the machine log once, here, where the pass ends.
                    _driver.Log?.Warn(
                        "brainstorm.pass", ("brainstorm", id), ("pass", pass), ("agent", agent), ("code", ran.Code ?? "not-started"), ("why", ran.Why ?? ""));
                    return Short(
                        BrainstormPassRecord.Unavailable, restart ? [Limit, ran.Code ?? "not-started"] : [ran.Code ?? "not-started"], turns, ran.Why);
                }

                if (ran.Code is Limit or AgentOffers.NotOffered && !restart && !cut)
                {
                    // The account cooled where the turn ran, no strike is counted, and the pass starts once more on the next
                    // ready account of that agent's walk, as its one repair, within its minutes. So does an account whose agent
                    // refused the voice's model at the open, that account passed for the voice and judged by the next account's reading;
                    // with none that offers it, the voice is dropped (below), never replaced by another entry or agent.
                    codes = [ran.Code];
                    if (check(files.ReadAnswer(id, turn)).Valid is { } given) soFar = (given, ran.Account ?? account);
                    if (ran.Code == AgentOffers.NotOffered && ran.Account is { Length: > 0 } refusedAt) passedAccounts.Add(refusedAt);
                    restart = true;
                    continue;
                }

                var (valid, failed) = check(files.ReadAnswer(id, turn));
                if (valid is not null) return Ended(BrainstormPassRecord.Answered, BrainstormPassRecord.CodesOf(valid), turns, valid);
                if (cut || window.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    return stop.IsCancellationRequested
                        ? Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], turns)
                        : Short(BrainstormPassRecord.Failed, [cutCode], turns);
                }

                codes = ran.Code is { } code ? [.. failed, code] : failed;
                restart = false;
            }

            return Short(BrainstormPassRecord.Failed, codes, 2);
        }
        finally
        {
            if (holding) FreeSlot();
        }
    }
}
