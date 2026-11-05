namespace Daoris.Driver;

/// <summary>
/// How an agent is asked an account's windows where its door carries none (CODEXUSE1, D125's CODEXUSE1 note): its own
/// server, started under the account's configuration home, asked one question and stopped. Declared on the toolchain as
/// <see cref="HarnessToolchain.Usage"/>, beside <see cref="HarnessToolchain.Windows"/>, which reads a door's frame instead.
/// </summary>
/// <param name="Harness">
/// The harness whose binary <paramref name="Command"/> names, resolved as every spawn is (D57): <c>driver.json</c>'s command
/// for it, then its managed pin, then <c>PATH</c>.
/// </param>
/// <param name="Command">The binary and the arguments that start the server.</param>
/// <param name="Recorded">The answers the reader was written against: an entry grows only with a recorded answer.</param>
/// <param name="Kind">Which protocol the command speaks (USAGEREAD2a): Codex's JSON-RPC by default.</param>
public sealed record UsageQuestion(
    string Harness, IReadOnlyList<string> Command, IReadOnlyList<RecordedFrame> Recorded, UsageKind Kind = UsageKind.Codex) : IUsageQuestion
{
    /// <summary>
    /// How long a reading stands before a start asks the account again: Codex's five minutes, and Claude's minute, a floor
    /// beside it (USAGEREAD2a) because its read is its only whole-account reading and a walk ranks on it.
    /// </summary>
    public TimeSpan Fresh => Kind == UsageKind.Claude ? ClaudeUsage.Floor : CodexUsage.Fresh;
}

/// <summary>The protocol a <see cref="UsageQuestion"/>'s command speaks (USAGEREAD2a).</summary>
public enum UsageKind
{
    /// <summary><c>codex app-server</c>: JSON-RPC 2.0 over stdio (<see cref="CodexUsage"/>).</summary>
    Codex,

    /// <summary>The native <c>claude</c>'s stream-json control requests (<see cref="ClaudeUsage"/>).</summary>
    Claude,
}

/// <summary>
/// What a <see cref="UsageQuestion"/> answered (USAGECAP1a1, D179 point 2): the walk's readings, as every caller always had
/// them, and beside them what the usage history keeps: every window the answer said (<see cref="Seen"/>), the plan's word where
/// the answer names it, the question asked and its reader's version. A caller that reads the walk's alone is unchanged.
/// </summary>
public sealed class UsageAnswer(
    IReadOnlyList<WindowReading> walk, IReadOnlyList<WindowSeen> seen, string? plan, string source, int reader)
    : IReadOnlyList<WindowReading>
{
    /// <summary>An answer that said nothing: no reading, nothing for the history.</summary>
    public static UsageAnswer None(string source, int reader) => new([], [], null, source, reader);

    /// <summary>Every window the answer said, read beside the walk's own (<see cref="WindowSeen"/>).</summary>
    public IReadOnlyList<WindowSeen> Seen => seen;

    /// <summary>The plan's word as the answer said it (Claude's <c>subscription_type</c>), or null: never who, nor the organisation.</summary>
    public string? Plan => plan;

    /// <summary>The question asked: <c>get_usage</c>, or Codex's <see cref="CodexUsage.Method"/>.</summary>
    public string Source => source;

    /// <summary>The version of the reader that parsed it.</summary>
    public int Reader => reader;

    public WindowReading this[int index] => walk[index];

    public int Count => walk.Count;

    public IEnumerator<WindowReading> GetEnumerator() => walk.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
