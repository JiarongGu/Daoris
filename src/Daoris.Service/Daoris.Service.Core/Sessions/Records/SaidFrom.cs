namespace Daoris.Knowledge;

/// <summary>The authors a word on a record names in its <see cref="SaidWord.From"/> (COWORK1b, D188): never the person, who is the absence.</summary>
public static class SaidFrom
{
    /// <summary>Daoris's own words to a session: a reopen told, a step's go-on (the message-urgency design §2.2).</summary>
    public const string Daoris = "daoris";

    /// <summary>
    /// Another agent's claim on an ask's channel that the person passed on to a session (COWORK1d, the message-urgency design §4.2):
    /// its words are the agent's, and its <see cref="SaidWord.Channel"/> names the message. Never the person's.
    /// </summary>
    public const string Agent = "agent";

    /// <summary>
    /// The supervisor the owner appointed, answering a park's question on their behalf (SUPERVISOR1a-i, D208): kept only through the
    /// say's conditional step with its decision, question and choice, its text the service's own. Never the person's.
    /// </summary>
    public const string Supervisor = "supervisor";

    /// <summary>Whether this build keeps a word by <paramref name="from"/>: none named, or one it knows.</summary>
    public static bool Known(string? from) => from is null or Daoris or Agent or Supervisor;
}
