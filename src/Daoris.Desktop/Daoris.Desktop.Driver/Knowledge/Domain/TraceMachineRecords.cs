using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>This machine's record of a session (D76).</summary>
public sealed record TraceEvents
{
    /// <summary>Its store: <see cref="TraceStores.Events"/>.</summary>
    public string Source => TraceStores.Events;

    /// <summary>Why it could not be read: <see cref="TraceEventGaps.NotAnId"/>, <see cref="TraceEventGaps.NoneHere"/> or <see cref="TraceEventGaps.Unread"/>.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    /// <summary>The driver's notes before its instruction, in the driver's words: they name the account a start chose and why.</summary>
    public IReadOnlyList<string> Starts { get; init; } = [];

    /// <summary>Each instruction it was handed, by its event and size, never its words.</summary>
    public IReadOnlyList<TraceInstruction> Instructions { get; init; } = [];

    /// <summary>Each acceptance of its work a landing's press kept (D100), in the driver's words.</summary>
    public IReadOnlyList<TraceAcceptance> Accepted { get; init; } = [];
}

/// <summary>One instruction: its event, its length, what the record keeps of a cut one, and its account (CONTEXT1).</summary>
public sealed record TraceInstruction(long Seq, int Chars)
{
    /// <summary>Where the record cut it: how many of its characters it keeps.</summary>
    public int? Kept { get; init; }

    /// <summary>What it was handed, section by section; null for one handed before the driver kept an account.</summary>
    public InstructionAccount? Account { get; init; }
}

public sealed record TraceAcceptance(DateTimeOffset At, string Said);

/// <summary>The rules a session was handed (PERM1, D72), from the file the driver wrote for it while that file stands.</summary>
public sealed record TraceRules
{
    /// <summary>Its store: <see cref="TraceStores.Rules"/>.</summary>
    public string Source => TraceStores.Rules;

    /// <summary>Why none are read: <see cref="TraceRuleGaps.NoneWhileRunning"/>, <see cref="TraceRuleGaps.GoneWithRun"/> or <see cref="TraceRuleGaps.Unread"/>.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    public int Allowed { get; init; }

    public int Asked { get; init; }

    public int Denied { get; init; }

    /// <summary>Hard denials beside the harness's own.</summary>
    public int Hard { get; init; }

    /// <summary>The tree guard holds it to its tree (PERM3).</summary>
    public bool Guarded { get; init; }

    /// <summary>
    /// Whether a handed settings file carries the tree guard: a <c>PreToolUse</c> entry on the guard's own matcher. Not any
    /// <c>PreToolUse</c> hook, since the lineage hook (FABRIC-C1a) is one too and guards nothing.
    /// </summary>
    public static bool HandsTreeGuard(System.Text.Json.JsonElement settings) =>
        settings.ValueKind == System.Text.Json.JsonValueKind.Object
        && settings.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == System.Text.Json.JsonValueKind.Object
        && hooks.TryGetProperty("PreToolUse", out var before) && before.ValueKind == System.Text.Json.JsonValueKind.Array
        && before.EnumerateArray().Any(entry => entry.ValueKind == System.Text.Json.JsonValueKind.Object
            && entry.TryGetProperty("matcher", out var matcher) && matcher.ValueKind == System.Text.Json.JsonValueKind.String
            // The edit tools alone is a guard handed before TREESHELL1, by a session still running from then.
            && matcher.GetString() is { } named && (named == KnowledgeWorkplace.Current.TreeGuardMatcher || named == KnowledgeWorkplace.Current.TreeGuardEditMatcher));
}

/// <summary>
/// What this machine's events file for one session held (STRUCT3g-i): its events, or, where none were read, why
/// (<see cref="TraceEventGaps"/>) and the problem the read met. The chain takes it as a value, so it opens no file.
/// </summary>
internal sealed record TraceEventsRead(IReadOnlyList<SessionEvent>? All, string? Missing = null, string? Problem = null);

/// <summary>What the rules file written for one session held: its text, or null when none stands, or the problem the read met.</summary>
internal sealed record TraceRulesRead(string? Text, string? Problem = null);

/// <summary>
/// This machine's side of a trace, as values the chain reads without touching a file (STRUCT3g-i): the driver's settings,
/// and a read of each session's events and of the rules file it was handed.
/// </summary>
internal sealed record TraceHere(DriverConfig Config, Func<string, TraceEventsRead> ReadEvents, Func<string, TraceRulesRead> ReadRules);
