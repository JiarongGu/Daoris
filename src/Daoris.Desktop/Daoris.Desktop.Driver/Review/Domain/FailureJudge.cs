using System.Globalization;
using System.Runtime.ExceptionServices;

namespace Daoris.Driver;

/// <summary>What a prefix's selection run answered: it passed, it failed with a signature, or it could not run (it fails another way, a build that does not build).</summary>
internal enum PrefixResult
{
    Pass,
    Fail,
    Cannot,
}

/// <summary>One prefix run's answer: the result, and the signature of the failure it ended with (none for a pass).</summary>
internal sealed record PrefixOutcome(PrefixResult Result, FailureSignature? Signature = null)
{
    internal static readonly PrefixOutcome Passed = new(PrefixResult.Pass);

    internal static PrefixOutcome Failed(FailureSignature signature) => new(PrefixResult.Fail, signature);

    internal static PrefixOutcome Cannot(FailureSignature signature) => new(PrefixResult.Cannot, signature);
}

/// <summary>Whether a failure names a test or only the gate that failed.</summary>
internal enum FailureNames
{
    Test,
    Gate,
}

/// <summary>A failure of a candidate's gate as the judge takes it: the gate, the test it names (none for a gate that names none), and its signature.</summary>
internal sealed record JudgedFailure(FailureNames Names, string Gate, string? Test, FailureSignature Signature)
{
    /// <summary>What the failure is called in a hand-back and a record: its test, else its gate.</summary>
    public string Label => Test ?? Gate;
}

/// <summary>The open known red a failure's signature matches: whether its 72 hours are past, and the commit it was last seen failing on.</summary>
internal sealed record KnownRed(bool Expired, string? LastSeen);

/// <summary>A reason the whole admission could not be shown, where it arose (<c>start</c>, <c>beat</c>, <c>end</c>).</summary>
internal sealed record WholeDoubt(string At, string Why);

/// <summary>What the admission shows of a confirm run: whether it held the whole machine and nothing else did, the doubts that say otherwise, and the wait.</summary>
internal sealed record WholeEvidence(bool Conclusive, IReadOnlyList<WholeDoubt> Doubts, long? WaitedMs = null, IReadOnlyList<string>? Ids = null);

/// <summary>The composition a confirm run kept: its units (null where it ran whole) and the digest of its composition.</summary>
internal sealed record ConfirmComposition(IReadOnlyList<string>? Units, string? Digest);

/// <summary>The words a confirm run's reading is one of; anything else reads inconclusive.</summary>
internal static class ConfirmResults
{
    internal const string Confirmed = "confirmed";
    internal const string Unconfirmed = "unconfirmed";
    internal const string Inconclusive = "inconclusive";
    internal const string Dropped = "dropped";
}

/// <summary>A failure's reading by its gate's confirm run (<see cref="FailureJudge.ConfirmReadings"/>), as <see cref="JudgeContext.Confirm"/> hands it to the judgement.</summary>
internal sealed record ConfirmReading(string? Result, string? Why = null)
{
    public WholeEvidence? Evidence { get; init; }

    public ConfirmComposition? Composition { get; init; }
}

/// <summary>How a failure was judged.</summary>
internal enum JudgeOutcome
{
    /// <summary>Filed as known red, pinned to none.</summary>
    Inherited,

    /// <summary>It stops the line (<see cref="Judged.Why"/> says <c>base</c> or <c>expired</c>).</summary>
    Stop,

    /// <summary>Pinned to a member.</summary>
    Pinned,

    /// <summary>The prefixes did not place it.</summary>
    Unresolved,

    /// <summary>A sighting: it did not fail again on the confirm run. Never a pass.</summary>
    Unconfirmed,

    /// <summary>The confirm run could not show it. Read as split, never land.</summary>
    Inconclusive,

    /// <summary>The candidate was dropped during its confirm run.</summary>
    Dropped,
}

/// <summary>One run the judgement made: a prefix (the base plus its first <c>k</c> members), or a member run <see cref="Alone"/> on the base.</summary>
internal sealed record JudgeRun(int Prefix, bool Alone, PrefixOutcome Outcome);

