using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Why a name cannot be given to an account (ACCT2).</summary>
public enum AccountNameProblemKind
{
    /// <summary>The account is not on this machine.</summary>
    Missing,

    /// <summary>The name is blank.</summary>
    Blank,

    /// <summary>The name is not one word a terminal can type: a space, a control character, a backtick, or a leading dash.</summary>
    Characters,

    /// <summary>The name is longer than <see cref="AccountNames.Longest"/> characters.</summary>
    Long,

    /// <summary>The name is another account's id or name, in any case.</summary>
    Taken,

    /// <summary><c>accounts.json</c> does not read, so writing it would lose every other name.</summary>
    Unreadable,
}

/// <summary>Why a name was refused, and the other account it collides with where it is <see cref="AccountNameProblemKind.Taken"/>.</summary>
public sealed record AccountNameProblem(AccountNameProblemKind Kind, string? Other = null);

/// <summary>A name refused (ACCT2): the driver's sentence, and the problem a twin table reads.</summary>
public sealed class AccountNameException(AccountNameProblem problem, string message) : DriverException(message)
{
    public AccountNameProblem Problem { get; } = problem;
}

/// <summary>
/// Which account a sign-in reaches (ACCT1): <see cref="Account"/> when it is one here; else the name that is no account here
/// (<see cref="Missing"/>), or neither where none was named and the machine names no default.
/// </summary>
public sealed record AccountTarget(string? Account, string? Missing);

/// <summary>
/// An account's name and its stable id (ACCT2, D125's ACCT2 note): <c>accounts.json</c> under the home holds the name the
/// person gave each account, by agent (the accounts' owner, AGT7) and id. <b>The id is the folder's name</b>, so an
/// <c>account-N</c> made before this keeps its name as its id, and an account made now takes a fresh one
/// (<see cref="NewId"/>). Everything else is keyed by the id: the wiring's defaults and lists, <c>reads.json</c>,
/// <c>cooling.json</c>, <c>windows.json</c>, <c>keys.json</c>, the probe locks, session records and the usage ledger, so a
/// rename touches this file alone and every one of them keeps working.
/// </summary>
/// <remarks>
/// <para><b>Beside the account, never inside it</b>: the folder is the tool's (D49 §4), and moving it would move a home a
/// harness may have keyed its credential to (D66 §3's rejected rename). <b>Written only by a person's rename</b>: who signed
/// in is offered as the name at a sign-in's end and written only where the person keeps it (D66 §3); a read writes nothing.</para>
/// <para><b>A twin</b> of the CLI's <c>accountnames.ts</c>, over the same file; <c>AccountNamesTwinTests</c> and the CLI's
/// <c>accountnames.test.ts</c> hold the same tables, row for row. The file's shape:
/// <c>{ "&lt;agent&gt;": { "&lt;id&gt;": { "name": "…" } } }</c>; each writer keeps what it has no field for, LF, with a final
/// newline. Agents and ids compare without case, as <c>reads.json</c> compares them. No HTTP route reaches it (D47 §4).</para>
/// </remarks>
public static class AccountNames
{
    public const string FileName = "accounts.json";

    /// <summary>The longest name: one a row and a command line both hold.</summary>
    public const int Longest = 64;

    /// <summary>What a fresh id begins with, so a log line or a record says it is an account's.</summary>
    public const string IdPrefix = "acct-";

    private const string NameKey = "name";

    // One writer at a time in this process: two screens may rename at once.
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        // A name in Chinese is written as typed, as the CLI's writer writes it.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Where the file is: beside <c>reads.json</c>, under the home.</summary>
    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>The name the person gave an account, trimmed, or null where none.</summary>
    public static string? NameOf(string home, string agent, string account) =>
        Of(home, agent).TryGetValue(account, out var name) ? name : null;

    /// <summary>What an account reads as: its name, else its id.</summary>
    public static string Shown(string home, string agent, string account) => NameOf(home, agent, account) ?? account;

    /// <summary>Every name one agent's accounts were given, by id, compared without case; none where nothing was.</summary>
    public static IReadOnlyDictionary<string, string> Of(string home, string agent) => Of(Load(home).Root, agent);

