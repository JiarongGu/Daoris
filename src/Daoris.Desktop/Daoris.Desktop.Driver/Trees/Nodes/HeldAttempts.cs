namespace Daoris.Driver;

/// <summary>
/// One attempt held for the person (FABRIC-PERFORM1g; the fabric events design §D6, §E2, §D3's <i>blocked, needs the person</i>): what it
/// is on, how far its node got, why nothing establishes it by itself, and the person's word where they gave it.
/// </summary>
/// <param name="Unit">The unit (<see cref="WorkUnit.Id"/>).</param>
/// <param name="Node">The node's id in its graph.</param>
/// <param name="Attempt">The node's attempt number it is.</param>
/// <param name="Plugin">The plugin that performs it.</param>
/// <param name="Step">The step it was sent as (<c>&lt;plugin&gt;/&lt;id&gt;</c>).</param>
/// <param name="NodeState">Where its node stands now (<see cref="NodeStates"/>).</param>
/// <param name="Why">Why it is held: <c>unknown</c>, <c>no-reconcile</c>, <c>no-outcome</c>, <c>not-current</c>, or the code the one table refused its outcome with.</param>
public sealed record HeldAttempt(string Unit, string Node, int Attempt, string Plugin, string Step, string NodeState, string Why)
{
    /// <summary>What its plugin said of it, where it said anything: an outcome that did not bind keeps its state here.</summary>
    public string? Outcome { get; init; }

    /// <summary>The revision it was sent on.</summary>
    public string Revision { get; init; } = "";

    /// <summary>When it was written, before its first send.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>The person's word, given and not applied yet (<see cref="HeldResolutions"/>), or null.</summary>
    public string? Resolution { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>
    /// The words the person may say of it (FABRIC-CLOSE1q): a plugin's attempt's three (<see cref="HeldResolutions.Plugin"/>), or a close's
    /// recoveries and its stop (<see cref="HeldResolutions.Close"/>), <c>record</c> only where its door's answer is kept on it.
    /// </summary>
    public IReadOnlyList<string> Words { get; init; } = HeldResolutions.Plugin;

    /// <summary>A failure kept for the person's code (FABRIC-PERFORM1k), as its plugin gave it; null on an attempt held.</summary>
    public string? Code { get; init; }

    /// <summary>Whether it is a failure kept for the person (FABRIC-PERFORM1k), which takes their new attempt alone, rather than an attempt held.</summary>
    public bool Failed => Why is WorkPerformer.FailureKept or WorkPerformer.TriesSpent;
}

/// <summary>
/// The attempts held for the person, read from the units' node records (FABRIC-PERFORM1g): what <c>WORK_STATE</c> and
/// <c>daoris-driver work held</c> list, and the sentences both say. Read-only: no record is written, no lease taken. An install no graph
/// naming a plugin's performed node governs has no record, so it lists nothing.
/// </summary>
public static class HeldAttempts
{
    /// <summary>The usage of <c>daoris-driver work held</c>, as the terminal says it when its words are not understood.</summary>
    public const string Usage =
        "usage: daoris-driver work held [--unit <id>]  |  daoris-driver work held <unit> <node> <attempt> happened|did-not|stop  (a held close: record|resend|stop; a failed step: again)";

