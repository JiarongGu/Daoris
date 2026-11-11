namespace Daoris.Driver;

/// <summary>
/// The open routes the host's verbs send over the pipe: the reads, which go through the
/// instance where one answers and alone otherwise, and the two presses the census leaves open, <c>SYNC_NOW</c> and
/// <c>REGISTRY_REFRESH</c>. Held to the census route by route by <c>PipeCensusTests</c>, which also names each verb left off the pipe
/// with its reason (<see cref="Verbs"/>).
/// </summary>
public static class OpenRoutes
{
    private static InstanceRoute Open(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Open);

    /// <summary><c>trees clean</c>'s list: every session branch here with what it holds.</summary>
    public static InstanceRoute SweepPlan { get; } = Open("SWEEP_PLAN");

    /// <summary><c>trees sync</c>'s list: what bringing the repositories up to date would change.</summary>
    public static InstanceRoute TreesSyncPlan { get; } = Open("TREES_SYNC_PLAN");

    /// <summary><c>trees land &lt;session&gt; --plan</c>: where accepting the session would put its work.</summary>
    public static InstanceRoute Landing { get; } = Open("LANDING");

    /// <summary><c>trees land --entry &lt;id&gt; --plan</c>.</summary>
    public static InstanceRoute EntryLanding { get; } = Open("ENTRY_LANDING");

    /// <summary><c>trees land --ask &lt;id&gt; --plan</c>.</summary>
    public static InstanceRoute AskLanding { get; } = Open("ASK_LANDING");

    /// <summary><c>sessions</c>' list.</summary>
    public static InstanceRoute SessionGroups { get; } = Open("SESSION_GROUPS");

    /// <summary><c>sessions supervise &lt;id&gt;</c>'s read of the course decisions on a session's referred points.</summary>
    public static InstanceRoute Supervised { get; } = Open("SESSION_SUPERVISOR");

    /// <summary><c>sessions at-work</c>: who is at work on a session.</summary>
    public static InstanceRoute AtWork { get; } = Open("SESSION_AT_WORK");

    /// <summary><c>notices</c>: what came to the person, as the windows hold it.</summary>
    public static InstanceRoute Notifications { get; } = Open("NOTIFICATIONS");

    /// <summary><c>sync</c>: the tick's own pass for one circle.</summary>
    public static InstanceRoute SyncNow { get; } = Open("SYNC_NOW");

    /// <summary><c>register</c>: one repository followed from its line.</summary>
    public static InstanceRoute RegistryRefresh { get; } = Open("REGISTRY_REFRESH");

    /// <summary><c>space free --yes</c>'s follow: the disk space read each second, its <c>run</c> until it ends.</summary>
    public static InstanceRoute SystemSpace { get; } = Open("SYSTEM_SPACE");

    /// <summary><c>ask --abandon &lt;id&gt;</c> and <c>quest abandon &lt;id&gt;</c> without <c>--yes</c>: what an abandon would take and keep.</summary>
    public static InstanceRoute WorkPlan { get; } = Open("WORK_PLAN");

    /// <summary><c>history</c>'s reading, and a clear's list without <c>--yes</c>: what a clear would take and keep.</summary>
    public static InstanceRoute HistoryPlan { get; } = Open("HISTORY_PLAN");

    /// <summary><c>help list</c>: a page of Ask Daoris's conversations, a search's among them.</summary>
    public static InstanceRoute HelpConversations { get; } = Open("HELP_CONVERSATIONS");

    /// <summary><c>opinion show &lt;session|opinion&gt;</c>: where a work's second opinion stands, its findings and their answers.</summary>
    public static InstanceRoute OpinionGate { get; } = Open("OPINION_GATE");

    /// <summary><c>place &lt;repository&gt;… --plan</c>: a quick set-up's plan, read and never written.</summary>
    public static InstanceRoute PlacePlan { get; } = Open("PLACE_PLAN");

