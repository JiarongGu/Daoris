using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The four kinds every agent's failure is read into at the agent interface (AGENTERRORS1a, D210, the agent-errors design §3.1).</summary>
public enum AgentErrorKind
{
    /// <summary>The account's usage is spent and comes back at a reset: D206's.</summary>
    Limit,

    /// <summary>The account or its credential cannot be used until a person acts: signed out, refused, not permitted.</summary>
    Access,

    /// <summary>The provider, or the way to it, failed for a moment: tried again in the same conversation (§5.1).</summary>
    Transient,

    /// <summary>The request cannot succeed by repeating it, or Daoris cannot tell: fails with its reason, as today.</summary>
    Fatal,
}

/// <summary>Where on its door a failure was met (the design §4.1).</summary>
public static class DoorWhere
{
    /// <summary>A <c>session/new</c>, <c>session/resume</c> or <c>session/load</c> the agent refused (§5.6, row 1a2's).</summary>
    public const string Open = "open";

    /// <summary>A prompt the agent answered with an error: the run's first, a held word's, or a try's.</summary>
    public const string Prompt = "prompt";

    /// <summary>The pipe door's failed <c>result</c> (row 1c's).</summary>
    public const string Result = "result";

    /// <summary>The harness's own lines beside a failed result or exit, read for access alone (§3.3).</summary>
    public const string Harness = "harness";
}

/// <summary>
/// What a door's wire carried when a turn ended on a failure, and nothing the agent wrote (the design §4.1, §3.3): the agent's own
/// streamed message, which carries the same words first, is never one, since an agent can write any sentence (D125 point 1.4).
/// </summary>
/// <param name="Door"><c>protocol</c> or <c>pipe</c>.</param>
/// <param name="Code">The JSON-RPC code, or null where the wire carried none.</param>
/// <param name="Kind">The wire's own kind word (<c>data.errorKind</c>, the harness's <c>error</c> field), or null: an absent kind is no reading, never <c>unknown</c>.</param>
/// <param name="Status">An HTTP status the wire names, or null.</param>
/// <param name="Words">The error's own sentence, or null.</param>
/// <param name="Where">Where it was met (<see cref="DoorWhere"/>).</param>
public sealed record DoorFailure(string Door, int? Code, string? Kind, int? Status, string? Words, string Where)
{
    /// <summary>The protocol door's prompt failure (§4.2): the refusal's code, its <c>data.errorKind</c> and its own sentence.</summary>
    public static DoorFailure OfPrompt(AcpRefusal refused) =>
        new("protocol", refused.Code, refused.ErrorKind, null, refused.Words, DoorWhere.Prompt);
}

/// <summary>
/// The reader's answer (the design §4.1): the kind, the failure it read, and <see cref="By"/>, the table entry that decided it, which
/// the machine log names with where it came from (<c>own:code:-32000</c>, <c>owner:kind:overloaded</c>, <c>owner:words:transient-1</c>).
/// </summary>
/// <param name="Limit">A limit's reading: the table's, or, read by its code with no sentence, D125's unstated reset (§3.1).</param>
public sealed record AgentError(AgentErrorKind Kind, DoorFailure Failure, string By, LimitSeen? Limit = null)
{
    /// <summary>The kind as the machine log and a test say it.</summary>
    public string Spelled => Kind.ToString().ToLowerInvariant();
}

/// <summary>
/// One error an agent's door was seen failing with, or its source shows (the design §4.4): the evidence an entry in
/// <see cref="ErrorWords"/> stands on, as <see cref="RecordedLimit"/> is a limit's.
/// </summary>
/// <param name="Sentence">As recorded, a part elided written <c>…</c>.</param>
/// <param name="Seen">The day it was recorded, <c>yyyy-MM-dd</c>.</param>
/// <param name="Channel">Where it arrived, and how, or the source it was read in.</param>
/// <param name="SourceDerived">Whether it was read from the adapter's source rather than captured on the real door (§3.3).</param>
public sealed record RecordedError(string Sentence, string Seen, string Channel, bool SourceDerived = false);

