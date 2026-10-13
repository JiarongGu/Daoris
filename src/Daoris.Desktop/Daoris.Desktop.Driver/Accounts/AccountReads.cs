using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What one reading of a sign-in said, and when it was made (ROSTER1, D150 §5.3).</summary>
/// <param name="Login">The agent's word: in, out, or unknown where its answer did not read or the question could not be asked.</param>
/// <param name="At">When the answer came.</param>
public sealed record AccountRead(LoginState Login, DateTimeOffset At);

/// <summary>An agent's readings: its own sign-in's, and each account's by its name; none where nothing was read.</summary>
public sealed record AgentReads(AccountRead? Own, IReadOnlyDictionary<string, AccountRead> Accounts);

/// <summary>
/// <c>reads.json</c> under the home (ROSTER1, D150 §5.3): what was last read of each account's sign-in, and of the tool's own,
/// per agent (the accounts' owner, AGT7), with when. The roster reports from it, so a restart, a look and a view opening start
/// from what was last read and ask nothing.
/// </summary>
/// <remarks>
/// <para><b>Written only by a reading</b>: the person's press, a sign-in's end, a start asking a signed-out account again
/// once per sign-out (TOOL6g), and a start its agent refused for its sign-in, which reads that account signed out
/// (ROSTER1b, <see cref="HarnessRoster.SignedOut"/>). A question that was never asked (an account a session of Daoris's runs on, an agent with no
/// login question) writes nothing, so the last word stands with its own time. An older reading never replaces a newer one.</para>
/// <para><b>A word and a time, never who</b>: who signed in is read fresh and written nowhere (D66 §3), so after a restart an
/// account says its state and when, and who once it is read again. Missing or unreadable is nothing known: never read, never
/// signed in (D57). Beside <c>cooling.json</c> and <c>windows.json</c>, not in <c>harnesses.json</c>, which is the person's
/// wiring. Written atomically, LF, keeping what it has no field for. No HTTP route reaches it (D47 §4).</para>
/// <para><b>A TWIN with the CLI's <c>accountreads.ts</c></b> (AGENTREAD1), which a terminal's reading writes through: both are
/// held, row for row, to one table, the CLI's <c>test/fixtures/account-reads.json</c>, which <c>AccountReadsTests</c> reads
/// (AGENTREAD1b). A rule changed here is changed there in the same commit.</para>
/// <para>The file's shape: <c>{ "&lt;agent&gt;": { "own": { "login": "in", "read": "…Z" }, "accounts": { "&lt;account&gt;":
/// { "login": "out", "read": "…Z" } } } }</c>.</para>
/// </remarks>
public static class AccountReads
{
    public const string FileName = "reads.json";

    private const string Own = "own";
    private const string AccountsKey = "accounts";

    // One writer at a time in this process: a press and a start's question may answer together.
    private static readonly object Gate = new();

    // A name or a word beyond plain ASCII written as it is, as the CLI's `accountreads.ts` writes it (AGENTREAD1b), so the
    // twins write the same bytes for a name in Chinese or one holding HTML's marks, not only the same JSON.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly AgentReads Nothing = new(null, new Dictionary<string, AccountRead>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Where the file is: beside <c>cooling.json</c>, under the home.</summary>
    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>What was last read of <paramref name="agent"/>'s accounts and its own sign-in; nothing where nothing was.</summary>
    public static AgentReads Of(string home, string agent)
    {
        if (Child(Load(home), agent) is not { } held) return Nothing;

        var accounts = new Dictionary<string, AccountRead>(StringComparer.OrdinalIgnoreCase);
        if (Child(held, AccountsKey) is { } named)
        {
            foreach (var (name, node) in named)
            {
                if (node is JsonObject entry && Reading(entry) is { } read) accounts[name] = read;
            }
        }

        return new AgentReads(Child(held, Own) is { } own ? Reading(own) : null, accounts);
    }

    /// <summary>
    /// A reading of one account's sign-in, or with <paramref name="account"/> null the tool's own: kept unless a newer reading
    /// of it is already there.
    /// </summary>
    public static void Keep(string home, string agent, string? account, LoginState login, DateTimeOffset at)
    {
        lock (Gate)
        {
            var root = Load(home);
            var held = Child(root, agent) ?? (JsonObject)(root[agent] = new JsonObject());
            var parent = account is null ? held : Child(held, AccountsKey) ?? (JsonObject)(held[AccountsKey] = new JsonObject());
            var name = account ?? Own;
            if (Child(parent, name) is { } was && Reading(was) is { } before && before.At > at) return;

            if (Key(parent, name) is { } old) parent.Remove(old);
            parent[name] = new JsonObject { ["login"] = Word(login), ["read"] = AccountCooling.Stamp(at) };
            Save(home, root);
        }
    }

    /// <summary>An account removed from this machine: its reading goes, so one made later under its name starts never read.</summary>
    public static void Forget(string home, string agent, string account)
    {
        lock (Gate)
        {
            var root = Load(home);
            if (Child(Child(root, agent), AccountsKey) is not { } named || Key(named, account) is not { } key) return;
            named.Remove(key);
            Save(home, root);
        }
    }

    private static void Save(string home, JsonObject root)
    {
        Directory.CreateDirectory(home);
        AtomicFile.WriteText(PathOf(home), root.ToJsonString(Indented) + "\n");
    }

    private static string Word(LoginState login) => login switch
    {
        LoginState.In => "in",
        LoginState.Out => "out",
        _ => "unknown",
    };

    /// <summary>One entry's reading, or null where its word or its time does not read.</summary>
    private static AccountRead? Reading(JsonObject entry)
    {
        LoginState? login = entry["login"] is JsonValue value && value.TryGetValue<string>(out var word)
            ? word switch { "in" => LoginState.In, "out" => LoginState.Out, "unknown" => LoginState.Unknown, _ => null }
            : null;
        return login is { } said && Moment(entry, "read") is { } at ? new AccountRead(said, at) : null;
    }

    /// <summary>The file's root, or an empty one when it is missing or does not read.</summary>
    private static JsonObject Load(string home)
    {
        try
        {
            return File.Exists(PathOf(home)) && JsonNode.Parse(File.ReadAllText(PathOf(home))) is JsonObject root ? root : new JsonObject();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new JsonObject();
        }
    }

    /// <summary>A name in an object, compared without case, as the wiring compares names.</summary>
    private static string? Key(JsonObject held, string name) =>
        held.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? Child(JsonObject? parent, string name) =>
        parent is not null && Key(parent, name) is { } key ? parent[key] as JsonObject : null;

    /// <summary>A moment as ISO 8601 writes it, and only so, as <c>cooling.json</c> reads one.</summary>
    private static DateTimeOffset? Moment(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<string>(out var text)
        && DateTimeOffset.TryParseExact(
            text, IsoMoments, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var moment)
            ? moment
            : null;

    private static readonly string[] IsoMoments =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];
}