/// <summary>A failure judged: how, the member it is pinned to with the ones it needs and the ones it interacts with, why, and every run it made.</summary>
internal sealed record Judged(JudgeOutcome Outcome, IReadOnlyList<JudgeRun> Runs)
{
    public int? Member { get; init; }

    public IReadOnlyList<int> With { get; init; } = [];

    public IReadOnlyList<int> Interacting { get; init; } = [];

    public string? Why { get; init; }

    public string? Said { get; init; }

    public ConfirmReading? Confirm { get; init; }
}

/// <summary>
/// What a judgement is handed: the candidate's member count, the function that runs the failure's selection on a prefix (<c>0</c> the base,
/// <see cref="Members"/> the whole candidate), the one that runs it on the base with a member alone, the known red its signature matches, the
/// candidate's base, and the confirm run's reading, asked only where the judgement reaches that step.
/// </summary>
internal sealed record JudgeContext(int Members, Func<JudgedFailure, int, PrefixOutcome> Run)
{
    public Func<JudgedFailure, int, PrefixOutcome>? Alone { get; init; }

    public KnownRed? Known { get; init; }

    public string? BaseCommit { get; init; }

    public Func<ConfirmReading?>? Confirm { get; init; }
}

/// <summary>Where the prefixes place a failure: pinned to a member with the earlier transitions interacting, or unresolved and why.</summary>
internal sealed record Placement(bool Pinned, int Member, IReadOnlyList<int> Interacting, bool Monotonic, string? Why);

/// <summary>What taking the whole admission answered: held, with the function that ends it and answers its evidence, or not held, because of a drop or for a reason.</summary>
internal sealed record WholeTake(bool Held, Func<WholeEvidence?>? End = null, bool Dropped = false, string? Why = null, WholeEvidence? Evidence = null);

/// <summary>The failing gate's run once more as composed: <c>pass</c>, <c>fail</c> (with the failures it had, a null for one that has no signature), or another word with its own <see cref="Why"/>.</summary>
internal sealed record GateRerun(string? Result, string? Why = null)
{
    public IReadOnlyList<JudgedFailure?>? Failures { get; init; }

    public ConfirmComposition? Composition { get; init; }
}

/// <summary>A confirm run's answer (<see cref="FailureJudge.ConfirmStep"/>): <c>dropped</c>, <c>inconclusive</c>, <c>unconfirmed</c> or <c>confirmed</c>.</summary>
internal sealed record ConfirmAnswer(string Result, string Why, WholeEvidence? Evidence, ConfirmComposition? Composition)
{
    public IReadOnlyList<JudgedFailure>? Failures { get; init; }

    public Exception? Error { get; init; }
}

/// <summary>The gate's attempts so far, one death of memory per attempt: whether it died and the peak commit its own job reached.</summary>
internal sealed record MemoryAttempt(bool Died, double? PeakGb = null);

/// <summary>What happens next to a gate that may have died of memory: <c>run</c>, <c>pass</c>, <c>again</c>, <c>hold</c> or <c>judge</c>.</summary>
internal sealed record MemoryStepResult(string Next, string? Note = null);

/// <summary>A recheck of a stopped queue's gate selection on the base.</summary>
internal sealed record QueueRecheck(string At, bool Passed, string By);

/// <summary>A queue's stop: when it was made and the rechecks since.</summary>
internal sealed record QueueStop(string At, IReadOnlyList<QueueRecheck> Rechecks);

/// <summary>An open known red's standing at a time: expired past its 72 hours, warned within twelve hours of that.</summary>
internal sealed record KnownRedStanding(bool Expired, bool Warn);

