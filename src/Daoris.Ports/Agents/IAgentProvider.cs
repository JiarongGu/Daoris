namespace Daoris.Ports.Agents;

/// <summary>A harness the application can start a session on: what it is called and what it can do, and the means to start a run of it.</summary>
public interface IAgentProvider
{
    /// <summary>What the host knows of the harness.</summary>
    ProviderDescription Description { get; }

    /// <summary>Makes a run of the harness for <paramref name="start"/>; the run is opened by the caller.</summary>
    ValueTask<IAgentRun> CreateAsync(AgentStart start, CancellationToken ct);
}

/// <summary>What the host knows of a harness.</summary>
/// <param name="Id">The harness's stable id.</param>
/// <param name="Name">What a person calls it.</param>
/// <param name="Abilities">What it can do, by the host's words for an ability.</param>
public sealed record ProviderDescription(string Id, string Name, IReadOnlyList<string> Abilities);

/// <summary>What a run of a harness is made for.</summary>
/// <param name="Session">The session the run belongs to.</param>
/// <param name="Folder">The folder its work happens in.</param>
/// <param name="Conversation">The conversation to resume, or null to begin one.</param>
public sealed record AgentStart(string Session, string Folder, string? Conversation = null);

/// <summary>
/// One run of a harness. Its two word ports stay apart: a native door's input is acknowledged once the harness names its conversation
/// and replayed to the next process, and a protocol door's request is answered by its session.
/// </summary>
public interface IAgentRun : IAsyncDisposable
{
    /// <summary>Begins a conversation.</summary>
    ValueTask OpenAsync(CancellationToken ct);

    /// <summary>Resumes <paramref name="conversation"/>.</summary>
    ValueTask ResumeAsync(string conversation, CancellationToken ct);

    /// <summary>Hands text to a native door's input, and answers once the door has taken it.</summary>
    ValueTask InputAsync(string text, CancellationToken ct);

    /// <summary>Sends a request to a protocol door and answers what its session replied.</summary>
    ValueTask<string> RequestAsync(string request, CancellationToken ct);

    /// <summary>Ends the turn that runs.</summary>
    ValueTask StopTurnAsync(CancellationToken ct);

    /// <summary>Closes the run.</summary>
    ValueTask CloseAsync(CancellationToken ct);

    /// <summary>Waits for the run to end and answers how.</summary>
    ValueTask<AgentEnd> EndAsync(CancellationToken ct);
}

/// <summary>How a run ended: the harness's exit where it has one, and the word for its ending.</summary>
public sealed record AgentEnd(int? Exit, string Ending);
