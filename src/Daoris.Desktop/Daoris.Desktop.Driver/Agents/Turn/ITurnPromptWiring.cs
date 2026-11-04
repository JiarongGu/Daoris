using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands the parts that write its prompts and steers and read their answers (<see cref="AcpPrompting"/>,
/// <see cref="AcpSteering"/>, <see cref="AcpTry"/>): the open prompts' state, the one request door to the agent, and what the run was
/// handed. They hold nothing of the session's but this.
/// </summary>
internal interface ITurnPromptWiring : ITurnRetryWiring
{
    /// <summary>The prompts on the wire and the run's door they pass, each lock of that state its own.</summary>
    OpenPrompts Open { get; }

    /// <summary>
    /// A request to the agent: <paramref name="admit"/> read under the write lock as it is written (no writes nothing), <paramref name="sent"/>
    /// told as the write returns, and <paramref name="answered"/> on the reader's thread before its task completes.
    /// </summary>
    Task<JsonElement> RequestAsync(
        string method, object parameters, CancellationToken ct, Action? sent = null, Action<JsonElement>? answered = null, Func<bool>? admit = null);

    /// <summary>The turn in flight stopped and the session kept: a notification, nothing to wait for.</summary>
    Task CancelTurnAsync();

    /// <summary>Whether the run's own token was cancelled: the one cancellation a run puts on the wire. A conversation has no run, and keeps its stop.</summary>
    bool Stopped { get; }

    /// <summary>Whether the agent said it steers words into a running turn.</summary>
    bool Steers { get; }

    /// <summary>The run's inbox; null for a conversation, and for a session nobody may tell anything.</summary>
    IAgentInbox? Inbox { get; }

    /// <summary>What the run's first prompt's write return tells, told again as each try's write returns.</summary>
    Action? OnPrompted { get; }

    /// <summary>The stage that reads the turn's models, and forgets the names a turn an error ended announced.</summary>
    AcpMeasure Measure { get; }

    /// <summary>The calls announced and not yet finished, and those a steer cut.</summary>
    StepCalls Calls { get; }
}