    /// <summary>
    /// The account a person means by <paramref name="given"/>: the one whose id it is, exactly, as the wiring compares an
    /// id; else the one here whose name it is, in any case; null where neither.
    /// </summary>
    /// <param name="accounts">The agent's accounts on this machine, <see cref="HarnessSettings.Profiles"/>.</param>
    public static string? Resolve(IReadOnlyCollection<string> accounts, IReadOnlyDictionary<string, string> names, string given)
    {
        var wanted = given.Trim();
        if (accounts.Contains(wanted, StringComparer.Ordinal)) return wanted;
        return accounts.FirstOrDefault(account =>
            names.TryGetValue(account, out var name) && string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Which account a sign-in into an account that is here reaches (ACCT1): the one named, by its id or its name, else the
    /// machine's default. Never one that is not here: a sign-in into an existing account never makes a folder.
    /// </summary>
    public static AccountTarget SignInTarget(
        IReadOnlyCollection<string> accounts, IReadOnlyDictionary<string, string> names, HarnessSettings settings, string agent,
        string? given)
    {
        var named = given?.Trim() is { Length: > 0 } spelled
            ? spelled
            : settings.Defaults.TryGetValue(agent, out var held) && held.Trim() is { Length: > 0 } fallback ? fallback : null;
        if (named is null) return new AccountTarget(null, null);
        return Resolve(accounts, names, named) is { } account ? new AccountTarget(account, null) : new AccountTarget(null, named);
    }

    /// <summary>
    /// The refusal a sign-in into an account that is not here says (ACCT1): which name is none, the accounts there by name, and
    /// the two ways on — a new account, or an account named first.
    /// </summary>
    public static string SignInRefusal(
        string agent, AccountTarget target, IReadOnlyCollection<string> accounts, IReadOnlyDictionary<string, string> names) =>
        (target.Missing is { } missing
            ? $"`{agent}` has no account `{missing}` on this machine, so nothing was signed in and no account was made"
            : $"name the `{agent}` account to sign in to: this machine names no default for it, and a sign-in into an account "
              + "never makes one")
        + $" — accounts there: {Listed(accounts, names)}. `daoris agent login {agent} --new` signs in to a new account.";

    /// <summary>
    /// Why <paramref name="name"/> cannot name <paramref name="account"/>, or null when it can (ACCT2). Null, or the account's
    /// own id, clears its name and is never refused but for an account that is not here.
    /// </summary>
    public static AccountNameProblem? Problem(
        IReadOnlyCollection<string> accounts, IReadOnlyDictionary<string, string> names, string account, string? name)
    {
        if (!accounts.Contains(account, StringComparer.Ordinal)) return new AccountNameProblem(AccountNameProblemKind.Missing);
        if (name is null) return null;

        var wanted = name.Trim();
        if (wanted.Length == 0) return new AccountNameProblem(AccountNameProblemKind.Blank);
        if (wanted == account) return null;
        if (wanted.StartsWith('-') || wanted.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '`'))
        {
            return new AccountNameProblem(AccountNameProblemKind.Characters);
        }

        if (wanted.Length > Longest) return new AccountNameProblem(AccountNameProblemKind.Long);

        foreach (var other in accounts.Where(other => other != account))
        {
            if (string.Equals(other, wanted, StringComparison.OrdinalIgnoreCase)
                || (names.TryGetValue(other, out var called) && string.Equals(called, wanted, StringComparison.OrdinalIgnoreCase)))
            {
                return new AccountNameProblem(AccountNameProblemKind.Taken, other);
            }
        }

        return null;
    }

    /// <summary>
    /// Give an account its name, or clear it with null or the account's own id (ACCT2): the person's word, both doors'. Asked
    /// <see cref="Problem"/> first, and refused with its sentence, writing nothing.
    /// </summary>
    /// <param name="accounts">The agent's accounts on this machine, <see cref="HarnessSettings.Profiles"/>.</param>
    /// <returns>The account's name now, or null where it has none.</returns>
    /// <exception cref="AccountNameException">The name is refused, or the file does not read.</exception>
    public static string? Rename(string home, string agent, IReadOnlyCollection<string> accounts, string account, string? name)
    {
        lock (Gate)
        {
            var (root, unreadable) = Load(home);
            var names = Of(root, agent);
            if (Problem(accounts, names, account, name) is { } problem)
            {
                throw new AccountNameException(problem, Sentence(agent, problem, account, name, accounts, names));
            }

            if (unreadable)
            {
                throw new AccountNameException(
                    new AccountNameProblem(AccountNameProblemKind.Unreadable),
                    $"{FileName} could not be read, so `{account}` was not renamed — writing it would lose every other account's "
                    + "name. Fix the file or remove it, then rename again.");
            }

            var wanted = name?.Trim();
            var clear = wanted is null || wanted == account;
            var held = Child(root, agent);
            var entry = held is null ? null : Child(held, account);
            if (clear)
            {
                if (entry is null) return null;
                entry.Remove(NameKey);
                if (entry.Count == 0) held!.Remove(Key(held, account)!);
                if (held!.Count == 0) root.Remove(Key(root, agent)!);
            }
            else
            {
                held ??= (JsonObject)(root[agent] = new JsonObject());
                if (entry is null) held[account] = entry = new JsonObject();
                // The name goes first in its entry, as the CLI writes it, whatever else the entry holds.
                var rest = entry.Where(pair => pair.Key != NameKey).Select(pair => (pair.Key, pair.Value?.DeepClone())).ToList();
                entry.Clear();
                entry[NameKey] = wanted;
                foreach (var (key, value) in rest) entry[key] = value;
            }

            Save(home, root);
            return clear ? null : wanted;
        }
    }

    /// <summary>An account removed from this machine (D66 §3): its name goes with it. Nothing to forget writes nothing.</summary>
    public static void Forget(string home, string agent, string account)
    {
        lock (Gate)
        {
            var (root, unreadable) = Load(home);
            if (unreadable || Child(root, agent) is not { } held || Key(held, account) is not { } key) return;
            held.Remove(key);
            if (held.Count == 0) root.Remove(Key(root, agent)!);
            Save(home, root);
        }
    }

    /// <summary>
    /// A new account's id (ACCT2): <see cref="IdPrefix"/> and eight lowercase hex characters, drawn again while an account of
    /// the agent already has it. Never reused, unlike the first free <c>account-N</c> it replaces, so a reading, a cool-off
    /// or a usage total kept under one id never lands on another account. The person reads its name, never this.
    /// </summary>
    /// <param name="draw">Eight hex characters; the system's random source unless a test holds its own.</param>
    public static string NewId(string home, string agent, Func<string>? draw = null)
    {
        var taken = HarnessSettings.Profiles(home, agent).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var id = IdPrefix + (draw?.Invoke() ?? RandomNumberGenerator.GetHexString(8, lowercase: true));
            if (!taken.Contains(id)) return id;
        }
    }

