using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The four kinds every agent's failure is read into at the agent interface.</summary>
public enum AgentErrorKind
{
    /// <summary>The account's usage is spent and comes back at a reset.</summary>
    Limit,

    /// <summary>The account or its credential cannot be used until a person acts: signed out, refused, not permitted.</summary>
    Access,

    /// <summary>The provider, or the way to it, failed for a moment: tried again in the same conversation.</summary>
    Transient,

    /// <summary>The request cannot succeed by repeating it, or Daoris cannot tell: fails with its reason.</summary>
    Fatal,
}

/// <summary>
/// The reader's answer: the kind, the failure it read, and <see cref="By"/>, the table entry that decided it, which
/// the machine log names with where it came from (<c>own:code:-32000</c>, <c>owner:kind:overloaded</c>, <c>owner:words:transient-1</c>).
/// </summary>
/// <param name="Limit">A limit's reading: the table's, or, read by its code with no sentence, the unstated reset.</param>
/// <param name="Access">An access failure's disposition, from the entry that matched; null for every other kind. No reader reads policy from <see cref="By"/>.</param>
public sealed record AgentError(AgentErrorKind Kind, DoorFailure Failure, string By, LimitSeen? Limit = null, AccessDisposition? Access = null)
{
    /// <summary>The kind as the machine log and a test say it.</summary>
    public string Spelled => Kind.ToString().ToLowerInvariant();
}

/// <summary>
/// One error an agent's door has failed with, or its source shows: the evidence an entry in
/// <see cref="ErrorWords"/> stands on, as <see cref="RecordedLimit"/> is a limit's.
/// </summary>
/// <param name="Sentence">As recorded, a part elided written <c>…</c>.</param>
/// <param name="Seen">The day it is recorded, <c>yyyy-MM-dd</c>.</param>
/// <param name="Channel">Where it arrived, and how, or the source it is read in.</param>
/// <param name="SourceDerived">Whether it is read from the adapter's source rather than captured on the real door.</param>
public sealed record RecordedError(string Sentence, string Seen, string Channel, bool SourceDerived = false);

/// <summary>A range of HTTP statuses and the kind they read as.</summary>
public sealed record StatusKind(int From, int To, AgentErrorKind Kind);

/// <summary>
/// One agent's map from its door's codes, kind words and statuses to a kind, with its recorded transient sentences:
/// declared on its toolchain as <see cref="AccountAgent.ErrorWords"/>, beside <c>Limits</c> and <c>SignIn</c>.
/// </summary>
/// <remarks>
/// <para><b>Read own, then owner</b>: the adapter's own entry and its owner's, each step of the order over both
/// before the next step, a door's own entry winning where the two name the same word. The provider's entries sit on the owner, so
/// both of its doors read them.</para>
///
/// <para><b><see cref="AuthRequiredCode"/> is never inherited</b>: a JSON-RPC code means what that server says, and an owner's
/// declaration says nothing about another door. The protocol stub answers every failed turn <c>-32000</c>.</para>
///
/// <para><b>An entry grows only with a recorded sentence or frame held both ways</b>: <c>AgentErrorsTests</c> holds each.</para>
/// </remarks>
/// <param name="Kinds">The wire's kind words (<c>data.errorKind</c>) and the kind each reads as, case-blind.</param>
/// <param name="Statuses">HTTP statuses the wire names, by range.</param>
/// <param name="Transient">Anchored, case-blind patterns over the error's own words, each a recorded transient sentence's.</param>
/// <param name="Recorded">The sentences and frames the entries stand on.</param>
/// <param name="AuthRequiredCode">The JSON-RPC code this door's own server says means authentication required, or null.</param>
/// <param name="TypedFailures">
/// Whether this door's server marks a terminal failure, by a category in <see cref="Kinds"/>, only for a client that asked for AIR's
/// typed session failures: the client then asks, for this door alone.
/// </param>
public sealed record ErrorWords(
    IReadOnlyDictionary<string, AgentErrorKind> Kinds,
    IReadOnlyList<StatusKind> Statuses,
    IReadOnlyList<string> Transient,
    IReadOnlyList<RecordedError> Recorded,
    int? AuthRequiredCode = null,
    bool TypedFailures = false);

