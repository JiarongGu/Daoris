using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Daoris.Driver;

// The ports a provider written on the contract alone needs (AGENTPROVIDER1 row b; the agent-provider design §2.3, §11). A part states
// its port or is stated absent; today's behaviour for an agent that says nothing is each port's default (D53). Row b declares them and
// the stand-ins use the ones they need; the rows that move a port's readers (e, h, i, k, m) fill the rest.

/// <summary>
/// A door's spawn (the design §2.3, §4.6): a start's, a conversation's and a native resume's process, and the carriers it hands its
/// own process. Row h turns the answers into Platform's <c>ProcessRecipe</c> completed by the transports' one shell; until then a door
/// answers today's <see cref="ProcessStartInfo"/>, prepared by <see cref="Spawning"/>, so no spawn changes.
/// </summary>
public interface ISpawn
{
    /// <summary>The process that would be a driven session: preparation only, the driver owns its lifetime (D46 §5).</summary>
    ProcessStartInfo Start(SessionTarget target, IReadOnlyList<string>? command);

    /// <summary>The process that would be a conversation: the same spawn with stdin open.</summary>
    ProcessStartInfo Chat(ChatTarget target, IReadOnlyList<string>? command);

    /// <summary>The native door's resume of a conversation the agent kept; null where the door knows none.</summary>
    ProcessStartInfo? Resume(SessionTarget target, IReadOnlyList<string>? command, string conversation, string prompt) => null;

    /// <summary>The variables that load a session's tools with its first request; empty where there is no such switch.</summary>
    IReadOnlyDictionary<string, string> ToolsUpFront => ReadOnlyDictionary<string, string>.Empty;

    /// <summary>Hands a pipe-door spawn the servers Daoris offers, as the agent's own flag; nothing by default.</summary>
    void HandServers(ProcessStartInfo info, string configFile) { }

    /// <summary>Hands a pipe-door spawn its rules file; nothing by default.</summary>
    void HandSettings(ProcessStartInfo info, string settingsFile) { }

    /// <summary>Hands a pipe-door spawn the harness plugins composed for it; nothing by default.</summary>
    void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins) { }
}

/// <summary>What a door reads of its program's wire beyond the transport's: a native door's lines, or a protocol door's frames (§2.6).</summary>
public interface IDialect;

/// <summary>
/// A native door's dialect (the design §2.6): a reader of its program's structured stdout, new per session, and how a person's message
/// and an interrupt are framed on its stdin. Absent, a door is read as plain lines and an end, nothing more, which is every pipe door
/// until its wire is checked.
/// </summary>
public interface INativeDialect : IDialect
{
    /// <summary>A reader of this program's own structured stdout, new per session (today's <see cref="IStreamMapper"/>).</summary>
    IStreamMapper Mapper();

    /// <summary>A person's message as the program reads it on stdin.</summary>
    string FrameMessage(string text) => text;

    /// <summary>The line that stops a turn and keeps the session; null where the program has none.</summary>
    string? FrameInterrupt() => null;
}

/// <summary>
/// A protocol door's dialect (the design §2.6): what the shared transport hands the agent's own readings. Declared in row b and read
/// from row i; until then the transport reads every agent's extensions inline.
/// </summary>
public interface IProtocolDialect : IDialect
{
    /// <summary>What <c>session/new</c> carries to take the rules file and the plugins composed for the session; null sends no <c>_meta</c>.</summary>
    object? SessionMeta(string settingsFile, IReadOnlyList<string> plugins) => null;
}

/// <summary>How an account's windows are asked between turns (the design §2.3): the program, the command, the answers recorded and the freshness.</summary>
public interface IUsageQuestion
{
    /// <summary>The harness whose binary the command names.</summary>
    string Harness { get; }

    /// <summary>The binary and arguments that start the question.</summary>
    IReadOnlyList<string> Command { get; }

    /// <summary>The answers the reader was written against.</summary>
    IReadOnlyList<RecordedFrame> Recorded { get; }

    /// <summary>The protocol the command speaks.</summary>
    UsageKind Kind { get; }

    /// <summary>How long a reading stands before an account is asked again.</summary>
    TimeSpan Fresh { get; }
}

/// <summary>How an agent's lineage names a child (the design §2.3): its hook's lines, its calls' <c>_meta</c> keys, its stream's signals.</summary>
public interface IChildVocabulary
{
    IReadOnlyList<string> HookLines { get; }

    IReadOnlyList<string> CallKeys { get; }

    IReadOnlyList<string> StreamSignals { get; }

    /// <summary>Whether the agent says a child's end; where it does not, an end stays unknown.</summary>
    bool SaysEnd { get; }
}

/// <summary>What a conversation keeps under an account's home, by the agent's version (the design §2.3).</summary>
public interface IConversationHome
{
    /// <summary>The paths under an account's home a conversation keeps, for this version of the agent (null: any).</summary>
    IReadOnlyList<string> Keeps(string? version);
}