    /// <summary>
    /// Each verb of the open routes' scope, the host's verbs with a window twin, in the verb's words:
    /// the open route it reads where the instance answers, or null with the reason it stays off the pipe. A person's press is
    /// <see cref="PersonPresses"/>'; <c>PipeCensusTests</c> holds which verbs stay off, so one that leaves the list or joins it is said.
    /// </summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("trees clean", SweepPlan, "the list; `--yes` is the person's press and acts alone until its client path is built"),
        ("trees sync", TreesSyncPlan, "the list; `--yes` is the person's press and acts alone until its client path is built"),
        ("trees land <session> --plan", Landing, "the plan; the press is the person's and acts alone until its client path is built"),
        ("trees land --entry <id> --plan", EntryLanding, "the plan; the press is the person's and acts alone until its client path is built"),
        ("trees land --ask <id> --plan", AskLanding, "the plan; the press is the person's and acts alone until its client path is built"),
        ("sessions", SessionGroups, "the list"),
        ("sessions at-work <session>", AtWork, "the read, asked again once a second while it is pending"),
        ("ask at-work <ask>", AtWork, "the read keyed by the ask, asked again once a second while the slices are pending; an ask the window does not hold is refused at once"),
        ("notices", Notifications, "the read; alone, the notices' record, the live groups said not read"),
        ("sync", SyncNow, "an open press (D156 §3: sync)"),
        ("register", RegistryRefresh, "an open press (D156 §3: refresh)"),
        ("space free --yes", SystemSpace, "the run its press started, read until it ends (KEEP1e)"),
        ("ask --abandon <id>", WorkPlan, "the list; with `--yes` the person's press (WORK_ABANDON)"),
        ("quest abandon <id>", WorkPlan, "the list; with `--yes` the person's press (WORK_ABANDON)"),
        ("history", HistoryPlan, "the reading; `--json` prints the route's own answer"),
        ("history clear --workspace <name>", HistoryPlan, "the list; with `--yes` the person's press (HISTORY_CLEAR)"),
        ("quest clear <id>", HistoryPlan, "the list; with `--yes` the person's press (HISTORY_CLEAR)"),
        ("ask --clear <id>", HistoryPlan, "the list; with `--yes` the person's press (HISTORY_CLEAR)"),
        ("help list", HelpConversations, "the list; `--json` prints the route's own answer"),
        ("help delete <id>", null, "alone: the first ask reads the conversation's name here, which no route answers; with `--yes` the person's press (SESSION_DELETE)"),
        ("opinion show <session|opinion>", OpinionGate, "the gate's reading"),
        ("place --workspace <name> --adoption", null, "alone: no route writes a workspace's paced plan; PLACE_APPLY writes one when an adoption covers more repositories than it asks at once"),
        ("place <repository>… --plan", PlacePlan, "the plan; with `--apply` the person's press (PLACE_APPLY)"),
        ("ask channel <id>", null,
            "alone: the read moves the person's own mark (CHANNEL_MARK, the person's), a door of the service, which writes it once whoever "
            + "asks; with words the person's press (CHANNEL_POST)"),
        // The open reads left alone beside an instance, with the same words: a verb whose words name this machine's
        // folders or quote its trees' files stays alone, since no answer carries a machine path and what
        // may travel over the local pipe is a decision first; the rest until their route answers them as `said`.
        ("trace", null, "alone: its words are built from the sources the host reads itself; TRACE answers the page's shape only"),
        ("git branches", null, "alone: its words are built from the registry's checkouts the host reads itself; GIT_BRANCHES answers the page's shape only"),
        ("git review <session>", null,
            "alone: its `--time` counts and times the git this process starts itself (GITAPP1a-i), which a route's answer cannot carry; SESSION_DIFF answers the page's shape only"),
        ("workflow", null,
            "alone: WORKFLOW_RUN answers the page's shape only, and `workflow keep` writes its run; `workflow skip`, `stop`, `accept` and the other node words are the person's press (WORKFLOW_NODE), and `workflow fix` is WORK_HELD's"),
        ("plugins show <id>", null, "alone: its words name the plugin's folders on this machine, which PLUGIN's page answers apart, and no said line carries"),
        ("plugins activity <id>", null, "alone: a read of the machine log this process reads itself; with no log its words name the log's folder"),
        ("plugins try <folder|id>", null, "alone: a trial starts the plugin as this terminal's own child, and its words name the folder it tried"),
        ("plugins install <file>", null, "alone: PLUGIN_INSTALL installs one of the install's own offers; no route reads a package file, and its words name the folder"),
        ("plugins new <id>", null, "alone: it writes a plugin's folder where the terminal stands, never under the home"),
        ("logs", null, "alone: a read of the machine log this process reads itself, whose words name its folder"),
        ("sessions partner <id> --list", null, "alone: a read of the pieces' box under the home, which says the same with or without the instance"),
        // What stays off the pipe by design: a conversation the terminal runs, a loop, the updater's request, and the
        // service's own doors, which the service writes once whoever asks.
        ("drive", null, "alone: it is a loop, the home's lease's holder, refused where the desktop holds the home unless `--share` (DRV8a)"),
        ("update", null, "alone: a request file under the home the desktop's updater watches (D139); its window twin is the update banner, no route"),
        ("quest check", null, "alone: the service's door, its one writer, as the page's own HTTP call is (§7.1)"),
        ("ask \"…\"", null, "alone: the service publishes an ask, its one writer, as the page's own HTTP call does (§7.1)"),
        ("sync status", null, "alone: a read of the service's own standing (`/api/sync`), as the page's read is"),
    ];
}