    /// <summary>Each account as a person reads it in a sentence: its id, with its name before it where it has one.</summary>
    public static string Listed(IReadOnlyCollection<string> accounts, IReadOnlyDictionary<string, string> names) =>
        accounts.Count == 0
            ? "(none)"
            : string.Join(", ", accounts.Select(account => names.TryGetValue(account, out var name) ? $"{name} ({account})" : account));

    private static string Sentence(
        string agent, AccountNameProblem problem, string account, string? name, IReadOnlyCollection<string> accounts,
        IReadOnlyDictionary<string, string> names) => problem.Kind switch
    {
        AccountNameProblemKind.Missing =>
            $"`{agent}` has no account `{account}` on this machine — accounts there: {Listed(accounts, names)}.",
        AccountNameProblemKind.Blank =>
            $"a name is not blank — name `{account}` by its id, `{account}`, to give it none.",
        AccountNameProblemKind.Characters =>
            $"`{name?.Trim()}` is not a name a terminal can type: a name is one word, with no space or backtick, not starting with a dash.",
        AccountNameProblemKind.Long =>
            $"a name is at most {Longest} characters — `{name?.Trim()}` is {name?.Trim().Length}.",
        _ => $"`{name?.Trim()}` already names `{problem.Other}` — each `{agent}` account has a name of its own, and an id is one.",
    };

    /// <summary>The name an entry holds, trimmed, or null where it holds none.</summary>
    private static string? Name(JsonObject entry) =>
        entry[NameKey] is JsonValue value && value.TryGetValue<string>(out var text) && text.Trim() is { Length: > 0 } name ? name : null;

    private static IReadOnlyDictionary<string, string> Of(JsonObject root, string agent)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Child(root, agent) is not { } held) return names;
        foreach (var (id, node) in held)
        {
            if (node is JsonObject entry && Name(entry) is { } name) names.TryAdd(id, name);
        }

        return names;
    }

    private static void Save(string home, JsonObject root)
    {
        Directory.CreateDirectory(home);
        AtomicFile.WriteText(PathOf(home), root.ToJsonString(Indented) + "\n");
    }

    /// <summary>The file's root, and whether it was there and did not read; a missing file is an empty root.</summary>
    private static (JsonObject Root, bool Unreadable) Load(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return (new JsonObject(), false);
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) is JsonObject root ? (root, false) : (new JsonObject(), true);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return (new JsonObject(), true);
        }
    }

    /// <summary>A name in an object, compared without case, as the readings compare names.</summary>
    private static string? Key(JsonObject held, string name) =>
        held.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? Child(JsonObject? parent, string name) =>
        parent is not null && Key(parent, name) is { } key ? parent[key] as JsonObject : null;
}
