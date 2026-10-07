using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Each repository's line and how its work lands, the page's `bridge/lines.ts` (MOD5): the lines and
/// landing rules as set and as they resolve (WSR1, WSR2), the clean-up of the branches sessions and
/// landings left (WSR3), and bringing each repository up to date after a pull request merged (WSR6).
/// </summary>
public sealed partial class DriverModule
{
    // A repository's line, or a workspace's (WSR2), or either cleared with no branch — the same
    // file `daoris driver line` edits: one truth, two doors (D50).
    [DriverRoute("SET_LINE")]
    private object? SetLine(IpcRequest request)
    {
        var branch = Optional(request, "branch");
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a line is set for a `repository` or a `workspace` — name one of them.");
        }

        Change(config => workspace is not null
            ? config.WithWorkspaceLine(workspace, branch)
            : config.WithLine(repository!, branch));
        return State();
    }

    // A repository's session language, or a workspace's (LANG1c, D142 point 7), or either cleared with no language — the
    // same file `daoris driver language` edits: one truth, two doors (D50). A code the table does not hold is the driver's
    // refusal.
    [DriverRoute("SET_LANGUAGE")]
    private object? SetLanguage(IpcRequest request)
    {
        var language = Optional(request, "language");
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a session language is set for a `repository` or a `workspace` — name one of them.");
        }

        Change(config => workspace is not null
            ? config.WithWorkspaceLanguage(workspace, language)
            : config.WithLanguage(repository!, language));
        return State();
    }

    // Every repository's line here and what said so (WSR2), for the screen. The checkouts are
    // the registry's, so this waits for the driver like the review does.
    [DriverRoute("LINES")]
    private async Task<object?> LinesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var lines = await CanonicalLine.OfAsync(
            config,
            snapshot.Repositories
                .OrderBy(known => known.Repository, StringComparer.Ordinal)
                .Select(known => (known.Repository, (string?)known.Workspace, known.Root)),
            cancellationToken).ConfigureAwait(false);
        return new
        {
            Lines = lines.Select(line => new { line.Repository, line.Workspace, line.Branch, line.Source }).ToArray(),
            // How each one's work lands (WSR1), from the same file and the same circles.
            Landings = lines.Select(line => (line, landing: LandingRules.Choose(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    pair.landing.Rule.Form,
                    pair.landing.Rule.Pattern,
                    pair.landing.Rule.Tidy,
                    pair.landing.Rule.Plugin,
                    pair.landing.Rule.AutoAccept,
                    pair.landing.Source,
                })
                .ToArray(),
            // What each one's sessions are asked to write to the person in, and where that was set (LANG1c): the driver's
            // resolution, read rather than recomputed by the page. None leaves the language and its source out.
            Languages = lines.Select(line => (line, language: SessionLanguages.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Language = pair.language?.Code,
                    pair.language?.Name,
                    pair.language?.Source,
                })
                .ToArray(),
        };
    }

    // A repository's landing rule or a workspace's, or either cleared with no form — the same file
    // `daoris driver landing` edits: one truth, two doors (D50).
    [DriverRoute("SET_LANDING")]
    private object? SetLanding(IpcRequest request)
    {
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a landing rule is set for a `repository` or a `workspace` — name one of them.");
        }

        // *Accept automatically* rides the rule (LAND2a, D145): a save that dropped it would switch it off unasked.
        var rule = Optional(request, "form") is { } form
            ? new LandingRule(form, Optional(request, "pattern"), Flag(request, "tidy"), Optional(request, "plugin"), Flag(request, "autoAccept"))
            : null;
        // The rule's plugin must be able to land work here, said as `daoris driver landing` says it (D100);
        // the shape is the file's own question, asked first by the edit below.
        if (rule is { Plugin: { } plugin } && LandingRules.Problem(rule) is null
            && LandingRules.PluginProblem(plugin, PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names)) is { } problem)
        {
            throw new DriverException(problem);
        }

        Change(config => workspace is not null
            ? config.WithWorkspaceLanding(workspace, rule)
            : config.WithLanding(repository!, rule));
        return State();
    }

    // The clean-up (WSR3, D88): every session branch here with what it holds — then, on the
    // person's press, those the proof clears, and only those the page listed to go (`only`).
    /// <summary>
    /// The clean-up's list, or its press (WSR3, D88), over every repository with a checkout here. A tree a
    /// session still running or waiting names is kept whatever it holds. Beside the session branches, the
    /// branches landings made (WSR5): each goes where its work reached the line, by the same press.
    /// </summary>
    [DriverRoute("SWEEP_PLAN")]
    [DriverRoute("SWEEP")]
    private async Task<object?> SweepAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(null, cancellationToken);
        var trees = new SessionTrees(_loop.Home);

        if (request.Type == "SWEEP_PLAN")
        {
            var plan = await trees.CleanPlanAsync(repositories, inUse, cancellationToken);
            return new { Branches = plan.Sessions.Select(SweepRow).ToArray(), Landed = plan.Landed.Select(LandedRow).ToArray() };
        }

        HashSet<string>? only = null;
        if (request.Payload is { } payload && payload.TryGetProperty("only", out var named) && named.ValueKind == JsonValueKind.Array)
        {
            only = named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        var done = await trees.CleanAsync(repositories, inUse, only, cancellationToken);
        _loop.Nudge();
        return new
        {
            Results = done.Sessions.Select(result => new { Branch = SweepRow(result.Item), result.Removed, result.Message }).ToArray(),
            Landed = done.Landed.Select(result => new { Branch = LandedRow(result.Item), result.Removed, result.Message }).ToArray(),
            // The empty folders trees left where something held them open, each removed or still held (WSR6's first run).
            Folders = done.Folders ?? [],
            Removed = done.Sessions.Count(result => result.Removed) + done.Landed.Count(result => result.Removed),
        };
    }

    // Discarding a failed or superseded attempt's branch (LAND3b, D102's LAND3 note), beside the clean-up whose list offers
    // it: its commits are on no branch of the person's, so no landing's tidy and no clean-up takes it, and once its tree is
    // gone the review has nothing to discard. `daoris-driver trees remove <branch> --repository <name> --force` is the
    // terminal's door (D50), and both end in the driver's one discard (LAND3c), which keeps a branch a live session's tree holds.
    /// <summary>
    /// Discard a session branch by its name and its repository (LAND3b): with its tree where it still has one.
    /// </summary>
    /// <remarks>
    /// A refusal is an ANSWER, as discarding a tree's is: `{ done: false, message }` with the driver's sentence. Unforced it
    /// is D88's proof, refusing a branch whose commits no branch of the person's holds; the page asks once, naming the
    /// branch and its commits, and sends `force`. A branch whose tree a session still running or waiting holds is kept
    /// whatever the press says (<see cref="SessionBranchDiscard"/>).
    /// </remarks>
    [DriverRoute("DISCARD_SESSION_BRANCH")]
    private async Task<object?> DiscardBranchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var branch = PayloadHelper.GetRequiredValue<string>(request.Payload, "branch");
        var force = Flag(request, "force");
        var service = _loop.Service ?? throw NotReady();

        var removal = await new SessionBranchDiscard(new SessionTrees(_loop.Home)).DiscardAsync(service, repository, branch, force, cancellationToken);
        _loop.Nudge();
        return new { Repository = repository, Branch = branch, Done = removal.Removed, removal.Message };
    }

    // Bringing repositories up to date after a pull request merged (WSR6, D109): the list, which fetches each line —
    // moving only origin's refs — and the press, which does not fetch again: it acts on what the list fetched, and
    // only on the rows the page listed (`only`). `daoris-driver trees sync` is the terminal's door (D50).
    /// <summary>
    /// Bringing each repository with a checkout here up to date (WSR6): its line fast-forwarded, the branches still at
    /// work replayed onto it, the landed branches whose work reached it deleted — listed first, done by a press.
    /// </summary>
    /// <remarks>
    /// <b>A row that is not done is an answer, not an error</b>, as the clean-up's are: a conflict, a checkout with work
    /// in flight, a branch on its remote. Each comes back as its row with the sentence the tree layer wrote.
    /// <para><b>A workspace's page asks for its own</b> (BRSCOPE1, WSP5): with `workspace`, the look fetches, and the press
    /// judges, that workspace's checkouts alone, and what it leaves apart is its own. With none, every checkout here.</para>
    /// </remarks>
    [DriverRoute("TREES_SYNC_PLAN")]
    [DriverRoute("TREES_SYNC")]
    private async Task<object?> TreesSyncAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var named = Optional(request, "repository");
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(named, cancellationToken, Optional(request, "workspace"));
        var trees = new SessionTrees(_loop.Home);
        // Which repositories it takes (D112): those holding Daoris's branches, and the ones the person included — every
        // one (`all`), the ones ticked (`also`), or the one named. The press takes each a listed row names besides.
        var also = Names(request, "also");
        if (named is not null) also.Add(named);
        var scope = Flag(request, "all") ? SyncScope.Everything : SyncScope.Named(also);
        static object Repository(SyncRepository each) => new { each.Repository, each.Workspace, each.Holds };

        static string? Short(string? commit) => commit is null ? null : commit[..Math.Min(8, commit.Length)];
        object Pull(LinePull pull) => new
        {
            pull.Repository, pull.Workspace, pull.Line, pull.Kind, From = Short(pull.From), To = Short(pull.To), pull.Commits,
            pull.Fetch, pull.Detail, pull.Moves,
            // Where the fetch failed (WSR7): when this checkout last heard from origin, and how origin is reached.
            pull.LastFetch, pull.Reach,
        };
        object Rebase(RebaseItem item) => new
        {
            item.Repository, item.Workspace, item.Branch, item.Landed, item.Kind, item.Onto, item.Cut, item.CutBy, item.GrewFrom,
            item.Commits, item.Detail, item.Replays,
        };

        if (request.Type == "TREES_SYNC_PLAN")
        {
            var plan = await trees.SyncPlanAsync(repositories, inUse, fetch: true, cancellationToken, scope);
            return new
            {
                Lines = plan.Lines.Select(Pull).ToArray(),
                Rebases = plan.Rebases.Select(Rebase).ToArray(),
                Deletes = plan.Deletes.Select(LandedRow).ToArray(),
                // What the look took, and every other repository with a checkout, listed apart for the person to include (D112).
                Looked = plan.Looked.Select(Repository).ToArray(),
                Apart = plan.Apart.Select(Repository).ToArray(),
            };
        }

        HashSet<string>? only = request.Payload is { } payload && payload.TryGetProperty("only", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? Names(request, "only").ToHashSet(StringComparer.Ordinal)
            : null;

        // The sessions in use, asked again while each repository's trees are held for its replays (LEFT2): the
        // driver may have opened one in a listed tree between the list and this press.
        var service = _loop.Service ?? throw NotReady();
        var done = await trees.SyncAsync(repositories, inUse, only, fetch: false, cancellationToken,
            inUseNow: async token => await InUseAsync(service, token), scope: scope);
        _loop.Nudge();
        return new
        {
            Lines = done.Lines.Select(result => new { Line = Pull(result.Pull), result.Moved, result.Message }).ToArray(),
            Rebases = done.Rebases.Select(result => new { Branch = Rebase(result.Item), result.Replayed, result.Message }).ToArray(),
            Deletes = done.Deletes.Select(result => new { Branch = LandedRow(result.Item), result.Removed, result.Message }).ToArray(),
            Changed = done.Lines.Count(result => result.Moved) + done.Rebases.Count(result => result.Replayed) + done.Deletes.Count(result => result.Removed),
        };
    }

    // Which repositories a look would take (WSR7, D112): every one with a checkout here, and whether it holds a branch
    // of Daoris's. Read on the machine, never fetched, so the screen can say what a look will fetch before it is asked.
    // The machine's whole, each row naming its workspace: a workspace's page counts its own rows (BRSCOPE1), and Ask
    // Daoris's sync card, whose look is the machine's, waits by the whole.
    [DriverRoute("TREES_SYNC_SCOPE")]
    private async Task<object?> TreesSyncScopeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (repositories, _) = await CheckoutsAndSessionsAsync(null, cancellationToken);
        var known = await new SessionTrees(_loop.Home).SyncScopeAsync(repositories, cancellationToken);
        return new { Repositories = known.Select(each => new { each.Repository, each.Workspace, each.Holds }).ToArray() };
    }

    /// <summary>The strings of a payload's array field, or none where it has no such array.</summary>
    private static HashSet<string> Names(IpcRequest request, string field) =>
        request.Payload is { } payload && payload.TryGetProperty(field, out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// A session branch's row: what the proof found (D88), and whether its discard is offered beside it (LAND3b), by the
    /// driver's own rule, so the page offers it beside the rows `daoris-driver trees clean` prints its line beside: a
    /// failed or superseded attempt's commits, which no clean-up takes. The tree's path stays here; the page is told only
    /// whether there is one.
    /// </summary>
    public static object SweepRow(SweepItem item) => new
    {
        item.Repository, item.Workspace, item.Branch, HasTree = item.Tree is not null,
        item.Kind, item.Commits, item.Where, item.Detail, item.Removable,
        Discardable = SessionTrees.RemovalOffered(item) is not null,
    };

    /// <summary>A landed branch's row: what the proof found, and the files that keep it where some do (WSR5).</summary>
    private static object LandedRow(LandedItem item) => new
    {
        item.Repository, item.Workspace, item.Branch, item.Kind, item.Where, item.Files, item.Detail,
        item.PullRequest, item.Commits, item.Removable,
    };

    /// <summary>
    /// Every repository with a checkout here — or the one named, or one workspace's — and the trees sessions still running
    /// or waiting name: what the clean-up and bringing up to date both judge by, from the service, so they wait for the driver.
    /// </summary>
    private async Task<(List<(string Repository, string? Workspace, string? Root)> Repositories, HashSet<string> InUse)> CheckoutsAndSessionsAsync(
        string? repository, CancellationToken cancellationToken, string? workspace = null)
    {
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken);
        var inUse = await InUseAsync(service, cancellationToken);
        return (Checkouts(registry, repository, workspace), inUse);
    }

    /// <summary>
    /// The registry's rows with a checkout here, by name: the one <paramref name="repository"/> names where it names one, and
    /// <paramref name="workspace"/>'s alone where it names one (BRSCOPE1, WSP5), matched without case, a row with no
    /// workspace read as the default's, as <see cref="RemoteTarget.Workspace"/> reads it for every door.
    /// </summary>
    public static List<(string Repository, string? Workspace, string? Root)> Checkouts(
        IEnumerable<RepoView> registry, string? repository, string? workspace) =>
        registry
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .Where(row => repository is null || string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(row => workspace is null
                || string.Equals(RemoteTarget.Workspace(row.Workspace), RemoteTarget.Workspace(workspace), StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Repository, StringComparer.Ordinal)
            .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
            .ToList();

    /// <summary>The trees sessions still running or waiting name, as the service's ledger says now.</summary>
    private static async Task<HashSet<string>> InUseAsync(ServiceClient service, CancellationToken cancellationToken) =>
        (await service.ActiveSessionsAsync(cancellationToken))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