/// <summary>
/// The person's presses the host's verbs send over the pipe: each goes to
/// the instance that answers, which shows it in its window naming this terminal, and runs on the person's Confirm alone; the verb says
/// it waits, then says the confirmed press's answer in its own words, or the person's refusal or the two minutes' end, exit 1. Where
/// no instance runs, the verb acts alone. Held to the census route by route by <c>PipeCensusTests</c>.
/// </summary>
/// <remarks>
/// <c>sessions archive</c> and <c>unarchive</c> prove the door; the other sessions verbs, the host's presses and the CLI's
/// edits go the same way, each with its words from its route's answer (<c>said</c>), held to the alone path's.
/// </remarks>
public static class PersonPresses
{
    /// <summary><c>sessions archive &lt;id&gt;…</c> and <c>unarchive &lt;id&gt;…</c>: this machine's marks, the person's close.</summary>
    public static InstanceRoute SessionArchive { get; } = new(TreesRoutes.DriverModule, "SESSION_ARCHIVE", RouteClass.Person);

    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    /// <summary><c>sessions stop &lt;id&gt;</c>: a session stopped.</summary>
    public static InstanceRoute SessionStop { get; } = Person("STOP_SESSION");

    /// <summary><c>sessions stop &lt;id&gt; --turn</c>: the keeping stop, a session's turn stopped so what waits goes next.</summary>
    public static InstanceRoute SessionStopTurn { get; } = Person("CANCEL_TURN");

    /// <summary><c>sessions finish</c> and <c>decline</c>: a parked session's resolution.</summary>
    public static InstanceRoute SessionResolve { get; } = Person("RESOLVE_SESSION");

    /// <summary><c>sessions say</c>: the person's words to a session.</summary>
    public static InstanceRoute SessionSay { get; } = Person("SESSION_INPUT");

    /// <summary><c>sessions take-back</c> and <c>reword</c>: a word waiting on a session, taken back or reworded.</summary>
    public static InstanceRoute SessionQueueEdit { get; } = Person("SESSION_QUEUE_EDIT");

    /// <summary><c>sessions delete &lt;id&gt;</c>: a conversation's record deleted.</summary>
    public static InstanceRoute SessionDelete { get; } = Person("SESSION_DELETE");

    /// <summary><c>sessions start-from &lt;id&gt;</c>: a conversation started with the words a session could not go on with.</summary>
    public static InstanceRoute SessionStartFrom { get; } = Person("SESSION_START_FROM");

    /// <summary><c>sessions hand-over &lt;id&gt;</c>: a session handed to another agent, or, naming none, gone on in a new session.</summary>
    public static InstanceRoute SessionHandOver { get; } = Person("SESSION_HAND_OVER");

    /// <summary><c>sessions helper &lt;id&gt; "…"</c>: the person's ask of a helper.</summary>
    public static InstanceRoute SessionHelperAsk { get; } = Person("SESSION_HELPER_ASK");

    /// <summary><c>sessions helper --approve &lt;ask&gt;</c>: the yes to an agent's ask of a helper.</summary>
    public static InstanceRoute SessionHelperApprove { get; } = Person("SESSION_HELPER_APPROVE");

    /// <summary><c>sessions helper --decline &lt;ask&gt;</c>: the no to an agent's ask of a helper.</summary>
    public static InstanceRoute SessionHelperDecline { get; } = Person("SESSION_HELPER_DECLINE");

    /// <summary><c>sessions pass &lt;ask&gt;</c>: a helper's answer kept for the person, passed to the session that asked.</summary>
    public static InstanceRoute SessionHelperPass { get; } = Person("SESSION_HELPER_PASS");

    /// <summary><c>sessions partner &lt;id&gt; "…" --agent &lt;name&gt;</c>: the person's ask of a piece.</summary>
    public static InstanceRoute SessionPartnerAsk { get; } = Person("SESSION_PARTNER_ASK");

    /// <summary><c>sessions hand-over &lt;id&gt; --approve</c>: the yes to an agent's ask to hand its session over.</summary>
    public static InstanceRoute SessionHandOverApprove { get; } = Person("SESSION_HAND_OVER_APPROVE");

    /// <summary><c>sessions hand-over &lt;id&gt; --decline</c>: the no to an agent's ask to hand its session over.</summary>
    public static InstanceRoute SessionHandOverDecline { get; } = Person("SESSION_HAND_OVER_DECLINE");

