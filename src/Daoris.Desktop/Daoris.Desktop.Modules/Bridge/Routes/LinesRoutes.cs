using System.Text.Json;
using static Daoris.Driver.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Each repository's line and how its work lands, the page's `bridge/lines.ts` (MOD5): the lines and
/// landing rules as set and as they resolve (WSR1, WSR2), the clean-up of the branches sessions and
/// landings left (WSR3), and bringing each repository up to date after a pull request merged (WSR6).
/// </summary>
public sealed class LinesRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// What a press that removes, replays or moves session branches and trees changes (STATEHUB1b, D169): the sessions'
    /// trees, every plan judged against a line, the reviews, the landed branches a plugin could take, the clean-up's list,
    /// and which repositories hold Daoris's branches. About no one session, so every one's is read again.
    /// </summary>
    private const StateScopes Moves = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff | StateScopes.HandOff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>
    /// What a standing choice over a repository's or a workspace's rules changes (STATEHUB1h, D169): the driver's state it
    /// answers, and under the driver's key what each repository resolves to, every plan and gate that reads it, and the Current
    /// workflow drawn from it.
    /// </summary>
    private const StateScopes Chosen = StateScopes.Driver;

    // A repository's line, or a workspace's (WSR2), or either cleared with no branch — the same
    // file `daoris driver line` edits: one truth, two doors (D50).
    [DriverRoute("SET_LINE")]
    [Publishes("SET_LINE", Chosen)]
    private object? SetLine(IpcRequest request)
    {
        var branch = Optional(request, "branch");
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a line is set for a `repository` or a `workspace` — name one of them.");
        }

        _context.Change(config => workspace is not null
            ? config.WithWorkspaceLine(workspace, branch)
            : config.WithLine(repository!, branch));
        return DriverRoutes.State(_loop);
    }

    // A repository's session language, or a workspace's (LANG1c, D142 point 7), or either cleared with no language — the
    // same file `daoris driver language` edits: one truth, two doors (D50). A code the table does not hold is the driver's
    // refusal.
    [DriverRoute("SET_LANGUAGE")]
    [Publishes("SET_LANGUAGE", Chosen)]
    private object? SetLanguage(IpcRequest request)
    {
        var language = Optional(request, "language");
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a session language is set for a `repository` or a `workspace` — name one of them.");
        }

        _context.Change(config => workspace is not null
            ? config.WithWorkspaceLanguage(workspace, language)
            : config.WithLanguage(repository!, language));
        return DriverRoutes.State(_loop);
    }

    // Every repository's line here and what said so (WSR2), for the screen. The checkouts are
    // the registry's, so this waits for the driver like the review does. Git is asked each checkout's line, so an ask while
    // one runs shares it (SWEEPFLIGHT1).
    [DriverRoute("LINES")]
    [OneFlight("LINES")]
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
                    // The ticket's key and the squash (DOORS1b), for the page to show and keep: none is left out.
                    pair.landing.Rule.Key,
                    pair.landing.Rule.Squash,
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
            // Where each one's work is reviewed before it lands, and where that was set (REVIEWENV1a): the driver's resolution,
            // read rather than recomputed by the page. Nothing set anywhere leaves the rule and its source out.
            Reviews = lines.Select(line => (line, review: ReviewRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Rule = pair.review is null ? null : ReviewWire(pair.review.Rule),
                    pair.review?.Source,
                })
                .ToArray(),
            // Which other agent reads each one's work before it lands, and where that was set (XAGENT1a): the driver's
            // resolution, read rather than recomputed by the page. Nothing set anywhere leaves the rule and its source out.
            Opinions = lines.Select(line => (line, opinion: OpinionRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Rule = pair.opinion is null ? null : OpinionWire(pair.opinion.Rule, config.Adapter, _loop.Harnesses.Adapters),
                    pair.opinion?.Source,
                })
                .ToArray(),
            // What a helper each one's agent asks for does, and how its work is committed, each with where it was set (DOORS1b,
            // D163 point 3, D164's amendment point 3): the driver's resolution, never null, `default` where nothing is set.
            Helpers = lines.Select(line => (line, helpers: HelperRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = HelperWire(pair.helpers.Rule), pair.helpers.Source })
                .ToArray(),
            Commits = lines.Select(line => (line, commits: CommitRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = CommitWire(pair.commits.Rule), pair.commits.Source })
                .ToArray(),
        };
    }

    // A repository's landing rule or a workspace's, or either cleared with no form — the same file
    // `daoris driver landing` edits: one truth, two doors (D50).
    [DriverRoute("SET_LANDING")]
    [Publishes("SET_LANDING", StateScopes.Landing | StateScopes.Lines)]
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
        // The ticket's key and the squash (DOORS1b, D164's DOORS1a note) are set where the press names them, an empty key taking
        // it away; a press naming neither keeps the ones the rule set here holds, where the new rule can hold them, as Ask
        // Daoris's card keeps them. A save that rebuilt the rule without them dropped what the terminal set.
        if (rule is not null)
        {
            var config = DriverConfig.Load(_loop.ConfigPath);
            var standing = (workspace is not null ? config.WorkspaceLandings : config.Landings)
                .TryGetValue((workspace ?? repository)!, out var own) ? own : null;
            rule = LandingKept(rule, standing, request.Payload);
        }

        // The rule's plugin must be able to land work here, said as `daoris driver landing` says it (D100);
        // the shape is the file's own question, asked first by the edit below.
        if (rule is { Plugin: { } plugin } && LandingRules.Problem(rule) is null
            && LandingRules.PluginProblem(plugin, PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names)) is { } problem)
        {
            throw new DriverException(problem);
        }

        _context.Change(config => workspace is not null
            ? config.WithWorkspaceLanding(workspace, rule)
            : config.WithLanding(repository!, rule));
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// The rule a landing press leaves (DOORS1b): its <c>key</c> where the press names one (a string; empty or null is none), else
    /// the standing rule's where the new one is a branch whose pattern says <c>{ticket}</c>; its <c>squash</c> where the press says
    /// <c>true</c> or <c>false</c>, else the standing rule's on a branch. A merge keeps neither, and one the press names is the
    /// rule's shape to refuse (<see cref="LandingRules.Problem(LandingRule)"/>). Ask Daoris's <c>landing</c> card keeps them alike.
    /// </summary>
    internal static LandingRule LandingKept(LandingRule rule, LandingRule? standing, JsonElement? payload)
    {
        var (keyNamed, key) = (false, (string?)null);
        bool? squash = null;
        if (payload is { ValueKind: JsonValueKind.Object } root)
        {
            if (root.TryGetProperty("key", out var written) && written.ValueKind is JsonValueKind.String or JsonValueKind.Null)
            {
                keyNamed = true;
                key = written.ValueKind == JsonValueKind.String && written.GetString() is { Length: > 0 } shape ? shape : null;
            }

            if (root.TryGetProperty("squash", out var said) && said.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                squash = said.GetBoolean();
            }
        }

        var branch = rule.Form == LandingForm.Branch;
        var ticketed = branch && (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal);
        return rule with
        {
            Key = keyNamed ? key : ticketed ? standing?.Key : null,
            Squash = squash ?? (branch && standing?.Squash == true),
        };
    }

    // The clean-up (WSR3, D88): every session branch here with what it holds — then, on the
    // person's press, those the proof clears, and only those the page listed to go (`only`).
    /// <summary>
    /// The clean-up's list, or its press (WSR3, D88), over every repository with a checkout here. A tree a
    /// session still running or waiting names is kept whatever it holds. Beside the session branches, the
    /// branches landings made (WSR5): each goes where its work reached the line, by the same press. The list is a git fan-out
    /// over every checkout, so an ask while one runs shares it (<see cref="OneFlightAttribute"/>); the press is never shared.
    /// </summary>
    [DriverRoute("SWEEP_PLAN")]
    [DriverRoute("SWEEP")]
    [Publishes("SWEEP", Moves)]
    [OneFlight("SWEEP_PLAN")]
    private async Task<object?> SweepAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken);
        var trees = new SessionTrees(_loop.Home);

        if (request.Type == "SWEEP_PLAN")
        {
            var plan = await trees.CleanPlanAsync(repositories, inUse, cancellationToken);
            // LAND4: the session each kept branch's tree is, so its row says how to land it as that session's page does. The
            // records are read only where a row could offer one, and records that do not read offer none: the list stands.
            var records = plan.Sessions.Any(item => item.Kind == SweepKind.Unlanded && item.Tree is not null)
                ? await SessionBranchDoors.RecordsOrNoneAsync(_loop.Service ?? throw NotReady(), cancellationToken)
                : [];
            return new
            {
                Branches = plan.Sessions.Select(item => SweepRow(item, SessionBranchDoors.SessionOfTree(records, item.Tree))).ToArray(),
                Landed = plan.Landed.Select(LandedRow).ToArray(),
            };
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
    [Publishes("DISCARD_SESSION_BRANCH", Moves)]
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
    [Publishes("TREES_SYNC", Moves)]
    private async Task<object?> TreesSyncAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var named = Optional(request, "repository");
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(_loop, named, cancellationToken, Optional(request, "workspace"));
        var trees = new SessionTrees(_loop.Home);
        // Which repositories it takes (D112): those holding Daoris's branches, and the ones the person included — every
        // one (`all`), the ones ticked (`also`), or the one named. The press takes each a listed row names besides.
        var also = Names(request, "also");
        if (named is not null) also.Add(named);
        var scope = Flag(request, "all") ? SyncScope.Everything : SyncScope.Named(also);
        static object Repository(SyncRepository each) => new { each.Repository, each.Workspace, each.Holds };

        static string? Short(string? commit) => commit is null ? null : Daoris.Driver.TextBounds.ShortCommit(commit, 8);
        object Pull(LinePull pull) => new
        {
            pull.Repository, pull.Workspace, pull.Line, pull.Kind, From = Short(pull.From), To = Short(pull.To), pull.Commits,
            pull.Fetch, pull.Detail, pull.Moves,
            // Where the fetch failed (WSR7): when this checkout last heard from origin, and how origin is reached.
            pull.LastFetch, pull.Reach,
        };
        if (request.Type == "TREES_SYNC_PLAN")
        {
            var plan = await trees.SyncPlanAsync(repositories, inUse, fetch: true, cancellationToken, scope);
            return new
            {
                Lines = plan.Lines.Select(Pull).ToArray(),
                Rebases = plan.Rebases.Select(RebaseRow).ToArray(),
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
            Rebases = done.Rebases.Select(result => new { Branch = RebaseRow(result.Item), result.Replayed, result.Message }).ToArray(),
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
        var (repositories, _) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken);
        var known = await new SessionTrees(_loop.Home).SyncScopeAsync(repositories, cancellationToken);
        return new { Repositories = known.Select(each => new { each.Repository, each.Workspace, each.Holds }).ToArray() };
    }

    /// <summary>
    /// Each unit of work's milestone (FABRIC-MILESTONE1a, the fabric events design §D0, §D3), the page's read of what
    /// `daoris-driver work state` says (D50): whether its end was reached, the result kept, the cleanup pending (what waits and
    /// why) or completed (what went), and the recovery retained until its date, each with the inventory entries it is read from.
    /// Derived at each read from the records the tree lifecycle keeps, never stored; a look that changes nothing, so it declares
    /// nothing (D169 point 2). With `unit`, the units that name picks (its id, session or branch).
    /// </summary>
    [DriverRoute("WORK_STATE")]
    private async Task<object?> WorkStateAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken);
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repository, _, root) in repositories)
        {
            if (!string.IsNullOrWhiteSpace(root)) roots.TryAdd(repository, root);
        }

        var unread = new List<string>();
        var (units, recovery) = await WorkInventories.ReadAsync(_loop.Home, roots, inUse, unread, cancellationToken);
        if (Optional(request, "unit") is { } named)
        {
            units = WorkInventories.Named(units, named);
            recovery = [];
        }

        static object Entry(InventoryEntry each) => new { each.Unit, each.Kind, each.Resource, each.Role, each.Disposition, each.Detail, each.Until, each.Proof, each.Repository, each.Commit };
        return new
        {
            Units = units.Select(unit => new
            {
                unit.Unit, unit.Repository, unit.Branch, unit.Session,
                End = new { unit.End.State, unit.End.By, unit.End.On, unit.End.Revision, unit.End.At, unit.End.Said },
                Result = unit.Result.Select(Entry).ToArray(),
                Cleanup = new { unit.Cleanup.State, Waits = unit.Cleanup.Waits.Select(Entry).ToArray(), Gone = unit.Cleanup.Gone.Select(Entry).ToArray() },
                Recovery = unit.Recovery.Select(Entry).ToArray(),
                Entries = unit.Entries.Select(Entry).ToArray(),
            }).ToArray(),
            // CLEANUP1, additive: Daoris's own recoveries no unit names (a folder moved aside, a recovery ref), each by its rule.
            Recovery = recovery.Select(Entry).ToArray(),
            Unread = unread.ToArray(),
        };
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
    /// <param name="session">
    /// The session its tree is (LAND4, <see cref="SessionBranchDoors.SessionOfTree"/>), or null: named by id and how it ended, with
    /// whether its landing is offered by the driver's rule (<see cref="SessionBranchDoors.LandingOffered"/>), the line `trees clean`
    /// prints beside the same row. A clean-up's press answers none.
    /// </param>
    public static object SweepRow(SweepItem item, SessionRecord? session = null) => new
    {
        item.Repository, item.Workspace, item.Branch, HasTree = item.Tree is not null,
        item.Kind, item.Commits, item.Where, item.Detail, item.Removable,
        Discardable = SessionBranchDoors.RemovalOffered(item) is not null,
        Session = item.Tree is not null && session is not null ? new { session.Id, session.State } : null,
        Landable = SessionBranchDoors.LandingOffered(item, session) is not null,
    };

    /// <summary>
    /// A branch's row in bringing up to date (WSR6), the list's and the press's alike: what the press does with it, where its own
    /// commits start, how many it carries, and how many the line already holds, which the replay leaves out (LANDSYNC3b, D87's
    /// 2026-10-16 note), as `trees sync` says them.
    /// </summary>
    public static object RebaseRow(RebaseItem item) => new
    {
        item.Repository, item.Workspace, item.Branch, item.Landed, item.Kind, item.Onto, item.Cut, item.CutBy, item.GrewFrom,
        item.Commits, item.LeftOut, item.Detail, item.Replays,
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
    internal static async Task<(List<(string Repository, string? Workspace, string? Root)> Repositories, HashSet<string> InUse)> CheckoutsAndSessionsAsync(
        DriverLoop loop, string? repository, CancellationToken cancellationToken, string? workspace = null)
    {
        var service = loop.Service ?? throw NotReady();
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
    internal static async Task<HashSet<string>> InUseAsync(ServiceClient service, CancellationToken cancellationToken) =>
        (await service.ActiveSessionsAsync(cancellationToken))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

    // REVIEWENV1a (D154 point 2, design §1.7): where each repository's work is reviewed before it lands, the screens' half of
    // `daoris driver review`, sent by `bridge/lines.ts`. Declared only: nothing here composes a step, gates or serves.

    /// <summary>
    /// One change to a review rule, as the page sends it: the twin's edit in the shared table's own shape (<c>repository</c>
    /// or <c>workspace</c>; <c>put</c> with <c>required</c> if given, <c>none</c>, <c>drop</c>, <c>required</c> alone, or
    /// <c>clear</c>), judged by <see cref="ReviewRules.Apply"/> before anything is read or written. An environment put has its
    /// procedure looked for in each checkout the rule reaches, as the terminal looks: a repository's checkout that does not hold
    /// it refuses the change; a workspace's names each one that does not.
    /// </summary>
    /// <remarks>
    /// The answer is the state, with <c>reviewed</c> saying what the procedure's look found: the repositories of the workspace
    /// whose checkout lacks it, and whether it could be looked for at all (no checkout here).
    /// </remarks>
    [DriverRoute("SET_REVIEW")]
    [Publishes("SET_REVIEW", Chosen)]
    private async Task<object?> SetReviewAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var edit = ReviewEditOf(request.Payload);
        // The twin's refusals come first, in its words, before the registry is asked anything.
        ReviewRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);

        IReadOnlyList<string> lacking = [];
        var unchecked_ = false;
        if (edit.Put is { Procedure: { } procedure })
        {
            var service = _loop.Service ?? throw NotReady();
            var registry = (await service.RegistryAsync(cancellationToken).ConfigureAwait(false))
                .Select(row => (Repository: row.Repository, Workspace: (string?)row.Workspace, Root: row.Root)).ToList();
            lacking = ReviewRules.Lacking(registry, edit, procedure);
            if (edit.Repository is { } repository)
            {
                if (lacking.Count > 0) throw new DriverException(ReviewRules.NotHeld(repository.Trim(), procedure));
                unchecked_ = !registry.Any(row => row.Root is { Length: > 0 }
                    && string.Equals(row.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                unchecked_ = !registry.Any(row => row.Root is { Length: > 0 }
                    && string.Equals(RemoteTarget.Workspace(row.Workspace), RemoteTarget.Workspace(edit.Workspace), StringComparison.OrdinalIgnoreCase));
            }
        }

        _context.Change(config => ReviewRules.Apply(config, edit));
        // Only a put looked for anything, so only a put says what the look found.
        return DriverRoutes.State(_loop, reviewed: edit.Put is null ? null : new { Lacking = lacking, Unchecked = unchecked_ });
    }

    /// <summary>The page's edit, read as the shared table's: a field of another type is absent, <c>none</c> and <c>clear</c> only when true.</summary>
    private static ReviewEdit ReviewEditOf(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } root) return new ReviewEdit();

        return new ReviewEdit
        {
            Repository = Text(root, "repository"),
            Workspace = Text(root, "workspace"),
            Put = root.TryGetProperty("put", out var put) && put.ValueKind == JsonValueKind.Object
                ? new ReviewSpelled(Text(put, "name"), Text(put, "kind"), Text(put, "procedure"), Text(put, "address"), Text(put, "run"))
                : null,
            Required = root.TryGetProperty("required", out var required) && required.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? required.GetBoolean()
                : null,
            None = root.TryGetProperty("none", out var none) && none.ValueKind == JsonValueKind.True,
            Drop = Text(root, "drop"),
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>
    /// A rule as the bridge carries it (REVIEWENV1a): whether it is none here, whether work waits for the person's look, and
    /// each environment, the first the default. Rows rather than an object's keys, as every map here: a key policy on the
    /// bridge would respell a name.
    /// </summary>
    internal static object ReviewWire(ReviewRule rule) => new
    {
        None = rule.IsNone,
        rule.Required,
        Environments = rule.Environments.Select(each => new { each.Name, each.Kind, each.Procedure, each.Address, each.Run }).ToArray(),
    };

    // XAGENT1a (D155 point 3, the second-agent design §2.5): which other agent reads each repository's work before it lands,
    // the screens' half of `daoris driver opinion`, sent by `bridge/lines.ts`. Declared only: nothing here chooses a reviewer,
    // starts a pass or holds a landing.

    /// <summary>
    /// One change to a second-opinion rule, as the page sends it: the twin's edit in the shared table's own shape
    /// (<c>repository</c> or <c>workspace</c>; <c>set</c> with any of <c>reviewers</c>, <c>on</c>, <c>required</c>,
    /// <c>verify</c>, <c>minutes</c> and <c>recheck</c>, <c>none</c>, or <c>clear</c>), judged by
    /// <see cref="OpinionRules.Apply"/> before anything is written. It reads nothing else, so it waits for nothing.
    /// </summary>
    [DriverRoute("SET_OPINION")]
    [Publishes("SET_OPINION", Chosen)]
    private object? SetOpinion(IpcRequest request)
    {
        var edit = OpinionRules.EditOf(request.Payload ?? default);
        // The twin's refusals come first, in its words, before anything is written.
        OpinionRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => OpinionRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// A rule as the bridge carries it (XAGENT1a): whether it is none here, its occasions and reviewers in order, each switch,
    /// the bound of a pass (the default where none is written), and the reviewers of the working agent's own family, judged
    /// against <paramref name="working"/>, the agent this machine's work runs on, so the page says what the terminal says.
    /// </summary>
    /// <param name="adapters">
    /// The loop's agents, the machine's plugins among them, so a maker a plugin declares counts on the screen as it counts at
    /// the terminal and at Ask Daoris's door (XAGENT1b3).
    /// </param>
    internal static object OpinionWire(OpinionRule rule, string working, AdapterSet adapters) => new
    {
        None = rule.IsNone,
        rule.On,
        rule.Reviewers,
        rule.Required,
        rule.Verify,
        Minutes = rule.Bound,
        rule.Recheck,
        SameAgent = OpinionRules.SameAgent(rule, working, adapters),
    };

    // DOORS1b (D163 point 3, D164's amendment points 1 and 3): what a helper an agent asks for does, and how work is committed,
    // the screens' half of `daoris driver helpers` and `daoris driver commits`, sent by `bridge/lines.ts`. Ask Daoris's `setting`
    // kind's `helpers` and `commits` doors are their third (DOORS1a).

    /// <summary>
    /// One change to a helpers rule, as the page sends it: the twin's edit in the shared table's own shape (<c>repository</c> or
    /// <c>workspace</c>; <c>set</c> with <c>mode</c> and/or <c>agents</c>, or <c>clear</c>), judged by
    /// <see cref="HelperRules.Apply"/> before anything is written. It reads nothing else, so it waits for nothing.
    /// </summary>
    [DriverRoute("SET_HELPERS")]
    [Publishes("SET_HELPERS", Chosen)]
    private object? SetHelpers(IpcRequest request)
    {
        var edit = HelperRules.EditOf(request.Payload ?? default);
        // The twin's refusals come first, in its words, before anything is written.
        HelperRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => HelperRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// One change to a commit rule, as the page sends it: the twin's edit (<c>repository</c> or <c>workspace</c>; <c>set</c> with
    /// <c>mode</c> and/or <c>convention</c>, the empty convention taking it away, or <c>clear</c>), judged by
    /// <see cref="CommitRules.Apply"/> before anything is written. It reads nothing else, so it waits for nothing.
    /// </summary>
    [DriverRoute("SET_COMMITS")]
    [Publishes("SET_COMMITS", Chosen)]
    private object? SetCommits(IpcRequest request)
    {
        var edit = CommitRules.EditOf(request.Payload ?? default);
        CommitRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => CommitRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>A helpers rule as the bridge carries it (DOORS1b): its mode, and the agents in the order they are tried.</summary>
    internal static object HelperWire(HelperRule rule) => new { rule.Mode, rule.Agents };

    /// <summary>A commit rule as the bridge carries it (DOORS1b): its mode, and the person's convention, left out where none.</summary>
    internal static object CommitWire(CommitRule rule) => new { rule.Mode, rule.Convention };
}