/// <summary>
/// How a red candidate's failures are judged (INTEGRATE1f part f1; D207, the integration design §4.5, §5.3, §11 row f): in order, a death of
/// memory is the machine's; a known red unexpired and last seen failing on the base is inherited with no run, and past its 72 hours is run
/// on the base and stops the line where it fails there; the confirm run's reading comes before the base (a sighting or an inconclusive
/// reading answers at once, with no base and no prefix run); the base; then the prefixes. Everything here is a value or a function: the
/// runs, the admission and the clock are handed in, so the integration step Daoris runs judges as the merge queue judges on the same runs.
/// </summary>
/// <remarks>
/// <b>A twin</b> of <c>placeOnPrefixes</c>, <c>judgeFailure</c>, <c>memoryStep</c>, <c>confirmStep</c>, <c>confirmReadings</c>,
/// <c>recheckDue</c> and <c>noteRecheck</c> in <c>tools/merge-queue.mjs</c> and <c>knownRedStanding</c> in <c>tools/ci-background.mjs</c>: no
/// code is shared, so both hold one table, the driver tests' <c>fixtures/failure-judging.json</c>, row for row, sentence for sentence
/// (<c>FailureJudgeTests</c>, <c>integration-judge.test.ts</c>). The candidate's outcome is <see cref="CandidateJudgement"/>. A sighting is
/// never a pass: <c>unconfirmed</c> is never green, never a flake and never stored as a pass, and only the owner's <c>land</c> promotes on
/// it, naming the gates it passed on.
/// </remarks>
internal static class FailureJudge
{
    /// <summary>How long after a stop, or its last recheck, the runner's own recheck falls due.</summary>
    internal static readonly TimeSpan RecheckEvery = TimeSpan.FromHours(1);

    /// <summary>How long a known red is inherited from its first sighting; past it, the line stops where it fails on the base.</summary>
    internal static readonly TimeSpan KnownRedLasts = TimeSpan.FromHours(72);

    /// <summary>How long before its end the watch warns of a known red's expiry.</summary>
    internal static readonly TimeSpan KnownRedWarns = TimeSpan.FromHours(12);

    private static bool Same(PrefixOutcome? outcome, FailureSignature signature) =>
        outcome is { Result: PrefixResult.Fail } && FailureReader.Same(outcome.Signature, signature);

    private static bool CannotRun(PrefixOutcome? outcome, FailureSignature signature) =>
        outcome is { Result: PrefixResult.Cannot } && !FailureReader.Same(outcome.Signature, signature);

    private static Placement Unplaced(string why) => new(false, 0, [], false, why);

    /// <summary>
    /// The prefixes' answers to one question, does it fail with this signature (<paramref name="seq"/>[k] the base plus the first k
    /// members, [0] the base and [n] the whole candidate), placed: the member at the <b>last</b> transition into the signature, earlier
    /// transitions named as interacting; any prefix that cannot run the selection with another signature makes it unresolved, and so does a
    /// whole candidate that does not fail with it. Members are numbered from 1.
    /// </summary>
    public static Placement PlaceOnPrefixes(IReadOnlyList<PrefixOutcome> seq, FailureSignature signature)
    {
        var n = seq.Count - 1;
        var blocked = -1;
        for (var at = 0; at < seq.Count && blocked < 0; at += 1)
        {
            if (CannotRun(seq[at], signature)) blocked = at;
        }

        if (blocked >= 0)
        {
            var who = blocked == 0 ? "the base" : $"the base and {blocked} member{(blocked == 1 ? "" : "s")}";
            return Unplaced($"prefix {blocked} cannot run the selection ({who}): it fails another way");
        }

        if (n < 0 || !Same(seq[n], signature)) return Unplaced("the whole candidate does not fail again with its signature");
        var transitions = new List<int>();
        for (var k = 1; k <= n; k += 1)
        {
            if (!Same(seq[k - 1], signature) && Same(seq[k], signature)) transitions.Add(k);
        }

        if (transitions.Count == 0) return Unplaced("no prefix places it");
        return new Placement(true, transitions[^1], [.. transitions.Take(transitions.Count - 1)], transitions.Count == 1, null);
    }

