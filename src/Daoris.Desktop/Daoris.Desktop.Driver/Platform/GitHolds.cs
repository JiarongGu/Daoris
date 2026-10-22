using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A remote that could not authenticate, held (GITAUTH1a, D198 point 7; the git-auth design §5.2): keyed by the repository's checkout,
/// the remote, its address, what served the act, what the hold covers and the git that ran.
/// </summary>
/// <param name="Root">The repository's main checkout: the key, never said.</param>
/// <param name="Repository">The repository's name, as the registry names it, for the row; empty where the act's caller knew none.</param>
/// <param name="Workspace">The workspace it is in, which scopes the row; null where the caller knew none.</param>
/// <param name="Address">The remote's address with no user-info (<see cref="GitWords.Redact"/>).</param>
/// <param name="Host">The address's host (<see cref="GitWords.HostOf"/>).</param>
/// <param name="Served">What served the act: <see cref="GitHolds.Ambient"/> until git identities are built (GITAUTH1c).</param>
/// <param name="Holds"><c>all</c> (reads and writes) or <c>writes</c>.</param>
/// <param name="Class">The failure's class (<see cref="GitFailures.Code"/>).</param>
/// <param name="Git">The git that ran (<c>WorkingTree.GitInUse</c>): a changed git is another key.</param>
/// <param name="Words">git's last line, redacted: what the transport said.</param>
public sealed record GitHold(
    string Root, string Repository, string? Workspace, string Remote, string Address, string? Host, string Served, string Holds,
    string Class, string Git, string Words, DateTimeOffset Since);

/// <summary>
/// The holds on remotes that cannot authenticate (GITAUTH1a, D198 point 7; the git-auth design §5.2), kept in the home's
/// <c>run/git/held.json</c> so a restart of Daoris tries each once more and no sooner.
/// </summary>
/// <remarks>
/// <para><b>While held</b>, no Daoris act connects for what the hold covers: <see cref="Holding"/> answers it before git starts, and the
/// act says it in place. Nothing retries on a clock.</para>
/// <para><b>What lifts it</b>: the person's <i>Try again</i> (<see cref="Lift"/>); a changed address or a changed git, which is another
/// key, so the act is tried and the old hold goes (<see cref="Holding"/>); an act at the same key that passed (<see cref="Passed"/>).
/// The identities', pointers' and host keys' changes join with the rows that build them.</para>
/// <para><b>The network's failures and the unclassed hold nothing</b> (<see cref="GitFailures.Holds"/>): today's minute bounds the next
/// try (D148).</para>
/// <para>Machine-local, as everything in <c>run/</c> is; its words passed <see cref="GitWords.Redact"/> before they were kept. Each
/// hold and lift is a coded machine-log line (§11), with no address's user-info and no path beyond the repository's name.</para>
/// </remarks>
public static class GitHolds
{
    /// <summary>What serves an act before git identities: whatever git finds ambient on this machine (the design §0.2).</summary>
    public const string Ambient = "ambient";

    /// <summary>A hold over reads and writes.</summary>
    public const string All = "all";

    /// <summary>A hold over writes alone.</summary>
    public const string Writes = "writes";

    private static readonly Lock Gate = new();

    /// <summary>Every hold the home keeps; none where the file is absent or unreadable.</summary>
    public static IReadOnlyList<GitHold> Read(string home)
    {
        lock (Gate) return Load(home);
    }

