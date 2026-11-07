using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A quest as every door answers it, and what a door hands the exchange. Links and file names travel on every read; a kept file's
/// path, and this machine's id, answer only a caller on a local host's own machine.
/// </summary>
public sealed class QuestAnswers(HostWiring host)
{
    /// <summary>Which of <paramref name="quests"/> this host would delete: the exchange's judgement, none at a shared host, which has no delete door.</summary>
    public async Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Quest> quests, CancellationToken ct) =>
        host.Mode == ServiceMode.Local ? await host.Service.Exchange.DeletableAsync(quests, ct) : new HashSet<string>();

    /// <summary>One quest, with whether it may be deleted, and who holds it and how its lost takes stand.</summary>
    public async Task<QuestResponse> AnswerAsync(HttpContext http, Quest quest, CancellationToken ct) =>
        ToQuest(
            quest, host.Service.Files, host.MachineLocal(http), (await DeletableAsync([quest], ct)).Contains(quest.Id),
            await host.Service.Quests.ReadTakesAsync(quest.Id, ct), HereFor(http));

    /// <summary>A door's answer of a move: the quest as it now stands, and the exchange's sentence.</summary>
    public async Task<Answer<QuestActionResponse>> MovedAsync(HttpContext http, Quest quest, string message, CancellationToken ct) =>
        new QuestActionResponse(await AnswerAsync(http, quest, ct), message);

    /// <summary>This machine's id: an id no person gave, by which the page tells its own conflict and take from another machine's.</summary>
    public string? HereFor(HttpContext http) => host.MachineLocal(http) ? host.Service.Quests.Machine : null;

    /// <param name="taken">The quest's log read for its holder and its conflicts; null where nothing is read, which answers none of them.</param>
    public static QuestResponse ToQuest(
        Quest q, QuestFiles? files, bool machineLocal, bool deletable = false, QuestTakeReading? taken = null, string? here = null) => new(
        q.Id, q.From, q.To, q.Title, q.Body, q.Status.ToString(), q.Note, q.Filed, q.Updated, q.Workspace,
        q.Links,
        q.Attachments.Select(a => new QuestAttachmentResponse(
            a.Name, a.Sha256, a.Bytes,
            Path: machineLocal && files is not null && files.Has(q.Id, a) ? files.PathOf(q.Id, a) : null)).ToList(),
        q.Then.Select(s => new QuestStepResponse(s.To, s.Title, s.Body, s.SetUpIn)).ToList(),
        q.Parent,
        q.Conflicts.Select(c => ToConflict(c, taken?.Conflicts.FirstOrDefault(read => read.Conflict.Machine == c.Machine && read.Conflict.Sequence == c.Sequence)))
            .ToList(),
        q.Awaits,
        q.PublishedBy,
        deletable,
        q.Lanes,
        q.Requirements.Select(r => new QuestRequirementResponse(
            r.Quote, r.Check, r.Evidence.Select(e => new QuestEvidenceResponse(e.Path, e.Gate)).ToList())).ToList(),
        q.Answers.Select(a => new QuestAnswerResponse(a.Requirement, a.Met, a.Departed, a.Quote)).ToList(),
        q.Held,
        q.Accepted,
        q.Name,
        q.Hold is { } hold ? QuestEvidenceCodes.Spell(hold) : null,
        q.AwaitsEvidence,
        q.Evidence is { } read
            ? new QuestEvidenceVerdictResponse(
                read.Commit, read.How, read.Session,
                read.Items.Select(i => new QuestEvidenceReadResponse(i.Requirement, i.Path, i.Gate, i.Result, i.Object, i.Changed, i.Spelled)).ToList(),
                read.At, read.Machine,
                read.Submission is { } asked ? new QuestSubmissionRefResponse(asked.Machine, asked.Sequence) : null)
            : null,
        // A local set-up's address is absent where it came from another machine, as the wire left it.
        q.Review is { } chosen ? new QuestReviewChoiceResponse(chosen.Choice, chosen.Words) : null,
        q.SetUpIn,
        q.SetUps.Count == 0
            ? null
            : q.SetUps.Select(s => new QuestSetUpWire(s.Commit, s.Look, s.Shows, s.Again, s.Served, s.Run, s.Session, s.Local, s.At, s.Machine, s.Sequence)).ToList(),
        q.Verdicts.Count == 0
            ? null
            : q.Verdicts.Select(v => new QuestReviewVerdictWire(
                v.Said, v.SetUp is { } named ? new QuestSetUpRefResponse(named.Machine, named.Sequence) : null, v.Commit, v.Words, v.At, v.Machine)).ToList(),
        // The note's parts travel uncleaned, as the note itself does.
        NoteParts.Element(q.NoteParts),
        q.Closure,
        q.Submission is { } submitted
            ? new QuestSubmissionWire(submitted.Machine, submitted.Sequence, submitted.At, submitted.Note, q.SubmissionCommit, submitted.Session)
            : null,
        q.GrewFrom is { } grew ? new QuestSubmissionRefResponse(grew.Machine, grew.Sequence) : null,
        q.SourceReopened is { } reopened ? new QuestSubmissionRefResponse(reopened.Machine, reopened.Sequence) : null,
        q.Reopened is { } reopen
            ? new QuestReopenWire(
                reopen.Machine, reopen.Sequence, new QuestSubmissionRefResponse(reopen.Of.Machine, reopen.Of.Sequence), reopen.Note, reopen.At,
                reopen.Session)
            : null,
        taken is { Settled.Count: > 0 } ? taken.Settled.Select(read => ToConflict(read.Conflict, read)).ToList() : null,
        taken?.Holder,
        here,
        q.WorkflowChoices.Count == 0
            ? null
            : q.WorkflowChoices.Select(c => new AskWorkflowChoiceResponse(c.Kind, c.Workflow, c.At ?? q.Updated, c.Words)).ToList(),
        q.Weight,
        q.Paths,
        q.Request);

    /// <summary>A conflict as the quest answers it, with what its log says of it: each fact absent where unset.</summary>
    public static QuestConflictResponse ToConflict(QuestConflict c, QuestConflictReading? read) => new(
        c.Machine, c.Attempted.ToString(), c.Note, c.At, c.Sequence,
        read is { LostToTheReopen: true } ? true : null,
        read?.Ended switch
        {
            QuestConflictEnd.Dropped => "dropped",
            QuestConflictEnd.BroughtForward => "brought-forward",
            _ => null,
        },
        read?.Forward is { } forward ? ToForward(forward) : null);

    /// <summary>A bring-forward's take, its standing in the claim's words.</summary>
    public static QuestForwardResponse ToForward(QuestForwardTake forward) =>
        new(forward.Take.Machine, forward.Take.Sequence, forward.At, forward.Standing.ToString().ToLowerInvariant());

    /// <summary>One quest's moves, by the allow-list the history keeps.</summary>
    public static QuestMovesResponse MovesOf(string quest, IReadOnlyList<QuestMoveReading> moves) =>
        new(quest, [.. moves.Select(move => new QuestMoveResponse(
            QuestMoves.Spell(move.Kind), move.At, move.Machine, move.Sequence, move.Number, move.Applied, move.From?.ToString(),
            move.Attempted?.ToString(), move.Dismisses is { } named ? new QuestMoveRefResponse(named.Machine, named.Sequence) : null,
            move.WaitsOn, move.Session, move.Evidence, move.SetUp, move.Verdict, move.Hold))]);

    // What a door hands the exchange: a half left out, or a whole one, arrives blank and is refused there naming which, the same
    // sentence every door gives; so does an evidence item naming both or neither.

    /// <summary>The requirements a publish names.</summary>
    public static IReadOnlyList<QuestRequirement> RequirementsOf(IReadOnlyList<QuestRequirementWire?>? given) =>
        (given ?? []).Select(r => new QuestRequirement(r?.Quote ?? "", r?.Check ?? "")
        {
            Evidence = [.. (r?.Evidence ?? []).Select(e => new QuestEvidence(e?.Path, e?.Gate))],
        }).ToList();

    /// <summary>A chain's steps; a set-up step's environment rides with its words.</summary>
    public static IReadOnlyList<QuestStep> StepsOf(IReadOnlyList<QuestStepWire?>? given) =>
        (given ?? []).Select(step => new QuestStep(step?.To ?? "", step?.Title ?? "", step?.Body ?? "") { SetUpIn = step?.SetUpIn }).ToList();

    /// <summary>A chain's review choice.</summary>
    public static QuestReview? ReviewOf(QuestReviewWire? given) => given is null ? null : new QuestReview(given.Choice ?? "", given.Words);
}
