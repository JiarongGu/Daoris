namespace Daoris.Knowledge;

/// <summary>
/// The quest an ask becomes, in words other than the ask's own — what an intake session writes once
/// it has read the ticket. A publish with no draft uses the person's sentence, as it always has.
/// </summary>
/// <param name="Title">One line: what is wanted. Blank takes the ask's first line.</param>
/// <param name="Body">Why, and the evidence, with the ask's words quoted beneath. Blank takes the ask's words alone.</param>
public sealed record AskDraft(string? Title, string? Body)
{
    /// <summary>Addresses beyond the ask's own — the ask's always travel too.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files beyond the ask's own — the ask's always travel too.</summary>
    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    /// <summary>What to ask next once this closes done — judged by the exchange, like any chain.</summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>
    /// What the person requires, in their own words with the check that proves each —
    /// judged by the exchange against the ask's words.
    /// </summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// The quest's short title, in the intake's words: the few that tell it apart in a list. Blank leaves the
    /// quest to be named from its own words.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The mark the intake puts on the quest, <see cref="QuestWeights"/>: <c>design</c> where it reads the ask as design
    /// or decision work. The desk has no rule of its own for it, so the intake's reading is the mark; the exchange judges it.
    /// </summary>
    public string? Weight { get; init; }

    /// <summary>
    /// The paths the intake declares the work touches: added to the ask's own, never in place of them, so an agent's
    /// declaration cannot drop the person's. The exchange judges them.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>
    /// The chain's review choice (design §1.5): from an intake, only with the person's words it quotes, which
    /// the exchange checks against the ask's. Null for none.
    /// </summary>
    public QuestReview? Review { get; init; }

    /// <summary>
    /// The intake's review proposal for this chain, where it has none of the person's words to set one on (design §1.5–§1.6):
    /// kept on the ask, beside the choice, for the person's press. Null for none.
    /// </summary>
    public ReviewProposed? ReviewProposal { get; init; }

    /// <summary>
    /// The intake's kind proposal for this chain (design §4.3): its reason must quote the person's words on the ask,
    /// which the exchange checks. Kept on the ask for the person's press; the service never applies it. Null for none.
    /// </summary>
    public WorkflowProposed? WorkflowProposal { get; init; }
}