    /// <summary>
    /// The hold covering an act at <paramref name="address"/> by <paramref name="git"/>, or null where it may connect. A hold on the
    /// same remote at another address or by another git is lifted on the way, said in the machine log as why.
    /// </summary>
    public static GitHold? Holding(string home, string root, string remote, string address, string? git, GitAct act)
    {
        var clean = Clean(address);
        lock (Gate)
        {
            var holds = Load(home);
            GitHold? found = null;
            var kept = new List<GitHold>();
            var lifted = new List<(GitHold Hold, string Why)>();
            foreach (var hold in holds)
            {
                if (!SameRemote(hold, root, remote))
                {
                    kept.Add(hold);
                    continue;
                }

                if (!string.Equals(hold.Address, clean, StringComparison.Ordinal)) lifted.Add((hold, "address"));
                else if (!string.Equals(hold.Git, git ?? "", StringComparison.Ordinal)) lifted.Add((hold, "git"));
                else
                {
                    kept.Add(hold);
                    if (act == GitAct.Write || hold.Holds == All) found ??= hold;
                }
            }

            if (lifted.Count > 0)
            {
                Save(home, kept);
                foreach (var (hold, why) in lifted) Logged(home, "git.lifted", hold, why);
            }

            return found;
        }
    }

    /// <summary>
    /// Hold a remote after an act failed in <paramref name="failure"/>'s class, where that class holds anything; the hold kept, or
    /// null where it holds nothing. A hold already at the key is replaced, keeping its first time.
    /// </summary>
    public static GitHold? Hold(
        string home, string root, string repository, string? workspace, string remote, string address, string? git,
        GitFailureClass failure, string? words, DateTimeOffset now)
    {
        if (!GitFailures.Holds(failure)) return null;
        var clean = Clean(address);
        var hold = new GitHold(
            root, repository, workspace, remote, clean, GitWords.HostOf(clean), Ambient,
            GitFailures.WritesAlone(failure) ? Writes : All, GitFailures.Code(failure), git ?? "", LastLine(words), now);
        GitHold? was;
        lock (Gate)
        {
            var holds = Load(home);
            was = holds.FirstOrDefault(each => SameRemote(each, root, remote));
            if (was is not null) hold = hold with { Since = was.Since };
            Save(home, [.. holds.Where(each => !SameRemote(each, root, remote)), hold]);
        }

        if (was is null) Logged(home, "git.held", hold, failure: hold.Class);
        return hold;
    }

    /// <summary>An act on the remote passed: a hold it proves wrong is lifted, a read's only a hold over reads, since a fetch that
    /// passes says nothing of a push.</summary>
    public static void Passed(string home, string root, string remote, GitAct act) =>
        Lift(home, root, remote, "passed", hold => act == GitAct.Write || hold.Holds == All);

    /// <summary>
    /// The person's <i>Try again</i>, or an act that passed: every hold on the repository's checkout (on one remote where
    /// <paramref name="remote"/> names it) lifted, so the next act tries; how many were.
    /// </summary>
    public static int Lift(string home, string root, string? remote, string why) => Lift(home, root, remote, why, _ => true);

    /// <summary>The holds on the checkout (on one remote where <paramref name="remote"/> names it) that <paramref name="which"/> picks,
    /// lifted; how many were.</summary>
    internal static int Lift(string home, string root, string? remote, string why, Func<GitHold, bool> which)
    {
        List<GitHold> lifted;
        lock (Gate)
        {
            var holds = Load(home);
            lifted = [.. holds.Where(each => SameRoot(each, root) && (remote is null || each.Remote == remote) && which(each))];
            if (lifted.Count == 0) return 0;
            Save(home, [.. holds.Except(lifted)]);
        }

        foreach (var hold in lifted) Logged(home, "git.lifted", hold, why);
        return lifted.Count;
    }

    /// <summary>
    /// The person's <i>Try again</i> on a repository named as the registry names it, in a workspace where one is named: every hold on
    /// it lifted. How many were.
    /// </summary>
    public static int LiftRepository(string home, string repository, string? workspace, string why = "try-again")
    {
        var roots = Read(home)
            .Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                && (workspace is null || string.Equals(each.Workspace, workspace, StringComparison.OrdinalIgnoreCase)))
            .Select(each => each.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return roots.Sum(root => Lift(home, root, null, why));
    }