/// <summary>A range of HTTP statuses and the kind they read as (the design §3.1).</summary>
public sealed record StatusKind(int From, int To, AgentErrorKind Kind);

/// <summary>
/// One agent's map from its door's codes, kind words and statuses to a kind, with its recorded transient sentences (AGENTERRORS1a, the
/// design §4.1): declared on its toolchain as <see cref="HarnessToolchain.ErrorWords"/>, beside <c>Limits</c> and <c>SignIn</c>.
/// </summary>
/// <remarks>
/// <para><b>Read own, then owner</b> (§4.1, round 3): the adapter's own entry and its owner's, each step of the order over both
/// before the next step, a door's own entry winning where the two name the same word. The provider's entries sit on the owner, so
/// both of its doors read them.</para>
///
/// <para><b><see cref="AuthRequiredCode"/> is never inherited</b>: a JSON-RPC code means what that server says, and an owner's
/// declaration says nothing about another door (§3.1, §4.1). The protocol stub answers every failed turn <c>-32000</c>.</para>
///
/// <para><b>An entry grows only with a recorded sentence or frame held both ways</b> (§3.3): <c>AgentErrorsTests</c> holds each.</para>
/// </remarks>
/// <param name="Kinds">The wire's kind words (<c>data.errorKind</c>) and the kind each reads as, case-blind.</param>
/// <param name="Statuses">HTTP statuses the wire names, by range.</param>
/// <param name="Transient">Anchored, case-blind patterns over the error's own words, each a recorded transient sentence's.</param>
/// <param name="Recorded">The sentences and frames the entries stand on.</param>
/// <param name="AuthRequiredCode">The JSON-RPC code this door's own server says means authentication required, or null.</param>
public sealed record ErrorWords(
    IReadOnlyDictionary<string, AgentErrorKind> Kinds,
    IReadOnlyList<StatusKind> Statuses,
    IReadOnlyList<string> Transient,
    IReadOnlyList<RecordedError> Recorded,
    int? AuthRequiredCode = null);

/// <summary>
/// The one reader of an agent's failure at the agent interface (AGENTERRORS1a, D210, the design §3.2, §4.1): a door's failure read
/// into one of four kinds from the adapter's own tables and its owner's, in a fixed order that stops at the first that holds.
/// </summary>
/// <remarks>
/// <para><b>The order</b> (§3.2): a limit where the <c>Limits</c> table reads one in the words, whatever the code says, then where
/// the code or kind is a limit's, with D125's unstated reset; then access; then transient, by code and then by the recorded
/// sentences; anything unread is fatal, as today. A misreading errs toward today: a transient error read as fatal is today's
/// strike.</para>
///
/// <para><b>Pure but for its tables</b>: the moment seen and the machine's zone are arguments, as <see cref="AccountLimits.Read"/>'s are.</para>
/// </remarks>
public static class AgentErrors
{
    /// <summary>The table an entry came from, as <see cref="AgentError.By"/> names it.</summary>
    public const string Own = "own";

    /// <summary>The owner's table (<see cref="HarnessToolchain.AccountOf"/>), read after the adapter's own.</summary>
    public const string Owner = "owner";

    /// <summary>What decided a failure no table reads.</summary>
    public const string Unread = "unread";

