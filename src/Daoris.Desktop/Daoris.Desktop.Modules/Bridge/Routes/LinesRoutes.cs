using System.Text.Json;
using System.Text.Json.Serialization;
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
    // one runs shares it (SWEEPFLIGHT1). The lines slice pushes the same answer per repository, judged from the machine's facts
    // (PUSH1b1, D182); this pull stays for a page that does not read the slice.
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
        return Answer([.. lines.Select(line => new LineRow(line.Repository, line.Workspace, line.Branch, line.Source))], config, _loop.Harnesses.Adapters);
    }

    /// <summary>
    /// What <c>LINES</c> answers for <paramref name="lines"/>: each repository's line, and how its work lands, the language its
    /// sessions write in, where its work is reviewed, which other agent reads it, what a helper does, how its work is committed and
    /// the access setup its sessions start under, each as it resolves from <paramref name="config"/> and where it was set, in arrays keyed by repository and workspace. The lines
    /// slice pushes each repository's rows of it (PUSH1b1, D182): one builder for both doors, so the pull and the push cannot drift.
    /// </summary>
    /// <param name="adapters">The loop's agents, the machine's plugins among them, which an opinion's <c>sameAgent</c> is judged by.</param>
    internal static object Answer(IReadOnlyList<LineRow> lines, DriverConfig config, AdapterSet adapters)
    {
        return new
        {
            Lines = lines.ToArray(),
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
                    Rule = pair.opinion is null ? null : OpinionWire(pair.opinion.Rule, config.Adapter, adapters),
                    pair.opinion?.Source,
                })
                .ToArray(),
            // What a helper each one's agent asks for does, and how its work is committed, each with where it was set (DOORS1b,
            // D163 point 3, D164's amendment point 3): the driver's resolution, never null, `default` where nothing is set.
            Helpers = lines.Select(line => (line, helpers: HelperRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = HelperWire(pair.helpers.Rule), pair.helpers.Source })
                .ToArray(),
            // What an agent's ask to hand its session over does (MIXAGENT1c, D163 point 3), with where it was set, `default` where
            // nothing is: the driver's resolution, never null.
            HandOver = lines.Select(line => (line, handOver: HandOverRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = new { pair.handOver.Rule.Mode }, pair.handOver.Source })
                .ToArray(),
            Commits = lines.Select(line => (line, commits: CommitRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = CommitWire(pair.commits.Rule), pair.commits.Source })
                .ToArray(),
            // The access setup each one's sessions start under, every pause with its value and where it was set (AUTONOMY1a, D184
            // point 1): the driver's resolution, never null, `default` where nothing is set, so the page shows where each came from.
            Access = lines.Select(line => (line, access: AccessRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    pair.access.Full,
                    Pauses = pair.access.Pauses.Select(pause => new { pause.Pause, pause.Value, pause.Source }).ToArray(),
                })
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
        // RPC1b3: `trees clean --workspace` takes one workspace's checkouts alone, as that workspace's Branches tab does; none takes every one.
        var workspace = Optional(request, "workspace");
        // PUSH1c4a: the pull-request answers kept before this look, so what its asks wrote reaches the pushed list at once (below).
        var kept = request.Type == "SWEEP_PLAN" ? new LandedBranches(_loop.Home).Entries() : null;
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken, workspace);
        // RPCOPEN1: a terminal's list says a plugin's own lines as they come, under its name, as its alone path does; the page's says none.
        var pluginLines = new List<string>();
        var trees = kept is not null && Flag(request, "said") ? Asking(pluginLines) : new SessionTrees(_loop.Home);

        if (kept is not null)
        {
            var plan = await trees.CleanPlanAsync(repositories, inUse, cancellationToken);
            // The person's look asks the plugins (D148 point 2) and keeps their answers in the landing record, which no watch hears and
            // which moves no ref: each repository whose kept answers it wrote has its part of the pushed list judged again now
            // (PUSH1c4a), rather than at its next ref move or deadline. Asked of no plugin again: the part reads what was kept.
            SweepStateSlice.Asked(_loop.Facts, kept, new LandedBranches(_loop.Home).Entries());
            // LAND4: the session each kept branch's tree is, so its row says how to land it as that session's page does. The
            // records are read only where a row could offer one, and records that do not read offer none: the list stands.
            var records = plan.Sessions.Any(OffersLanding)
                ? await SessionBranchDoors.RecordsOrNoneAsync(_loop.Service ?? throw NotReady(), cancellationToken)
                : [];
            // RPCOPEN1: `trees clean` asks for the verb's words beside the list's fields (`said`); the page does not.
            return Flag(request, "said")
                ? SweepAnswer(plan, records, TreesPlanWords.CleanPlan(plan, records, new TreesAsk(TreesLook.Clean) { Workspace = workspace }).After(pluginLines))
                : SweepAnswer(plan, records);
        }

        HashSet<string>? only = null;
        if (request.Payload is { } payload && payload.TryGetProperty("only", out var named) && named.ValueKind == JsonValueKind.Array)
        {
            only = named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        // A workspace the registry does not name is refused, naming the ones it does, rather than clean nothing and say all is well.
        if (workspace is not null && TreesCommand.Take(
                await (_loop.Service ?? throw NotReady()).RegistryAsync(cancellationToken), new TreesAsk(TreesLook.Clean) { Workspace = workspace }).Refusal is { } refusal)
        {
            return new
            {
                Results = Array.Empty<object>(), Landed = Array.Empty<object>(), Folders = Array.Empty<string>(), Removed = 0,
                Said = TreesWords.Refused($"trees: {refusal}").ToWire(),
            };
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
            // RPC1b3: what `trees clean --yes` prints, each branch removed or kept with why, and the count (§7.3).
            Said = TreesWords.Clean(done).ToWire(),
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
        // RPC1b4: `trees remove <named>` names a session or a branch by one name, as the verb takes it, with `repository` only where it
        // must say which; the page's `repository` and `branch` read as they always did.
        var named = Optional(request, "named");
        var force = Flag(request, "force");
        if (named is not null)
        {
            var kept = _loop.Service ?? throw NotReady();
            var asked = await new SessionBranchDiscard(new SessionTrees(_loop.Home)).DiscardNamedAsync(
                kept, named, Optional(request, "repository"), force, cancellationToken);
            _loop.Nudge();
            return new
            {
                Repository = Optional(request, "repository"), Branch = (string?)null, Done = asked.Removed, asked.Message,
                Said = TreesWords.Remove(asked).ToWire(),
            };
        }

        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var branch = PayloadHelper.GetRequiredValue<string>(request.Payload, "branch");
        var service = _loop.Service ?? throw NotReady();

        var removal = await new SessionBranchDiscard(new SessionTrees(_loop.Home)).DiscardAsync(service, repository, branch, force, cancellationToken);
        _loop.Nudge();
        return new { Repository = repository, Branch = branch, Done = removal.Removed, removal.Message, Said = TreesWords.Remove(removal).ToWire() };
    }

    // The person's delete of the recovery refs nothing holds (RECOVERYDROP1, D102's CLEANUP1 note): CLEANUP1 keeps such a ref at any
    // age until they say so, and this is their saying, the page's half of `daoris-driver work recovery drop`. Both doors end in
    // RecoveryDropping.PressAsync, which re-reads the holders at the moment of the delete.
    /// <summary>
    /// Delete the recovery refs the page listed, `refs` each with the commit it was shown at, in `repository`'s checkout.
    /// </summary>
    /// <remarks>
    /// A refusal is an ANSWER, as a discard's is: `{ done, refused, dropped, skipped, message }`. A ref a branch or a tag now holds,
    /// one that moved since the page read it, and one gone already are in `skipped` with why; only the lease's holder acts.
    /// </remarks>
    [DriverRoute("RECOVERY_DROP")]
    [Publishes("RECOVERY_DROP", Moves)]
    private async Task<object?> DropRecoveriesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var asked = RecoveriesAsked(request.Payload);
        var service = _loop.Service ?? throw NotReady();

        var root = Checkouts(await service.RegistryAsync(cancellationToken), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        var done = await RecoveryDropping.PressAsync(_loop.Home, root, repository, asked, cancellationToken);
        _loop.Nudge();
        return RecoveryDropAnswer(repository, done);
    }

    /// <summary>The refs a press names, each with the commit the page showed (<c>{ ref, commit }</c>); a call naming none is malformed.</summary>
    public static IReadOnlyList<RecoveryDrop> RecoveriesAsked(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty("refs", out var refs) || refs.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("`refs` must name the recovery refs to delete.");
        }

        var asked = new List<RecoveryDrop>();
        foreach (var each in refs.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || !each.TryGetProperty("ref", out var named) || named.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("each of `refs` must be `{ ref, commit }`.");
            }

            asked.Add(new(named.GetString()!, each.TryGetProperty("commit", out var shown) && shown.ValueKind == JsonValueKind.String ? shown.GetString() : null));
        }

        return asked;
    }

    /// <summary>What a delete of recovery refs became on the wire: what went, what was kept and why, and the sentence the terminal says.</summary>
    public static object RecoveryDropAnswer(string repository, RecoveryDrops done) => new
    {
        Repository = repository,
        Done = done.Refused is null && done.Dropped.Count > 0,
        done.Refused,
        Dropped = done.Dropped.Select(each => new { each.Ref, each.Commit }).ToArray(),
        Skipped = done.Skipped.Select(each => new { each.Ref, each.Why }).ToArray(),
        Message = string.Join("\n", RecoveryDropping.Words(repository, done)),
    };

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
        // RPCOPEN1: a terminal's list says a plugin's own lines as they come, under its name, as its alone path does; the page's says none.
        var pluginLines = new List<string>();
        var trees = request.Type == "TREES_SYNC_PLAN" && Flag(request, "said") ? Asking(pluginLines) : new SessionTrees(_loop.Home);
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
            // RPCOPEN1: `trees sync` asks for the verb's words beside the list's fields (`said`); the page does not.
            if (Flag(request, "said"))
            {
                var asked = new TreesAsk(TreesLook.Sync) { Repository = named, Workspace = Optional(request, "workspace"), All = Flag(request, "all") };
                return new
                {
                    Lines = plan.Lines.Select(Pull).ToArray(),
                    Rebases = plan.Rebases.Select(RebaseRow).ToArray(),
                    Deletes = plan.Deletes.Select(LandedRow).ToArray(),
                    Looked = plan.Looked.Select(Repository).ToArray(),
                    Apart = plan.Apart.Select(Repository).ToArray(),
                    Said = TreesPlanWords.SyncPlan(plan, asked, DateTimeOffset.UtcNow).After(pluginLines).ToWire(),
                };
            }

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

        // RPC1b3: `trees sync --yes` takes the checkouts its words name, as the verb takes them, and says so where it takes none: a
        // workspace the registry does not name, or no checkout of the repository named.
        var ask = new TreesAsk(TreesLook.Sync) { Repository = named, Workspace = Optional(request, "workspace"), All = Flag(request, "all") };
        if (repositories.Count == 0)
        {
            var taken = TreesCommand.Take(await service.RegistryAsync(cancellationToken), ask);
            return new
            {
                Lines = Array.Empty<object>(), Rebases = Array.Empty<object>(), Deletes = Array.Empty<object>(), Changed = 0,
                Apart = Array.Empty<object>(), Followed = Array.Empty<string>(),
                Said = TreesWords.Refused(taken.Refusal is { } refusal ? $"trees: {refusal}" : TreesCommand.NothingToSync(ask)).ToWire(),
            };
        }

        // The verb fetches as it presses (`fetch`); the page's press acts on what its list fetched, and sends none. The lines the press
        // moved are followed by the registry at once where the verb asks (`follow`), as the page's next look would (WSSETUP5).
        var done = await trees.SyncAsync(repositories, inUse, only, fetch: Flag(request, "fetch"), cancellationToken,
            inUseNow: async token => await InUseAsync(service, token), scope: scope);
        _loop.Nudge();
        var followed = Flag(request, "follow")
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken)
            : null;
        return new
        {
            Lines = done.Lines.Select(result => new { Line = Pull(result.Pull), result.Moved, result.Message }).ToArray(),
            Rebases = done.Rebases.Select(result => new { Branch = RebaseRow(result.Item), result.Replayed, result.Message }).ToArray(),
            Deletes = done.Deletes.Select(result => new { Branch = LandedRow(result.Item), result.Removed, result.Message }).ToArray(),
            Changed = done.Lines.Count(result => result.Moved) + done.Rebases.Count(result => result.Replayed) + done.Deletes.Count(result => result.Removed),
            // What the press left apart (WSR7, D112) and the lines the registry followed, which the verb says; the page ignores both.
            Apart = done.Apart.Select(Repository).ToArray(),
            Followed = followed?.Out ?? [],
            Said = TreesWords.Sync(done, DateTimeOffset.UtcNow, followed).ToWire(),
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
    /// nothing (D169 point 2). With `unit`, the units that name picks (its id, session or branch). The work slice pushes the same
    /// units, entries and held attempts per repository, judged from the machine's facts (PUSH1c2a, D182); this pull stays for a
    /// page that does not read the slice, and for a unit's own read.
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

        return new
        {
            Units = units.Select(WorkUnit).ToArray(),
            // CLEANUP1, additive: Daoris's own recoveries no unit names (a folder moved aside, a recovery ref), each by its rule.
            Recovery = recovery.Select(WorkEntry).ToArray(),
            Unread = unread.ToArray(),
            // FABRIC-PERFORM1g, additive: the plugin steps whose attempt is held for the person, of every unit (a quest's too), or of the
            // units the name picked. An install no graph naming a plugin's performed node governs keeps no node record, and lists none.
            Held = HeldAnswer(HeldAttempts.Read(new NodeRecordFiles(_loop.Home), quests: _loop.LastQuests)
                .Where(held => Optional(request, "unit") is not { } picked || held.Unit == picked || units.Any(unit => unit.Unit == held.Unit))),
        };
    }

    /// <summary>
    /// One unit's milestone as <c>WORK_STATE</c> answers it and the work slice pushes it (PUSH1c2a): one builder, two doors, so the pull
    /// and the push cannot drift. A field added here is pushed only once <see cref="WorkStateSlice.Fields"/> names it.
    /// </summary>
    public static object WorkUnit(WorkMilestone unit) => new
    {
        unit.Unit, unit.Repository, unit.Branch, unit.Session,
        End = new { unit.End.State, unit.End.By, unit.End.On, unit.End.Revision, unit.End.At, unit.End.Said },
        Result = unit.Result.Select(WorkEntry).ToArray(),
        Cleanup = new { unit.Cleanup.State, Waits = unit.Cleanup.Waits.Select(WorkEntry).ToArray(), Gone = unit.Cleanup.Gone.Select(WorkEntry).ToArray() },
        Recovery = unit.Recovery.Select(WorkEntry).ToArray(),
        Entries = unit.Entries.Select(WorkEntry).ToArray(),
    };

    /// <summary>One inventory entry as <c>WORK_STATE</c> answers it and the work slice pushes it (PUSH1c2a).</summary>
    public static object WorkEntry(InventoryEntry each) =>
        new { each.Unit, each.Kind, each.Resource, each.Role, each.Disposition, each.Detail, each.Until, each.Proof, each.Repository, each.Commit };

    /// <summary>
    /// The attempts held for the person as the wire carries them: the page words <c>why</c> by its code. FABRIC-CLOSE1q, additive: <c>words</c>,
    /// the words offered on each (a plugin's three; a held close's recoveries and its stop), which a page before the field reads without.
    /// </summary>
    public static object[] HeldAnswer(IEnumerable<HeldAttempt> held) =>
    [
        .. held.Select(each => new
        {
            each.Unit, each.Node, each.Attempt, each.Plugin, each.Step, each.NodeState, each.Why, each.Outcome, each.Revision,
            At = each.RecordedAt, each.Resolution, each.ResolvedAt, Said = HeldAttempts.WhySaid(each), each.Words,
        }),
    ];

    // The person's word on a plugin step's attempt held for them (FABRIC-PERFORM1g, D173's note): the page's half of `daoris-driver work
    // held <unit> <node> <attempt> <word>`. Both doors end in HeldAttempts.Word, which records it under the unit's node record's lock with
    // the lease asked inside it; the loop's runtime applies it under the node's declaration at its next pass, which the nudge wakes.
    /// <summary>
    /// Record the person's word (`resolution`: `happened`, `did-not` or `stop`; on a held close `record`, `resend` or `stop`, FABRIC-CLOSE1q)
    /// on `attempt` of `node` on `unit`, held for them.
    /// </summary>
    /// <remarks>
    /// A refusal is an ANSWER, as a discard's is: `{ done, refused, message }`, the message the terminal's sentence. Only the lease's
    /// holder records; an attempt no longer held, or already worded, is refused and said.
    /// </remarks>
    [DriverRoute("WORK_HELD")]
    [Publishes("WORK_HELD", StateScopes.Sessions)]
    private Task<object?> WorkHeldAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var unit = PayloadHelper.GetRequiredValue<string>(request.Payload, "unit");
        var node = PayloadHelper.GetRequiredValue<string>(request.Payload, "node");
        var attempt = PayloadHelper.GetRequiredValue<int>(request.Payload, "attempt");
        var word = PayloadHelper.GetRequiredValue<string>(request.Payload, "resolution");
        _ = _loop.Service ?? throw NotReady();

        var done = HeldAttempts.Word(new NodeRecordFiles(_loop.Home), unit, node, attempt, word);
        var worded = done.What == PerformedWhat.Worded;
        if (worded) _loop.Nudge();
        return Task.FromResult<object?>(new
        {
            Unit = unit, Node = node, Attempt = attempt, Resolution = word, Done = worded, Refused = worded ? null : done.Code,
            Message = HeldAttempts.Said(done, attempt, word),
        });
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

    /// <summary>
    /// The clean-up's list as <c>SWEEP_PLAN</c> answers it and the pushed <c>sweep</c> slice carries each repository's part of it
    /// (PUSH1c4a, <see cref="SweepStateSlice"/>): one builder, two doors. A row names its session (LAND4) only where it could offer that
    /// session's landing (<see cref="OffersLanding"/>), the only rows the door reads the records for, so a row says the same whichever
    /// other rows stand beside it.
    /// </summary>
    /// <param name="records">The sessions' records: the service's at the door, the machine's facts' look in the slice.</param>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records) => new
    {
        Branches = plan.Sessions
            .Select(item => SweepRow(item, OffersLanding(item) ? SessionBranchDoors.SessionOfTree(records, item.Tree) : null)).ToArray(),
        Landed = plan.Landed.Select(LandedRow).ToArray(),
    };

    /// <summary>
    /// The trees' look a terminal asks for (RPCOPEN1): its landing plugins say their own lines into <paramref name="lines"/> as the alone
    /// path says them on its console, under the plugin's name, and the answer's words carry them first.
    /// </summary>
    private SessionTrees Asking(List<string> lines) => new(
        _loop.Home, new LandingPlugins(_loop.Home, say: (plugin, line) => lines.Add(TreesWords.PluginLine(plugin, line)), log: _loop.Log, health: _loop.Health));

    /// <summary><see cref="SweepAnswer(SweepPlan, IReadOnlyList{SessionRecord})"/> with the words <c>trees clean</c> prints for it (RPCOPEN1, §7.3).</summary>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records, TreesSaid said) => new
    {
        Branches = plan.Sessions
            .Select(item => SweepRow(item, OffersLanding(item) ? SessionBranchDoors.SessionOfTree(records, item.Tree) : null)).ToArray(),
        Landed = plan.Landed.Select(LandedRow).ToArray(),
        Said = said.ToWire(),
    };

    /// <summary>Whether a kept branch's row could offer its session's landing (LAND4): unlanded, its tree still here.</summary>
    private static bool OffersLanding(SweepItem item) => item.Kind == SweepKind.Unlanded && item.Tree is not null;

    /// <summary>A landed branch's row: what the proof found, and the files that keep it where some do (WSR5).</summary>
    public static object LandedRow(LandedItem item) => new
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
    /// One change to a hand-over rule (MIXAGENT1c, D163 point 3), as the page sends it: the twin's edit in the shared table's own
    /// shape (<c>repository</c> or <c>workspace</c>; <c>set</c> with its <c>mode</c>, or <c>clear</c>), judged by
    /// <see cref="HandOverRules.Apply"/> before anything is written; the screens' half of `daoris driver handover`.
    /// </summary>
    [DriverRoute("SET_HAND_OVER")]
    [Publishes("SET_HAND_OVER", Chosen)]
    private object? SetHandOver(IpcRequest request)
    {
        var edit = HandOverRules.EditOf(request.Payload ?? default);
        // The twin's refusals come first, in its words, before anything is written.
        HandOverRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => HandOverRules.Apply(config, edit));
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

    /// <summary>
    /// One change to a session access setup (AUTONOMY1a, D184 point 1), the setup page's half of <c>daoris-driver access</c> over the
    /// same keys: <c>repository</c>, <c>workspace</c> or <c>machine: true</c>, and <c>access</c>, <c>"full"</c>, the pauses it sets
    /// (a null value taking one back to the level above) or null to clear, judged by <see cref="AccessRules.Apply"/> before anything is
    /// written. Full access is the person's press alone: no proposal names it (D184's rejected options). It reads nothing else, so it
    /// waits for nothing; what each repository resolves to moves under the driver's key, so the lines slice pushes it again.
    /// </summary>
    [DriverRoute("SET_ACCESS")]
    [Publishes("SET_ACCESS", Chosen)]
    private object? SetAccess(IpcRequest request)
    {
        var edit = AccessRules.EditOf(request.Payload ?? default);
        // The refusals come first, before anything is written.
        var before = DriverConfig.Load(_loop.ConfigPath);
        AccessRules.Apply(before, edit);
        _context.Change(config => AccessRules.Apply(config, edit));
        // ACCESSSWAP1b (D183's note): each driven session running here whose permission prompts this moves is told on its console that
        // its next turn runs under them; its run reads the file at that turn's end, as it does after the terminal's change.
        DrivenAccess.TellRunning(_loop.Processes, before, DriverConfig.Load(_loop.ConfigPath), _loop.Output.Append);
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// A level's access setup as the bridge carries it (AUTONOMY1a): whether it is full access, and the pauses it names in their order,
    /// each with its value, as rows. None set is no pause and not full, which is what setting nothing means.
    /// </summary>
    internal static object AccessWire(AccessSetting? setting) => new
    {
        Full = setting?.Full ?? false,
        Pauses = setting is null || setting.Full
            ? []
            : AccessRules.Pauses.Where(pause => setting.Pauses.ContainsKey(pause.Name))
                .Select(pause => new { Pause = pause.Name, Value = setting.Pauses[pause.Name] }).ToArray(),
    };

    /// <summary>A helpers rule as the bridge carries it (DOORS1b): its mode, and the agents in the order they are tried.</summary>
    internal static object HelperWire(HelperRule rule) => new { rule.Mode, rule.Agents };

    /// <summary>A commit rule as the bridge carries it (DOORS1b): its mode, and the person's convention, left out where none.</summary>
    internal static object CommitWire(CommitRule rule) => new { rule.Mode, rule.Convention };
}

/// <summary>
/// One repository's line as the bridge carries it (WSR2): which, in which circle, and what said so. The lines slice adds
/// <see cref="NotReadYet"/> where the checkout's guess was not judged yet (PUSH1b1): the branch is none and the page says the line
/// not read yet, never what none would mean. The pull, which asks git, never answers it.
/// </summary>
internal sealed record LineRow(string Repository, string Workspace, string? Branch, string Source)
{
    /// <summary>The checkout's guess not judged yet: left out where false, so the pull's rows are as they always were.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NotReadYet { get; init; }
}