    /// <summary>
    /// <c>space free --yes</c>: a run removing what the person's list showed, their delete. The list is
    /// the same route's <c>plan</c> form, open (a <c>PipeRule</c> in the census), which the verb reads alone since a list runs anywhere.
    /// </summary>
    public static InstanceRoute SpaceFree { get; } = Person("SYSTEM_SPACE_FREE");

    /// <summary><c>git branch|tag &lt;repository&gt; &lt;name&gt; --yes</c>: a branch or a lightweight tag created, the person's yes.</summary>
    public static InstanceRoute GitRefCreate { get; } = Person("GIT_REF_CREATE");

    /// <summary><c>git branch|tag &lt;repository&gt; &lt;name&gt; --delete --yes</c>: a branch or a lightweight tag deleted, the person's yes.</summary>
    public static InstanceRoute GitRefDelete { get; } = Person("GIT_REF_DELETE");

    /// <summary><c>git branch|tag &lt;repository&gt; &lt;name&gt; --rename &lt;new-name&gt; --yes</c>: a branch or a lightweight tag renamed, the person's yes.</summary>
    public static InstanceRoute GitRefRename { get; } = Person("GIT_REF_RENAME");

    /// <summary><c>git fetch &lt;repository&gt; --yes</c>: origin's branches fetched into <c>refs/remotes/origin/*</c> alone, the person's yes.</summary>
    public static InstanceRoute GitFetch { get; } = Person("GIT_FETCH");

    /// <summary><c>git stash drop &lt;repository&gt; &lt;commit&gt; --yes</c>: one stash of Daoris's own Save dropped by its commit id, the person's yes.</summary>
    public static InstanceRoute GitStashDrop { get; } = Person("GIT_STASH_DROP");

    /// <summary><c>git push &lt;repository&gt; branch|tag &lt;name&gt; --yes</c>: one ref pushed to origin under its grant, the person's yes.</summary>
    public static InstanceRoute GitPush { get; } = Person("GIT_PUSH");

    /// <summary>
    /// <c>git pick|revert|rebase &lt;repository&gt; &lt;branch&gt; … --yes</c>: a branch's history edited from objects, the person's
    /// yes, carrying the ids its plan pinned. The plan is the same route's <c>plan</c> form, open (a <c>PipeRule</c> in the census), which the
    /// verb reads alone.
    /// </summary>
    public static InstanceRoute GitHistoryEdit { get; } = Person("GIT_HISTORY_EDIT");

    /// <summary>
    /// <c>git identity add-ssh … --import</c>, <c>test … --trust-host</c>, <c>read-only … off</c> and <c>remove</c>:
    /// a git identity's widening, as Settings → Git presses it.
    /// </summary>
    public static InstanceRoute GitIdentity { get; } = Person("GIT_IDENTITY");

    /// <summary><c>git use … own</c>: the person's own git sign-ins chosen for a remote, a workspace's host or this machine's.</summary>
    public static InstanceRoute GitUse { get; } = Person("GIT_USE");

    /// <summary><c>git access …</c> where it loosens a class: a level's git access widened.</summary>
    public static InstanceRoute SetGitAccess { get; } = Person("SET_GIT_ACCESS");
    /// <summary><c>git answer &lt;ask&gt; allow|refuse</c>: a git go-ahead answered, as its row in What needs you answers it.</summary>
    public static InstanceRoute GitAnswer { get; } = Person("GIT_ANSWER");

    /// <summary><c>browser favorite add|remove &lt;address&gt;</c>: a page kept on, or dropped from, Daoris's browser's bookmarks bar.</summary>
    public static InstanceRoute BrowserFavorite { get; } = Person("BROWSER_FAVORITE");

    /// <summary><c>browser use|links|extensions &lt;value&gt;</c>: which browser, where links open, and other software's extensions.</summary>
    public static InstanceRoute BrowserSetting { get; } = Person("BROWSER_SETTING");

    /// <summary><c>ask --pause &lt;id&gt;</c> and <c>quest pause &lt;id&gt;</c>: a work paused on this machine, what of it runs stopped.</summary>
    public static InstanceRoute WorkPause { get; } = Person("WORK_PAUSE");

    /// <summary><c>ask --resume &lt;id&gt;</c> and <c>quest resume &lt;id&gt;</c>: a pause taken away, each stop it made released.</summary>
    public static InstanceRoute WorkResume { get; } = Person("WORK_RESUME");

    /// <summary><c>ask --abandon &lt;id&gt; --reason "…" --yes</c> and <c>quest abandon …</c>: what the list holds now abandoned.</summary>
    public static InstanceRoute WorkAbandon { get; } = Person("WORK_ABANDON");