    /// <summary>
    /// A held remote in the words an act says in place of trying (the design §5.2): what the transport said, and what lifts it, naming
    /// the row in <i>What needs you</i> rather than repeating it. English, as the terminal's and the instruction's words are; the
    /// screen's row has its own catalogue.
    /// </summary>
    public static string Said(GitHold hold)
    {
        var host = hold.Host ?? hold.Address;
        var why = hold.Class switch
        {
            "write-refused" => $"{host} refused a push with the sign-in git has here",
            "host-unknown" => $"ssh does not know {host}'s key",
            "host-changed" => $"{host}'s key is not the one ssh knows for it",
            "own-input" => OwnInputWhy,
            _ => $"{host} refused the sign-in git has here",
        };
        var said = hold.Words.Length == 0 ? "" : $" ({hold.Words})";
        if (hold.Class == "own-input")
            return $"`{hold.Remote}` is held: {why}. Daoris does not reach it until that setting is removed from this repository";
        return $"`{hold.Remote}` is held: {why}{said}. Daoris does not try it again until you press Try again on its row in What needs "
            + "you, or its address or the git Daoris runs changes";
    }

    /// <summary>
    /// A session's Git section for its repository's held remotes (the design §5.3), or null where none is held: what is held and why,
    /// and that it is not to try, beside the boundary of its instruction.
    /// </summary>
    public static string? Told(IReadOnlyList<GitHold> holds) => Told(holds, rewritten: false, origin: null);

    /// <summary>
    /// A session's Git section (the design §5.3): where <paramref name="rewritten"/> says the remote helper is laid out for its start,
    /// what git shows a remote as under the <c>daoris::</c> rewrite and what to name a tool with (<see cref="GitRewriteWords"/>),
    /// <paramref name="origin"/>'s address (as <see cref="GitRemotes.AddressOf"/> reads it) as the example where it is an HTTP or HTTPS
    /// one; then its repository's held remotes. Null where there is neither.
    /// </summary>
    public static string? Told(IReadOnlyList<GitHold> holds, bool rewritten, string? origin)
    {
        var rewrite = rewritten ? GitRewriteWords.Told(origin) : null;
        if (holds.Count == 0) return rewrite is null ? null : "**Git.** " + rewrite;
        var lines = holds.OrderBy(hold => hold.Remote, StringComparer.Ordinal).Select(hold => hold.Holds == Writes
            ? $"`{hold.Remote}` ({hold.Host ?? hold.Address}) refused a push with the sign-in git has on this machine. You may fetch it; "
              + "pushing is not yours in any case."
            : hold.Class == "own-input"
            ? $"`{hold.Remote}` ({hold.Host ?? hold.Address}) is held: {OwnInputWhy}. Do not fetch, pull or push it, and do not change "
              + $"that setting: work from the line as this checkout has it, and say in your closing note that `{hold.Remote}` was not fetched."
            : $"`{hold.Remote}` ({hold.Host ?? hold.Address}) cannot be reached with the sign-in git has on this machine "
              + $"({Why(hold)}), so neither you nor Daoris can reach it now. Do not fetch, pull or push it: work from the line as this "
              + $"checkout has it, and say in your closing note that `{hold.Remote}` was not fetched.");
        return "**Git.** " + (rewrite is null ? "" : rewrite + " ") + string.Join(" ", lines)
            + " The person sees it in What needs you, where Try again lifts it.";
    }

    /// <summary>Why a repository is held as <c>own-input</c> (<see cref="GitOwnInput"/>), naming the setting and never its value.</summary>
    private const string OwnInputWhy =
        "this repository names its own ssh command (`core.sshCommand`), which git runs in place of Daoris's ssh door on this install, "
        + "since the install's folder holds a space or another character git's shell would read";

    private static string Why(GitHold hold) => hold.Class switch
    {
        "host-unknown" => "ssh does not know its key",
        "host-changed" => "its key is not the one ssh knows for it",
        _ => "the host refused it",
    };