    /// <summary>
    /// <paramref name="failure"/> read as one kind (§3.2), from <paramref name="adapter"/>'s tables and, resolved through
    /// <paramref name="adapters"/>, its owner's.
    /// </summary>
    /// <param name="seen">When the failure arrived: a limit's reset is read from it.</param>
    /// <param name="zone">This machine's zone, for a limit's sentence (D125 §2.1).</param>
    public static AgentError Read(
        AdapterSet adapters, ISessionAdapter adapter, DoorFailure failure, DateTimeOffset seen, TimeZoneInfo zone)
    {
        var own = adapter.Toolchain;
        var owner = OwnerOf(adapters, adapter);
        (string From, ErrorWords Words)[] tables =
        [
            .. own?.ErrorWords is { } mine ? [(Own, mine)] : Array.Empty<(string, ErrorWords)>(),
            .. owner?.ErrorWords is { } theirs ? [(Owner, theirs)] : Array.Empty<(string, ErrorWords)>(),
        ];

        // 1. A limit by its sentence, whatever the code says (D125, D206 unchanged), then by its code or kind with the unstated reset.
        if (AccountLimits.Read(own?.Limits ?? owner?.Limits, failure.Words, seen, zone) is { } limit)
        {
            return new AgentError(AgentErrorKind.Limit, failure, $"{(own?.Limits is not null ? Own : Owner)}:words:limit", limit);
        }

        if (ByCode(tables, failure, AgentErrorKind.Limit) is { } byLimit)
        {
            return new AgentError(
                AgentErrorKind.Limit, failure, byLimit,
                new LimitSeen(failure.Kind ?? $"{failure.Status}", null, seen + AccountLimits.DefaultCoolOff, Stated: false, AssumedZone: false));
        }

        // 2. Access: the door's own authentication-required code, never an owner's; then the codes; then the sign-in and refusal words.
        if (failure.Code is { } code && own?.ErrorWords?.AuthRequiredCode == code)
        {
            return new AgentError(AgentErrorKind.Access, failure, $"{Own}:code:{code}");
        }

        if (ByCode(tables, failure, AgentErrorKind.Access) is { } byAccess) return new AgentError(AgentErrorKind.Access, failure, byAccess);
        if (SignInRefusals.Read(own?.SignIn ?? owner?.SignIn, failure.Words))
        {
            return new AgentError(AgentErrorKind.Access, failure, $"{(own?.SignIn is not null ? Own : Owner)}:words:sign-in");
        }

        // AGT3b's refused credential, which AccountRefused reads with no owner's fallback today (§1.2): closed here.
        if ((own?.Refused ?? owner?.Refused) is { Length: > 0 } refused
            && failure.Words?.Contains(refused, StringComparison.OrdinalIgnoreCase) == true)
        {
            return new AgentError(AgentErrorKind.Access, failure, $"{(own?.Refused is not null ? Own : Owner)}:words:refused");
        }

        // The harness's own lines are read for access alone (§3.3).
        if (failure.Where == DoorWhere.Harness) return new AgentError(AgentErrorKind.Fatal, failure, Unread);

        // 3. Transient by code, then by the recorded sentences.
        if (ByCode(tables, failure, AgentErrorKind.Transient) is { } byTransient)
        {
            return new AgentError(AgentErrorKind.Transient, failure, byTransient);
        }

        foreach (var (from, words) in tables)
        {
            for (var at = 0; at < words.Transient.Count; at++)
            {
                if (failure.Words is { } said && Matches(words.Transient[at], said))
                {
                    return new AgentError(AgentErrorKind.Transient, failure, $"{from}:words:transient-{at + 1}");
                }
            }
        }

        // 4. Fatal: anything no table reads, as today.
        return new AgentError(AgentErrorKind.Fatal, failure, ByCode(tables, failure, AgentErrorKind.Fatal) ?? Unread);
    }

    /// <summary>
    /// The entry that reads <paramref name="failure"/>'s kind word or status as <paramref name="kind"/>, own first: a word the
    /// adapter's own table names is read there alone, so an owner's reading of the same word never overrides it.
    /// </summary>
    private static string? ByCode((string From, ErrorWords Words)[] tables, DoorFailure failure, AgentErrorKind kind)
    {
        if (failure.Kind is { Length: > 0 } word)
        {
            foreach (var (from, words) in tables)
            {
                if (!Named(words.Kinds, word, out var named)) continue;
                if (named == kind) return $"{from}:kind:{word}";
                break;
            }
        }

        if (failure.Status is { } status)
        {
            foreach (var (from, words) in tables)
            {
                if (words.Statuses.FirstOrDefault(range => range.From <= status && status <= range.To) is not { } range) continue;
                if (range.Kind == kind) return $"{from}:status:{status}";
                break;
            }
        }

        return null;
    }