    /// <summary>
    /// Every attempt held on the records that read, every close unknown with its node still on it (FABRIC-CLOSE1u,
    /// <see cref="QuestClosing.Unanswered"/>), and every failure kept for the person (FABRIC-PERFORM1k, <see cref="WorkPerformer.KeptFailure"/>),
    /// oldest first; with <paramref name="unit"/>, that unit's alone.
    /// </summary>
    public static IReadOnlyList<HeldAttempt> Read(NodeRecordFiles records, string? unit = null, IReadOnlyDictionary<string, QuestView>? quests = null) =>
        [.. records.All().Records
            .Where(record => unit is null || string.Equals(record.Unit, unit, StringComparison.Ordinal))
            .SelectMany(record => record.Attempts.Where(attempt => WorkPerformer.KeptFailure(record, attempt)).Select(attempt =>
                new HeldAttempt(record.Unit, attempt.Node, attempt.Attempt, attempt.Plugin, attempt.Call.Step, NodeStates.Failed,
                    record.Statuses[attempt.Node].Transient ? WorkPerformer.TriesSpent : WorkPerformer.FailureKept)
                {
                    Outcome = attempt.Outcome, Revision = attempt.Call.SubjectRevision, RecordedAt = attempt.RecordedAt, Resolution = attempt.Resolution,
                    ResolvedAt = attempt.ResolvedAt, Words = HeldResolutions.Failure, Code = record.Statuses[attempt.Node].Code ?? attempt.Code,
                })
            .Concat(record.Attempts.Where(attempt => attempt.State == AttemptStates.Held || QuestClosing.Unanswered(record, attempt)).Select(attempt =>
            {
                // FABRIC-CLOSE1q3: a close whose submission moved by the runtime's last read is offered resend alone: the pass takes back
                // record and stop, which the node records alone cannot tell (WORK_HELD holds no quest). No last read judges nothing.
                var moved = attempt.Plugin == QuestClosing.Sender && quests is not null && QuestClosing.Moved(attempt, quests) is not null;
                return new HeldAttempt(
                    record.Unit, attempt.Node, attempt.Attempt, attempt.Plugin, attempt.Call.Step,
                    record.Statuses.GetValueOrDefault(attempt.Node)?.State ?? NodeStates.Blocked,
                    moved ? WorkPerformer.SubmissionMoved : attempt.Code ?? ReconcileStates.Unknown)
                {
                    Outcome = attempt.Outcome, Revision = attempt.Call.SubjectRevision, RecordedAt = attempt.RecordedAt,
                    Resolution = attempt.Resolution, ResolvedAt = attempt.ResolvedAt, Words = moved ? [HeldResolutions.Resend] : HeldResolutions.Of(attempt),
                };
            })))
            .OrderBy(held => held.RecordedAt)];

    /// <summary>Why an attempt is held, as the terminal says it; the page words the same codes in the reader's language.</summary>
    public static string WhySaid(HeldAttempt held) => held.Plugin == QuestClosing.Sender ? CloseWhy(held) : held.Why switch
    {
        ReconcileStates.Unknown => "its plugin was asked and cannot say what became of it",
        WorkPerformer.NoReconcile => "its step declares no way to ask what became of it, so it is never sent again by itself",
        WorkPerformer.NoOutcome => "its plugin said it was done and named no outcome",
        WorkPerformer.NotCurrent => "its node moved off it while its effect may still run, and its step can be sent again but not asked",
        WorkPerformer.FailureKept => $"its plugin said it failed ({held.Code}), and Daoris does not try it again by itself",
        WorkPerformer.TriesSpent => $"its plugin said it failed ({held.Code}) and its tries are spent, so Daoris does not try it again by itself",
        NodeRefusal.NotOnWord => "your word that it happened could not be taken: its results are its plugin's to give",
        var code => $"its plugin's {held.Outcome ?? "answer"} did not bind to the work it was sent on ({code})",
    };

    /// <summary>
    /// Why Daoris's own close is held (FABRIC-CLOSE1q): its door answered and its node could not take the answer, by the one table's code;
    /// or no answer is kept; or the person's word was not a close's.
    /// </summary>
    private static string CloseWhy(HeldAttempt held) => (held.Why, held.Outcome) switch
    {
        (WorkPerformer.NotCloseWord, _) => "your word could not be taken: a close's outcome is its door's answer, never a person's word",
        (WorkPerformer.SubmissionMoved, _) =>
            "the submission its close names was reopened or replaced since, so the work there is now is never that close's to record or stop: it can be sent again",
        (var code, WorkStates.Reached) => $"the quest was closed, and Daoris could not record that on its close's node ({code})",
        (var code, WorkStates.Failed) => $"the quest's close was refused, and Daoris could not record that on its close's node ({code})",
        // FABRIC-CLOSE1y: an unknown close (FABRIC-CLOSE1u) is its door's silence, not a missing record: it is asked again at each look.
        (ReconcileStates.Unknown, _) => "its door has not answered the quest's close or the question of what became of it, and is asked again at each look",
        (var code, _) => $"Daoris keeps no answer to the quest's close ({code})",
    };

    /// <summary>The lines <c>daoris-driver work held</c> prints: one per attempt held, and what the person may say of it.</summary>
    public static IReadOnlyList<string> Lines(IReadOnlyList<HeldAttempt> held)
    {
        if (held.Count == 0) return ["work: no attempt is held for you."];
        return [.. held.SelectMany(each => new[]
        {
            $"{each.Unit}  {each.Node} (attempt {each.Attempt}, {each.Step}, node {each.NodeState}): {(each.Failed ? "failed" : "held")}, {WhySaid(each)}.",
            each.Resolution is { } word
                ? $"  your word `{word}` is recorded{(each.ResolvedAt is { } at ? $" ({Moments.Write(at)})" : "")}, and applied at the next look."
                : $"  say: daoris-driver work held {each.Unit} {each.Node} {each.Attempt} {string.Join('|', each.Words)}",
        })];
    }