    /// <summary><c>history clear --workspace &lt;name&gt; --yes</c>, <c>quest clear &lt;id&gt; --yes</c> and <c>ask --clear &lt;id&gt; --yes</c>: a clear.</summary>
    public static InstanceRoute HistoryClear { get; } = Person("HISTORY_CLEAR");

    /// <summary><c>help rename &lt;id&gt; "…"</c> and <c>--clear</c>: an Ask Daoris conversation's name, or its first question again.</summary>
    public static InstanceRoute HelpRename { get; } = Person("HELP_RENAME");

    /// <summary><c>help pin &lt;id&gt;</c> and <c>unpin</c>: an Ask Daoris conversation kept at the top of its history, or not.</summary>
    public static InstanceRoute HelpPin { get; } = Person("HELP_PIN");

    /// <summary><c>help focus &lt;id&gt; &lt;place&gt;</c>: what an Ask Daoris conversation is about, taken with its next words.</summary>
    public static InstanceRoute HelpFocus { get; } = Person("HELP_FOCUS");

    /// <summary><c>opinion ask &lt;session&gt;</c>: a second opinion asked, spending an account at the person's choice.</summary>
    public static InstanceRoute AskOpinion { get; } = Person("ASK_OPINION");

    /// <summary><c>opinion stop &lt;opinion&gt;</c>: a reviewer's reading stopped.</summary>
    public static InstanceRoute StopOpinion { get; } = Person("STOP_OPINION");

    /// <summary><c>opinion anyway &lt;session&gt;</c>: going on without a settled opinion, the person's answer kept.</summary>
    public static InstanceRoute OpinionAnyway { get; } = Person("OPINION_ANYWAY");

    /// <summary><c>opinion myself &lt;session&gt;</c>: the person's own reading in the opinion's place.</summary>
    public static InstanceRoute OpinionMyself { get; } = Person("OPINION_MYSELF");

    /// <summary><c>ask channel &lt;id&gt; "…"</c>: the person's words on an ask's channel, which every session on the ask reads.</summary>
    public static InstanceRoute ChannelPost { get; } = Person("CHANNEL_POST");

    /// <summary>
    /// <c>ask channel join &lt;ask&gt;</c> or <c>--line &lt;session&gt;</c>: an agent outside Daoris admitted to a channel as a
    /// member of its own, on the person's Confirm; refused before any card from a process in an agents' job.
    /// </summary>
    public static InstanceRoute ChannelJoin { get; } = Person("CHANNEL_JOIN");

    /// <summary>
    /// <c>place &lt;repository&gt;… --preset &lt;id&gt; … --apply [--accept …]</c>: a place's plan applied, the configuration's changes and the
    /// acceptances chosen, and each adoption quest. The plan is the same ask's <c>PLACE_PLAN</c>, open, which the verb reads alone.
    /// </summary>
    public static InstanceRoute PlaceApply { get; } = Person("PLACE_APPLY");

    /// <summary>
    /// <c>plugins approve &lt;id&gt; &lt;server&gt;</c> and <c>withdraw</c>: the tools a plugin's server offers Ask Daoris approved,
    /// its reads only or the writes named, or the approval withdrawn.
    /// </summary>
    public static InstanceRoute PluginAction { get; } = Person("PLUGIN_ACTION");

    /// <summary>
    /// <c>workflow skip &lt;run&gt; &lt;node&gt; "…"</c>, <c>workflow stop &lt;run&gt; &lt;node&gt;</c> and <c>workflow changes &lt;run&gt; &lt;node&gt; "…"</c>:
    /// the person's word on a node of a run bound to a graph version, kept on the run's record for Daoris's next look.
    /// </summary>
    public static InstanceRoute WorkflowNode { get; } = Person("WORKFLOW_NODE");

    /// <summary>
    /// <c>workflow fix &lt;run&gt; [&lt;node&gt;]</c>: the window's <i>Ask for a fix…</i> on a failed verification, the person's
    /// word <c>ask-fix</c> on its held attempt, which publishes an ask and writes nothing on the record.
    /// </summary>
    public static InstanceRoute WorkHeld { get; } = Person("WORK_HELD");

    /// <summary><c>quest done &lt;id&gt; [--note "…"]</c>: a quest closed as the person's own done.</summary>
    public static InstanceRoute QuestDone { get; } = Person("QUEST_DONE");

    /// <summary><c>quest delete &lt;id&gt;</c>: a quest made by mistake deleted.</summary>
    public static InstanceRoute QuestDelete { get; } = Person("QUEST_DELETE");

