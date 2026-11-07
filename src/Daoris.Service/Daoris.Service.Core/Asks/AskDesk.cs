namespace Daoris.Knowledge;

/// <summary>
/// The judgement over asks: what may be asked where, what the no-model tier proposes, and
/// how an ask becomes a quest. The store holds state; this decides — the exchange's shape, for the
/// same reason: two doors (the page and `daoris ask`), one set of answers. The desk is the root that wires: each
/// responsibility is a type of its own (<see cref="AskStandings"/>, <see cref="AskPublishing"/>, <see cref="AskQuests"/>,
/// <see cref="AskChoices"/>, <see cref="AskGoAheadAnswers"/>, <see cref="AskDeletes"/>), and each door here is one line to it.
/// </summary>
/// <remarks>
/// <b>An ask's quests are asked BY the ask</b> — <c>ask #id</c> is their sender. It cannot be mistaken
/// for a repository, it names the record a reader can follow back to the person's words, and the
/// exchange places it in the ask's own circle, because an ask has no registry row to say it.
/// </remarks>
public sealed class AskDesk
{
    /// <summary>The tier that answers with no harness at all: the repositories' own declarations.</summary>
    public const string ByDeclarations = "declarations";

    /// <summary>The asker named the receiver — no tier had to decide.</summary>
    public const string ByName = "named";

    /// <summary>
    /// The ask's own intake session published it — a harness decided, and which harness is
    /// on the session record. Never a model's name: the harness carries the model.
    /// </summary>
    public const string ByIntake = "intake";

    private readonly AskStandings standings;
    private readonly AskPublishing publishing;
    private readonly AskChoices choices;
    private readonly AskGoAheadAnswers goAheads;
    private readonly AskDeletes deletes;

    public AskDesk(KnowledgeService service, AskStore asks, QuestExchange exchange, QuestFiles? files)
    {
        // The files an ask keeps, until it becomes quests: the home's AskFiles.
        var askFiles = files?.For(HomeEntry.AskFiles);
        standings = new(asks, exchange);
        publishing = new(service, asks, exchange, askFiles, standings, new AskQuests(exchange, askFiles));
        choices = new(asks, standings);
        goAheads = new(asks, standings);
        deletes = new(asks, exchange, askFiles, standings);
    }

    /// <summary>The sender every quest an ask becomes is published by.</summary>
    public static string SenderOf(string askId) => $"ask #{askId}";

    /// <summary>
    /// The ask that asked a quest, read from its sender — the reverse of <see cref="SenderOf"/>;
    /// null for a quest a repository asked. A chain step names the ask too, since it is published on the
    /// ask's behalf.
    /// </summary>
    public static string? AskOf(string? sender)
    {
        var prefix = SenderOf("");
        return sender is not null && sender.Length > prefix.Length && sender.StartsWith(prefix, StringComparison.Ordinal)
            ? sender[prefix.Length..]
            : null;
    }

    /// <summary>A circle's asks, or every circle's, newest first, each as it stands; see <see cref="AskStandings.ListAsync"/>.</summary>
    public Task<IReadOnlyList<Ask>> ListAsync(string? workspace = null, bool includeClosed = false, CancellationToken ct = default) =>
        standings.ListAsync(workspace, includeClosed, ct);

    /// <summary>One ask as it stands, or null when there is none.</summary>
    public Task<Ask?> FindAsync(string id, CancellationToken ct = default) => standings.FindAsync(id, ct);

    /// <summary>What an ask is, read against the quests asked by it; see <see cref="AskStandings.Standing"/>.</summary>
    public static Ask Standing(Ask ask, IReadOnlyList<Quest> asked) => AskStandings.Standing(ask, asked);

    /// <summary>What stands of an ask whose last quest is deleted; see <see cref="AskStandings.NoQuestLeft"/>.</summary>
    public static string NoQuestLeft(Ask ask) => AskStandings.NoQuestLeft(ask);

    /// <summary>Delete an ask made by mistake, with every quest asked by it; see <see cref="AskDeletes.DeleteAsync"/>.</summary>
    public Task<AskOutcome> DeleteAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        deletes.DeleteAsync(id, now, ct);

    /// <summary>Record an ask, and answer it; see <see cref="AskPublishing.AskAsync"/>.</summary>
    public Task<AskOutcome> AskAsync(AskRequest request, DateTimeOffset now, CancellationToken ct = default) =>
        publishing.AskAsync(request, now, ct);

    /// <summary>A person, or the ask's intake session, turns an ask into a quest; see <see cref="AskPublishing.PublishAsync"/>.</summary>
    public Task<AskOutcome> PublishAsync(
        string id, string to, DateTimeOffset now, CancellationToken ct = default,
        AskDraft? draft = null, string? session = null, bool byAgent = false) =>
        publishing.PublishAsync(id, to, now, ct, draft, session, byAgent);

    /// <summary>The person answers their own ask: closed, with the reason, and final.</summary>
    public Task<AskOutcome> CloseAsync(string id, string reason, DateTimeOffset now, CancellationToken ct = default) =>
        publishing.CloseAsync(id, reason, now, ct);

    /// <summary>The person's answer to a go-ahead; see <see cref="AskGoAheadAnswers.AnswerAsync"/>.</summary>
    public Task<GoAheadAnswerOutcome> AnswerGoAheadAsync(
        string id, int number, bool approved, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        goAheads.AnswerAsync(id, number, approved, words, now, ct);

    /// <summary>The person sets the ask's review choice; see <see cref="AskChoices.ChooseReviewAsync"/>.</summary>
    public Task<AskOutcome> ChooseReviewAsync(string id, string? choice, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        choices.ChooseReviewAsync(id, choice, words, now, ct);

    /// <summary>The person sets the ask's kind and workflow; see <see cref="AskChoices.ChooseWorkflowAsync"/>.</summary>
    public Task<AskOutcome> ChooseWorkflowAsync(
        string id, string? kind, string? workflow, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        choices.ChooseWorkflowAsync(id, kind, workflow, words, now, ct);
}
