using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A fetch of origin into a repository's checkout: <paramref name="Workspace"/> names the hold's row, as the line fetch's does.</summary>
public sealed record FetchAsk(string Repository, string Checkout, string? Workspace = null);

/// <summary>Whether a fetch is offered, and where not why: <paramref name="Reason"/> a word (<c>doors</c>, <c>no-origin</c>, <c>widened</c>, <c>unread</c>), <paramref name="Why"/> its sentence.</summary>
public sealed record FetchOffer(bool Offered, string? Reason, string? Why);

/// <summary>What the Branches tab reads of the fetch: whether it is offered, why not, origin's address (redacted) and the command a person types.</summary>
public sealed record FetchPlan(bool Offered, string? Reason, string? Why, string? Address, string Command);

/// <summary>
/// **Fetch origin's branches** (§12 row f). One <c>fetch</c> step of the act journal whose command
/// is fixed whatever the repository's configuration says of origin: its refspec is given, <c>--refmap=</c> turns the configured ones off, tags
/// and submodules and maintenance are off, so only <c>refs/remotes/origin/*</c> moves, and <c>--prune</c> removes only what origin no longer has
/// there. <c>FETCH_HEAD</c> is neither written nor emptied. A fetch is a Read through Daoris's doors, which must be
/// installed; it is refused where the remote is set to run another program or to mirror. It writes refs and no file of the checkout, so it asks no
/// checkout switch.
/// </summary>
public static class GitFetchActs
{
    public const string Kind = "ref.fetch";

    /// <summary>The git words of the act, the whole command after <c>git</c>.</summary>
    public static IReadOnlyList<string> Arguments { get; } =
    [
        "-c", "fetch.pruneTags=false", "-c", "remote.origin.pruneTags=false", "-c", "maintenance.auto=false", "-c", "gc.auto=0", "-c", "fetch.writeCommitGraph=false",
        "fetch", "--no-tags", "--no-recurse-submodules", "--no-write-fetch-head", "--refmap=", "--prune", "origin", "+refs/heads/*:refs/remotes/origin/*",
    ];

    /// <summary>The command a person types in the checkout for the same act.</summary>
    public static string Command => "git " + string.Join(' ', Arguments.Select(word => word.Contains('*') ? $"\"{word}\"" : word));

    // Settings of origin that run another program, or make a fetch mean something wider than its branches.
    private static readonly Regex Widening = new(@"^remote\.origin\.(uploadpack|receivepack|mirror|vcs)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether a fetch is offered here, read fresh: the doors installed beside the home, an <c>origin</c> remote, and none of its settings that
    /// widen it. Read without the lock and written nowhere.
    /// </summary>
    public static async Task<FetchOffer> OfferedAsync(string home, string checkout, CancellationToken ct)
    {
        if (GitMove.Missing(home) is { Length: > 0 } missing)
        {
            return new(false, "doors", $"Daoris's git doors are not all installed in this Daoris ({string.Join(", ", missing)} missing), and every remote act passes them");
        }

        if (await GitRemotes.AddressOf(checkout, "origin", ct: ct).ConfigureAwait(false) is null) return new(false, "no-origin", "there is no `origin` remote here");
        var (code, listed, err) = await WorkingTree.GitAsync(
            checkout, ["config", "--name-only", "--get-regexp", @"^remote\.origin\.(uploadpack|receivepack|mirror|vcs)$"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code == 1) return new(true, null, null);
        if (code != 0) return new(false, "unread", $"git could not read origin's settings ({ContentHolds.Said(err)})");
        var set = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(each => each.ToLowerInvariant()).FirstOrDefault(Widening.IsMatch);
        return set is null ? new(true, null, null) : new(false, "widened", $"`{set}` is set, which would run another program or mirror instead of fetching origin's branches");
    }

    /// <summary>
    /// The Branches tab's read of the fetch (a plan, changing nothing): whether it is offered and, where not, why in a word the page can sit on
    /// (<c>doors</c>, <c>no-origin</c>, <c>widened</c>, <c>unread</c>), origin's address redacted, and the command a person types.
    /// </summary>
    public static async Task<GitAnswer<FetchPlan>> PlanReadAsync(string home, string checkout, CancellationToken ct)
    {
        var offer = await OfferedAsync(home, checkout, ct).ConfigureAwait(false);
        var address = offer.Offered ? GitWords.Redact(await GitRemotes.AddressOf(checkout, "origin", ct: ct).ConfigureAwait(false)) : null;
        return new(new FetchPlan(offer.Offered, offer.Reason, offer.Why, address, Command), GitReadRefusal.None, null) { Commands = [Command] };
    }

    /// <summary>What a fetch would do now, read without the lock and written nowhere: the address it fetches from (redacted), or why it is refused.</summary>
    public static async Task<(string? Address, string? Refusal)> PlanAsync(string home, FetchAsk ask, CancellationToken ct)
    {
        var refusal = await JudgeAsync(home, ask, ct).ConfigureAwait(false);
        return refusal is not null
            ? (null, refusal)
            : (GitWords.Redact(await GitRemotes.AddressOf(ask.Checkout, "origin", ct: ct).ConfigureAwait(false)), null);
    }

    /// <summary>Fetch: refused before anything is written where it is not offered or origin is held, else run to its close and the refs it moved named.</summary>
    public static async Task<ActRun> FetchAsync(ActRuns runs, FetchAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        var home = runs.Journal.Home;
        if (await JudgeAsync(home, ask, ct).ConfigureAwait(false) is { } refusal) return new ActRun(null, null, refusal, true);
        var before = await TrackingAsync(ask.Checkout, ct).ConfigureAwait(false);
        if (before is null) return new ActRun(null, null, "git could not read origin's tracking refs, so nothing was fetched.", true);
        var address = GitWords.Redact(await GitRemotes.AddressOf(ask.Checkout, "origin", ct: ct).ConfigureAwait(false)) ?? "";

        var plan = new JsonObject
        {
            ["remote"] = "origin", ["address"] = address, ["moves"] = "refs/remotes/origin/*", ["commands"] = new JsonArray((JsonNode)Command),
        };
        var run = await runs.RunAsync(new ActPlan(Kind, press, ask.Repository, ask.Checkout, [ActFetchSteps.Fetch(before, ask.Repository, ask.Workspace, address)])
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed, Judge = token => JudgeAsync(home, ask, token),
        }, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null) return run;

        var said = runs.Journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Fetch)?.Said;
        var moved = await TrackingAsync(ask.Checkout, CancellationToken.None).ConfigureAwait(false) is { } after ? Moved(before, after) : "";
        var notes = string.Join(' ', new[] { moved, run.Outcome == ActOutcomes.Done || string.IsNullOrEmpty(said) ? "" : Capital(said) + "." }.Where(each => each.Length > 0));
        return notes.Length == 0 ? run : run with { Notes = notes, Said = $"{run.Said} {notes}" };
    }