    /// <summary>The holds on a repository named as the registry names it, in a workspace where one is named.</summary>
    public static IReadOnlyList<GitHold> OfRepository(string home, string repository, string? workspace) =>
    [
        .. Read(home).Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && (workspace is null || each.Workspace is null || string.Equals(each.Workspace, workspace, StringComparison.OrdinalIgnoreCase))),
    ];

    /// <summary>The holds on a repository's checkout.</summary>
    public static IReadOnlyList<GitHold> On(string home, string root) => [.. Read(home).Where(each => SameRoot(each, root))];

    private static bool SameRoot(GitHold hold, string root) =>
        string.Equals(Path.GetFullPath(hold.Root), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);

    private static bool SameRemote(GitHold hold, string root, string remote) => SameRoot(hold, root) && hold.Remote == remote;

    private static string Clean(string address) => GitWords.Redact(address.Trim())!;

    private static string LastLine(string? words)
    {
        var lines = (GitWords.Redact(words) ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // git's own `fatal:` line says what failed; ssh's sentence before it says why, and is the one worth keeping where there is one.
        var why = lines.LastOrDefault(line => !line.StartsWith("fatal: Could not read from remote repository", StringComparison.Ordinal)
            && !line.StartsWith("Please make sure you have the correct access rights", StringComparison.Ordinal)
            && !line.StartsWith("and the repository exists", StringComparison.Ordinal));
        return why ?? lines.LastOrDefault() ?? "";
    }

    private static List<GitHold> Load(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.GitHeld);
        if (!File.Exists(path)) return [];
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path))?["held"] is not JsonArray held) return [];
            return [.. held.OfType<JsonObject>().Select(Of).OfType<GitHold>()];
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable file holds nothing: every remote is tried, which is what a restart does anyway.
            return [];
        }
    }

    private static GitHold? Of(JsonObject each)
    {
        string? Text(string name) => each[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        if (Text("root") is not { } root || Text("remote") is not { } remote || Text("address") is not { } address) return null;
        if (Text("holds") is not (All or Writes) || GitFailures.OfCode(Text("class")) is null) return null;
        var since = DateTimeOffset.TryParse(Text("since"), out var at) ? at : DateTimeOffset.MinValue;
        return new GitHold(
            root, Text("repository") ?? "", Text("workspace"), remote, address, Text("host"), Text("served") ?? Ambient, Text("holds")!,
            Text("class")!, Text("git") ?? "", Text("words") ?? "", since);
    }

    /// <summary>
    /// A hold kept or lifted in this process, with the home it is in: <i>What needs you</i>'s slice hears it (GITAUTH1a). Raised off the
    /// store's lock, on the pool, so a handler that reads the holds again never waits on the write that told it.
    /// </summary>
    public static event Action<string>? Changed;

    private static void Save(string home, IReadOnlyList<GitHold> holds)
    {
        Written(home, holds);
        if (Changed is { } changed) ThreadPool.QueueUserWorkItem(_ => changed(home));
    }

    private static void Written(string home, IReadOnlyList<GitHold> holds)
    {
        var path = DaorisHome.Entry(home, HomeEntry.GitHeld);
        if (holds.Count == 0)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return;
        }

        var list = new JsonArray();
        foreach (var hold in holds)
        {
            list.Add(new JsonObject
            {
                ["root"] = hold.Root,
                ["repository"] = hold.Repository,
                ["workspace"] = hold.Workspace,
                ["remote"] = hold.Remote,
                ["address"] = hold.Address,
                ["host"] = hold.Host,
                ["served"] = hold.Served,
                ["holds"] = hold.Holds,
                ["class"] = hold.Class,
                ["git"] = hold.Git,
                ["words"] = hold.Words,
                ["since"] = hold.Since.ToString("O"),
            });
        }

        JsonFile.Write(path, new JsonObject { ["held"] = list });
    }

    private static void Logged(string home, string @event, GitHold hold, string? why = null, string? failure = null)
    {
        using var log = new MachineLog(home, "driver");
        log.Info(@event, ("repository", hold.Repository), ("remote", hold.Remote), ("host", hold.Host), ("holds", hold.Holds),
            ("class", failure ?? hold.Class), ("why", why), ("door", "daoris"));
    }
}
