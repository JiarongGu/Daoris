using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A push of one ref to origin (GITAPP2g, n): <paramref name="Kind"/> is <c>branch</c> or <c>tag</c>, <paramref name="At"/> the commit the page
/// showed it at (refused where it stands elsewhere), <paramref name="Workspace"/> the hold's row, as a fetch's is.
/// </summary>
public sealed record PushAsk(string Repository, string Checkout, string Kind, string Name, string? At = null, string? Workspace = null);

/// <summary>
/// What the Branches tab reads of the push: whether it is offered, why not, origin's push address (redacted), the place's Dangerous value
/// (which decides a tag), and the command a person types, with its ref left as <c>&lt;commit&gt;:&lt;ref&gt;</c>.
/// </summary>
public sealed record PushPlan(bool Offered, string? Reason, string? Why, string? Address, string Dangerous, string Command);

/// <summary>
/// **Push one ref to origin** (GITAPP2g, GITAPP2n, D204, D241 point 2; the git-writes design §9, §12 rows g and n): the one push Daoris makes.
/// One <c>push</c> step of the act journal (<see cref="ActPushSteps"/>) whose command is fixed whatever the repository's configuration says:
/// one ref, <c>&lt;commit&gt;:refs/heads/&lt;name&gt;</c> or <c>&lt;commit&gt;:refs/tags/&lt;name&gt;</c>, no <c>+</c>, no <c>-u</c>, no followed
/// tags, no submodules, no signing, no push options; the repository's hooks run. It carries a grant bound to origin's push address, the ref,
/// the commit the ref stands at on origin as last fetched (the zero id where none) and the commit it moves to, so the doors let through
/// that update and no other. A branch is a Write; a lightweight tag is the same act, and the doors judge it Dangerous by the place's value
/// whatever the grant. Refused before anything is written where origin names more than one push address or is set to run another
/// program or mirror, where Write denies or the identity serving origin is read only, and where origin is held.
/// </summary>
public static class GitPushActs
{
    public const string Kind = "ref.push";

    /// <summary>The id git gives a ref that does not stand.</summary>
    public const string Absent = "0000000000000000000000000000000000000000";

    /// <summary>The git words of the act, the whole command after <c>git</c>, for <paramref name="id"/> to <paramref name="reference"/>.</summary>
    public static IReadOnlyList<string> Arguments(string id, string reference) =>
    [
        "-c", "push.pushOption=", "-c", "push.followTags=false", "-c", "push.recurseSubmodules=no", "-c", "push.gpgSign=false",
        "push", "--no-follow-tags", "--recurse-submodules=no", "--no-signed", "--no-atomic", "origin", $"{id}:{reference}",
    ];

    /// <summary>The command a person types in the checkout for the same act.</summary>
    public static string Command(string id, string reference) => "git " + string.Join(' ', Arguments(id, reference));