/// <summary>
/// The one reader of an agent's failure at the agent interface: a door's failure read
/// into one of four kinds from the adapter's own tables and its owner's, in a fixed order that stops at the first that holds.
/// </summary>
/// <remarks>
/// <para><b>The order</b>: a limit where the <c>Limits</c> table reads one in the words, whatever the code says, then where
/// the code or kind is a limit's, with the unstated reset; then access; then transient, by code and then by the recorded
/// sentences; anything unread is fatal. A misreading errs toward fatal: a transient error read as fatal costs a strike.</para>
///
/// <para><b>Pure but for its tables</b>: the moment seen and the machine's zone are arguments, as <see cref="AccountLimits.Read"/>'s are.</para>
/// </remarks>
public static class AgentErrors
{
    /// <summary>The table an entry came from, as <see cref="AgentError.By"/> names it.</summary>
    public const string Own = "own";

    /// <summary>The owner's table (<see cref="AccountAgent.AccountOf"/>), read after the adapter's own.</summary>
    public const string Owner = "owner";

    /// <summary>What decided a failure no table reads.</summary>
    public const string Unread = "unread";

    /// <summary>
    /// <paramref name="failure"/> read as one kind, from <paramref name="adapter"/>'s tables and, resolved through
    /// <paramref name="adapters"/>, its owner's.
    /// </summary>
    /// <param name="seen">When the failure arrived: a limit's reset is read from it.</param>
    /// <param name="zone">This machine's zone, for a limit's sentence.</param>
    public static AgentError Read(
        ProviderRegistry adapters, AgentDoor adapter, DoorFailure failure, DateTimeOffset seen, TimeZoneInfo zone)
    {
        var own = adapters.AgentNamed(adapter.Id.Value);
        var owner = OwnerOf(adapters, adapter);
        (string From, ErrorWords Words)[] tables =
        [
            .. own?.Tables.ErrorWords is { } mine ? [(Own, mine)] : Array.Empty<(string, ErrorWords)>(),
            .. owner?.Tables.ErrorWords is { } theirs ? [(Owner, theirs)] : Array.Empty<(string, ErrorWords)>(),
        ];

        // 🔴 The source before any kind: the harness's own lines are read for its credential's
        // refusal alone. Read after the limit, a weekly limit's sentence on stderr would be a limit, and would cool an account.
        if (failure.Where == DoorWhere.Lines)
        {
            return (own?.Tables.Refused ?? owner?.Tables.Refused) is { Length: > 0 } words
                   && failure.Words?.Contains(words, StringComparison.OrdinalIgnoreCase) == true
                ? new AgentError(AgentErrorKind.Access, failure, $"{(own?.Tables.Refused is not null ? Own : Owner)}:words:refused", Access: AccessDisposition.Refused)
                : new AgentError(AgentErrorKind.Fatal, failure, Unread);
        }

        // 1. A limit by its sentence, whatever the code says, then by its code or kind with the unstated reset.
        if (AccountLimits.Read(own?.Tables.Limits ?? owner?.Tables.Limits, failure.Words, seen, zone) is { } limit)
        {
            return new AgentError(AgentErrorKind.Limit, failure, $"{(own?.Tables.Limits is not null ? Own : Owner)}:words:limit", limit);
        }

        if (ByCode(tables, failure, AgentErrorKind.Limit) is { } byLimit)
        {
            return new AgentError(
                AgentErrorKind.Limit, failure, byLimit,
                new LimitSeen(failure.Kind ?? $"{failure.Status}", null, seen + AccountLimits.DefaultCoolOff, Stated: false, AssumedZone: false));
        }

        // 2. Access: the door's own authentication-required code, never an owner's; then the codes; then the sign-in and refusal words.
        if (failure.Code is { } code && own?.Tables.ErrorWords?.AuthRequiredCode == code)
        {
            return new AgentError(AgentErrorKind.Access, failure, $"{Own}:code:{code}", Access: AccessDisposition.SignedOutByCode);
        }

        if (ByCode(tables, failure, AgentErrorKind.Access) is { } byAccess)
        {
            return new AgentError(AgentErrorKind.Access, failure, byAccess, Access: AccessDisposition.SignedOutByCode);
        }

        if (SignInRefusals.Read(own?.Tables.SignIn ?? owner?.Tables.SignIn, failure.Words))
        {
            return new AgentError(
                AgentErrorKind.Access, failure, $"{(own?.Tables.SignIn is not null ? Own : Owner)}:words:sign-in", Access: AccessDisposition.SignedOutByWords);
        }

        // The refused credential, which AccountRefused reads with no owner's fallback: closed here.
        if ((own?.Tables.Refused ?? owner?.Tables.Refused) is { Length: > 0 } refused
            && failure.Words?.Contains(refused, StringComparison.OrdinalIgnoreCase) == true)
        {
            return new AgentError(
                AgentErrorKind.Access, failure, $"{(own?.Tables.Refused is not null ? Own : Owner)}:words:refused", Access: AccessDisposition.Refused);
        }

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

        // 4. Fatal: anything no table reads.
        return new AgentError(AgentErrorKind.Fatal, failure, ByCode(tables, failure, AgentErrorKind.Fatal) ?? Unread);
    }

