namespace Daoris.Knowledge;

/// <summary>
/// A sentence entered at a WORKSPACE rather than at a repository (D65 §1a) — held as a record of what
/// was asked, with what, by whom, when, and what became of it.
/// </summary>
/// <param name="Id">Short, stable handle — the same sentence in the same circle is the same ask.</param>
/// <param name="Workspace">The circle it was asked in, and therefore whom it can reach (D48 §4).</param>
/// <param name="Sentence">The person's words, verbatim.</param>
/// <param name="State">What became of it.</param>
/// <param name="Tier">Which tier answered it — said on every record (<c>model-decoupling</c>).</param>
/// <param name="Asked">When it was asked.</param>
/// <param name="Updated">When it last changed.</param>
/// <param name="Asker">Who asked, when the door knows — a key's name at a keyed door; null is this machine's person.</param>
/// <param name="Note">Why it was closed, or why its named receiver was refused.</param>
public sealed record Ask(
    string Id, string Workspace, string Sentence, AskState State, string Tier,
    DateTimeOffset Asked, DateTimeOffset Updated, string? Asker = null, string? Note = null)
{
    /// <summary>Addresses it carries, judged as a quest's are — they travel to every quest it becomes.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>
    /// The short title its asker gave (D162 point 4), as a quest's is: at most <see cref="QuestTitles.MaxShort"/>
    /// characters on one line, as written. Null where none was given, which is every ask from before: a page then heads it with
    /// the sentence's first line.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The paths its asker declared the work touches, <see cref="QuestPaths"/>: each quest it publishes carries them, with
    /// whatever its intake's draft adds. Empty where none were given, which is every ask from before.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>Files it carries, by name — the bytes are kept under this machine's home until it becomes quests.</summary>
    public IReadOnlyList<QuestAttachment> Attachments { get; init; } = [];

    /// <summary>What the declarations tier proposed, best first, with the words that matched.</summary>
    public IReadOnlyList<DeclarationMatch> Proposal { get; init; } = [];

    /// <summary>The quests it became, in the order they were published.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>
    /// The intake session that served it (D65 §1b), once one was opened — the record a reader follows
    /// to what the intake read, decided and asked. Null for an ask no intake has served.
    /// </summary>
    public string? Intake { get; init; }

    /// <summary>
    /// Whether it may be deleted (D95): nothing stands on any quest asked by it. Answered by the desk's
    /// reads, so a page offers the verb only where the service would take it; false on a record read
    /// straight from the store, which judges nothing.
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// What the person said on it after asking it, oldest first: each answer to a session that
    /// parked to ask them and each message added to one while it ran. Appended where it is kept and never
    /// rewritten, so nothing the agents wrote stands in for it (D133 §1).
    /// </summary>
    public IReadOnlyList<AskWord> Later { get; init; } = [];

    /// <summary>
    /// Every word the person gave on it (D133 §1): its own sentence first, as it was asked and to
    /// no session, then <see cref="Later"/>. Derived from the sentence rather than stored beside it, so an
    /// ask from before the words were kept still opens with what it asked.
    /// </summary>
    public IReadOnlyList<AskWord> Words => [new(AskWordKind.Asked, Sentence, Asked), .. Later];

    /// <summary>
    /// From when its later words are kept, for an ask made before they were: the moment this
    /// machine's store first opened on a build that keeps them. Its answers and messages before then were
    /// never kept and are not back-filled, so <see cref="Words"/> is not all it was told. Null for every ask
    /// made since, whose words are kept from its first.
    /// </summary>
    /// <remarks>
    /// Kept from then by the doors that report a word: the say door's answer to a park at once, and a message added to a
    /// running session once its driver reports it (`POST /api/sessions/{id}/added`).
    /// </remarks>
    public DateTimeOffset? WordsKeptFrom { get; init; }

    /// <summary>
    /// The go-aheads its sessions asked the person for (D135 §2), one per act, oldest first, each with every
    /// session's request for it and the person's answer: kept beside their words, so a later session is handed what was
    /// approved and refused rather than asking it again.
    /// </summary>
    public IReadOnlyList<GoAhead> GoAheads { get; init; } = [];

    /// <summary>
    /// The person's review choices for every chain it publishes that sets none (D154 point 3; design §1.4 row 4,
    /// §1.5), oldest first, each with when and any words they gave. The latest stands, as a go-ahead's answer does.
    /// </summary>
    public IReadOnlyList<AskReviewChoice> ReviewChoices { get; init; } = [];

    /// <summary>
    /// Its intake's review proposals (design §1.5–§1.6), oldest first, each with its reason: a reading, kept
    /// beside the person's choice and applied only by their press.
    /// </summary>
    public IReadOnlyList<AskReviewProposal> ReviewProposals { get; init; } = [];

    /// <summary>
    /// The person's kind and workflow for every chain it publishes (D157 point 10; the workflow design §4.1 row 1, §4.3),
    /// oldest first, each with when and any words they gave. The latest stands; one naming neither clears it.
    /// </summary>
    public IReadOnlyList<AskWorkflowChoice> WorkflowChoices { get; init; } = [];

    /// <summary>
    /// Its intake's kind proposals (the workflow design §4.3), oldest first, each with its reason quoting the person: a
    /// reading, kept beside the person's choice and applied only by their press.
    /// </summary>
    public IReadOnlyList<AskWorkflowProposal> WorkflowProposals { get; init; } = [];
}