    /// <summary>
    /// One failure judged: a known red unexpired and last seen on the base is inherited with no run; the confirm run's reading (asked only
    /// from here) answers a sighting, an inconclusive reading or a drop at once; the base; then every prefix, the last transition pinned,
    /// the two prefixes either side of it run once more, and a pinned member that passes alone on the base needing the members before it.
    /// </summary>
    public static Judged JudgeFailure(JudgedFailure failure, JudgeContext context)
    {
        var runs = new List<JudgeRun>();
        PrefixOutcome At(int k)
        {
            var outcome = context.Run(failure, k);
            runs.Add(new JudgeRun(k, false, outcome));
            return outcome;
        }

        var known = context.Known;
        var forTest = failure.Names == FailureNames.Test;
        if (forTest && known is { Expired: false } && known.LastSeen == context.BaseCommit)
        {
            var base7 = context.BaseCommit ?? "null";
            return new Judged(JudgeOutcome.Inherited, runs) { Why = $"known red, last seen failing on the base {base7[..Math.Min(7, base7.Length)]}: its base run is skipped" };
        }

        Judged Expired() => new(JudgeOutcome.Stop, runs)
        {
            Why = "expired",
            Said = $"{failure.Test} is known red past 72 hours: no candidate inherits it, and the queue stops until a direct commit fixes it or skips it under its row",
        };

        PrefixOutcome? baseRun = null;
        if (context.Confirm is { } confirm)
        {
            if (forTest && known is { Expired: true })
            {
                baseRun = At(0);
                if (Same(baseRun, failure.Signature)) return Expired();
            }

            var reading = confirm();
            switch (reading?.Result)
            {
                case ConfirmResults.Dropped:
                    return new Judged(JudgeOutcome.Dropped, runs) { Why = "the candidate was dropped during its confirm run", Confirm = reading };
                case ConfirmResults.Unconfirmed:
                    return new Judged(JudgeOutcome.Unconfirmed, runs)
                    {
                        Why = "it did not fail again when its gate ran once more as composed, holding the whole admission: a sighting, never a pass",
                        Confirm = reading,
                    };
                case ConfirmResults.Confirmed:
                    break;
                default:
                    return new Judged(JudgeOutcome.Inconclusive, runs)
                    {
                        Why = $"its confirm run is inconclusive ({reading?.Why ?? "it gave no reading"}): read as split, never land",
                        Confirm = reading,
                    };
            }
        }

        baseRun ??= At(0);
        if (Same(baseRun, failure.Signature))
        {
            if (!forTest)
            {
                return new Judged(JudgeOutcome.Stop, runs)
                {
                    Why = "base",
                    Said = $"{failure.Gate} fails on the base with the same signature: main is broken, the queue stops until a direct commit or --queue --recheck",
                };
            }

            if (known is { Expired: true }) return Expired();
            return new Judged(JudgeOutcome.Inherited, runs) { Why = "it fails on the base with the same signature: filed as known red, pinned to none" };
        }

        var seq = new List<PrefixOutcome> { baseRun };
        var whole = At(context.Members);
        for (var k = 1; k < context.Members; k += 1) seq.Add(At(k));
        seq.Add(whole);
        var placed = PlaceOnPrefixes(seq, failure.Signature);
        if (!placed.Pinned) return new Judged(JudgeOutcome.Unresolved, runs) { Why = placed.Why };
        // A flake mid-bisection: the two prefixes either side of the transition run once more, and a result that differs leaves it unresolved.
        var before = At(placed.Member - 1);
        var after = At(placed.Member);
        if (Same(before, failure.Signature) != Same(seq[placed.Member - 1], failure.Signature) || Same(after, failure.Signature) != Same(seq[placed.Member], failure.Signature))
        {
            return new Judged(JudgeOutcome.Unresolved, runs) { Why = $"the prefixes either side of member {placed.Member} disagree on a second run" };
        }

        IReadOnlyList<int> needs = [];
        if (placed.Monotonic && placed.Member > 1 && context.Alone is { } alone)
        {
            var single = alone(failure, placed.Member);
            runs.Add(new JudgeRun(placed.Member, true, single));
            if (!Same(single, failure.Signature)) needs = [.. Enumerable.Range(1, placed.Member - 1).Where(k => !placed.Interacting.Contains(k))];
        }

        return new Judged(JudgeOutcome.Pinned, runs) { Member = placed.Member, With = needs, Interacting = placed.Interacting };
    }