    /// <summary>Why a fetch is refused now, or null: not offered, origin's settings, a hold on origin, or its address a program of the person's own.</summary>
    private static async Task<string?> JudgeAsync(string home, FetchAsk ask, CancellationToken ct)
    {
        var offer = await OfferedAsync(home, ask.Checkout, ct).ConfigureAwait(false);
        if (!offer.Offered) return $"{Capital(offer.Why!)}, so nothing was fetched.";
        var address = (await GitRemotes.AddressOf(ask.Checkout, "origin", ct: ct).ConfigureAwait(false))!;
        if (await GitWiring.RefusesAsync(home, ask.Repository, "origin", address, GitAct.Read, ct).ConfigureAwait(false) is { } older)
            return $"{Capital(older)}, so nothing was fetched.";
        var git = WorkingTree.GitInUse(home);
        if (await GitOwnInput.HeldAsync(home, ask.Checkout, ask.Repository, ask.Workspace, "origin", address, git, ct).ConfigureAwait(false) is { } own)
        {
            return $"{GitHolds.Said(own)} Nothing was fetched.";
        }

        return GitHolds.Holding(home, ask.Checkout, "origin", address, git, GitAct.Read) is { } held ? $"{GitHolds.Said(held)} Nothing was fetched." : null;
    }

    /// <summary>Origin's tracking refs and their ids, symbolic ones left out; null where git could not list them.</summary>
    internal static async Task<SortedDictionary<string, string>?> TrackingAsync(string root, CancellationToken ct)
    {
        var (code, listed, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname)%09%(objectname)%09%(symref)", "refs/remotes/origin/"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code != 0) return null;
        var refs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var parts in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r').Split('\t')))
        {
            if (parts.Length >= 2 && (parts.Length < 3 || parts[2].Length == 0)) refs[parts[0]] = parts[1];
        }

        return refs;
    }

    private const int Named = 12;

    /// <summary>The refs that were made, moved or removed, named by their short names, a few and then how many more.</summary>
    internal static string Moved(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        var made = after.Keys.Where(name => !before.ContainsKey(name)).ToList();
        var moved = after.Where(each => before.TryGetValue(each.Key, out var was) && was != each.Value).Select(each => each.Key).ToList();
        var removed = before.Keys.Where(name => !after.ContainsKey(name)).ToList();
        var parts = new List<string>();
        if (made.Count > 0) parts.Add($"Made {Names(made)}.");
        if (moved.Count > 0) parts.Add($"Moved {Names(moved)}.");
        if (removed.Count > 0) parts.Add($"Removed {Names(removed)}, which origin no longer has.");
        return parts.Count == 0 ? "Nothing of origin's had changed." : string.Join(' ', parts);
    }

    private static string Names(List<string> refs)
    {
        var shown = refs.Take(Named).Select(name => $"`{name["refs/remotes/".Length..]}`");
        return string.Join(", ", shown) + (refs.Count > Named ? $" and {refs.Count - Named} more" : "");
    }

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
