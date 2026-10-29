namespace Daoris.Driver;

/// <summary>
/// What the course's executors ask of the chat runner (D49 §3): open a conversation, place and run Ask Daoris's, go on in an ended
/// conversation's own, walk an actor's order and say what a start was told. Course declares it and the chat runner in Loop implements it,
/// exactly as the executors called the runner before it was a port; the places are Course's own records.
/// </summary>
internal interface IChatRunning
{
    /// <summary>Open a conversation in a repository and put a harness behind it.</summary>
    Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool? ownTree = null,
        CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null, IReadOnlyList<ActorEffective>? order = null,
        EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false, bool ownFirst = false, MoveWords? moves = null);

    /// <summary>Place Ask Daoris's conversation in its workspace's room: its record open, its agent and account chosen, nothing spawned.</summary>
    Task<HelpPlaced> PlaceHelpAsync(
        string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default, string? carries = null,
        IReadOnlyList<ActorEffective>? order = null);

    /// <summary>Run the Ask Daoris conversation placed.</summary>
    Task<ChatStart> RunHelpAsync(
        HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default);

    /// <summary>Run a conversation in a place: a new one's first message, or an ended one going on in its own conversation.</summary>
    Task<ChatStart> RunAsync(
        string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
        ChatPending? pending = null);

    /// <summary>What differs about a conversation in a repository's tree.</summary>
    ChatPlace RepositoryPlace(
        DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree,
        IReadOnlyList<string>? continued = null, bool answers = false);

    /// <summary>What differs about Ask Daoris's conversation in its room.</summary>
    ChatPlace HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace = null);

    /// <summary>Whether a chat run's process names a second opinion for a conversation's tree.</summary>
    Task<bool> AnswersFindingsAsync(string sessionId, string tree, DriverConfig config);

    /// <summary>Whether a turn of the conversation is taking.</summary>
    bool Taking(string sessionId);

    /// <summary>The start's process and maker, consumed by the first ordinary or handed-over message.</summary>
    ChatMessage WithProcess(string sessionId, ChatMessage message);

    /// <summary>Walk an actor's order for a new conversation.</summary>
    Task<ConversationWalk> WalkAsync(
        string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
        (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0);
}