    /// <summary>
    /// The confirm run: the failing gate's run repeated once on the candidate's tip as composed, holding the whole machine admission, one per
    /// failing gate per candidate, answering for every failure that gate had. A drop of the candidate ends it: <paramref name="halted"/> is
    /// asked before the take, by the take while it waits, and after the run, and the run's own drop may throw.
    /// </summary>
    /// <remarks>
    /// Answers, in this order: <c>dropped</c> (the drop, with what the run threw for it; nothing is judged or filed); <c>inconclusive</c>,
    /// judged before the run's own answer (an admission not taken or not shown), or for an answer that is neither a pass nor failures with
    /// their signatures; <c>unconfirmed</c> (it passed); <c>confirmed</c> (it failed again, with what it failed with). A throw that is no drop
    /// is rethrown once every place is let go.
    /// </remarks>
    public static ConfirmAnswer ConfirmStep(Func<string, Func<bool>, WholeTake> take, Func<GateRerun?> run, Func<bool>? halted = null, string what = "a confirm run")
    {
        halted ??= () => false;
        ConfirmAnswer Dropped(WholeEvidence? evidence = null, Exception? error = null) =>
            new(ConfirmResults.Dropped, "the candidate was dropped during its confirm run", evidence, null) { Error = error };

        if (halted()) return Dropped();
        var whole = take(what, halted);
        if (!whole.Held)
        {
            if (whole.Dropped) return Dropped(whole.Evidence);
            return new ConfirmAnswer(ConfirmResults.Inconclusive, whole.Why ?? "the whole admission was not taken", whole.Evidence, null);
        }

        GateRerun? outcome = null;
        Exception? thrown = null;
        try
        {
            outcome = run();
        }
        catch (Exception error)
        {
            thrown = error;
        }

        var evidence = whole.End?.Invoke();
        if (halted()) return Dropped(evidence, thrown);
        if (thrown is not null) ExceptionDispatchInfo.Capture(thrown).Throw();
        var composition = outcome?.Composition is { } kept ? new ConfirmComposition(kept.Units, kept.Digest) : null;
        if (evidence is not { Conclusive: true })
        {
            return new ConfirmAnswer(ConfirmResults.Inconclusive, evidence?.Doubts.FirstOrDefault()?.Why ?? "the whole admission was not shown", evidence, composition);
        }

        if (outcome?.Result == "pass") return new ConfirmAnswer(ConfirmResults.Unconfirmed, "it passed, holding the whole admission", evidence, composition);
        IReadOnlyList<JudgedFailure?> failures = outcome is { Result: "fail", Failures: { } given } ? given : [];
        if (failures.Count > 0 && failures.All(failure => failure is not null))
        {
            return new ConfirmAnswer(ConfirmResults.Confirmed, "it failed again, holding the whole admission", evidence, composition) { Failures = [.. failures.Select(failure => failure!)] };
        }

        // A run that answers neither (a death of memory, the machine's) says why in its own words.
        var why = !string.IsNullOrEmpty(outcome?.Why) ? outcome.Why : "the confirm run's answer is neither a pass nor failures with their signatures";
        return new ConfirmAnswer(ConfirmResults.Inconclusive, why, evidence, composition);
    }

    /// <summary>
    /// Each failure of a gate read by its confirm run (<see cref="ConfirmStep"/>'s answer), as the judgement's confirm reading: <c>dropped</c>
    /// and <c>unconfirmed</c> read so for every failure the gate had, and anything else that is not <c>confirmed</c> reads <c>inconclusive</c>.
    /// Under <c>confirmed</c>, a failure whose signature failed again is <c>confirmed</c>, and one that did not is <c>inconclusive</c>: the gate
    /// failed another way, so whether it recurs is not shown. A failure of the confirm run's own, whose signature is none of the gate's, is
    /// added as <c>confirmed</c>: it failed with nothing heavy beside it, so it is the candidate's.
    /// </summary>
    public static IReadOnlyList<(JudgedFailure Failure, ConfirmReading Confirm)> ConfirmReadings(ConfirmAnswer? confirm, IReadOnlyList<JudgedFailure> failures)
    {
        ConfirmReading Reading(string result, string why) => new(result, why) { Evidence = confirm?.Evidence, Composition = confirm?.Composition };
        if (confirm?.Result != ConfirmResults.Confirmed)
        {
            var result = confirm is { Result: ConfirmResults.Dropped or ConfirmResults.Unconfirmed } ? confirm.Result : ConfirmResults.Inconclusive;
            return [.. failures.Select(failure => (failure, Reading(result, confirm?.Why ?? "no confirm run answered")))];
        }

        var again = new HashSet<string>((confirm.Failures ?? []).Select(each => each.Signature.Key), StringComparer.Ordinal);
        var had = new HashSet<string>(failures.Select(each => each.Signature.Key), StringComparer.Ordinal);
        return
        [
            .. failures.Select(failure => (failure, again.Contains(failure.Signature.Key)
                ? Reading(ConfirmResults.Confirmed, "it failed again with its signature, holding the whole admission")
                : Reading(ConfirmResults.Inconclusive, "its gate failed another way on the confirm run, so whether this failure recurs is not shown"))),
            .. (confirm.Failures ?? []).Where(each => !had.Contains(each.Signature.Key))
                .Select(failure => (failure, Reading(ConfirmResults.Confirmed, "a failure of the confirm run's own, holding the whole admission: the candidate's"))),
        ];
    }