    private static bool Named(IReadOnlyDictionary<string, AgentErrorKind> kinds, string word, out AgentErrorKind kind)
    {
        foreach (var (name, value) in kinds)
        {
            if (!string.Equals(name, word, StringComparison.OrdinalIgnoreCase)) continue;
            kind = value;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// The owner's toolchain (AGT7, the design §4.1): <see cref="HarnessToolchain.AccountOf"/>'s adapter in this set, or null where it
    /// names none, itself, or an agent this set has no door for.
    /// </summary>
    private static HarnessToolchain? OwnerOf(AdapterSet adapters, ISessionAdapter adapter)
    {
        var owner = adapter.Toolchain?.Owner(adapter.Name);
        return owner is not null
               && !string.Equals(owner, adapter.Name, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.Resolve(owner).Toolchain
            : null;
    }

    /// <summary>One pattern against the words; a pattern this build cannot run matches nothing, as <see cref="SignInRefusals"/>'s.</summary>
    internal static bool Matches(string pattern, string text)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

/// <summary>
/// Claude Code's error words (AGENTERRORS1a, the design §4.4): its provider's kind words, statuses and recorded transient sentences,
/// on the owner, <c>claude-code</c>, so both of its doors read them. <c>rate_limit</c> and a 429 are a limit, never transient (§3.1).
/// </summary>
public static class ClaudeErrors
{
    public static ErrorWords Words { get; } = new(
        Kinds: new Dictionary<string, AgentErrorKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["overloaded"] = AgentErrorKind.Transient,
            ["server_error"] = AgentErrorKind.Transient,
            ["rate_limit"] = AgentErrorKind.Limit,
        },
        Statuses:
        [
            new StatusKind(429, 429, AgentErrorKind.Limit),
            new StatusKind(500, 599, AgentErrorKind.Transient),
        ],
        Transient:
        [
            // At the words' start or after a door's `: `, so a sentence that quotes the error mid-way is not read as one.
            @"(?:^|: )API Error: 529 Overloaded\b",
            @"(?:^|: )API Error: Connection dropped \(ECONNRESET\)",
        ],
        Recorded:
        [
            new(
                "Internal error: API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.",
                "2026-10-24",
                "the protocol door, claude-code-acp: the JSON-RPC error answering session/prompt in sessions 61306f9f and 5bafda3b (the agent-errors design §0)"),
            new(
                "Internal error: API Error: Connection dropped (ECONNRESET)",
                "2026-10-14",
                "the protocol door, claude-code-acp: session cbf2fe70 (D137's TURNFAIL1 note)"),
            new(
                "data.errorKind: overloaded beside the 529's message",
                "2026-10-24",
                "claude-agent-acp 0.84.0's source: errorKindData(lastAssistantError), acp-agent.js:4178-4180, :7249-7251",
                SourceDerived: true),
        ]);

    /// <summary>
    /// The kind words and statuses alone, which the stub mirrors (AGENTERRORS1a): a stand-in's failure is read by the code it names,
    /// and one that names none keeps today's end.
    /// </summary>
    public static ErrorWords Codes { get; } = Words with { Transient = [] };
}

/// <summary>
/// <c>claude-code-acp</c>'s own error words (the design §4.4): its server's word that <c>-32000</c> means authentication required
/// (<i>Please run /login</i>, <c>claude-agent-acp</c> 0.84.0's <c>acp-agent.js:4170-4171</c>), read from its own table alone.
/// </summary>
public static class ClaudeAcpErrors
{
    public static ErrorWords Words { get; } = new(
        Kinds: new Dictionary<string, AgentErrorKind>(),
        Statuses: [],
        Transient: [],
        Recorded:
        [
            new(
                "-32000 Authentication required: Please run /login",
                "2026-10-24",
                "claude-agent-acp 0.84.0's source: RequestError.authRequired, acp-agent.js:4170-4171",
                SourceDerived: true),
        ],
        AuthRequiredCode: -32000);
}