    /// <summary><c>ask --delete &lt;id&gt;</c>: an ask made by mistake deleted with every quest it became.</summary>
    public static InstanceRoute AskDelete { get; } = Person("ASK_DELETE");

    /// <summary><c>quest accept &lt;id&gt;</c>: a done's departure from what the person required, accepted.</summary>
    public static InstanceRoute QuestAccept { get; } = Person("QUEST_ACCEPT");

    /// <summary><c>quest reopen &lt;id&gt; [--note "…"]</c>: a sealed submission reopened.</summary>
    public static InstanceRoute QuestReopen { get; } = Person("QUEST_REOPEN");

    /// <summary><c>quest go-on &lt;id&gt;</c>: the step a source's reopen held gone on with the new work.</summary>
    public static InstanceRoute QuestGoOn { get; } = Person("QUEST_GO_ON");

    /// <summary><c>quest review &lt;id&gt; …</c>: the person's verdict on a review, or the chain's choice at its gate.</summary>
    public static InstanceRoute QuestReview { get; } = Person("QUEST_REVIEW");

    /// <summary><c>quest workflow &lt;id&gt; …</c>: a quest's own kind and workflow, or none.</summary>
    public static InstanceRoute QuestWorkflow { get; } = Person("QUEST_WORKFLOW");

    /// <summary><c>ask workflow &lt;id&gt; …</c> and <c>ask --set-workflow</c>: an ask's kind and workflow, or none.</summary>
    public static InstanceRoute AskWorkflow { get; } = Person("ASK_WORKFLOW");

    /// <summary><c>ask review &lt;id&gt; …</c> and <c>ask --set-review</c>: an ask's review choice.</summary>
    public static InstanceRoute AskReview { get; } = Person("ASK_REVIEW");

    /// <summary><c>repository retire [&lt;name&gt;]</c>: a repository taken off the registry.</summary>
    public static InstanceRoute RegistryRetire { get; } = Person("REGISTRY_RETIRE");

    /// <summary><c>repository wire &lt;repository&gt; --workspace &lt;name&gt;</c>: a repository wired to another workspace.</summary>
    public static InstanceRoute RegistryRewire { get; } = Person("REGISTRY_REWIRE");

    /// <summary><c>repository excludes &lt;folder&gt; --add|--remove &lt;glob&gt;</c>: a folder department's excludes rewritten.</summary>
    public static InstanceRoute RegistryExcludes { get; } = Person("REGISTRY_EXCLUDES");

    /// <summary><c>repository import [&lt;folder&gt;]</c>: every repository a folder's subdirectories propose, registered.</summary>
    public static InstanceRoute RegistryImport { get; } = Person("REGISTRY_IMPORT");

