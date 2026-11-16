using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>The person's word on a held attempt: taken at their door, applied to its node under the node's declaration.</summary>
internal sealed class WorkPersonWord(NodeRecordFiles records, Func<DateTimeOffset> clock)
{
    /// <summary>
    /// The person's word on a held attempt (one of <see cref="HeldResolutions"/>), from their door: written on the attempt
    /// while it is still held and has no word, under the record's lock with the lease asked inside it, so only the lease's holder records it.
    /// Applied by <see cref="Resolve"/> under the node's declaration, which only the runtime holds.
    /// </summary>
    public WorkPerformed Word(string unit, string node, int attempt, string resolution, string? outcome = null, string? link = null, string? note = null)
    {
        if (resolution == HeldResolutions.DoneOutside) return Outside(unit, node, attempt, outcome, link, note);
        if (!HeldResolutions.All.Contains(resolution, StringComparer.Ordinal)) return new(unit, node, PerformedWhat.Refused, NotResolution);
        var now = clock();
        return records.Edited(unit, node, record =>
        {
            var held = record.Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt);

            // A failure kept for the person takes their new attempt alone.
            if (held is not null && KeptFailure(record, held))
            {
                if (held.Resolution is not null) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, Worded));
                // A failed check's *Not now* settles it as the person's: the verdict stands, and nothing asks about it again.
                var notNow = resolution == HeldResolutions.Stop && OnPort(held);
                if (resolution != HeldResolutions.Again && !notNow) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, NotAgain));
                return (With(record, held, held with { Resolution = resolution, ResolvedAt = now }), new WorkPerformed(unit, node, PerformedWhat.Worded, resolution));
            }

            // A close found closed only in part, its node live again, takes the person's stop alone: the recovery is theirs.
            if (held is not null && QuestClosing.Partly(record, held))
            {
                if (held.Resolution is not null) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, Worded));
                if (resolution != HeldResolutions.Stop) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, PartStop));
                return (With(record, held, held with { Resolution = resolution, ResolvedAt = now }), new WorkPerformed(unit, node, PerformedWhat.Worded, resolution));
            }

            // An unknown close its node is still on is the person's too, with a held close's words.
            if (held is null || (held.State != AttemptStates.Held && !QuestClosing.Unanswered(record, held)))
            {
                return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, NotAttempt));
            }

            if (held.Resolution is not null) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, Worded));

            // Each attempt takes its own words. A close's outcome is its door's answer, so a plugin's words are never taken on
            // it, and `record` only where that answer is kept; a plugin's attempt keeps its three.
            if (WordRefused(held, resolution) is { } code) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, code));
            return (With(record, held, held with { Resolution = resolution, ResolvedAt = now, Decision = resolution == HeldResolutions.Happened ? outcome : null }),
                new WorkPerformed(unit, node, PerformedWhat.Worded, resolution));
        });
    }

    /// <summary>
    /// The holder's <i>Done outside Daoris</i> on an attempt under way, held or failed (<see cref="StandDowns.Outside"/>), with its link,
    /// required, and a note: kept as <c>happened</c> with the link, which an older build reads as the word it knows and passes the link over.
    /// </summary>
    private WorkPerformed Outside(string unit, string node, int attempt, string? outcome, string? link, string? note)
    {
        if (string.IsNullOrWhiteSpace(link)) return new(unit, node, PerformedWhat.Refused, NoLink);
        var now = clock();
        return records.Edited(unit, node, record =>
        {
            var on = record.Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt);
            if (on is null || !StandDowns.Outside(record, on)) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, NotAttempt));
            if (on.Resolution is not null) return (null, new WorkPerformed(unit, node, PerformedWhat.Refused, Worded));
            var worded = on with
            {
                Resolution = HeldResolutions.Happened, ResolvedAt = now, Link = link.Trim(), Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Decision = outcome,
            };
            return (With(record, on, worded), new WorkPerformed(unit, node, PerformedWhat.Worded, HeldResolutions.DoneOutside));
        });
    }

    /// <summary>
    /// Every word the person gave on a held attempt, applied under the node's declaration: the attempt settled by the
    /// person, never as its plugin's word, and its node moved through the one table. A node no declaration names waits with its word.
    /// </summary>
    /// <remarks>
    /// <para><b>Its node still on it</b> (active, or unknown, on that attempt): an active one is first unknown (its outcome did not bind, so
    /// it cannot be established now), then <i>it happened</i> reaches it on the person's word, <i>it did not</i> fails it and makes it ready
    /// for the person's new attempt, which the next evaluation starts with a new key, and <i>stop</i> stops it.</para>
    /// <para><b>Its node moved off it</b> (stopped, skipped, rejected, invalidated): the word settles the attempt as a stale one's and
    /// lifts the gate, moving nothing; <i>stop</i> stops a node still live.</para>
    /// <para><b>A word the one table refuses</b> (<i>it happened</i> on a node exposing an output or an evidence schema, the plugin's to give)
    /// leaves the attempt held with the refusal's code and the word taken back, so the person sees why and says another.</para>
    /// </remarks>
    public IReadOnlyList<WorkPerformed> Resolve(Func<string, string, NodeDeclaration?> declared)
    {
        var done = new List<WorkPerformed>();
        foreach (var read in records.All().Records)
        {
            if (!read.Attempts.Any(attempt => HasWord(read, attempt))) continue;
            var (refused, resolved) = records.Edit<List<WorkPerformed>>(read.Unit, current =>
            {
                var said = new List<WorkPerformed>();
                var record = current;
                foreach (var attempt in current.Attempts.Where(attempt => HasWord(current, attempt)))
                {
                    if (declared(record.Unit, attempt.Node) is not { } node) continue;
                    var (next, result) = Settle(record, attempt, node);
                    record = next;
                    said.Add(result);
                }

                return (ReferenceEquals(record, current) ? null : record, said);
            });
            if (refused is not null) done.Add(new(read.Unit, "", PerformedWhat.Refused, refused));
            else done.AddRange(resolved!);
        }

        return done;

        // A close's word is its closer's (QuestCloser.Resolve), never applied here as a plugin's. The person's
        // new attempt is on a settled one, its failure; a done outside on one in flight, or on a failure still kept.
        static bool HasWord(NodeRecord record, NodeAttempt attempt) =>
            attempt.Plugin != QuestClosing.Sender
            && (attempt is { State: AttemptStates.Held, Resolution: not null } or { State: AttemptStates.Settled, Resolution: HeldResolutions.Again }
                || (attempt is { Resolution: HeldResolutions.Happened, Link: not null } && (StandDowns.InFlight(attempt) || KeptFailure(record, attempt))));
    }

    /// <summary>
    /// The holder's <i>Done outside Daoris</i> applied: its step reached on the holder's word through the one table, the link its evidence
    /// and the note its words, the attempt in flight stood down (a failed one keeps its failure). A step its attempt is no longer on takes
    /// the word back, and so does one the table refuses, saying why.
    /// </summary>
    private (NodeRecord Next, WorkPerformed Result) Outside(NodeRecord record, NodeAttempt attempt, NodeDeclaration node)
    {
        var unit = record.Unit;
        var status = StatusOf(record, node.Id);
        NodeRecord TakenBack(string code) => With(record, attempt, attempt with
        {
            Resolution = null, ResolvedAt = null, Link = null, Note = null, Decision = null, Code = attempt.State == AttemptStates.Held ? code : attempt.Code,
        });

        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown or NodeStates.Failed))
        {
            return (TakenBack(Moved), new(unit, node.Id, PerformedWhat.NotTaken, Moved));
        }

        var move = new NodeMove(NodeStates.Reached, NodeCause.Person)
        {
            Source = NodeSource.Person, Attempt = attempt.Attempt, Link = attempt.Link, Words = attempt.Note,
            Acceptance = status.Binding is { } binding
                ? binding with
                {
                    Evidence = new NodePort(NodeSource.Person, attempt.Link!, binding.SubjectRevision, null),
                    Outcome = node.Outcomes.Count > 0 ? attempt.Decision : null,
                }
                : null,
        };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return (TakenBack(moved.Refused!), new(unit, node.Id, PerformedWhat.Refused, moved.Refused));

        record = Kept(record, moved, "holder-" + HeldResolutions.DoneOutside, clock());
        var settled = StandDowns.InFlight(attempt)
            ? attempt with { State = AttemptStates.Settled, Outcome = AttemptOutcomes.StoodDown, Code = HeldResolutions.DoneOutside, AskAt = null }
            : attempt;
        return (With(record, attempt, settled), new(unit, node.Id, PerformedWhat.Resolved, HeldResolutions.DoneOutside));
    }

    /// <summary>
    /// Whether <paramref name="attempt"/> is a failure kept for the person: a plugin's attempt its node failed on,
    /// settled, with no retry coming (its code not transient, its tries spent, or a retry the graph governing now refused). Read from the record
    /// alone, so the lists and the doors agree without a declaration; an observed node's failure dispatched nothing and is none.
    /// </summary>
    internal static bool KeptFailure(NodeRecord record, NodeAttempt attempt) =>
        attempt is { State: AttemptStates.Settled, RetryAt: null } && attempt.Plugin != QuestClosing.Sender
        && !(OnPort(attempt) && attempt.Resolution == HeldResolutions.Stop)
        && record.Statuses.GetValueOrDefault(attempt.Node) is { State: NodeStates.Failed } status && status.Attempt == attempt.Attempt;

    /// <summary>Why <paramref name="word"/> is not taken on <paramref name="held"/>, or null where it is one of its words.</summary>
    internal static string? WordRefused(NodeAttempt held, string word) =>
        HeldResolutions.Of(held).Contains(word, StringComparer.Ordinal) ? null
        : word == HeldResolutions.Again ? NotAgain
        : held.Plugin != QuestClosing.Sender ? NotOffered
        : word == HeldResolutions.Record ? NotKept
        : NotCloseWord;

    /// <summary>One word applied (<see cref="Resolve"/>): the record after it, and what it came to.</summary>
    private (NodeRecord Next, WorkPerformed Result) Settle(NodeRecord record, NodeAttempt attempt, NodeDeclaration node)
    {
        var now = clock();
        var unit = record.Unit;
        var word = attempt.Resolution!;
        if (word == HeldResolutions.Happened && attempt.Link is not null) return Outside(record, attempt, node);

        // The person's new attempt on a failure kept for them (failed becomes ready by the person), which the next evaluation
        // starts with a new key. The word is cleared as it is applied: the failure stays its plugin's.
        // A node that moved off the failure meanwhile (a new revision, a retry) needs no new attempt, so nothing moves.
        if (word == HeldResolutions.Again && attempt.State == AttemptStates.Settled)
        {
            var cleared = With(record, attempt, attempt with { Resolution = null, ResolvedAt = null });
            if (!KeptFailure(record, attempt)) return (cleared, new(unit, node.Id, PerformedWhat.NotTaken, Moved));
            var again = NodeLifecycle.Apply(node, StatusOf(record, node.Id), new NodeMove(NodeStates.Ready, NodeCause.Person));
            if (!again.Moved) return (cleared, new(unit, node.Id, PerformedWhat.Refused, again.Refused));
            return (Kept(cleared, again, "person-again", now), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        // A held check's *Run again*, the one table's own: its node stopped and made ready by the
        // person for a new attempt, the held attempt settled as theirs to answer for. Its *Not now* is the stop below.
        if (word == HeldResolutions.Again && OnPort(attempt) && attempt.State == AttemptStates.Held)
        {
            var held = StatusOf(record, node.Id);
            if (NodeStates.Live.Contains(held.State)) record = Kept(record, NodeLifecycle.Apply(node, held, new NodeMove(NodeStates.Stopped, NodeCause.Person)), "person-stop", now);
            var stopped = StatusOf(record, node.Id);
            record = With(record, attempt, attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Resolution = null, ResolvedAt = null });
            record = record with { Statuses = With(record.Statuses, NodeLifecycle.Reconciled(stopped, attempt.Attempt)) };
            var ready = NodeLifecycle.Apply(node, StatusOf(record, node.Id), new NodeMove(NodeStates.Ready, NodeCause.Person));
            return ready.Moved
                ? (Kept(record, ready, "person-again", now), new(unit, node.Id, PerformedWhat.Resolved, word))
                : (record, new(unit, node.Id, PerformedWhat.Refused, ready.Refused));
        }

        // A close's word is never a plugin attempt's; one no door would record is taken back, saying why.
        if (WordRefused(attempt, word) is { } refused)
        {
            return (With(record, attempt, attempt with { Resolution = null, ResolvedAt = null, Code = refused }), new(unit, node.Id, PerformedWhat.Refused, refused));
        }

        var status = StatusOf(record, node.Id);
        var current = status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown;
        var outcome = word switch { HeldResolutions.Happened => WorkStates.Reached, HeldResolutions.DidNot => WorkStates.Failed, _ => WorkStates.Unknown };
        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome };

        if (!current || word == HeldResolutions.Stop)
        {
            // A word on an attempt its node moved off settles it as a stale one's; a stop stops a node still live. Either lifts the gate.
            if (word == HeldResolutions.Stop && NodeStates.Live.Contains(status.State))
            {
                record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Stopped, NodeCause.Person)), "person-stop", now);
            }

            var after = StatusOf(record, node.Id);
            record = With(record, attempt, settled);
            return (record with { Statuses = With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        // An outcome that did not bind left its node active: it cannot be established now, so it is unknown before the person settles it.
        if (status.State == NodeStates.Active)
        {
            var unknown = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt });
            record = Kept(record, unknown, "unknown", now);
            status = StatusOf(record, node.Id);
        }

        var move = word == HeldResolutions.Happened
            ? new NodeMove(NodeStates.Reached, NodeCause.Person)
            {
                Source = NodeSource.Person, Attempt = attempt.Attempt,
                Acceptance = status.Binding is { } binding
                    ? binding with
                    {
                        Evidence = new NodePort(NodeSource.Person, $"{NodeSource.Person}:{attempt.Call.Invocation}", binding.SubjectRevision, null),
                        Outcome = node.Outcomes.Count > 0 ? attempt.Decision : null,
                    }
                    : null,
            }
            : new NodeMove(NodeStates.Failed, NodeCause.Person) { Source = NodeSource.Person, Attempt = attempt.Attempt, Code = HeldResolutions.DidNot };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved)
        {
            // The word cannot be taken (the one table's refusal): the attempt stays held, saying why, and the word is taken back for another.
            return (With(record, attempt, attempt with { Code = moved.Refused, Resolution = null, ResolvedAt = null, Decision = null }), new(unit, node.Id, PerformedWhat.Refused, moved.Refused));
        }

        record = Kept(record, moved, "person-" + word, now);
        if (word == HeldResolutions.DidNot)
        {
            // The person's new attempt (failed becomes ready by the person): the next evaluation starts it with a new key.
            record = Kept(record, NodeLifecycle.Apply(node, moved.Status, new NodeMove(NodeStates.Ready, NodeCause.Person)), "person-again", now);
        }

        return (With(record, attempt, settled), new(unit, node.Id, PerformedWhat.Resolved, word));
    }
}