    // Settings of origin that run another program, or make a push mean something wider than its one ref.
    private static readonly Regex Widening = new(@"^remote\.origin\.(uploadpack|receivepack|mirror|vcs)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether a push is offered here, read fresh: the doors installed beside the home, an <c>origin</c> with exactly one push address, and none
    /// of its settings that widen it. Read without the lock and written nowhere.
    /// </summary>
    public static async Task<FetchOffer> OfferedAsync(string home, string checkout, CancellationToken ct)
    {
        if (GitWiring.DoorPath(home, GitWiring.SshDoor) is null || GitWiring.DoorPath(home, GitWiring.RemoteHelper) is null)
        {
            return new(false, "doors", "Daoris's git doors are not installed in this Daoris, and every remote act passes them");
        }

        if (await GitRemotes.AddressOf(checkout, "origin", push: true, ct: ct).ConfigureAwait(false) is null) return new(false, "no-origin", "there is no `origin` remote here");
        var (urls, listed, urlErr) = await WorkingTree.GitAsync(checkout, ["remote", "get-url", "--push", "--all", "origin"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (urls != 0) return new(false, "unread", $"git could not read origin's push address ({ContentHolds.Said(urlErr)})");
        if (listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length != 1)
        {
            return new(false, "widened", "origin names more than one push address, and a push goes to the one its grant names");
        }

        var (code, set, err) = await WorkingTree.GitAsync(
            checkout, ["config", "--name-only", "--get-regexp", @"^remote\.origin\.(uploadpack|receivepack|mirror|vcs)$"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code == 1) return new(true, null, null);
        if (code != 0) return new(false, "unread", $"git could not read origin's settings ({ContentHolds.Said(err)})");
        var widening = set.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(each => each.ToLowerInvariant()).FirstOrDefault(Widening.IsMatch);
        return widening is null ? new(true, null, null) : new(false, "widened", $"`{widening}` is set, which would run another program or mirror instead of pushing one ref");
    }

    /// <summary>The Branches tab's read of the push (a plan, changing nothing): whether it is offered, why not, the address, the place's Dangerous value.</summary>
    public static async Task<GitAnswer<PushPlan>> PlanReadAsync(string home, string repository, string? workspace, string checkout, CancellationToken ct)
    {
        var offer = await OfferedAsync(home, checkout, ct).ConfigureAwait(false);
        var address = offer.Offered ? GitWords.Redact(await GitRemotes.AddressOf(checkout, "origin", push: true, ct: ct).ConfigureAwait(false)) : null;
        var dangerous = GitAccessKeys.Resolve(home, repository, workspace).Of(GitAccessKeys.Dangerous).Value;
        var command = Command("<commit>", "<ref>");
        return new(new PushPlan(offer.Offered, offer.Reason, offer.Why, address, dangerous, command), GitReadRefusal.None, null) { Commands = [command] };
    }

    /// <summary>What a push would do now, read without the lock and written nowhere: the update it would carry and where to, or why it is refused.</summary>
    public static async Task<(GitGrantRef? Update, string? Address, string? Refusal)> PlanAsync(string home, PushAsk ask, CancellationToken ct)
    {
        var (update, refusal) = await JudgeAsync(home, ask, null, ct).ConfigureAwait(false);
        return update is null
            ? (null, null, refusal)
            : (update, GitWords.Redact(await GitRemotes.AddressOf(ask.Checkout, "origin", push: true, ct: ct).ConfigureAwait(false)), null);
    }

    /// <summary>Push: refused before anything is written where it is not offered, not allowed or origin is held, else run to its close.</summary>
    public static async Task<ActRun> PushAsync(ActRuns runs, PushAsk ask, string press, string? confirmed, CancellationToken ct)
    {
        var home = runs.Journal.Home;
        var (update, refusal) = await JudgeAsync(home, ask, null, ct).ConfigureAwait(false);
        if (update is null) return new ActRun(null, null, refusal!, true);
        var address = GitWords.Redact(await GitRemotes.AddressOf(ask.Checkout, "origin", push: true, ct: ct).ConfigureAwait(false)) ?? "";
        var arguments = Arguments(update.New, update.Ref);
        var plan = new JsonObject
        {
            ["kind"] = ask.Kind, ["name"] = ask.Name, ["ref"] = update.Ref, ["at"] = update.New, ["old"] = update.Old, ["remote"] = "origin", ["address"] = address,
            ["commands"] = new JsonArray((JsonNode)Command(update.New, update.Ref)),
        };
        var run = await runs.RunAsync(new ActPlan(Kind, press, ask.Repository, ask.Checkout, [ActPushSteps.Push(arguments, ask.Repository, ask.Workspace, address, update)])
        {
            Plan = plan.ToJsonString(), Confirmed = confirmed,
            Judge = async token => (await JudgeAsync(home, ask, update, token).ConfigureAwait(false)).Refusal,
        }, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null || run.Outcome == ActOutcomes.Done) return run;

        var said = runs.Journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Push)?.Said?.TrimEnd('.', ' ');
        return string.IsNullOrEmpty(said) ? run : run with { Notes = Capital(said) + ".", Said = $"{run.Said} {Capital(said)}." };
    }

    /// <summary>
    /// The update the push would carry, or why it is refused now: not offered, the ref not as asked, a git older than the doors, origin held or
    /// its own input in the way, Write denied or the serving identity read only. <paramref name="planned"/> is the update the plan judged,
    /// which a fresh read must equal.
    /// </summary>
    private static async Task<(GitGrantRef? Update, string? Refusal)> JudgeAsync(string home, PushAsk ask, GitGrantRef? planned, CancellationToken ct)
    {
        var offer = await OfferedAsync(home, ask.Checkout, ct).ConfigureAwait(false);
        if (!offer.Offered) return (null, $"{Capital(offer.Why!)}, so nothing was pushed.");
        if (await ReadRefAsync(ask, ct).ConfigureAwait(false) is not { } read) return (null, "git could not read the ref, so nothing was pushed.");
        if (read.Refusal is { } wrong) return (null, wrong);

        var address = (await GitRemotes.AddressOf(ask.Checkout, "origin", push: true, ct: ct).ConfigureAwait(false))!;
        if (await GitWiring.RefusesAsync(home, ask.Repository, "origin", address, GitAct.Write, ct).ConfigureAwait(false) is { } older)
            return (null, $"{Capital(older)}, so nothing was pushed.");
        if (Access(home, ask, address) is { } denied) return (null, $"{Capital(denied)}, so nothing was pushed.");
        var git = WorkingTree.GitInUse(home);
        if (await GitOwnInput.HeldAsync(home, ask.Checkout, ask.Repository, ask.Workspace, "origin", address, git, ct).ConfigureAwait(false) is { } own)
        {
            return (null, $"{GitHolds.Said(own)} Nothing was pushed.");
        }

        if (GitHolds.Holding(home, ask.Checkout, "origin", address, git, GitAct.Write) is { } held) return (null, $"{GitHolds.Said(held)} Nothing was pushed.");
        var update = new GitGrantRef(read.Full!, read.Id!, read.Old);
        return planned is not null && planned != update
            ? (null, $"`{ask.Name}` or origin's `{ask.Name}` moved since the plan, so nothing was pushed.")
            : (update, null);
    }

    /// <summary>
    /// Why the person's git access refuses the push, or null where it lets it: the press is the yes for exactly this push, as the landing's
    /// Accept is, so only <c>deny</c> or an identity read only on its host refuses. The doors refuse the same push; a tag's Dangerous value
    /// is theirs to judge.
    /// </summary>
    private static string? Access(string home, PushAsk ask, string address)
    {
        var value = GitAccessKeys.Resolve(home, ask.Repository, ask.Workspace).Of(GitAccessKeys.Write).Value;
        var host = GitWords.HostOf(address);
        var way = address.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? GitIdentity.Https : GitIdentity.Ssh;
        var serves = host is null ? null : GitIdentityUse.Resolve(
            GitIdentities.Read(home), GitIdentityUse.Read(home), ask.Repository, ask.Workspace, ["origin"], GitHosts.Canonical(host), way);
        var verdict = GitAccessRules.Judge(GitAct.Write, value, granted: true, serves?.Identity is { ReadOnly: true } readOnly ? readOnly.Label : null);
        if (verdict.Allowed) return null;
        return verdict.Code == "read-only"
            ? $"{serves!.Identity!.Label} is read only on {host}, so it cannot push to origin"
            : "this repository's git access denies writes to origin";
    }

    /// <summary>What the push rests on, read fresh: the ref's full name, its commit, and the commit origin's ref stood at as last fetched.</summary>
    private sealed record RefRead(string? Full, string? Id, string? Old, string? Refusal);

    private static async Task<RefRead?> ReadRefAsync(PushAsk ask, CancellationToken ct)
    {
        if (GitRefActs.FullName(ask.Kind, ask.Name) is not { } full) return new(null, null, null, $"`{ask.Kind}` is neither a branch nor a tag, so nothing was pushed.");
        var noun = ask.Kind == "tag" ? "tag" : "branch";
        if (ask.Name.Length == 0 || ask.Name.StartsWith('-') || ask.Name.Any(char.IsWhiteSpace) || ask.Name.Contains(':'))
        {
            return new(null, null, null, $"`{ask.Name}` is not a name git takes for a {noun}, so nothing was pushed.");
        }

        if (ask.Kind == "branch" && (ask.Name == "daoris" || ask.Name.StartsWith("daoris/", StringComparison.Ordinal)))
        {
            return new(null, null, null, $"`{ask.Name}` is under `daoris/`, Daoris's own for its sessions' branches, which a workflow's `push` node pushes, so nothing was pushed.");
        }

        var (code, listed, err) = await WorkingTree.GitAsync(
            ask.Checkout, ["for-each-ref", "--format=%(refname)%09%(objectname)%09%(objecttype)%09%(symref)", "--", full], ct).ConfigureAwait(false);
        if (code != 0) return new(null, null, null, $"git could not read `{ask.Name}` ({ContentHolds.Said(err)}), so nothing was pushed.");
        var row = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(each => each.TrimEnd('\r').Split('\t')).FirstOrDefault(parts => parts.Length >= 3 && parts[0] == full);
        if (row is null) return new(null, null, null, $"no {noun} named `{ask.Name}` stands, so nothing was pushed.");
        if (row.Length > 3 && row[3].Length > 0) return new(null, null, null, $"`{ask.Name}` is a symbolic ref, so nothing was pushed.");
        if (ask.Kind == "tag" && row[2] == "tag") return new(null, null, null, $"`{ask.Name}` is an annotated or signed tag, and only a lightweight tag is pushed here, so nothing was pushed.");
        if (row[2] != "commit" || !WorkingTree.IsCommitId(row[1])) return new(null, null, null, $"`{ask.Name}` names no commit, so nothing was pushed.");
        if (ask.At is not null && !string.Equals(ask.At, row[1], StringComparison.OrdinalIgnoreCase) && !row[1].StartsWith(ask.At, StringComparison.OrdinalIgnoreCase))
        {
            return new(null, null, null, $"`{ask.Name}` no longer stands at the commit shown, so nothing was pushed.");
        }

        // Origin's ref as last fetched: a tag is never fetched (§9), so it is pushed as new, and a branch never fetched is pushed as new too.
        var old = Absent;
        if (ask.Kind == "branch")
        {
            var (known, at, _) = await WorkingTree.GitAsync(ask.Checkout, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{ask.Name}"], ct).ConfigureAwait(false);
            if (known == 0 && WorkingTree.IsCommitId(at.Trim())) old = at.Trim();
        }

        return new(full, row[1], old, null);
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
