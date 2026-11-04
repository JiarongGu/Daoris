namespace Daoris.Driver;

/// <summary>
/// What an executor may reach of the chat runner that builds it: the runner's shared state, its own start, run and walk
/// (<see cref="IChatRunning"/>), the doors a start says and ends through, and the other executors (<see cref="Of{T}"/>). The runner
/// implements it; an executor names only these, never the runner, so it lives in the subdomain of its job and the runner holds the order
/// and the wiring alone.
/// </summary>
internal interface IExecutorWiring : IChatRunning
{
    ChatContext Context { get; }

    /// <summary>
    /// Another executor, by its type or its port's: the one way an executor reaches a sibling, so no executor is handed another through a
    /// constructor or a callback the runner sets. Read when the executor runs: the set is the runner's once every executor is built.
    /// </summary>
    T Of<T>()
        where T : IExecutor;

    /// <summary>Hands a live conversation a person's message: false where nothing listens.</summary>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    bool Say(string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null);

    /// <summary>Ends a conversation as a person finishing does: the turns asked for run first.</summary>
    bool Finish(string sessionId);

    /// <summary>Ends a conversation's record in <paramref name="state"/>, saying why, and tells <paramref name="onEnded"/>.</summary>
    Task ConcludeAsync(string sessionId, string state, Noted note, Func<string, string, Task>? onEnded);

    /// <summary>What a conversation going on from a limit carries, for its opening line; null for a start that carries none.</summary>
    RotatedOpening.Carried? LimitCarried(string? carries);
}
