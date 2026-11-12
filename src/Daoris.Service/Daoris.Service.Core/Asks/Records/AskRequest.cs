namespace Daoris.Knowledge;

/// <summary>What a person asks, at a workspace.</summary>
/// <param name="Workspace">The circle it is asked in.</param>
/// <param name="Sentence">The person's words.</param>
public sealed record AskRequest(string Workspace, string Sentence)
{
    public IReadOnlyList<string> Links { get; init; } = [];

    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    /// <summary>The receiver, when the asker already knows whose problem it is — published at once.</summary>
    public string? To { get; init; }

    /// <summary>
    /// The asker's short title: the few words that tell the ask apart in a list, judged as a quest's is
    /// (<see cref="QuestTitles.Judge"/>) and refused past its bound. Null or blank for none.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>The paths the asker declares the work touches, judged as a quest's are (<see cref="QuestPaths.Judge"/>).</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>The steps that follow the named receiver's quest once it closes done; refused where <see cref="To"/> names none.</summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>Who asked, when the door knows.</summary>
    public string? Asker { get; init; }

    /// <summary>
    /// The person's review choice for the ask, from the composer (design §1.5): <c>off</c>, <c>on</c> or an
    /// environment's name. Null for none.
    /// </summary>
    public string? Review { get; init; }

    /// <summary>The person's words with their review choice, where they give any.</summary>
    public string? ReviewWords { get; init; }

    /// <summary>
    /// The person's kind for the ask's work, from the composer (design §4.3): a kind's id its workspace declares. Null for
    /// none.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>The person's workflow for the ask's work, from the composer: a workflow's id, or <c>current</c>. Null for none.</summary>
    public string? Workflow { get; init; }

    /// <summary>The person's words with their kind and workflow, where they give any.</summary>
    public string? WorkflowWords { get; init; }

    /// <summary>The person's requirements for the quest published at once to <see cref="To"/>, their quotes kept as words they said.</summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    public string? Through { get; init; }
}
