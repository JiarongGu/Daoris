namespace Daoris.Driver;

/// <summary>
/// How an agent is asked an account's windows where its door carries none (CODEXUSE1, D125's CODEXUSE1 note): its own
/// server, started under the account's configuration home, asked one question and stopped. Declared on the toolchain as
/// <see cref="HarnessToolchain.Usage"/>, beside <see cref="HarnessToolchain.Windows"/>, which reads a door's frame instead. Each
/// agent's is its own type, in its provider's folder, that says how it is asked, how long a reading stands and what the history
/// calls its source: <see cref="CodexUsageQuestion"/> and <see cref="ClaudeUsageQuestion"/>.
/// </summary>
/// <param name="Harness">
/// The harness whose binary <paramref name="Command"/> names, resolved as every spawn is (D57): <c>driver.json</c>'s command
/// for it, then its managed pin, then <c>PATH</c>.
/// </param>
/// <param name="Command">The binary and the arguments that start the server.</param>
/// <param name="Recorded">The answers the reader was written against: an entry grows only with a recorded answer.</param>
public abstract record UsageQuestion(string Harness, IReadOnlyList<string> Command, IReadOnlyList<RecordedFrame> Recorded) : IUsageQuestion
{
    /// <summary>How long a reading stands before a start asks the account again.</summary>
    public abstract TimeSpan Fresh { get; }

    /// <summary>The question's name as the usage history writes a reading's source (USAGECAP1a1).</summary>
    public abstract string Source { get; }

    /// <summary>Starts the server the command names under the account's home, asks it and stops it; null where no reading could be had.</summary>
    public abstract Task<IReadOnlyList<WindowReading>?> AskAsync(UsageAsk ask, CancellationToken ct);
}

/// <summary>What a <see cref="UsageQuestion"/> is asked with: the command as resolved, the toolchain it runs as, the account's home and the moment.</summary>
/// <param name="Run">The resolved command: the binary and its arguments.</param>
/// <param name="Toolchain">The toolchain the question's server runs under (its home variable and tools).</param>
/// <param name="ProfileHome">The account's configuration home, where the server is started, never none (D197 point 3).</param>
/// <param name="Managed">The managed binary the command's first word resolved to, or null for the command as named.</param>
/// <param name="Now">The moment the question is asked, from which a window not yet opened takes its reset.</param>
public sealed record UsageAsk(IReadOnlyList<string> Run, HarnessToolchain Toolchain, string ProfileHome, string? Managed, DateTimeOffset Now);

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