    /// <summary>Each person's press a verb sends over the pipe, in the verb's words, with its route.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute Route)> Presses { get; } =
    [
        ("space free --yes", SpaceFree),
        ("git branch <repository> <name> [<commit>] --yes", GitRefCreate),
        ("git tag <repository> <name> [<commit>] --yes", GitRefCreate),
        ("git branch <repository> <name> [<commit>] --delete --yes", GitRefDelete),
        ("git tag <repository> <name> [<commit>] --delete --yes", GitRefDelete),
        ("git branch <repository> <name> [<commit>] --rename <new-name> --yes", GitRefRename),
        ("git tag <repository> <name> [<commit>] --rename <new-name> --yes", GitRefRename),
        ("git fetch <repository> --yes", GitFetch),
        ("git stash drop <repository> <commit> --yes", GitStashDrop),
        ("git push <repository> branch <name> [<commit>] --yes", GitPush),
        ("git push <repository> tag <name> [<commit>] --yes", GitPush),
        ("git pick <repository> <branch> <commit|A..B> --yes", GitHistoryEdit),
        ("git revert <repository> <branch> <commit|A..B> --yes", GitHistoryEdit),
        ("git rebase <repository> <branch> <onto> --yes", GitHistoryEdit),
        ("ask --pause <id>", WorkPause),
        ("quest pause <id>", WorkPause),
        ("ask --resume <id>", WorkResume),
        ("quest resume <id>", WorkResume),
        ("ask --abandon <id> --reason \"…\" --yes", WorkAbandon),
        ("quest abandon <id> --reason \"…\" --yes", WorkAbandon),
        ("history clear --workspace <name> --yes", HistoryClear),
        ("quest clear <id> [--failed] --yes", HistoryClear),
        ("ask --clear <id> --yes", HistoryClear),
        ("help rename <id> \"…\"|--clear", HelpRename),
        ("help pin <id>", HelpPin),
        ("help unpin <id>", HelpPin),
        ("help focus <id> machine|<workspace>|<workspace>/<repository>", HelpFocus),
        ("help delete <id> --yes", SessionDelete),
        ("opinion ask <session>", AskOpinion),
        ("opinion stop <opinion>", StopOpinion),
        ("opinion anyway <session>", OpinionAnyway),
        ("opinion myself <session>", OpinionMyself),
        ("ask channel <id> \"…\"", ChannelPost),
        ("ask channel join <ask> | --line <session>", ChannelJoin),
        ("place <repository>… --preset <id> … --apply [--accept <repository>:<purpose>,…]", PlaceApply),
        ("plugins approve <id> <server>", PluginAction),
        ("plugins withdraw <id> <server>", PluginAction),
        ("workflow skip <run> <node> \"…\"", WorkflowNode),
        ("workflow stop <run> <node> [\"…\"]", WorkflowNode),
        ("workflow changes <run> <node> \"…\"", WorkflowNode),
        ("workflow accept <run> <node> [\"…\"]", WorkflowNode),
        ("workflow done <run> <node> [\"…\"]", WorkflowNode),
        ("workflow approve <run> <node> [\"…\"]", WorkflowNode),
        ("workflow reject <run> <node> [\"…\"]", WorkflowNode),
        ("workflow fix <run> [<node>]", WorkHeld),
        ("quest done <id> [--note \"…\"]", QuestDone),
        ("quest delete <id>", QuestDelete),
        ("ask --delete <id>", AskDelete),
        ("quest accept <id>", QuestAccept),
        ("quest reopen <id> [--note \"…\"]", QuestReopen),
        ("quest go-on <id>", QuestGoOn),
        ("quest review <id> reviewed|not-yet|skip|off|on|<environment> [\"…\"]", QuestReview),
        ("quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]", QuestWorkflow),
        ("ask workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]", AskWorkflow),
        ("ask review <id> on|<environment>|off [\"…\"]", AskReview),
        ("repository retire [<name>]", RegistryRetire),
        ("repository wire <repository> --workspace <name>", RegistryRewire),
        ("repository excludes <folder> [--add <glob>]… [--remove <glob>]…", RegistryExcludes),
        ("repository import [<folder>] [--workspace <name>]", RegistryImport),
        ("browser favorite add <address> [--title \"…\"]", BrowserFavorite),
        ("browser favorite remove <address>", BrowserFavorite),
        ("browser use daoris|edge", BrowserSetting),
        ("browser links system|daoris", BrowserSetting),
        ("browser extensions offer|refuse", BrowserSetting),
        ("sessions archive <id>…", SessionArchive),
        ("sessions unarchive <id>…", SessionArchive),
        ("sessions stop <id>", SessionStop),
        ("sessions stop <id> --turn", SessionStopTurn),
        ("sessions finish <id>", SessionResolve),
        ("sessions decline <id>", SessionResolve),
        ("sessions say <id> \"…\"", SessionSay),
        ("sessions take-back <id> <word>", SessionQueueEdit),
        ("sessions reword <id> <word> \"…\"", SessionQueueEdit),
        ("sessions delete <id>", SessionDelete),
        ("sessions start-from <id>", SessionStartFrom),
        ("sessions hand-over <id>", SessionHandOver),
        ("sessions hand-over <id> --approve", SessionHandOverApprove),
        ("sessions hand-over <id> --decline", SessionHandOverDecline),
        ("sessions helper <id> \"…\"", SessionHelperAsk),
        ("sessions helper --approve <ask>", SessionHelperApprove),
        ("sessions helper --decline <ask>", SessionHelperDecline),
        ("sessions pass <ask>", SessionHelperPass),
        ("sessions partner <id> \"…\" --agent <name>", SessionPartnerAsk),
        ("sessions partner <piece> --stop", SessionStop),
    ];
}

/// <summary>
/// The marks of the window's routes the <c>trees</c> verbs' presses are twins of: the names the shell's
/// modules declare each route by, which <see cref="TreesRoutes"/> sends and which a terminal that has acted tells (<see cref="PressesTold"/>,
/// whose constants are these, and the modules hold each to a route that declares what it changes). The text is the wire's: a mark changes
/// nowhere but with the modules'.
/// </summary>
public static class TreesMarks
{
    /// <summary><c>trees land &lt;session&gt;</c>: the review's Accept.</summary>
    public const string Land = "LAND_SESSION_TREE";

    /// <summary><c>trees remove &lt;path&gt;</c>: a tree by its path.</summary>
    public const string DiscardTree = "DISCARD_SESSION_TREE";

