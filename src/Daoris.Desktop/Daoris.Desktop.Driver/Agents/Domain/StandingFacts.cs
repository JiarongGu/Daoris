namespace Daoris.Driver;

/// <summary>
/// What a session's standing block is composed from (CONTEXTONE1c, D230 point 3; the context-one design §3 C), as plain values: every
/// input the driver's, read at the session's start, or at a move with the memory its line read. Context composes it.
/// </summary>
public sealed record StandingFacts
{
    /// <summary>The suffix a session's folder for its block's file carries after its id under <c>spawn/</c>.</summary>
    public const string FolderSuffix = ".standing";

    /// <summary>The repository it works in; null for a kind with no repository, which is handed the workspace's part alone.</summary>
    public string? Repository { get; init; }

    /// <summary>The workspace it works for; null where none is named.</summary>
    public string? Workspace { get; init; }

    /// <summary>The code map its tree keeps, repository-relative; null where it keeps none.</summary>
    public string? CodeMap { get; init; }

    /// <summary>The indexes its tree keeps for the repository's own documents, repository-relative (<see cref="RepositoryIndexes"/>).</summary>
    public IReadOnlyList<string> Indexes { get; init; } = [];

    /// <summary>Whether its instruction lists other checkouts it may read, which the block names among the places to look.</summary>
    public bool Checkouts { get; init; }

    /// <summary>Whether it may call the connector's knowledge and memory tools: false for a brainstorm's pass, which reads its room alone.</summary>
    public bool Knowledge { get; init; } = true;

    /// <summary>The tree's skill folders, named only where its harness loads none of them itself (<see cref="AgentDoor.LoadsTreeSkills"/>).</summary>
    public IReadOnlyList<string> Skills { get; init; } = [];

    /// <summary>The repository's rules, carried only where its harness cuts the native instruction file (<see cref="AgentDoor.InstructionFileLimit"/>).</summary>
    public RuleIndex? Rules { get; init; }

    /// <summary>Its repository's routing standing, for a work that may publish to that repository's lanes; null for every other.</summary>
    public string? Routing { get; init; }

    /// <summary>The lanes its repository declares, by their ids, which the routing names by their addresses.</summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>The person's choices on this machine for its repository (the standing answer, D135 §3, D230 point 4); null where none is kept.</summary>
    public StandingAnswer? Choices { get; init; }

    /// <summary>Whether its harness takes no rules file (<see cref="AgentDoor.TakesSettings"/>), so none of the person's denies reaches it.</summary>
    public bool NoDenies { get; init; }

    /// <summary>The memory its line read, newest first, as the service rechecked it when a move's brief was composed; none at a first start.</summary>
    public IReadOnlyList<string> Memory { get; init; } = [];

    /// <summary>
    /// The file the block goes through where the whole first prompt would pass its bound with it; null where no file can be written for
    /// the session, and the block is handed inline whatever its length.
    /// </summary>
    public string? File { get; init; }
}

/// <summary>The repository's rules, past what its harness reads of its instruction file: their ids, which <c>knowledge_get</c> reads.</summary>
/// <param name="File">The instruction file the harness cuts, repository-relative.</param>
/// <param name="Limit">The most of it the harness reads, in bytes.</param>
/// <param name="Ids">Each rule's id in the knowledge, the repository's own row.</param>
public sealed record RuleIndex(string File, int Limit, IReadOnlyList<string> Ids);

/// <summary>A standing block as composed: its words, and its section of the composer's account (CONTEXT1).</summary>
/// <param name="Text">What the first prompt carries: the block, or the line naming the file it went through.</param>
public sealed record StandingBlockTold(string Text, HandedSection Account)
{
    /// <summary>Whether it went through its file, and <see cref="Text"/> is the line naming it.</summary>
    public bool Filed { get; init; }

    /// <summary>
    /// What a conversation's record keeps of the block told with its first message (the note beside <c>chat.told-browser</c>): the
    /// lead by its code, then the block as the program's, so the person sees what the agent was told.
    /// </summary>
    public Noted Note() => Noted.Of(ChatNoteCodes.ChatToldBlock, "told the agent Daoris's standing block:").Then(" ", Noted.Said(Text, NoteBy.Program));
}