    /// <summary>
    /// A bound kind's failure, which arrives already read: its own reading, with no table and no word read
    /// again, its access disposition the opening's reading of the evidence kept apart.
    /// </summary>
    public static AgentError OfNormalized(NormalizedFailure failure) =>
        new(failure.Kind, failure.Failure, failure.By, failure.Limit, failure.Access?.Opening);

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
    /// The owner's toolchain: <see cref="AccountAgent.AccountOf"/>'s adapter in this set, or null where it
    /// names none, itself, or an agent this set has no door for.
    /// </summary>
    private static AccountAgent? OwnerOf(ProviderRegistry adapters, AgentDoor adapter)
    {
        var owner = adapters.AgentNamed(adapter.Id.Value) is null ? null : adapters.OwnerOfAgent(adapter.Id.Value).Value;
        return owner is not null
               && !string.Equals(owner, adapter.Id.Value, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.AgentNamed(owner)
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
/// Claude Code's error words: its provider's kind words, statuses and recorded transient sentences,
/// on the owner, <c>claude-code</c>, so both of its doors read them. <c>rate_limit</c> and a 429 are a limit, never transient.
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
    /// The kind words and statuses alone, which the stub mirrors: a stand-in's failure is read by the code it names,
    /// and one that names none is fatal.
    /// </summary>
    public static ErrorWords Codes { get; } = Words with { Transient = [] };
}

/// <summary>
/// <c>claude-code-acp</c>'s own error words: its server's word that <c>-32000</c> means authentication required
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

/// <summary>
/// <c>codex-acp</c>'s error words: the categories of AIR's typed session failure, which its server marks
/// a terminal failure with only for a client that asked (<see cref="ErrorWords.TypedFailures"/>). The category is the adapter's own
/// field, never the agent's words, so only it is read: <c>service</c> and <c>connection</c> are the provider's or the way to it, for a
/// moment; <c>limit</c> is read first by <see cref="CodexLimits"/>'s sentences and else is a limit with the unstated reset;
/// <c>access</c> needs the person; <c>request</c> cannot succeed by repeating it.
/// </summary>
public static class CodexErrors
{
    public static ErrorWords Words { get; } = new(
        Kinds: new Dictionary<string, AgentErrorKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = AgentErrorKind.Transient,
            ["connection"] = AgentErrorKind.Transient,
            ["limit"] = AgentErrorKind.Limit,
            ["access"] = AgentErrorKind.Access,
            ["request"] = AgentErrorKind.Fatal,
        },
        Statuses: [],
        Transient: [],
        Recorded:
        [
            new(
                "_meta.jetbrains.air.sessionFailure.category: service | connection | limit | access | request",
                "2026-10-27",
                "codex-acp 2.1.1's source: SESSION_FAILURE_POLICY and createSessionFailureMeta, dist/index.js (read, not measured)",
                SourceDerived: true),
        ],
        TypedFailures: true);
}