    /// <summary><c>trees remove &lt;session|branch&gt;</c>: a session's branch, its tree with it.</summary>
    public const string DiscardBranch = "DISCARD_SESSION_BRANCH";

    /// <summary><c>trees remove &lt;landing branch&gt; --why "…"</c>: a landing's branch the line will never hold, withdrawn.</summary>
    public const string WithdrawLanding = "WITHDRAW_LANDING_BRANCH";

    /// <summary><c>trees clean --yes</c>: the clean-up's press.</summary>
    public const string Sweep = "SWEEP";

    /// <summary><c>trees sync --yes</c>: bringing the repositories up to date.</summary>
    public const string TreesSync = "TREES_SYNC";

    /// <summary><c>trees land --entry &lt;id&gt;</c>: a line of work's one Accept.</summary>
    public const string LandEntry = "LAND_ENTRY";

    /// <summary><c>trees land --ask &lt;id&gt;</c>: an ask's one Accept.</summary>
    public const string LandAsk = "LAND_ASK";
}

/// <summary>
/// The routes the <c>trees</c> verbs' presses are twins of: each verb acts alone until its client path is built.
/// </summary>
/// <remarks>
/// 🔴 Every one is the person's (a landing is the person's yes, a discard and a sweep their deletes, and
/// <c>TREES_SYNC</c> moves their lines and branches). The window's confirmation of a person's press, which
/// <see cref="PersonPresses"/>' verbs use, applies to them too: a trees verb that becomes a client sends its route through
/// <see cref="InstanceDoor.PressAsync"/> as they do, and the three land presses send <c>follow: true</c>, so the lines they
/// move are followed as the alone path follows them. Its words come from the route's answer, held to the alone path's case for case:
/// each route answers them as <c>said</c>, which <see cref="TreesSaid.Words"/> prints and <see cref="TreesWords"/> builds for
/// the alone path and the route alike.
/// </remarks>
public static class TreesRoutes
{
    /// <summary>The driver's module, as the desktop's <c>DriverModule.Name</c> spells it (the settings' <see cref="InstanceRoute.DriverModule"/>).</summary>
    public const string DriverModule = InstanceRoute.DriverModule;

    /// <summary><c>trees land &lt;session&gt;</c>: the review's Accept.</summary>
    public static InstanceRoute Land { get; } = new(DriverModule, TreesMarks.Land, RouteClass.Person);

    /// <summary><c>trees land --entry &lt;id&gt;</c>: a line of work's one Accept.</summary>
    public static InstanceRoute LandEntry { get; } = new(DriverModule, TreesMarks.LandEntry, RouteClass.Person);

    /// <summary><c>trees land --ask &lt;id&gt;</c>: an ask's one Accept.</summary>
    public static InstanceRoute LandAsk { get; } = new(DriverModule, TreesMarks.LandAsk, RouteClass.Person);

    /// <summary><c>trees remove &lt;path&gt;</c>: a tree discarded.</summary>
    public static InstanceRoute DiscardTree { get; } = new(DriverModule, TreesMarks.DiscardTree, RouteClass.Person);

    /// <summary><c>trees remove &lt;session|branch&gt;</c>: a failed or superseded attempt's branch discarded.</summary>
    public static InstanceRoute DiscardBranch { get; } = new(DriverModule, TreesMarks.DiscardBranch, RouteClass.Person);

    /// <summary><c>trees remove &lt;landing branch&gt; --why "…"</c>: a landing's branch the line will never hold, withdrawn.</summary>
    public static InstanceRoute Withdraw { get; } = new(DriverModule, TreesMarks.WithdrawLanding, RouteClass.Person);

    /// <summary><c>trees clean --yes</c>: the clean-up's press.</summary>
    public static InstanceRoute Sweep { get; } = new(DriverModule, TreesMarks.Sweep, RouteClass.Person);

    /// <summary><c>trees sync --yes</c>: bringing the repositories up to date.</summary>
    public static InstanceRoute Sync { get; } = new(DriverModule, TreesMarks.TreesSync, RouteClass.Person);

    /// <summary>Each press of the <c>trees</c> verbs, in the verb's words, with its route.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute Route)> Presses { get; } =
    [
        ("trees land <session>", Land),
        ("trees land --entry <id>", LandEntry),
        ("trees land --ask <id>", LandAsk),
        ("trees remove <path>", DiscardTree),
        ("trees remove <session|branch>", DiscardBranch),
        ("trees remove <landing branch> --why", Withdraw),
        ("trees clean --yes", Sweep),
        ("trees sync --yes", Sync),
    ];
}
