namespace Daoris.Knowledge;

/// <summary>
/// What a publish is asked for: who asks, of whom, what — and what the quest carries beside its words.
/// </summary>
/// <remarks>
/// A file arrives one of two ways and the type says which. <see cref="Uploads"/> come WITH their
/// bytes, from a door on the machine that has them, and are kept under this machine's home.
/// <see cref="Named"/> come by name only, from a machine that keeps the bytes itself and is relaying
/// to the quest's home — the only shape a shared deployment ever takes, because it keeps names and
/// never bytes. A relay has no field for bytes at all: absent, not policed.
/// </remarks>
public sealed record QuestAsk(string From, string To, string Title, string Body)
{
    public IReadOnlyList<string> Links { get; init; } = [];

    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    public IReadOnlyList<QuestAttachment> Named { get; init; } = [];

    /// <summary>What to ask next when this closes done — judged here, when the chain is composed.</summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>
    /// The circle a sender that is NOT a repository asks from — an ask, which has no registry
    /// row to say it. Ignored for a registered sender, whose row is the machine's wiring and decides.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>The session whose connector publishes, when one does — kept on the quest.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>
    /// Whether an agent publishes it, named session or not: its authority, kept apart from
    /// <see cref="PublishedBy"/>, which only says which session to credit. A connector always sets it, since a connector is
    /// always an agent's. Only a door that sets neither is the person's own, and only it sets a review choice unquoted.
    /// </summary>
    public bool ByAgent { get; init; }

    /// <summary>
    /// What the person requires, each quoting their words on the ask that asks it —
    /// judged here against that ask's words, so every door refuses the same quote.
    /// </summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// Its short title: the few words that tell it apart in a list, the publisher's own — judged here, so every
    /// door refuses the same one. Null or blank is none, and the quest is then named from its words.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The mark on the work, <see cref="QuestWeights"/>: <c>design</c> where the publisher or the intake reads the quest
    /// as design or decision work. Judged here, so every door refuses the same one. Null or blank is none.
    /// </summary>
    public string? Weight { get; init; }

    /// <summary>
    /// The paths the work declares it touches, <see cref="QuestPaths"/>: repository-relative globs, judged here so every
    /// door refuses the same one. Empty is none.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>
    /// The workflow request's key, <see cref="QuestRequests"/>: <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>, which widens the quest's id so
    /// the same key sent again is its own quest. Null for every other publish.
    /// </summary>
    public string? Request { get; init; }

    /// <summary>
    /// The chain's review choice, inherited by each step: the person's, with any words they give,
    /// or an agent's quoting them, which is judged here against the ask's words as a requirement's quote is. An agent's
    /// <c>off</c> is kept on the ask as a proposal instead, never as the choice. Null for none.
    /// </summary>
    public QuestReview? Review { get; init; }

    /// <summary>
    /// An intake's kind proposal, whose reason is judged here against the ask's words. The exchange keeps
    /// nothing of it: the ask's desk does, and the person's press applies it. Null for none.
    /// </summary>
    public WorkflowProposed? WorkflowProposal { get; init; }
}