    /// <summary>
    /// A death of memory, the machine's: never run on the base or over the prefixes, never a stop or a hand-back. Given the gate's attempts so
    /// far (oldest first), whether the hold was released by an admission, and its reserved need, answers what happens next: <c>again</c> (once
    /// more, whole and alone, once its memory is free), <c>pass</c> (a pass after a death, counted as the gate's pass), <c>hold</c> (held as
    /// memory until the admission reserves its need), or <c>judge</c> (after the hold, its own job's peak past its need: the gate's failure,
    /// judged as one that names no test).
    /// </summary>
    public static MemoryStepResult MemoryStep(IReadOnlyList<MemoryAttempt> attempts, bool afterHold = false, double? needGb = null)
    {
        if (attempts.Count == 0) return new MemoryStepResult("run");
        var last = attempts[^1];
        if (!last.Died)
        {
            return attempts.Count > 1 ? new MemoryStepResult("pass", "FLAKE of memory: it passed once its memory was free") : new MemoryStepResult("pass");
        }

        if (afterHold)
        {
            if (needGb is { } need && last.PeakGb is { } peak && peak > need)
            {
                return new MemoryStepResult("judge", $"its own job's peak commit ({Number(peak)} GB) passed its reserved need ({Number(need)} GB): the gate's failure");
            }

            return new MemoryStepResult("hold", "it died within its reserved need, under load from outside the runners: held again, not judged");
        }

        if (attempts.Count == 1) return new MemoryStepResult("again", "a death of memory: run again once, whole and alone, once its memory is free (MEMGATE1)");
        return new MemoryStepResult("hold", "a second death of memory: the candidate is held as memory until the admission record reserves its need");
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool Time(string? text, out DateTimeOffset time) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);

    /// <summary>Whether a stopped queue is due its own recheck: an hour after the stop or the last recheck. A stop whose time does not read is never due.</summary>
    public static bool RecheckDue(QueueStop? stop, DateTimeOffset now, TimeSpan? every = null)
    {
        if (stop is null) return false;
        var last = stop.Rechecks.Count > 0 ? stop.Rechecks[^1].At : stop.At;
        return Time(last, out var at) && now - at >= (every ?? RecheckEvery);
    }

    /// <summary>A stop lifted (null) by a recheck that passed, or kept with the recheck recorded.</summary>
    public static QueueStop? NoteRecheck(QueueStop stop, string at, bool passed, string by) =>
        passed ? null : stop with { Rechecks = [.. stop.Rechecks, new QueueRecheck(at, passed, by)] };

    /// <summary>
    /// An open known red's standing at <paramref name="now"/>: expired when more than 72 hours have passed since its test's first sighting,
    /// warned within twelve hours of that. A first sighting whose time does not read neither expires nor warns.
    /// </summary>
    public static KnownRedStanding StandingOf(string? since, DateTimeOffset now)
    {
        if (!Time(since, out var first)) return new KnownRedStanding(false, false);
        var age = now - first;
        return new KnownRedStanding(age > KnownRedLasts, age <= KnownRedLasts && age > KnownRedLasts - KnownRedWarns);
    }
}
