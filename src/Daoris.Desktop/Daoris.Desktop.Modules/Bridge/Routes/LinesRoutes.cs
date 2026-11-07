using System.Text.Json;
using System.Text.Json.Serialization;
using static Daoris.Driver.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Each repository's line and how its work lands, the page's `bridge/lines.ts`: the lines and
/// landing rules as set and as they resolve, the clean-up of the branches sessions and
/// landings left, and bringing each repository up to date after a pull request merged.
/// </summary>
public sealed class LinesRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;
    private readonly TreesTidyDesk _tidy = new(context.Loop);

    /// <summary>
    /// What a press that removes, replays or moves session branches and trees changes: the sessions'
    /// trees, every plan judged against a line, the reviews, the landed branches a plugin could take, the clean-up's list,
    /// and which repositories hold Daoris's branches. About no one session, so every one's is read again.
    /// </summary>
    private const StateScopes Moves = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>
    /// What a standing choice over a repository's or a workspace's rules changes: the driver's state it
    /// answers, and under the driver's key what each repository resolves to, every plan and gate that reads it, and the Current
    /// workflow drawn from it.
    /// </summary>
    private const StateScopes Chosen = StateScopes.Driver;

    // A repository's line, or a workspace's, or either cleared with no branch — the same
    // file `daoris place line` edits: one truth, two doors.
    [DriverRoute("SET_LINE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_LINE", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_LINE", "lines", "workspaceLines", Sets = "branch")]
    [Says("SET_LINE", "repository:repository")]
    [Publishes("SET_LINE", Chosen)]
    private object? SetLine(SetLine press)
    {
        var (repository, workspace) = (press.Repository, press.Workspace);
        // Sent at all, a branch sets the line, a blank or null one clearing it; a press with a policy and no branch keeps the line.
        var branch = press.Branch is { ValueKind: JsonValueKind.String } sent && sent.GetString() is { Length: > 0 } text ? text : null;
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a line is set for a `repository` or a `workspace` — name one of them.");
        }

        // The line's policy (how it is integrated) is edited here beside its branch, set where the press names an object,
        // withdrawn on null; a press that names only a policy leaves the line as it is. Its values are LandingKey's to judge.
        var named = press.Policy is not null;
        LandingIntegrate? policy = null;
        if (press.Policy is { ValueKind: not JsonValueKind.Null } wire)
        {
            policy = LinePolicyKey.IntegrateOf(wire, out var shape) ?? throw new DriverException(shape!);
        }

        var keepsLine = named && press.Branch is null;
        _context.Change(config =>
        {
            if (!keepsLine)
            {
                config = workspace is not null ? config.WithWorkspaceLine(workspace, branch) : config.WithLine(repository!, branch);
            }

            if (!named) return config;
            return workspace is not null ? config.WithWorkspaceLinePolicy(workspace, policy) : config.WithLinePolicy(repository!, policy);
        });
        return DriverRoutes.State(_loop);
    }

    // A repository's session language, or a workspace's, or either cleared with no language — the
    // same file `daoris place language` edits: one truth, two doors. A code the table does not hold is the driver's
    // refusal.
    [DriverRoute("SET_LANGUAGE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_LANGUAGE", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_LANGUAGE", "languages", "workspaceLanguages", Sets = "language")]
    [Says("SET_LANGUAGE", "repository:repository")]
    [Publishes("SET_LANGUAGE", Chosen)]
    private object? SetLanguage(SetLanguage press)
    {
        var (language, repository, workspace) = press;
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a session language is set for a `repository` or a `workspace` — name one of them.");
        }

        _context.Change(config => workspace is not null
            ? config.WithWorkspaceLanguage(workspace, language)
            : config.WithLanguage(repository!, language));
        return DriverRoutes.State(_loop);
    }

    // Every repository's line here and what said so, for the screen. The checkouts are
    // the registry's, so this waits for the driver like the review does. Git is asked each checkout's line, so an ask while
    // one runs shares it. The lines slice pushes the same answer per repository, judged from the machine's facts;
    //this pull stays for a page that does not read the slice.
    [DriverRoute("LINES", PipeClass.Open)]
    [OneFlight("LINES")]
    private async Task<object?> LinesAsync(NoPayload _, CancellationToken cancellationToken)
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

    /// <summary>What <c>LINES</c> answers for <paramref name="lines"/>, one builder for the pull and the slice (<see cref="LinesWires.Answer"/>).</summary>
    internal static object Answer(IReadOnlyList<LineRow> lines, DriverConfig config, ProviderRegistry adapters) => LinesWires.Answer(lines, config, adapters);

    /// <summary>A rule's <c>integrate</c> as the file writes it (<see cref="LinesWires.IntegrateWire"/>).</summary>
    internal static object? IntegrateWire(LandingIntegrate? integrate) => LinesWires.IntegrateWire(integrate);

    // The clean-up: every session branch here with what it holds — then, on the
    // person's press, those the proof clears, and only those the page listed to go (`only`).
    /// <summary>
    /// The clean-up's list, or its press, over every repository with a checkout here. A tree a
    /// session still running or waiting names is kept whatever it holds. Beside the session branches, the
    /// branches landings made: each goes where its work reached the line, by the same press. The list is a git fan-out
    /// over every checkout, so an ask while one runs shares it (<see cref="OneFlightAttribute"/>); the press is never shared.
    /// </summary>
    [DriverRoute("SWEEP_PLAN", PipeClass.Open)]
    [OneFlight("SWEEP_PLAN")]
    private Task<object?> SweepPlanAsync(SweepPlanRead read, CancellationToken cancellationToken) => _tidy.SweepPlanAsync(read, cancellationToken);

    // The clean-up's press: those the proof clears, and only those the page listed to go (`only`).
    [DriverRoute("SWEEP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SWEEP", Workspace = "workspace")]
    [GroundRead("SWEEP", nameof(PipeReads.SweepPlan))]
    [Publishes("SWEEP", Moves)]
    private Task<object?> SweepAsync(SweepPress press, CancellationToken cancellationToken) => _tidy.SweepAsync(press, cancellationToken);

    /// <summary>
    /// Discard a failed or superseded attempt's session branch by its name and its repository, with its tree where it still has
    /// one: its commits are on no branch of the person's, so no tidy takes it. A refusal is data; unforced it refuses a branch
    /// whose commits no branch of the person's holds, and a branch a running or waiting session's tree holds is always kept.
    /// </summary>
    [DriverRoute("DISCARD_SESSION_BRANCH", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("DISCARD_SESSION_BRANCH", Repository = "repository")]
    [GroundRepository("DISCARD_SESSION_BRANCH", "repository")]
    [GroundBranch("DISCARD_SESSION_BRANCH", "repository", "branch", "named")]
    [Says("DISCARD_SESSION_BRANCH", "repository:repository")]
    [Publishes("DISCARD_SESSION_BRANCH", Moves)]
    private async Task<object?> DiscardBranchAsync(DiscardBranchPress press, CancellationToken cancellationToken)
    {
        var named = press.Named;
        var force = press.Force;
        if (named is not null)
        {
            var kept = _loop.Service ?? throw NotReady();
            var asked = await new SessionBranchDiscard(new SessionTrees(_loop.Home)).DiscardNamedAsync(
                kept, named, press.Repository, force, cancellationToken);
            _loop.Nudge();
            return new
            {
                Repository = press.Repository, Branch = (string?)null, Done = asked.Removed, asked.Message,
                Said = TreesWords.Remove(asked).ToWire(),
            };
        }

        var repository = ActField.Needs(press.Repository, "repository");
        var branch = ActField.Needs(press.Branch, "branch");
        var service = _loop.Service ?? throw NotReady();

        var removal = await new SessionBranchDiscard(new SessionTrees(_loop.Home)).DiscardAsync(service, repository, branch, force, cancellationToken);
        _loop.Nudge();
        return new { Repository = repository, Branch = branch, Done = removal.Removed, removal.Message, Said = TreesWords.Remove(removal).ToWire() };
    }

    /// <summary>
    /// Withdraw a branch a landing made that the line will never hold, which `trees clean` keeps for ever: its commits kept on a
    /// recovery ref, the branch deleted at the commit judged. A refusal is data; `plan: true` changes nothing. Only the holder of
    /// the home's lease acts.
    /// </summary>
    [DriverRoute("WITHDRAW_LANDING_BRANCH", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WITHDRAW_LANDING_BRANCH", Repository = "repository")]
    [GroundRepository("WITHDRAW_LANDING_BRANCH", "repository")]
    [GroundBranch("WITHDRAW_LANDING_BRANCH", "repository", "branch")]
    [Says("WITHDRAW_LANDING_BRANCH", "repository:repository", "why:words")]
    [Publishes("WITHDRAW_LANDING_BRANCH", Moves)]
    private async Task<object?> WithdrawLandingBranchAsync(WithdrawLandingBranchPress press, CancellationToken cancellationToken)
    {
        var (repository, branch, plan) = (press.Repository, press.Branch, press.Plan);
        var why = press.Why ?? "";
        var service = _loop.Service ?? throw NotReady();

        var withdrawal = await new LandedBranchWithdrawal(new SessionTrees(_loop.Home), _loop.Home)
            .WithdrawAsync(service, repository, branch, why, plan, cancellationToken);
        if (!plan) _loop.Nudge();
        return new
        {
            Repository = repository, Branch = branch, Done = withdrawal.Done, Planned = withdrawal.Planned, withdrawal.Message, withdrawal.KeptRef,
            Said = TreesWords.Withdraw(withdrawal).ToWire(),
        };
    }

    /// <summary>
    /// The person's delete of the recovery refs the page listed, each at the commit it was shown at, which are kept at any age until
    /// they say so. The holders are read again at the delete: a ref now held, moved or gone is in `skipped` with why.
    /// </summary>
    [DriverRoute("RECOVERY_DROP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("RECOVERY_DROP", Repository = "repository")]
    [GroundRepository("RECOVERY_DROP", "repository")]
    [GroundRead("RECOVERY_DROP", nameof(PipeReads.RecoveryRefs))]
    [Says("RECOVERY_DROP", "repository:repository")]
    [Publishes("RECOVERY_DROP", Moves)]
    private async Task<object?> DropRecoveriesAsync(RecoveryDropPress press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var asked = RecoveriesAsked(press.Refs);
        var service = _loop.Service ?? throw NotReady();

        var root = Checkouts(await service.RegistryAsync(cancellationToken), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        var done = await RecoveryDropping.PressAsync(_loop.Home, root, repository, asked, cancellationToken);
        _loop.Nudge();
        return RecoveryDropAnswer(repository, done);
    }

    /// <summary>The refs a press names, each with the commit the page showed (<c>{ ref, commit }</c>); a call naming none is malformed.</summary>
    public static IReadOnlyList<RecoveryDrop> RecoveriesAsked(IReadOnlyList<RecoveryRefAsked>? refs)
    {
        if (refs is null) throw new ArgumentException("`refs` must name the recovery refs to delete.");
        return [.. refs.Select(each => each.Ref is { } named
            ? new RecoveryDrop(named, each.Commit)
            : throw new ArgumentException("each of `refs` must be `{ ref, commit }`."))];
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

    /// <summary>
    /// Bringing each repository with a checkout here up to date: its line fast-forwarded, the branches at work replayed onto it,
    /// the landed branches whose work reached it deleted. The list fetches; the press acts on what it fetched, and only on the rows
    /// the page listed (`only`). A row not done is an answer with the tree layer's sentence. `workspace` keeps both to its checkouts.
    /// </summary>
    [DriverRoute("TREES_SYNC_PLAN", PipeClass.Open)]
    private Task<object?> TreesSyncPlanAsync(TreesSyncPlanRead read, CancellationToken cancellationToken) => _tidy.TreesSyncPlanAsync(read, cancellationToken);

    // The press of the sync list: the rows it listed (`only`), and the repositories the person included besides.
    [DriverRoute("TREES_SYNC", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("TREES_SYNC", Repository = "repository", Workspace = "workspace")]
    [GroundRead("TREES_SYNC", nameof(PipeReads.SyncPlan))]
    [Says("TREES_SYNC", "repository:repository")]
    [Publishes("TREES_SYNC", Moves)]
    private Task<object?> TreesSyncAsync(TreesSyncPress press, CancellationToken cancellationToken) => _tidy.TreesSyncAsync(press, cancellationToken);

    // Which repositories a look would take: every one with a checkout here, and whether it holds a branch
    // of Daoris's. Read on the machine, never fetched, so the screen can say what a look will fetch before it is asked.
    // The machine's whole, each row naming its workspace: a workspace's page counts its own rows, and Ask
    // Daoris's sync card, whose look is the machine's, waits by the whole.
    [DriverRoute("TREES_SYNC_SCOPE", PipeClass.Open)]
    private async Task<object?> TreesSyncScopeAsync(NoPayload _, CancellationToken cancellationToken)
    {
        var (repositories, _) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken);
        var known = await new SessionTrees(_loop.Home).SyncScopeAsync(repositories, cancellationToken);
        return new { Repositories = known.Select(each => new { each.Repository, each.Workspace, each.Holds }).ToArray() };
    }

    /// <summary>
    /// Each unit of work's milestone, the page's read of what
    /// `daoris work state` says: whether its end was reached, the result kept, the cleanup pending (what waits and
    /// why) or completed (what went), and the recovery retained until its date, each with the inventory entries it is read from.
    /// Derived at each read from the records the tree lifecycle keeps, never stored; a look that changes nothing, so it declares
    /// nothing. With `unit`, the units that name picks (its id, session or branch). The work slice pushes the same
    /// units, entries and held attempts per repository, judged from the machine's facts; this pull stays for a
    /// page that does not read the slice, and for a unit's own read.
    /// </summary>
    [DriverRoute("WORK_STATE", PipeClass.Open)]
    private async Task<object?> WorkStateAsync(WorkStateRead read, CancellationToken cancellationToken)
    {
        var (repositories, inUse) = await CheckoutsAndSessionsAsync(_loop, null, cancellationToken);
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repository, _, root) in repositories)
        {
            if (!string.IsNullOrWhiteSpace(root)) roots.TryAdd(repository, root);
        }

        var unread = new List<string>();
        // each tree joined to the session that recorded it, from the sessions' look the work slice reads too.
        var looked = _loop.Facts.Current.Value<SessionLook>(HelpFacts.Sessions, HelpFacts.Whole);
        // Which quests the landings the line does not hold name are closed, the service's, read once and only where one asks.
        var closed = _loop.Service is { } ledger
            ? await LandedStanding.ClosedAsync(new LandedBranches(_loop.Home).Entries(), ledger.EveryQuestAsync, cancellationToken)
            : new HashSet<string>();
        var (units, recovery) = await WorkInventories.ReadAsync(
            _loop.Home, roots, inUse, unread, cancellationToken, looked is null ? null : WorkInventories.RecordedOf(looked.Records), closed);
        if (read.Unit is { } named)
        {
            units = WorkInventories.Named(units, named);
            recovery = [];
        }

        return new
        {
            Units = units.Select(WorkUnit).ToArray(),
            // Daoris's own recoveries no unit names (a folder moved aside, a recovery ref), each by its rule.
            Recovery = recovery.Select(WorkEntry).ToArray(),
            Unread = unread.ToArray(),
            // The plugin steps whose attempt is held for the person, of every unit (a quest's too), or of the
            // units the name picked. An install no graph naming a plugin's performed node governs keeps no node record, and lists none.
            Held = HeldAnswer(HeldAttempts.Read(new NodeRecordFiles(_loop.Home), quests: _loop.LastQuests)
                .Where(held => read.Unit is not { } picked || held.Unit == picked || units.Any(unit => unit.Unit == held.Unit))),
            // The node records that do not read, each stopping its unit until a person mends it; with a name,
            // those of the units it picked, by the unit a record names or its file's key.
            Unreadable = UnreadableAnswer(HeldAttempts.Unreadable(new NodeRecordFiles(_loop.Home))
                .Where(record => read.Unit is not { } picked
                    || units.Select(unit => unit.Unit).Append(picked).Any(unit => HeldAttempts.Picks(record, unit)))),
            // The closure graphs ended by the person's own done, or closed only in part, read from the same records.
            Ends = EndsAnswer(GraphEnds.Read(new NodeRecordFiles(_loop.Home))
                .Where(end => read.Unit is not { } picked || end.Unit == picked || units.Any(unit => unit.Unit == end.Unit))),
        };
    }

    /// <summary>The unit's milestone as <c>WORK_STATE</c> answers it (<see cref="WorkWires.WorkUnit"/>).</summary>
    public static object WorkUnit(IKnowledgeMilestone unit) => WorkWires.WorkUnit(unit);

    /// <summary>One inventory entry as <c>WORK_STATE</c> answers it (<see cref="WorkWires.WorkEntry"/>).</summary>
    public static object WorkEntry(InventoryEntry each) => WorkWires.WorkEntry(each);

    /// <summary>The attempts held for the person (<see cref="WorkWires.HeldAnswer"/>).</summary>
    public static object[] HeldAnswer(IEnumerable<HeldAttempt> held) => WorkWires.HeldAnswer(held);

    /// <summary>The node records that do not read (<see cref="WorkWires.UnreadableAnswer"/>).</summary>
    public static object[] UnreadableAnswer(IEnumerable<UnreadableRecord> unreadable) => WorkWires.UnreadableAnswer(unreadable);

    // Open the node records' folder: What needs you's press on a record that does not read, which the person mends
    // there. The module names the folder, never the page, and opens it through the window kit's launcher, through the start gate, as a
    // session's and a plugin's folder are.
    /// <summary>Open the folder the units' node records are kept in, in the system's file manager.</summary>
    /// <remarks>Nothing machine-local comes back: whether it opened. With no record kept there is no folder, which is refused and said.</remarks>
    [DriverRoute("WORK_RECORDS_FOLDER", PipeClass.Window)]
    [Owner("WORK_RECORDS_FOLDER", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> WorkRecordsFolderAsync(NoPayload _, CancellationToken cancellationToken)
    {
        var folder = NodeRecordFiles.FolderOf(_loop.Home);
        if (!Directory.Exists(folder)) throw new DriverException("no unit keeps a node record here now, so there is no folder to open.");
        if (_context.OpenFolder is not { } open) return new { Opened = false };
        try
        {
            await open.OpenAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new DriverException($"the node records' folder would not open: {error.Message}");
        }

        return new { Opened = true };
    }

    /// <summary>The closure graphs' ends (<see cref="WorkWires.EndsAnswer"/>).</summary>
    public static object[] EndsAnswer(IEnumerable<GraphEnd> ends) => WorkWires.EndsAnswer(ends);

    // The person's word on a plugin step's attempt held for them: the page's half of `daoris work
    // held <unit> <node> <attempt> <word>`. Both doors end in HeldAttempts.Word, which records it under the unit's node record's lock with
    // the lease asked inside it; the loop's runtime applies it under the node's declaration at its next pass, which the nudge wakes.
    /// <summary>
    /// Record the person's word (`resolution`: `happened`, `did-not` or `stop`; on a held close `record`, `resend` or `stop`;
    /// on a failure kept for them `again`) on `attempt` of `node` on `unit`, held for them.
    /// </summary>
    /// <remarks>
    /// A refusal is an ANSWER, as a discard's is: `{ done, refused, message }`, the message the terminal's sentence. Only the lease's
    /// holder records; an attempt no longer held, or already worded, is refused and said.
    /// </remarks>
    [DriverRoute("WORK_HELD", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORK_HELD", Unit = "unit")]
    [GroundRead("WORK_HELD", nameof(PipeReads.HeldAttempt))]
    [Publishes("WORK_HELD", StateScopes.Sessions)]
    private async Task<object?> WorkHeldAsync(WorkHeldPress press, CancellationToken cancellationToken)
    {
        var (unit, node, attempt, word) = (press.Unit, press.Node, press.Attempt, press.Resolution);
        var service = _loop.Service ?? throw NotReady();

        // *Ask for a fix…* on a failed verification publishes its ask; it moves no node and writes nothing on the record.
        if (word == HeldResolutions.AskFix) return await HeldFixPress.AnswerAsync(service, new NodeRecordFiles(_loop.Home), press, cancellationToken);

        var done = HeldAttempts.Word(new NodeRecordFiles(_loop.Home), unit, node, attempt, word);
        var worded = done.What == PerformedWhat.Worded;
        if (worded) _loop.Nudge();
        return new
        {
            Unit = unit, Node = node, Attempt = attempt, Resolution = word, Done = worded, Refused = worded ? null : done.Code,
            Message = HeldAttempts.Said(done, attempt, word),
        };
    }

    /// <summary>A session branch's row (<see cref="SweepWires.SweepRow"/>).</summary>
    public static object SweepRow(SweepItem item, SessionRecord? session = null) => SweepWires.SweepRow(item, session);

    /// <summary>A branch's row in bringing up to date (<see cref="SweepWires.RebaseRow"/>).</summary>
    public static object RebaseRow(RebaseItem item) => SweepWires.RebaseRow(item);

    /// <summary>The clean-up's list, one builder for the door and the slice (<see cref="SweepWires.SweepAnswer(SweepPlan, IReadOnlyList{SessionRecord})"/>).</summary>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records) => SweepWires.SweepAnswer(plan, records);

    /// <summary>The clean-up's list with the words <c>trees clean</c> prints for it (<see cref="SweepWires.SweepAnswer(SweepPlan, IReadOnlyList{SessionRecord}, TreesSaid)"/>).</summary>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records, TreesSaid said) => SweepWires.SweepAnswer(plan, records, said);

    /// <summary>A landed branch's row (<see cref="SweepWires.LandedRow"/>).</summary>
    public static object LandedRow(LandedItem item) => SweepWires.LandedRow(item);

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
    /// <paramref name="workspace"/>'s alone where it names one, matched without case, a row with no
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

    /// <summary>The trees sessions still hold, as the service's ledger says now: the driver's one reading.</summary>
    internal static Task<HashSet<string>> InUseAsync(ServiceClient service, CancellationToken cancellationToken) =>
        BranchTidying.HeldTreesAsync(service, cancellationToken);

    // where each repository's work is reviewed before it lands, the screens' half of
    // `daoris place review`, sent by `bridge/lines.ts`. Declared only: nothing here composes a step, gates or serves.

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
    [DriverRoute("SET_REVIEW", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_REVIEW", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_REVIEW", "reviews", "workspaceReviews")]
    [NotAlready("SET_REVIEW", "merges into the repository's review rule")]
    [Publishes("SET_REVIEW", Chosen)]
    private async Task<object?> SetReviewAsync(SetReview press, CancellationToken cancellationToken)
    {
        var edit = ReviewRules.EditOf(RuleEdit.Shape(press));
        // The twin's refusals come first, in its words, before the registry is asked anything.
        ReviewRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);

        // the look is the library's operation, which `daoris place review` acting alone calls too.
        ReviewLooked? looked = null;
        if (edit.Put is { Procedure: { } procedure })
        {
            var service = _loop.Service ?? throw NotReady();
            var registry = (await service.RegistryAsync(cancellationToken).ConfigureAwait(false))
                .Select(row => (Repository: row.Repository, Workspace: (string?)row.Workspace, Root: row.Root)).ToList();
            looked = ReviewLooks.Of(registry, edit, procedure);
        }

        _context.Change(config => ReviewRules.Apply(config, edit));
        // Only a put looked for anything, so only a put says what the look found.
        return DriverRoutes.State(_loop, reviewed: looked is null ? null : new { looked.Lacking, looked.Unchecked });
    }

    /// <summary>A review rule as the bridge carries it (<see cref="LinesWires.ReviewWire"/>).</summary>
    internal static object ReviewWire(ReviewRule rule) => LinesWires.ReviewWire(rule);

    // which other agent reads each repository's work before it lands,
    // the screens' half of `daoris place opinion`, sent by `bridge/lines.ts`. Declared only: nothing here chooses a reviewer,
    // starts a pass or holds a landing.

    /// <summary>
    /// One change to a second-opinion rule, as the page sends it: the twin's edit in the shared table's own shape
    /// (<c>repository</c> or <c>workspace</c>; <c>set</c> with any of <c>reviewers</c>, <c>on</c>, <c>required</c>,
    /// <c>verify</c>, <c>minutes</c> and <c>recheck</c>, <c>none</c>, or <c>clear</c>), judged by
    /// <see cref="OpinionRules.Apply"/> before anything is written. It reads nothing else, so it waits for nothing.
    /// </summary>
    [DriverRoute("SET_OPINION", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_OPINION", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_OPINION", "opinions", "workspaceOpinions")]
    [NotAlready("SET_OPINION", "merges into the repository's opinion rule")]
    [Publishes("SET_OPINION", Chosen)]
    private object? SetOpinion(SetOpinion press)
    {
        var edit = OpinionRules.EditOf(RuleEdit.Shape(press));
        // The twin's refusals come first, in its words, before anything is written.
        OpinionRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => OpinionRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>A second-opinion rule as the bridge carries it (<see cref="LinesWires.OpinionWire"/>).</summary>
    internal static object OpinionWire(OpinionRule rule, string working, ProviderRegistry adapters) => LinesWires.OpinionWire(rule, working, adapters);

    // what a helper an agent asks for does, and how work is committed,
    // the screens' half of `daoris place helpers` and `daoris place commits`, sent by `bridge/lines.ts`. Ask Daoris's `setting`
    // kind's `helpers` and `commits` doors are their third.

    /// <summary>
    /// One change to a helpers rule, as the page sends it: the twin's edit in the shared table's own shape (<c>repository</c> or
    /// <c>workspace</c>; <c>set</c> with <c>mode</c> and/or <c>agents</c>, or <c>clear</c>), judged by
    /// <see cref="HelperRules.Apply"/> before anything is written. It reads nothing else, so it waits for nothing.
    /// </summary>
    [DriverRoute("SET_HELPERS", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_HELPERS", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_HELPERS", "helpers", "workspaceHelpers")]
    [NotAlready("SET_HELPERS", "merges into the repository's helpers rule")]
    [Publishes("SET_HELPERS", Chosen)]
    private object? SetHelpers(SetHelpers press)
    {
        var edit = HelperRules.EditOf(RuleEdit.Shape(press));
        // The twin's refusals come first, in its words, before anything is written.
        HelperRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => HelperRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// One change to a hand-over rule, as the page sends it: the twin's edit in the shared table's own
    /// shape (<c>repository</c> or <c>workspace</c>; <c>set</c> with its <c>mode</c>, or <c>clear</c>), judged by
    /// <see cref="HandOverRules.Apply"/> before anything is written; the screens' half of `daoris settings handover`.
    /// </summary>
    [DriverRoute("SET_HAND_OVER", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_HAND_OVER", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_HAND_OVER", "handOver", "workspaceHandOver")]
    [NotAlready("SET_HAND_OVER", "merges into the repository's hand-over rule")]
    [Publishes("SET_HAND_OVER", Chosen)]
    private object? SetHandOver(SetHandOver press)
    {
        var edit = HandOverRules.EditOf(RuleEdit.Shape(press));
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
    [DriverRoute("SET_COMMITS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_COMMITS", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_COMMITS", "commits", "workspaceCommits")]
    [NotAlready("SET_COMMITS", "merges into the repository's commits rule")]
    [Publishes("SET_COMMITS", Chosen)]
    private object? SetCommits(SetCommits press)
    {
        var edit = CommitRules.EditOf(RuleEdit.Shape(press));
        CommitRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => CommitRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// One change to a session access setup, the setup page's half of <c>daoris place access</c> over the
    /// same keys: <c>repository</c>, <c>workspace</c> or <c>machine: true</c>, and <c>access</c>, <c>"full"</c>, the pauses it sets
    /// (a null value taking one back to the level above) or null to clear, judged by <see cref="AccessRules.Apply"/> before anything is
    /// written. Full access is the person's press alone: no proposal names it. It reads nothing else, so it
    /// waits for nothing; what each repository resolves to moves under the driver's key, so the lines slice pushes it again.
    /// </summary>
    [DriverRoute("SET_ACCESS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_ACCESS", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_ACCESS", "access", "workspaceAccess")]
    [NotAlready("SET_ACCESS", "merges into the repository's access rule")]
    [Publishes("SET_ACCESS", Chosen)]
    private object? SetAccess(SetAccess press)
    {
        var edit = AccessRules.EditOf(RuleEdit.Shape(press));
        // The refusals come first, before anything is written.
        var before = DriverConfig.Load(_loop.ConfigPath);
        AccessRules.Apply(before, edit);
        _context.Change(config => AccessRules.Apply(config, edit));
        // each driven session running here whose permission prompts this moves is told on its console that
        // its next turn runs under them; its run reads the file at that turn's end, as it does after the terminal's change.
        DrivenAccess.TellRunning(_loop.Processes, before, DriverConfig.Load(_loop.ConfigPath), _loop.Output.Append);
        return DriverRoutes.State(_loop);
    }

    /// <summary>
    /// One change to git access, the page's half of
    /// <c>daoris git access</c>: <c>repository</c>, <c>workspace</c> or <c>machine: true</c>, and <c>access</c> (the classes it
    /// sets, a null value taking one back to the level above), <c>oneValue</c> (Read and Write together), or neither to clear. A
    /// separate route from <c>SET_ACCESS</c>: a payload with no <c>access</c> clears the access setup on a host that does not know the
    /// field. Judged by <see cref="GitAccessActs.Changes"/> before anything is written; the window confirms a widening in its own
    /// <c>InlineConfirm</c>, and the route carries no flag for it.
    /// </summary>
    [DriverRoute("SET_GIT_ACCESS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_GIT_ACCESS", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_GIT_ACCESS", "gitAccess", "workspaceGitAccess")]
    [NotAlready("SET_GIT_ACCESS", "merges into the repository's git access")]
    [Publishes("SET_GIT_ACCESS", Chosen)]
    private object? SetGitAccess(SetGitAccess press)
    {
        var edit = GitAccessActs.EditOf(RuleEdit.Shape(press));
        GitAccessActs.Apply(_loop.Home, edit);
        return DriverRoutes.State(_loop);
    }

    /// <summary>The git access levels in the file <paramref name="config"/> was read from (<see cref="LinesWires.GitLevelsOf"/>).</summary>
    internal static GitAccessLevels GitLevelsOf(DriverConfig config) => LinesWires.GitLevelsOf(config);

    /// <summary>A level's git access as set (<see cref="LinesWires.GitClassesWire"/>).</summary>
    internal static object[] GitClassesWire(IReadOnlyDictionary<string, string> level) => LinesWires.GitClassesWire(level);

    /// <summary>A level's access setup as the bridge carries it (<see cref="LinesWires.AccessWire"/>).</summary>
    internal static object AccessWire(AccessSetting? setting) => LinesWires.AccessWire(setting);

    /// <summary>A helpers rule as the bridge carries it (<see cref="LinesWires.HelperWire"/>).</summary>
    internal static object HelperWire(HelperRule rule) => LinesWires.HelperWire(rule);

    /// <summary>A commit rule as the bridge carries it (<see cref="LinesWires.CommitWire"/>).</summary>
    internal static object CommitWire(CommitRule rule) => LinesWires.CommitWire(rule);
}