    /// <summary>
    /// <c>daoris-driver work held [--unit &lt;id&gt;]</c> lists; <c>daoris-driver work held &lt;unit&gt; &lt;node&gt; &lt;attempt&gt;
    /// happened|did-not|stop</c> (a held close's: <c>record|resend|stop</c>, FABRIC-CLOSE1q; a failure kept for the person's: <c>again</c>,
    /// FABRIC-PERFORM1k) records the person's word (FABRIC-PERFORM1g, D50:
    /// the page's <c>WORK_HELD</c> is the other door). Only the
    /// process holding the home's lease records it, asked inside the record's lock. Exit 0 clean; 1 refused; 2 words not understood.
    /// </summary>
    public static int Run(string home, IReadOnlyList<string> args, TextWriter output, TextWriter error, Func<DateTimeOffset>? clock = null)
    {
        var records = new NodeRecordFiles(home);
        if (args is [] or ["--unit", _])
        {
            foreach (var line in Lines(Read(records, args.Count == 2 ? args[1] : null, QuestReads.Read(home)))) output.WriteLine(line);
            return 0;
        }

        if (args is not [var unit, var node, var number, var word] || !int.TryParse(number, out var attempt) || attempt < 1)
        {
            error.WriteLine(Usage);
            return 2;
        }

        var done = Word(records, unit, node, attempt, word, clock);
        (done.What == PerformedWhat.Worded ? output : error).WriteLine(Said(done, attempt, word));
        return done.What == PerformedWhat.Worded ? 0 : 1;
    }

    /// <summary>
    /// The person's word on a held attempt, from either door (<see cref="WorkPerformer.Word"/>): recorded under the record's lock, the lease
    /// asked inside it, and applied by the loop holding the lease at its next pass.
    /// </summary>
    public static WorkPerformed Word(NodeRecordFiles records, string unit, string node, int attempt, string word, Func<DateTimeOffset>? clock = null) =>
        new WorkPerformer(records, new Unperformed(), clock ?? (() => DateTimeOffset.UtcNow)).Word(unit, node, attempt, word);

    /// <summary>A terminal sends nothing to a plugin: the word is recorded, and the loop holding the lease applies it.</summary>
    private sealed class Unperformed : IWorkPerformPort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new InvalidOperationException("a terminal sends nothing");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new InvalidOperationException("a terminal asks nothing");

        public int? GenerationOf(string plugin) => null;
    }

    /// <summary>The sentence a door says for what the person's word came to: recorded, or refused and why.</summary>
    public static string Said(WorkPerformed done, int attempt, string resolution) => (done.What, done.Code) switch
    {
        (PerformedWhat.Worded, _) => $"work: your word `{resolution}` on {done.Node} (attempt {attempt}) of {done.Unit} is recorded; the next look applies it.",
        (_, WorkPerformer.NotHeld) => "work: this process does not hold the home, so it records nothing: the desktop holds it while it runs, so say it there, or close the desktop and run this again.",
        (_, WorkPerformer.Unreadable) => $"work: the node record of {done.Unit} does not read, so nothing on it is changed.",
        (_, WorkPerformer.Worded) => $"work: {done.Node} (attempt {attempt}) of {done.Unit} already has your word, which the next look applies.",
        (_, WorkPerformer.NotResolution) =>
            $"work: `{resolution}` is not a word for a held attempt: say happened, did-not or stop, on a held close record, resend or stop, or on a failed step again.",
        (_, WorkPerformer.NotAgain) when resolution == HeldResolutions.Again =>
            $"work: {done.Node} (attempt {attempt}) of {done.Unit} is held, not failed, so it takes no new attempt by itself: say what became of it first.",
        (_, WorkPerformer.NotAgain) => $"work: {done.Node} (attempt {attempt}) of {done.Unit} failed, and its plugin said so: say again for a new attempt.",
        (_, WorkPerformer.NotCloseWord) => $"work: `{resolution}` is not a word for a held close, whose outcome is its door's answer and never a person's word: say record, resend or stop.",
        (_, WorkPerformer.NotOffered) => $"work: `{resolution}` is a word for a held close, and {done.Node} is a plugin's step: say happened, did-not or stop.",
        (_, WorkPerformer.NotKept) => $"work: no answer of its door's is kept on {done.Node} (attempt {attempt}) of {done.Unit}, so there is nothing to record again: say resend or stop.",
        _ => $"work: {done.Node} (attempt {attempt}) of {done.Unit} is not held now, so nothing was recorded.",
    };
}
