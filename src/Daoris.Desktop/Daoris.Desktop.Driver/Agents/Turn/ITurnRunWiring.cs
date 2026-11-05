namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands its run (<see cref="AcpRun"/>): the open, the resume and the close, the run's state to set as it
/// begins, and what the run's end reads and leaves.
/// </summary>
internal interface ITurnRunWiring : ITurnPromptWiring, ITurnReadWiring
{
    /// <summary>The run begins: its token, its listener, its inbox, its retry and its first write's word, set before anything is sent.</summary>
    void Begin(CancellationToken stops, Action<IAgentWord>? asked, IAgentInbox? inbox, TransientRetry? retry, Action? prompted);

    /// <summary>A new session opened on the tree.</summary>
    Task OpenAsync(string cwd, CancellationToken ct, ConnectorPlan? plan = null);

    /// <summary>A conversation resumed instead of a session opened.</summary>
    Task ResumeAsync(string cwd, string conversation, CancellationToken ct, ConnectorPlan? plan = null);

    /// <summary>The session closed politely, bounded.</summary>
    Task CloseAsync(CancellationToken ct);

    /// <summary>The reader let go.</summary>
    void Release();

    /// <summary>The updates counted into the session's outcome.</summary>
    int Updates { get; }

    /// <summary>The largest context reading the session reported, or null.</summary>
    AcpUsage? Usage { get; }

    /// <summary>Whether the agent said it takes a prompt during a turn.</summary>
    bool NextStep { get; }

    /// <summary>Asked at a turn's end before a held word is prompted: whether the setup the session opened under changed since.</summary>
    Func<bool>? SetupChanged { get; }

    /// <summary>The background work still running as the run ended, kept for the driver.</summary>
    void Left(IReadOnlyList<string> running);
}
