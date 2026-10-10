using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris reaches every door (HELP9, D110): each <c>daoris driver</c> verb and each control a Settings domain
/// holds is a door a kind of proposal takes, a door owed with what it waits on, or exempt with its reason. A verb
/// or a control on none of the three fails here, so the next door cannot be built without being answered for.
/// </summary>
/// <remarks>
/// <para><b>Read as text, the way a reviewer would</b> (<c>.claude/knowledge/twins.md</c>): the CLI, the page and the
/// driver share no code. The verbs are the CLI's command table (<c>src/Daoris.Cli/src/cli/driver.ts</c>). The
/// controls are each Settings domain's component and every file it imports, the page's bridge hooks and its own service
/// calls (<c>queries.ts</c>) among them that change something, and each action such a hook's payload names. Since ENTRY1a
/// every other start the page offers is read too, from each of its source files (<see cref="Starts"/>).</para>
///
/// <para><b>Four answers.</b> A door names the kind and its door, held against <see cref="IHelpProposalKind.Doors"/>.
/// An exemption is a principle: one of D89's own presses (a sign-in, a key, a discard), something that changes
/// nothing, a viewer's own look, what an agent may do, or the person's act on the application itself (D139). A door owed is a door Ask Daoris should have, and says
/// what it waits on. A quest's or a session's own act is its own conversation, which Ask Daoris reaches by a go to the
/// view it names (D161's ENTRY1 note).</para>
/// </remarks>
public sealed partial class HelpCoverageTests
{
    private abstract record Answer;

    /// <summary>A door a kind takes: Ask Daoris proposes it, and the person applies it.</summary>
    private sealed record Door(string Kind, string Name) : Answer;

    /// <summary>Not Ask Daoris's to propose, for the reason given.</summary>
    private sealed record Exempt(string Reason) : Answer;

    /// <summary>A door Ask Daoris should have, not built yet, and what it waits on.</summary>
    private sealed record Owed(string Reason) : Answer;

    /// <summary>
    /// A quest's or a session's own act, done in its own place and conversation, which Ask Daoris reaches by a go to the view
    /// named and does not take over (D161's ENTRY1 note, ENTRY1a): the view is one of <see cref="HelpPlaces.Views"/>.
    /// </summary>
    private sealed record Conversation(string Place, string Reason) : Answer;

    private const string SignIn = "a sign-in is the person's own press, where it already is (D89).";

    private const string Key = "a key is the person's own, typed where it already is (D89).";

    /// <summary>Settings → Tools' *System* and *Use this version* (TOOLS7): the `tool` kind's `use` door, TOOLS8's.</summary>
    private const string ToolUseOwed =
        "it narrows what runs to the machine's own, or to a version the lists name, so Ask Daoris should propose it, judged "
        + "against the lists as last fetched; that is the `tool` kind's `use` door, which TOOLS8 builds (D121 §4.3), and "
        + "until then `daoris tool use` is the terminal's door.";

    /// <summary>The default cool-off (TOOL4g, D125 §6): the `setting` kind's `cooloff`, whose service half is another lane's.</summary>
    private const string CoolOffOwed =
        "the cool-off a limit naming no time takes is a setting that undoes itself, so Ask Daoris should propose it; that is "
        + "the `setting` kind's `cooloff` door (D125 §6), which waits on the service's setting writer listing it beside this "
        + "side's doors, and until then `daoris driver cooloff` and Settings → Driver are its doors.";

    /// <summary>
    /// A named workflow saved, or chosen, from the terminal (WORKFLOW1d, WORKFLOW1e; D157 point 13): Ask Daoris's `workflow` kind is
    /// WORKFLOW1h's, its card a version and optionally a choice (the workflow design §8.1).
    /// </summary>
    private const string WorkflowOwed =
        "a named workflow's version, a choice of one and a kind of task are changes to how work moves that the person applies, so "
        + "Ask Daoris should propose them as the `workflow` kind's card, its part said first (D157 point 13, the workflow design §8), "
        + "which WORKFLOW1h builds; until then `daoris driver workflow` is its terminal's door, and every gate reads the version "
        + "each run bound (WORKFLOW1f).";

    /// <summary>
    /// How an agent's accounts are used, the agent's page's controls (TOOL4g, UX6e; D125 §6, D130 §9): each a door of the `agent`
    /// kind that the service's `agent_propose` does not write yet, which another lane's row holds.
    /// </summary>
    private static Owed AccountDoorOwed(string door, string what, string terminal) => new(
        $"{what}, so Ask Daoris should propose it as a card the person applies (D89); that is the `agent` kind's `{door}` "
        + $"door (D125 §6, D130 §9), which waits on the service's `agent_propose` writing it, and until then `{terminal}` is the "
        + "terminal's door.");

    private const string Rules =
        "what an agent may do is never proposed by an agent: the room is not handed `permission_propose` (HELP1c), "
        + "since a rule narrowing is applied with nobody's press (PERM2).";

    /// <summary>
    /// ACCT2's <c>daoris agent profile rename</c> and the route's <c>profile-rename</c>: exempt, since an account's name is the
    /// person's own word. The agent's page presses it (UX7b), and this is its control's row.
    /// </summary>
    private static readonly Exempt RenameDoor = new(
        "an account's name is the person's own word for it, offered at a sign-in's end as who signed in and kept only by "
        + "their own press (D66 §3), so Ask Daoris never proposes one.");

    /// <summary>
    /// ACCT1's <c>daoris agent profile join</c> and the route's <c>profile-join</c>: putting an account in a list widens what
    /// Daoris may spend, as <c>order</c> does, so it is the <c>agent</c> kind's <c>order</c> door, owed with it. The agent's page
    /// presses it (UX7b), and this is its control's row.
    /// </summary>
    private static readonly Owed JoinDoor = AccountDoorOwed(
        "order", "putting an account in a list widens what Daoris may spend", "daoris agent profile join");

    /// <summary>
    /// The driver lock's <c>--share</c> (DRV8, D104), decided: a flag on the start of a headless loop, not a setting.
    /// </summary>
    private static readonly Exempt Share = new(
        "`daoris-driver drive --share` starts a headless loop beside a live one; it is a process a person runs at a "
        + "terminal, not a setting: nothing is stored to apply, Ask Daoris starts no loop, and the desktop's own loop "
        + "waits on the lock instead (D104).");

    /// <summary>
    /// LAYOUT7's <c>daoris-driver setup</c> (D117 §6.1), a verb of the headless host rather than the CLI's table: a door
    /// Ask Daoris owes, until LAYOUT8 builds the <c>setup</c> kind it is to take.
    /// </summary>
    private static readonly Owed SetupDoor = new(
        "setting a repository up publishes a quest to it and adds a rule its session needs, a widening the person applies "
        + "as a card, so Ask Daoris should propose it; that waits on LAYOUT8's `setup` kind: the service's box and tool, the "
        + "driver's judge against the same facts, and the card (D110).");

    /// <summary>
    /// WSSETUP5's <c>daoris-driver register</c> (D124 §3.1), a verb of the headless host: a door Ask Daoris owes, until WSSETUP7
    /// gives LAYOUT8's <c>setup</c> kind its <c>register</c> door (§4.5).
    /// </summary>
    private static readonly Owed RegisterDoor = new(
        "registering a repository from its line changes only what the line already says, so Ask Daoris should propose it and "
        + "the person apply it; that waits on WSSETUP7's `register` door of LAYOUT8's `setup` kind, judged against the same "
        + "line (D124 §4.5).");

    /// <summary>
    /// PAUSE1b's <c>daoris-driver ask --pause|--resume</c> and <c>quest pause|resume</c> (D132 §7.2), verbs of the headless host:
    /// doors Ask Daoris owes until PAUSE1f gives it the <c>pause</c> kind, its doors <c>pause</c> and <c>resume</c>, beside the
    /// pages' and a session's acts PAUSE1e builds.
    /// </summary>
    private static readonly Owed PauseDoor = new(
        "pausing stops what of an ask's or a quest's work runs on this machine and resuming carries it on, each a change the "
        + "person applies as a card, so Ask Daoris should propose them; that is PAUSE1f's `pause` kind, its doors `pause` and "
        + "`resume`, judged against this machine's pauses and each work's live sessions (D132 §7.4).");

    /// <summary>
    /// PAUSE1d's <c>daoris-driver ask --abandon</c> and <c>quest abandon</c> (D132 §7.2), verbs of the headless host: exempt, since
    /// an abandon declines quests with the person's reason, which is their answer (D37), as D126 §7.3 exempted *Decline…*.
    /// </summary>
    private static readonly Exempt AbandonDoor = new(
        "abandoning declines quests with the person's reason, which is their answer (D37), as *Decline…* is exempt (D126 §7.3); "
        + "Ask Daoris names the ask's page and the terminal line, and never writes the reason (D132 §7.4).");

    /// <summary>
    /// WSSETUP6's <c>daoris-driver setup --workspace</c> (D124 §4.5), a verb of the headless host: the press, and its pause,
    /// resume and stop, doors Ask Daoris owes until WSSETUP7 gives LAYOUT8's <c>setup</c> kind its <c>workspace</c>,
    /// <c>pause</c>, <c>resume</c> and <c>stop</c> doors, and the screen its route.
    /// </summary>
    private static readonly Owed WorkspaceDoor = new(
        "setting a workspace up writes a plan that publishes a quest to each repository in turn and adds a rule at the "
        + "workspace's scope, a widening the person applies as a card, and pausing, resuming or stopping it changes only what "
        + "the person already said, so Ask Daoris should propose each; that waits on WSSETUP7's `workspace`, `pause`, `resume` "
        + "and `stop` doors of LAYOUT8's `setup` kind, judged against the same plan (D124 §4.5).");

    /// <summary>
    /// SESSUX1a's <c>useArchiveSessions</c> (D126 §7.3), a bridge hook no Settings domain presses and Sessions does (a
    /// <see cref="Starts"/> row since ENTRY1a): a door Ask Daoris owes, until SESSUX1h builds the <c>session</c> kind's
    /// <c>archive</c> and <c>unarchive</c> doors.
    /// </summary>
    private static readonly Owed SessionArchiveDoor = new(
        "archiving takes ended sessions out of this machine's list and unarchiving brings them back, a tidy that undoes "
        + "itself, so Ask Daoris should propose it; that is the `session` kind's `archive` and `unarchive` doors, which "
        + "SESSUX1h builds beside Sessions' own press (D126 §7.3).");

    /// <summary>
    /// SESSUX1f's <c>useDeleteSession</c> (D126 §7.3), the bridge hook Sessions' *Delete…* presses: a door Ask Daoris owes,
    /// until SESSUX1h gives the <c>delete</c> kind its <c>session</c> door, judged by §5.4.
    /// </summary>
    private static readonly Owed SessionDeleteDoor = new(
        "deleting a conversation that served no quest removes its record and what this machine kept of it, a removal the "
        + "person applies as a card, so Ask Daoris should propose it; that is the `delete` kind's `session` door, judged by "
        + "the same two halves, which SESSUX1h builds (D126 §7.3).");

    /// <summary>
    /// SESSUX1g's <c>daoris-driver sessions</c> (D126 §7.1), a verb of the headless host: its stop, archive, unarchive and
    /// delete are doors Ask Daoris owes until SESSUX1h builds the <c>session</c> kind and the <c>delete</c> kind's
    /// <c>session</c> door; its finish and decline are the person's answer, exempt as the screen's are (§7.3).
    /// </summary>
    private static readonly Owed SessionsVerbDoor = new(
        "stopping a session holds its quest until the person tries again, and archiving and deleting tidy this machine's "
        + "list, each a change the person applies as a card, so Ask Daoris should propose them; that is SESSUX1h's `session` "
        + "kind and the `delete` kind's `session` door, while finishing and declining stay the person's answer (D126 §7.3).");

    /// <summary>
    /// HIST1d's <c>daoris-driver history clear --workspace</c>, <c>quest clear [--failed]</c> and <c>ask --clear</c> (D153 point 7,
    /// the history-clearing design §6.4), verbs of the headless host: doors Ask Daoris owes until HIST1f builds the <c>clear</c>
    /// kind, its doors <c>quest</c>, <c>ask</c> and <c>workspace</c>. The reading, <c>history</c>, changes nothing.
    /// </summary>
    private static readonly Owed ClearDoor = new(
        "clearing a closed quest's work, an ask's or a workspace's finished history removes records and what this machine kept "
        + "of them, a press the person makes on a list, so Ask Daoris should propose it as a card whose Apply is that press and "
        + "never clear itself; that is HIST1f's `clear` kind, its doors `quest`, `ask` and `workspace` (D153, design §6.4), while "
        + "`daoris-driver history` reads and changes nothing.");

    /// <summary>
    /// MSG1e's <c>daoris-driver sessions say</c> (D137 §5.4), a verb of the headless host: exempt, since the words said to a
    /// session are the person's (D133 §1). The room says where the box is and names the command, so the helper points there.
    /// </summary>
    private static readonly Exempt SayDoor = new(
        "the words said to a session are the person's own (D133 §1), so Ask Daoris composes none in their name; the room says "
        + "where the box is and names `daoris-driver sessions say` (D137 §5.4).");

    /// <summary>
    /// MSG1g's <c>daoris-driver sessions go-on-new</c> (D137 §2.2), a verb of the headless host: exempt, since it moves the
    /// person's words and trades their conversation's context for time, both the person's to choose (D133 §1, D143 point 4).
    /// </summary>
    private static readonly Exempt GoOnNewDoor = new(
        "going on in a new session moves the person's own words (D133 §1) and gives up their session's conversation for time, "
        + "a choice that stays the person's (D143 point 4), so Ask Daoris proposes none; it can name "
        + "`daoris-driver sessions go-on-new` and the screen's press once the page has it (D137 §2.2).");

    /// <summary>
    /// MSG1f3's <c>daoris-driver sessions start-from</c> (D137 §2.2, D50), a verb of the headless host: exempt, since it moves
    /// the person's words into a conversation of their own, the person's press as the screen's is (D133 §1).
    /// </summary>
    private static readonly Exempt StartFromDoor = new(
        "starting a conversation with words a session could not go on with moves the person's own words (D133 §1) into a "
        + "conversation with none of that session's context, a press that stays the person's (D137 §2.2), so Ask Daoris "
        + "proposes none; it can name `daoris-driver sessions start-from` and the screen's press.");

    /// <summary>
    /// TRACE1's <c>daoris-driver trace</c> (D143), a verb of the headless host: exempt, since it reads the records back to an ask
    /// and changes nothing, as <c>driver list</c> is (D110 §4). A screen's door to the same read is a row of its own.
    /// </summary>
    private static readonly Exempt TraceDoor = new(
        "it reads from a commit, a session or a quest back to its ask and changes nothing, so there is nothing to propose, as "
        + "`driver list` is exempt (D110 §4); a screen's door to the same read is a row of its own (D143).");

    /// <summary>
    /// WORKFLOW1c2's <c>daoris-driver workflow run</c> (D157, the workflow design §5.2, §7), a verb of the headless host: exempt,
    /// since it reads where a piece of work stands in its workflow and changes nothing, as <c>trace</c> is. The session's
    /// <i>Workflow</i> view is the screen's door to the same read, and its presses are their owners'.
    /// </summary>
    private static readonly Exempt WorkflowRunDoor = new(
        "it reads where a piece of work stands in its workflow and changes nothing, so there is nothing to propose, as `trace` "
        + "is exempt (D157, D110 §4); each press a waiting step needs is its owner's door, never this read's.");

    /// <summary>
    /// WORKFLOW1f's <c>daoris-driver workflow keep</c> (D157 point 10, the workflow design §4.4), a verb of the headless host:
    /// exempt, since keeping a workflow that asks less of the person for work outside its kind's paths is their say-so, as going
    /// on without a second opinion is. The screen's press is the editor's row (WORKFLOW1g).
    /// </summary>
    private static readonly Exempt WorkflowKeepDoor = new(
        "keeping a workflow that asks less of you for work that changed paths outside its kind's is your say-so on that work, a "
        + "judgement Ask Daoris cannot have made (D157 point 10, D110), as going on without a second opinion is; it can name "
        + "`daoris-driver workflow keep`.");

    /// <summary>
    /// EVID1b's <c>daoris-driver quest check</c> (D144 §5), a verb of the headless host: a door Ask Daoris owes, until EVID1c
    /// builds the check card beside the quest page's <i>Check again</i>.
    /// </summary>
    private static readonly Owed CheckDoor = new(
        "reading a done's evidence again posts a verdict that can lift its hold and publish what it held, a move the person "
        + "applies as a card, so Ask Daoris should propose it; that is EVID1c's check card beside the quest page's Check again "
        + "(D144 §5), and until then `daoris-driver quest check` is the terminal's door.");

    /// <summary>
    /// XAGENT1f's <c>daoris-driver opinion ask</c> and <c>opinion stop</c> (D155 point 10; the second-agent design §9), verbs of the
    /// headless host: exempt, since asking another agent for a second opinion spends an account at the person's choice, as going on
    /// in a new session does (MSG1g), and a stop is the person's own (D104). <c>opinion show</c> reads and changes nothing.
    /// </summary>
    private static readonly Exempt OpinionAskDoor = new(
        "asking another agent for a second opinion, or the same agent in a fresh conversation, spends an account at the person's "
        + "choice, as going on in a new session does (D137 §2.2), and stopping a reviewer is their own stop (D104), so Ask Daoris "
        + "proposes neither (the second-agent design §9); it can name `daoris-driver opinion ask`, and `opinion show` changes nothing.");

    /// <summary>
    /// XAGENT1f's <c>daoris-driver opinion anyway</c> and <c>opinion myself</c> (D155 point 10; the second-agent design §9): exempt,
    /// since going on without a settled opinion, or reading the work oneself in its place, is a judgement Ask Daoris cannot have
    /// made (D110), as D154's verdict is.
    /// </summary>
    private static readonly Exempt OpinionJudgementDoor = new(
        "going on without a settled second opinion, or recording one's own reading in its place, is a judgement Ask Daoris cannot "
        + "have made (D110), as D154's verdict is, so it proposes neither (the second-agent design §9); it can name "
        + "`daoris-driver opinion anyway` and `opinion myself`.");

    /// <summary>
    /// GIT1c's <c>daoris-driver git branches</c> (D147 §3.3), a verb of the headless host: exempt, since it reads each
    /// repository's branches and changes nothing, as <c>trace</c> is. The acts (fetch, branch, push, delete) are owed to a
    /// <c>git</c> kind, GIT1k's, once GIT1g and GIT1h build them.
    /// </summary>
    private static readonly Exempt GitBranchesDoor = new(
        "it reads each repository's line and branches and changes nothing, so there is nothing to propose, as `trace` is "
        + "exempt (D147 §3.3, D110 §4); the acts it will sit beside are owed to GIT1k's `git` kind.");

    /// <summary>
    /// PLUGHOOK1c's <c>daoris-driver trees state</c> (D148 point 2, design §2.5), a verb of the headless host: exempt, since it
    /// asks a landed branch's plugin about its pull request and keeps the answer, changing nothing of the person's and nothing on
    /// the platform (D110 §4). The room names it, so the helper can point the person at it.
    /// </summary>
    private static readonly Exempt TreesStateDoor = new(
        "it asks a landed branch's plugin whether its pull request completed and keeps the answer, which changes nothing of the "
        + "person's and nothing on the platform, so there is nothing to propose, as `trace` is exempt (D148, D110 §4).");

    /// <summary>
    /// The install's update (D139): *Update when idle*, *Update now* and *Not now* on the banner and Settings → Driver
    /// (UPDATE1e's <c>useSayUpdate</c>), and the headless host's <c>daoris-driver update</c> (UPDATE1f). Exempt, since it is
    /// the person's act on the application; the room names both doors, so the helper points there.
    /// </summary>
    private static readonly Exempt UpdateDoor = new(
        "an update is the person's act on the application, not on the work it drives (D139): *Update now* closes Daoris, "
        + "Ask Daoris's window with it, and *Not now* keeps a build from installing, so each stays their own press on the "
        + "banner, Settings → Driver or `daoris-driver update --when-idle|--now|--cancel`.");

    /// <summary>
    /// ASKHIST1's history of Ask Daoris's own conversations: the panel's rename, pin and new conversation from an earlier one,
    /// and the headless host's <c>daoris-driver help</c>. Exempt, since the conversations and what the person keeps of them are
    /// their own; the room names both doors, so the helper points there.
    /// </summary>
    private static readonly Exempt HistoryDoor = new(
        "Ask Daoris's conversations are the person's own words and knowledge, kept on this machine only (ASKHIST1): going on in "
        + "one is their words (D133 §1), and naming, pinning, starting from or deleting one is their tidy of their own history, so "
        + "Ask Daoris proposes none; the room names the panel's History and `daoris-driver help`.");

    /// <summary>
    /// The page's bridge hooks that change something and that no Settings domain presses, answered one by one wherever they
    /// are pressed (UPDATE1f), where <see cref="Starts"/> answers a press by its place (ENTRY1a). Each row is held to a hook
    /// the bridge still exports, that still changes something, that the page still presses, and that no domain presses, where
    /// it would be one of <see cref="Controls"/> instead.
    /// </summary>
    private static readonly (string Hook, Answer Answer)[] Elsewhere =
    [
        // UPDATE1: the banner's *Dismiss*, once an update installed or rolled back; since UPDATE1d the last update outlives it.
        ("useDismissUpdate", new Exempt(
            "it puts away an update's outcome once said, the banner's *Dismiss*, and changes nothing about the install or its "
            + "work: the last update still reads on Settings → Driver and from plain `daoris-driver update` (D139), so there "
            + "is nothing to propose.")),
        // BRW7: a link pressed on the page, opened in Daoris's browser where the links setting says so.
        ("useLinkOpener", new Exempt(
            "it opens a link the person pressed in Daoris's browser, their own click, and changes nothing on the machine; "
            + "where links open is Settings → Browser's, which the `browser` kind's `links` door proposes (D110 §4).")),
        // REVIEWENV1d: *Show it again* on the strip's review chip, a set-up's build served to its tab again.
        ("useShowReviewAgain", new Exempt(
            "it serves a set-up's build to its tab of Daoris's browser again for the person to look at, and changes nothing of "
            + "the work, its review or the machine's settings; what the person sees there is theirs to judge, and the verdict "
            + "beside it is theirs alone (D154 point 8), so there is nothing to propose.")),
        // ASKHIST1: the Ask Daoris panel's history.
        ("useRenameHelp", HistoryDoor),
        ("usePinHelp", HistoryDoor),
        ("useHelpStartFrom", HistoryDoor),
        // XAGENT1g: the second opinion's presses on a session's page and What needs you, the terminal's `opinion` verbs' twins.
        ("useAskOpinion", OpinionAskDoor),
        ("useStopOpinion", OpinionAskDoor),
        ("useOpinionAnyway", OpinionJudgementDoor),
        ("useOpinionMyself", OpinionJudgementDoor),
    ];

    /// <summary>
    /// ENTRY1a: a quest's or a session's own act (D161's ENTRY1 note), said as what it is, done in that place and its
    /// conversation: Ask Daoris takes the person there with a go to the view and never proposes the act.
    /// </summary>
    private static Conversation Own(string place, string what) => new(
        place, $"{what}, so it is done in its own place and conversation, where Ask Daoris takes the person with a go and does "
        + "not take over (D161's ENTRY1 note).");

    /// <summary>ENTRY1d's: a repository added or a folder imported, today a go to Repositories' Add and Import (audit §4.4).</summary>
    private static readonly Owed RepositoryAddOwed = new(
        "adding a repository or importing a folder's registers what the person picked, a widening they apply as a card, so Ask "
        + "Daoris should propose it while the folder stays their pick (D48 §3/§7); that is ENTRY1d's design, mirroring the "
        + "`plugin` kind's `add`, and until then a go reaches Repositories' Add repository and Import a folder.");

    /// <summary>
    /// ENTRY1a: Ask Daoris's own conversation, pressed in its panel: the words and the turns are the person's with it, so a
    /// card would be Ask Daoris proposing its own conversation. Its history (rename, pin, delete) is <see cref="HistoryDoor"/>.
    /// </summary>
    private static readonly Exempt HelpConversation = new(
        "it is Ask Daoris's own conversation with the person, opened, said to, stopped or ended where they are talking to it "
        + "(HELP1a): the words are theirs (D133 §1), and a card for them would be Ask Daoris proposing its own conversation, so "
        + "there is nothing to propose.");

    private static readonly Exempt OpensAWindow = new(
        "it opens another of Daoris's windows, a session's or a monitor, and changes nothing of the work or the machine, so "
        + "there is nothing to propose; a go takes the person to the place instead.");

    private static readonly Exempt OpensTheBrowser = new(
        "it opens Daoris's browser for the person to look, and changes nothing of the work or the machine's settings; which "
        + "browser and where links open are the `browser` kind's doors (D110 §4).");

    private static readonly Conversation PersonDone = Own("quests",
        "the person's done is their word on a quest's work, given on the quest's page or at a finish from a checkpoint (QUESTCLOSE1)");

    private static readonly Conversation TrustFolder = Own("quests",
        "trusting a folder lets the agent start a quest's work in it, the person's grant where the start is held (D73)");

    private static readonly Conversation PublishAsk = Own("quests",
        "publishing an ask to a receiver hands its intake's proposal on as quests, the person's press on the ask's page or What "
        + "needs you (INT4a)");

    private static readonly Conversation AnswerGoAhead = Own("quests",
        "a go-ahead an ask's work asked is answered on the ask's page or What needs you, and every session on the ask is handed "
        + "it at its next start (KNOWUSE1a)");

    /// <summary>
    /// Each start the page offers outside a Settings domain (ENTRY1a, D161's ENTRY1 note): a hook that changes something,
    /// pressed where it is (<see cref="Presses"/>), with the action where the hook carries several. A hook a domain's control
    /// or <see cref="Elsewhere"/> already answers is answered here too, since the answer is the act's, not the place's.
    /// </summary>
    /// <remarks>
    /// Each answer follows the rule D161's ENTRY1a note gives. A press that is not a mutation is no start and no row: *Look
    /// now* (`useNudge`) is a callback that asks the loop to look sooner and changes nothing.
    /// </remarks>
    private static readonly (string Where, string Hook, string? Action, Answer Answer)[] Starts =
    [
        // The application's frame: the strip's opens, the index's refresh and Sync now.
        ("App", "useOpenBrowser", null, OpensTheBrowser),
        ("App", "useOpenWindow", null, OpensAWindow),
        ("App", "useRefreshIndex", null, new Exempt(
            "it reads the repositories again into the index and changes nothing of the work or the machine's settings, as reading "
            + "the roster again is exempt, so there is nothing to propose.")),
        ("App", "useSyncNow", null, new Exempt(
            "it runs now the pass with the workspace's remote that the loop's next tick runs anyway (SYNC6b), and changes nothing "
            + "the person has not set; whether a workspace syncs is its remote's wiring, the person's own (D89). It is not the "
            + "`sync` kind's door, which brings branches up to date (D109).")),
        ("SecondaryWindowRoot", "useOpenBrowser", null, OpensTheBrowser),

        // Quests: a quest's own page, and the list's presses.
        ("QuestsView", "usePublishQuest", null, Own("quests",
            "publishing a quest by hand is the person's own request to a repository, made on Quests beside the intake that "
            + "publishes from an ask (D65)")),
        ("QuestsView", "useRespondQuest", "take", Own("quests",
            "taking a quest is its receiver's answer to it, given on the quest's own page")),
        ("QuestsView", "useRespondQuest", "done", Own("quests",
            "saying a quest is done answers it with its work, on the quest's own page where that work is read")),
        ("QuestsView", "useRespondQuest", "decline", new Exempt(
            "declining a quest is the person's answer with their reason (D37), as *Decline…* is exempt (D126 §7.3); Ask Daoris "
            + "takes the person to the quest and never writes the reason.")),
        ("QuestsView", "useAcceptQuest", null, new Door("accept", "accept")),
        ("QuestsView", "useDeleteQuest", null, new Door("delete", "quest")),
        ("QuestsView", "useRetryQuest", null, new Door("setting", "retry")),
        ("QuestsView", "usePersonDone", null, PersonDone),
        ("QuestsView", "useTrustFolder", null, TrustFolder),
        ("QuestsView", "useDismissConflict", null, new Exempt(
            "it puts away a conflict the person has read, an operation that travels and changes no status of the quest or its "
            + "work (SYNC6c), as the banner's *Dismiss* is exempt, so there is nothing to propose.")),

        // Asks, on Quests: the composer and an ask's page.
        ("asks", "useAsk", null, new Door("ask", "ask")),
        ("asks", "useDeleteAsk", null, new Door("delete", "ask")),
        ("asks", "usePublishAsk", null, PublishAsk),
        ("asks", "useAnswerGoAhead", null, AnswerGoAhead),
        ("asks", "useCloseAsk", null, new Exempt(
            "closing an ask ends it with the person's reason, which is their answer (D37), as abandoning one is exempt (D132 §7.4) "
            + "and *Decline…* is (D126 §7.3); Ask Daoris names the ask's page and never writes the reason.")),
        ("asks", "useChooseAskReview", null, new Owed(
            "an ask's review choice says how its work is shown to the person before it lands, a choice the person applies, so "
            + "Ask Daoris should carry it when it proposes the ask; the `ask` door takes the sentence, workspace and why only, "
            + "which ENTRY1c widens with the review and the kind, and until then the ask's page is its door (REVIEWENV1b).")),

        // Ask Daoris's own panel.
        ("help", "useStartHelp", null, HelpConversation),
        ("help", "useSendMessage", null, HelpConversation),
        ("help", "useCancelTurn", null, HelpConversation),
        ("help", "useStopSession", null, HelpConversation),
        ("help", "useEndChat", null, HelpConversation),
        ("help", "useDeleteSession", null, HistoryDoor),
        ("help", "useSettleHelp", null, new Exempt(
            "Apply and Not now on a card are the person's own press, the one every proposal waits on (D89): a card that settled "
            + "itself would apply with nobody's press, so Ask Daoris proposes none.")),

        // Sessions, What needs you and the work's own acts (`work/`): a session's page, its review and its trees.
        ("work", "useStartChat", null, Own("sessions",
            "starting a conversation with an agent in a repository opens a session of its own (D49 §3)")),
        ("work", "useSendMessage", null, Own("sessions",
            "the words said in a conversation are the person's own (D133 §1), said in its box")),
        ("work", "useSay", null, Own("sessions",
            "the words said to a session are the person's own (D133 §1), said in its box (D137 §5.4)")),
        ("work", "useCancelTurn", null, Own("sessions",
            "stopping a conversation's turn keeps the conversation and hands back what was waiting (CONV4a)")),
        ("work", "useEndChat", null, Own("sessions",
            "ending a conversation is the person's, from the conversation itself")),
        ("work", "useSetSessionOption", null, Own("sessions",
            "a conversation's model and effort are its agent's options, set from its composer (AGT6b, D98)")),
        ("work", "useStopTask", null, Own("sessions",
            "stopping a task a session runs is that session's own stop, from its tab (CONSOLE3a)")),
        ("work", "useStopSession", null, Own("sessions",
            "stopping a session ends its process and holds its quest until the person tries again (D126 §7.3)")),
        ("work", "useResolveSession", null, Own("sessions",
            "finishing, declining or stopping a session parked at a checkpoint is the person's answer to it (D126 §7.3)")),
        ("work", "useAnswerSession", null, Own("sessions",
            "answering a session parked to ask the person sends its own conversation on with their words (STANDDOWN2, D131)")),
        ("work", "useParkGoAhead", null, Own("sessions",
            "a go-ahead a parked session asked is answered on the session's own page (KNOWUSE1a2, D135 §2)")),
        ("work", "useLandSessionTree", null, Own("sessions",
            "landing a session's work is the person's verdict on it (D154 point 8), pressed at its review")),
        ("work", "useReviewVerdict", null, Own("quests",
            "a review's verdict is the person's on the work they were shown (D154 point 8), given at the quest's review "
            + "(REVIEWENV1g)")),
        ("work", "usePublishSetUpStep", null, Own("quests",
            "a set-up step following a done quest is the person's *Set it up in*, pressed at its review (REVIEWENV1b)")),
        ("work", "usePersonDone", null, PersonDone),
        ("work", "useTrustFolder", null, TrustFolder),
        ("work", "usePublishAsk", null, PublishAsk),
        ("work", "useAnswerGoAhead", null, AnswerGoAhead),
        ("work", "useAcceptQuest", null, new Door("accept", "accept")),
        ("work", "useRetryQuest", null, new Door("setting", "retry")),
        ("work", "useHandOffPress", null, new Door("hand", "hand")),
        ("work", "usePauseWork", null, PauseDoor),
        ("work", "useResumeWork", null, PauseDoor),
        ("work", "useAbandonWork", null, AbandonDoor),
        ("work", "useArchiveSessions", null, SessionArchiveDoor),
        ("work", "useDeleteSession", null, SessionDeleteDoor),
        ("work", "useGoOnNew", null, GoOnNewDoor),
        ("work", "useStartFrom", null, StartFromDoor),
        ("work", "useDiscardSessionTree", null, new Exempt(
            "it discards a session's tree and its work for good, a discard, which stays the person's own press (D89), as the "
            + "clean-up and a failed attempt's branch do.")),
        ("work", "useOpenWindow", null, OpensAWindow),
        ("work", "useOpenSessionFolder", null, new Exempt(
            "it opens a session's folder in the system's file browser and changes nothing, as the log's folder is exempt; a go "
            + "reaches the session.")),
    ];

    /// <summary>Each <c>daoris driver</c> verb, and <c>across</c> by its two forms, as the CLI's command table spells them.</summary>
    private static readonly (string Verb, Answer Answer)[] Verbs =
    [
        ("list", new Exempt("it reads `driver.json` and changes nothing; the room already carries what it lists, from the same file.")),
        ("drive", new Door("setting", "drive")),
        ("undrive", new Door("setting", "undrive")),
        ("hold", new Door("setting", "hold")),
        ("resume", new Door("setting", "resume")),
        ("trees", new Door("setting", "trees")),
        ("line", new Door("setting", "line")),
        ("landing", new Door("setting", "landing")),
        ("across read", new Door("setting", "across")),
        ("across write-to", new Door("setting", "across")),
        // KNOWUSE1b: a standing answer for a repository, the person's words, which Ask Daoris proposes and the person applies.
        ("standing", new Door("setting", "standing")),
        // LANG1c: the work's session language, for a repository or a workspace; LANG1c2 gave it the service's writer.
        ("language", new Door("setting", "language")),
        // REVIEWENV1a: where work is shown to the person before it lands, with the service's writer in the same change.
        ("review", new Door("setting", "review")),
        // XAGENT1a: which other agent reads work before it lands, with the service's writer in the same change.
        ("opinion", new Door("setting", "opinion")),
        // WORKFLOW1a: Current, drawn from the rules as they stand; a named workflow's Ask Daoris door is WORKFLOW1h's.
        ("workflow show", new Exempt(
            "it reads `driver.json` and the plugins beside it and changes nothing; it draws the landing, review and opinion rules "
            + "the room already carries, from the same file.")),
        // WORKFLOW1d: named workflows, each a file of versions under the home; the reads are exempt, the saves owed to WORKFLOW1h.
        ("workflow list", new Exempt("it reads the home's workflows and the presets built in, and changes nothing.")),
        ("workflow new", new Owed(WorkflowOwed)),
        ("workflow edit", new Owed(WorkflowOwed)),
        ("workflow apply", new Owed(WorkflowOwed)),
        ("workflow export", new Exempt(
            "it copies a workflow's file out, whole, to a new file or the terminal, for the person to carry, and changes nothing here.")),
        ("workflow import", new Owed(WorkflowOwed)),
        // WORKFLOW1e: which workflow work follows, and a workspace's kinds of task; Ask Daoris proposes a choice and a kind with
        // the `workflow` kind (the workflow design §8.1, §8.3), WORKFLOW1h's.
        ("workflow use", new Owed(WorkflowOwed)),
        ("workflow kind", new Owed(WorkflowOwed)),
        ("notify", new Door("setting", "notify")),
        ("intake", new Door("setting", "intake")),
        ("helper", new Door("setting", "helper")),
        ("strikes", new Door("setting", "strikes")),
        ("retry", new Door("setting", "retry")),
        ("timeout", new Door("setting", "timeout")),
        // TOOL4e: the default cool-off's terminal door (D125 §6); TOOL4g built Settings → Driver's control, and its Ask Daoris
        // door waits on the service.
        ("cooloff", new Owed(CoolOffOwed)),
        ("cap", new Door("setting", "cap")),
        ("adapter", new Door("setting", "adapter")),
    ];

    /// <summary>
    /// Each control a Settings domain holds that changes something: the page's bridge hook it presses, and the
    /// action where the hook carries several.
    /// </summary>
    private static readonly (string Domain, string Hook, string? Action, Answer Answer)[] Controls =
    [
        ("ai", "useSetIntake", null, new Door("setting", "intake")),
        ("ai", "useSetHelper", null, new Door("setting", "helper")),

        ("driver", "useSetNotify", null, new Door("setting", "notify")),
        ("driver", "useSetStrikes", null, new Door("setting", "strikes")),
        ("driver", "useSetCoolOff", null, new Owed(CoolOffOwed)),
        // UPDATE1e: the install's update, *Update when idle*, *Update now* and *Not now*, read once the hooks exported as
        // functions were; UPDATE1f gave the room its row.
        ("driver", "useSayUpdate", null, UpdateDoor),

        // TOOL4g: how an agent's accounts are used. The `use` door is judged and applied on this side already
        // (`HelpAgentProposals`); every one waits on the service's writer.
        ("agents", "useAccountUse", "order", AccountDoorOwed(
            "order", "adding an account to a list widens what Daoris may spend", "daoris agent profile order")),
        ("agents", "useAccountUse", "use", AccountDoorOwed(
            "use", "how a list is used changes which account a start spends, and undoes itself", "daoris agent profile use")),
        ("agents", "useAccountUse", "ready", AccountDoorOwed(
            "ready", "*Try now* spends an account sooner than its cool-off said", "daoris agent profile ready")),
        ("agents", "useAccountUse", "inherit", AccountDoorOwed(
            "order", "returning a workspace to this machine's accounts may widen what its work spends",
            "daoris agent profile order <agent> --clear --workspace <name>")),

        ("agents", "useHarnessAction", "install", new Exempt(
            "an install fetches a maker's release and runs its installer on this machine, the person's own press (HELP6).")),
        ("agents", "useHarnessAction", "update", new Door("agent", "update")),
        ("agents", "useHarnessAction", "pin", new Door("agent", "pin")),
        ("agents", "useHarnessAction", "unpin", new Exempt(
            "unpinning hands the agent back to whatever the PATH finds, a binary nobody chose here, the person's own press (HELP6).")),
        ("agents", "useHarnessAction", "login", new Exempt(SignIn)),
        ("agents", "useHarnessAction", "login-new", new Exempt(SignIn)),
        ("agents", "useHarnessAction", "key-add", new Exempt(Key)),
        ("agents", "useHarnessAction", "profile-add", new Exempt("an account is made by its own sign-in; " + SignIn)),
        ("agents", "useHarnessAction", "profile-remove", new Exempt(
            "it deletes an account with its sign-in, which only the person can make again (D89).")),
        ("agents", "useHarnessAction", "profile-default", new Door("agent", "default")),
        // UX7b: the agent's page presses ACCT2's rename and ACCT1's join, at a new account's end and from a row.
        ("agents", "useHarnessAction", "profile-rename", RenameDoor),
        ("agents", "useHarnessAction", "profile-join", JoinDoor),
        ("agents", "useSetAgentSettings", null, new Door("account", "settings")),
        ("agents", "useRefreshHarnesses", null, new Exempt("it reads the roster again and changes nothing.")),

        // TOOLS7: Settings → Tools, answered as D121 §4.3 decides, its doors owed until TOOLS8 builds the `tool` kind.
        ("tools", "useToolUse", "system", new Owed(ToolUseOwed)),
        ("tools", "useToolUse", "managed", new Owed(ToolUseOwed)),
        ("tools", "useToolUse", "file", new Exempt(
            "a program the person names runs as them on every call, so naming one stays their own press (D121 §4.3): a "
            + "helper that can invent a path must not be one press away from running it.")),
        ("tools", "useToolDownload", null, new Exempt(
            "a download changes nothing about what Daoris runs; the use that would is a door (D121 §4.3).")),
        ("tools", "useToolStop", null, new Exempt(
            "it stops a download the person started, theirs to stop, and nothing switches either way.")),
        ("tools", "useToolDelete", null, new Exempt(
            "deleting a downloaded version is a discard, which stays the person's own press (D89); the one in use is never deleted.")),
        ("tools", "useToolsLook", null, new Exempt(
            "a look reads the resource lists and changes nothing about what runs (D121 §4.3).")),
        ("tools", "useToolLocation", "add", new Exempt(
            "a location is a source of programs with hashes of its own choosing, so naming one is the person's own trust and "
            + "their own press (D121 §4.3).")),
        ("tools", "useToolLocation", "remove", new Owed(
            "removing a location only narrows what is offered, so Ask Daoris should propose it, its card listing the versions "
            + "it stops offering; that is the `tool` kind's `location` door, which TOOLS8 builds (D121 §4.3), and until then "
            + "`daoris tool locations remove` is the terminal's door.")),
        ("tools", "useToolPick", null, new Exempt("it is the system's file picker, for the person's own choice.")),
        ("agents", "useHarnessInput", null, new Exempt("it types into a running sign-in, the person's own words to it; " + SignIn)),
        ("agents", "useHarnessCancel", null, new Exempt("it stops a sign-in the person started, theirs to stop; " + SignIn)),
        // UX6e (D150 §3.1): what the agent may do moved to its page, the rules for every session and the proposals with it.
        ("agents", "useRuleAction", "add", new Exempt(Rules)),
        ("agents", "useRuleAction", "remove", new Exempt(Rules)),
        ("agents", "useRuleAction", "default", new Exempt(Rules)),
        ("agents", "useRuleProposal", null, new Exempt(
            "accepting or declining a rule another agent proposed is the person's review of it (PERM2, D74); a helper "
            + "answering it would be one agent approving another.")),

        // HELPSETUP1: a repository's Setup (UX6f, D150 §4.2), each value the `setting` kind's door that `daoris driver`
        // spells. Since UX6g a workspace's page (§4.3) presses the same hooks for its defaults, reading across and its
        // rules, where Settings → Workspace and Permissions did, so their rows are these.
        ("projects", "useSetDrivable", null, new Door("setting", "drive")),
        ("projects", "useSetHold", null, new Door("setting", "hold")),
        ("projects", "useSetTrees", null, new Door("setting", "trees")),
        ("projects", "useSetLine", null, new Door("setting", "line")),
        ("projects", "useSetLanding", null, new Door("setting", "landing")),
        ("projects", "useSetLanguage", null, new Door("setting", "language")),
        // REVIEWENV1a: *Review before landing*, on a repository's Setup and a workspace's Defaults.
        ("projects", "useSetReview", null, new Door("setting", "review")),
        // XAGENT1a: *Second opinion before landing*, on a repository's Setup and a workspace's Defaults.
        ("projects", "useSetOpinion", null, new Door("setting", "opinion")),
        ("projects", "useSetStanding", null, new Door("setting", "standing")),
        ("projects", "useSetReadAcross", null, new Door("setting", "across")),
        ("projects", "useSetWriteAcross", null, new Door("setting", "across")),
        ("projects", "useRuleAction", "add", new Exempt(Rules)),
        ("projects", "useRuleAction", "remove", new Exempt(Rules)),
        ("projects", "useRuleAction", "default", new Exempt(Rules)),
        // Repositories' own drawers, read with the view that holds them.
        ("projects", "useWriteDeclaration", null, new Exempt(
            "it writes a tracked file in the repository, left uncommitted for its own review (D48 §7), which is that "
            + "repository's own work: Ask Daoris routes such a change there with `ask_propose`, as it routes every change "
            + "to a repository's tree.")),
        ("projects", "usePickFolder", null, new Exempt("it is the system's folder picker, for the person's own choice.")),
        // UX6g (D150 §4.3, §3.1): a workspace's page, its Branches' clean-up and bringing up to date, and its remote, moved
        // here from Settings → Workspace.
        ("projects", "useTreesSync", null, new Door("sync", "sync")),
        ("projects", "useSweep", null, new Exempt(
            "it removes session trees and branches whose work landed, a discard, which stays the person's own press "
            + "(D89); its screen is a workspace's page → Branches → *Clean up…* since UX6g.")),
        // LAND3b (D102): a failed attempt's branch, discarded from a Branches row or a session whose tree is gone.
        // HIST1e (D153): a workspace's *Clear history…*, and a quest's and an ask's clears pressed through the same owner.
        ("projects", "useClearHistory", null, ClearDoor),
        ("projects", "useDiscardSessionBranch", null, new Exempt(
            "it discards a failed attempt's branch and its commits for good, a discard, which stays the person's own press "
            + "(D89), as the clean-up beside it does.")),
        // ENTRY1a: the registration lifecycle (D48 §3/§7), the page's own service calls, read since `queries.ts` is.
        ("projects", "useRegisterRepository", null, RepositoryAddOwed),
        ("projects", "useImportFolder", null, RepositoryAddOwed),
        ("projects", "useWireRepository", null, new Owed(
            "which workspace a repository is in is its registration, set as it is added and changed by the same kind of press, "
            + "a move that undoes itself, so Ask Daoris should propose it; ENTRY1d's design takes a repository's registration "
            + "through Ask Daoris (D48 §3/§7), and until then the repository's Manage drawer is its door.")),
        ("projects", "useRetireRepository", null, new Exempt(
            "retiring takes a repository off this machine's registry and its entries out of the index, a removal, which stays "
            + "the person's own press (D89), as a plugin's removal does; registering it again is the add, owed to ENTRY1d.")),
        ("projects", "useWireRemote", null, new Exempt("wiring a workspace to a remote takes that remote's key; " + Key)),
        ("projects", "useUnwireRemote", null, new Exempt(
            "unwiring drops the remote's key from this machine, which only the person can give back (D89).")),

        ("plugins", "usePluginAction", "enable", new Door("plugin", "enable")),
        ("plugins", "usePluginAction", "disable", new Door("plugin", "disable")),
        ("plugins", "usePluginAction", "remove", new Exempt(
            "a removal takes an installed plugin away, which only a fresh install brings back, so like a discard it stays "
            + "the person's own press (D89); Ask Daoris proposes switching it off, which undoes itself.")),
        ("plugins", "usePluginUpdate", null, new Door("plugin", "update")),
        ("plugins", "usePluginInstall", null, new Door("plugin", "add")),
        ("plugins", "usePluginNew", null, new Exempt(
            "a plugin is made as an ask at the repository that holds plugins, which `ask_propose` proposes (PLUG9); the "
            + "kit's first files are the person's own start, in a folder they chose.")),
        ("plugins", "usePluginTry", null, new Exempt(
            "a trial runs the plugin's code as the person to show what it answers (PLUG8), and changes nothing to propose.")),
        ("plugins", "usePickFolder", null, new Exempt("it is the system's folder picker, for the person's own choice.")),

        ("browser", "useSetBrowser", null, new Door("browser", "use")),
        ("browser", "useSetLinks", null, new Door("browser", "links")),
        ("browser", "useSetExtensions", null, new Door("browser", "extensions")),
        ("browser", "useAddFavorite", null, new Door("browser", "favorite")),
        ("browser", "useRemoveFavorite", null, new Door("browser", "favorite")),

        ("logs", "useOpenLogFolder", null, new Exempt(
            "it opens the log's folder in the system's file browser and changes nothing; a go reaches Settings → Logs.")),
    ];

    /// <summary>
    /// A domain's controls that press no bridge hook: each named by what its source must still say, so a row outlives
    /// its control by no more than the next run.
    /// </summary>
    private static readonly (string Domain, string Control, string Mentions, Answer Answer)[] Local =
    [
        ("start", "a step's door", "onGo", new Door("go", "go")),
        ("appearance", "theme", "useThemeChoice", new Exempt(
            "a viewer's own look, kept by this window and never the machine's (D66); Ask Daoris proposes what the machine does.")),
        ("appearance", "language", "changeLanguage", new Exempt(
            "a viewer's own language, kept by this window and never the machine's (D66); Ask Daoris proposes what the machine does.")),
    ];

    /// <summary>
    /// A form of a control answered for above that its door does not take yet: the same hook and action, another use
    /// of it, owed with what it waits on, and the terminal's spelling of it, which the room names meanwhile.
    /// </summary>
    private static readonly (string Control, string Form, string Terminal, Answer Answer)[] Forms =
    [
        // LEFT3: the terminal's `--clear` made this the screen's and the terminal's both (D50), and Ask Daoris's next.
        ("agents: useHarnessAction profile-default", "clearing an account's default back to the tool's own home",
            "daoris agent profile default <agent> <profile>|--clear", new Owed(
                "a cleared default is a setting that undoes itself, so Ask Daoris should propose it; the `agent` kind's "
                + "`default` door takes an account and no clear yet, which waits on the service's `agent_propose` taking "
                + "one and the driver's judge applying it through `IHelpDoors.SetDefaultAccountAsync` with none.")),
    ];

    private static string Page => Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src");

    [Fact]
    public void Every_driver_verb_is_a_door_owed_or_exempt_with_its_reason()
    {
        var table = File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", "driver.ts"));

        Same("`daoris driver` verbs", DriverVerbs(table), Verbs.Select(row => row.Verb));
    }

    [Fact]
    public void Every_settings_control_is_a_door_owed_or_exempt_with_its_reason()
    {
        var hooks = BridgeHooks();
        var found = new List<string>();
        foreach (var (domain, files) in Domains())
        {
            var used = files.SelectMany(Pressed).ToHashSet();
            foreach (var hook in used.Where(hooks.ContainsKey).Where(hook => hooks[hook].Changes))
            {
                found.AddRange(hooks[hook].Actions.Count == 0
                    ? [Control(domain, hook, null)]
                    : hooks[hook].Actions.Select(action => Control(domain, hook, action)));
            }
        }

        Same("Settings controls", found, Controls.Select(row => Control(row.Domain, row.Hook, row.Action)));
    }

    /// <summary>A domain with no control that changes anything is still answered for, and a local row still names what its source says.</summary>
    [Fact]
    public void Every_settings_domain_is_answered_for()
    {
        var domains = Domains();

        Same("Settings domains", domains.Keys, Controls.Select(row => row.Domain).Concat(Local.Select(row => row.Domain)).Distinct());
        foreach (var (domain, control, mentions, _) in Local)
        {
            Assert.True(
                domains[domain].Any(file => File.ReadAllText(file).Contains(mentions, StringComparison.Ordinal)),
                $"Settings → {domain}'s {control} is answered for by `{mentions}`, which its source no longer says.");
        }
    }

    [Fact]
    public void Every_door_named_is_one_its_kind_takes()
    {
        var doors = Verbs.Select(row => row.Answer).Concat(Controls.Select(row => row.Answer)).Concat(Local.Select(row => row.Answer))
            .Concat(Elsewhere.Select(row => row.Answer)).Concat(Starts.Select(row => row.Answer)).OfType<Door>();

        foreach (var door in doors.Distinct())
        {
            var kind = HelpProposalKinds.Find(door.Kind);
            Assert.True(kind is not null, $"`{door.Kind}` is not a kind Ask Daoris proposes.");
            Assert.True(kind!.Doors.Contains(door.Name), $"the `{door.Kind}` kind takes no door `{door.Name}` — one of {string.Join(", ", kind.Doors)}.");
        }
    }

    [Fact]
    public void Every_exemption_and_every_door_owed_says_why()
    {
        var reasons = Verbs.Select(row => row.Answer).Concat(Controls.Select(row => row.Answer)).Concat(Local.Select(row => row.Answer))
            .Concat(Forms.Select(row => row.Answer))
            .Concat(Elsewhere.Select(row => row.Answer))
            .Concat(Starts.Select(row => row.Answer))
            .Append(Share)
            .Append(SetupDoor)
            .Append(RegisterDoor)
            .Append(WorkspaceDoor)
            .Append(SessionArchiveDoor)
            .Append(SessionDeleteDoor)
            .Append(SessionsVerbDoor)
            .Append(ClearDoor)
            .Append(AbandonDoor)
            .Append(SayDoor)
            .Append(GoOnNewDoor)
            .Append(StartFromDoor)
            .Append(TraceDoor)
            .Append(WorkflowRunDoor)
            .Append(WorkflowKeepDoor)
            .Append(CheckDoor)
            .Append(GitBranchesDoor)
            .Append(TreesStateDoor)
            .Append(OpinionAskDoor)
            .Append(OpinionJudgementDoor)
            .Append(UpdateDoor)
            .Append(RenameDoor)
            .Append(JoinDoor)
            .Select(answer => answer switch
            {
                Exempt exempt => exempt.Reason,
                Owed owed => owed.Reason,
                Conversation conversation => conversation.Reason,
                _ => null,
            })
            .OfType<string>();

        foreach (var reason in reasons)
        {
            Assert.True(reason.Length >= 40 && reason.EndsWith('.'), $"a reason is a sentence that says why: `{reason}`.");
        }
    }

    /// <summary>
    /// HELP10: WSR6's <c>daoris-driver trees sync</c> (D109), a verb of the headless host rather than the CLI's table, is
    /// the <c>sync</c> kind's door while the host's usage still spells it (with WSR7's `--all`, D112).
    /// </summary>
    [Fact]
    public void The_headless_hosts_trees_sync_is_the_sync_kinds_door()
    {
        Assert.Contains("sync [--repository <name>] [--workspace <name>] [--all] [--yes]", DriverCommand.Usage);
        Assert.Contains("sync", HelpProposalKinds.Find("sync")!.Doors);
    }

    /// <summary>
    /// LAYOUT7: the headless host's <c>setup</c> is a door owed to LAYOUT8's <c>setup</c> kind (D110), while the host's usage
    /// spells it and no kind of that name is built; the room names it meanwhile, so the helper can point the person at it.
    /// When LAYOUT8 lands the kind, this owed door becomes its door.
    /// </summary>
    [Fact]
    public void The_headless_hosts_setup_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("setup <repository> [--plan]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver setup <repository> [--plan]`", StringComparison.Ordinal));
        Assert.Contains("LAYOUT8", SetupDoor.Reason);
    }

    /// <summary>
    /// WSSETUP5: the headless host's <c>register</c> is a door owed to the <c>setup</c> kind's <c>register</c> (D124 §4.5), while
    /// the host's usage spells it and no kind of that name is built; the room names it meanwhile.
    /// </summary>
    [Fact]
    public void The_headless_hosts_register_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("register [--repository <name>]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver register [--repository <name>]`", StringComparison.Ordinal));
        Assert.Contains("WSSETUP7", RegisterDoor.Reason);
    }

    /// <summary>
    /// WSSETUP6: the headless host's <c>setup --workspace</c>, its press and its pause, resume and stop, are doors owed to the
    /// <c>setup</c> kind (D124 §4.5) while the host's usage spells them and no kind of that name is built; the room names both
    /// forms meanwhile, so the helper can point the person at them. The screen's route is WSSETUP7's too.
    /// </summary>
    [Fact]
    public void The_headless_hosts_workspace_plan_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("setup --workspace <name> [--plan]", DriverCommand.Usage);
        Assert.Contains("setup --workspace <name> --pause | --resume | --stop", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver setup --workspace <name> [--plan]", StringComparison.Ordinal)
            && door.Terminal.Contains("--pause|--resume|--stop", StringComparison.Ordinal));
        Assert.Contains("WSSETUP7", WorkspaceDoor.Reason);
    }

    /// <summary>
    /// DRIFT1d2: the headless host's <c>quest accept</c> (D133 §4), owed since DRIFT1d, is the <c>accept</c> kind's door while
    /// the host's usage spells it, and the room names its screen, the quest page's yes, beside its command.
    /// </summary>
    [Fact]
    public void The_headless_hosts_quest_accept_is_the_accept_kinds_door()
    {
        Assert.Contains("quest accept <id>", DriverCommand.Usage);
        Assert.Contains("accept", HelpProposalKinds.Find("accept")!.Doors);
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver quest accept <id>`", StringComparison.Ordinal)
            && door.Screen.Contains("Accept the departure", StringComparison.Ordinal));
    }

    /// <summary>
    /// EVID1b: the headless host's <c>quest check</c> (D144 §5) is a door owed to EVID1c's check card while the host's usage
    /// spells it and no kind takes it; the room names it meanwhile, so the helper can point the person at it.
    /// </summary>
    [Fact]
    public void The_headless_hosts_opinion_verbs_are_the_persons_own_and_exempt()
    {
        Assert.Contains("opinion ask <session> [--reviewer <adapter>] [--same-agent]", DriverCommand.Usage);
        Assert.Contains("opinion stop <opinion>", DriverCommand.Usage);
        Assert.Contains("opinion anyway <session>", DriverCommand.Usage);
        Assert.Contains("opinion myself <session>", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("opinion"));
        Assert.Contains("second-agent design §9", OpinionAskDoor.Reason);
        Assert.Contains("D110", OpinionJudgementDoor.Reason);
    }

    [Fact]
    public void The_headless_hosts_quest_check_is_a_door_owed_to_the_check_card()
    {
        Assert.Contains("quest check <id> [--commit <sha>]", DriverCommand.Usage);
        Assert.DoesNotContain("check", HelpProposalKinds.Find("accept")!.Doors);
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver quest check <id> [--commit <sha>]`", StringComparison.Ordinal));
        Assert.Contains("EVID1c", CheckDoor.Reason);
    }

    /// <summary>
    /// PAUSE1b: the headless host's pause and resume are doors owed to PAUSE1f's <c>pause</c> kind (D132 §7.4) while the host's
    /// usage spells them and no kind of that name is built; the room names them meanwhile, so the helper can point at them.
    /// </summary>
    [Fact]
    public void The_headless_hosts_pause_and_resume_are_doors_owed_to_the_pause_kind()
    {
        Assert.Contains("ask --pause <id>  ·  ask --resume <id>  ·  quest pause <id>  ·  quest resume <id>", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("pause"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver ask --pause|--resume <id>`", StringComparison.Ordinal)
            && door.Terminal.Contains("`daoris-driver quest pause|resume <id>`", StringComparison.Ordinal));
        Assert.Contains("PAUSE1f", PauseDoor.Reason);
    }

    /// <summary>
    /// PAUSE1d: the headless host's abandon is exempt from Ask Daoris (D132 §7.4) while its usage spells it; the room names it,
    /// marked exempt with its reason, so the helper points the person at the ask's page and the terminal line instead.
    /// </summary>
    [Fact]
    public void The_headless_hosts_abandon_is_exempt_and_the_room_says_so()
    {
        Assert.Contains("ask --abandon <id> [--reason \"…\" --yes]  ·  quest abandon <id> [--reason \"…\" --yes]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("abandon"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver ask --abandon <id> [--reason \"…\" --yes]`", StringComparison.Ordinal)
            && door.Terminal.Contains("`daoris-driver quest abandon <id> [--reason \"…\" --yes]`", StringComparison.Ordinal)
            && door.To.Contains("Ask Daoris never proposes it", StringComparison.Ordinal));
        Assert.Contains("D37", AbandonDoor.Reason);
    }

    /// <summary>
    /// MSG1e: the headless host's <c>sessions say</c> is exempt from Ask Daoris (D137 §5.4) while its usage spells it; the room
    /// names it, marked exempt with its reason, and says where the box is, so the helper points the person at both doors.
    /// </summary>
    [Fact]
    public void The_headless_hosts_say_is_exempt_and_the_room_says_so()
    {
        Assert.Contains("sessions say <id> \"…\" [--file <path>]…", DriverCommand.Usage);
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver sessions say <id> \"…\" [--file <path>]…`", StringComparison.Ordinal)
            && door.Screen.Contains("box", StringComparison.Ordinal)
            && door.To.Contains("Ask Daoris never proposes it", StringComparison.Ordinal));
        Assert.Contains("D133", SayDoor.Reason);
    }

    /// <summary>
    /// ASKHIST1: the headless host's <c>help</c> is exempt from Ask Daoris while its usage spells it, as the panel's history is:
    /// the room names both doors, marked exempt with the reason, and no kind of that name takes it.
    /// </summary>
    [Fact]
    public void The_headless_hosts_help_history_is_exempt_and_the_room_says_so()
    {
        Assert.Contains("help list [--search \"…\"] [--json]", DriverCommand.Usage);
        Assert.Contains("help rename <id> \"…\" | --clear", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("help"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver help list [--search \"…\"]`", StringComparison.Ordinal)
            && door.Terminal.Contains("`daoris-driver help rename|pin|unpin|delete <id>`", StringComparison.Ordinal)
            && door.Screen.Contains("History", StringComparison.Ordinal)
            && door.To.Contains("Ask Daoris never proposes it", StringComparison.Ordinal));
        Assert.Contains("ASKHIST1", HistoryDoor.Reason);
        Assert.All(["useRenameHelp", "usePinHelp", "useHelpStartFrom"], hook => Assert.Same(HistoryDoor, Elsewhere.Single(row => row.Hook == hook).Answer));
    }

    /// <summary>
    /// MSG1g: the headless host's <c>sessions go-on-new</c> is exempt from Ask Daoris while its usage spells it: the person's
    /// words, and the person's choice to give up their conversation for time, with no kind of that name to take it.
    /// </summary>
    [Fact]
    public void The_headless_hosts_go_on_new_is_exempt_as_the_person_s_choice()
    {
        Assert.Contains("sessions go-on-new <id>", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("go-on-new"));
        Assert.Contains("D133", GoOnNewDoor.Reason);
        Assert.Contains("D143", GoOnNewDoor.Reason);
    }

    /// <summary>
    /// MSG1f3: the headless host's <c>sessions start-from</c> is exempt from Ask Daoris while its usage spells it, as the
    /// screen's *Start a conversation with these words* is: the person's words, moved by the person's press, with no kind of
    /// that name to take it.
    /// </summary>
    [Fact]
    public void The_headless_hosts_start_from_is_exempt_as_the_person_s_press()
    {
        Assert.Contains("sessions start-from <id>", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("start-from"));
        Assert.Contains("D133", StartFromDoor.Reason);
        Assert.Contains("D137", StartFromDoor.Reason);
    }

    /// <summary>
    /// UPDATE1f: the install's update is exempt from Ask Daoris (D139), the screen's three words and the headless host's
    /// <c>update</c> alike, while its usage spells it; the room names it, marked exempt with its reason, and says where the
    /// three words are, so the helper points the person at both doors.
    /// </summary>
    [Fact]
    public void The_install_update_is_exempt_and_the_room_says_so()
    {
        Assert.Contains("update [--install <folder>]  ·  update --when-idle | --now | --cancel [--install <folder>]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("update"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver update --when-idle|--now|--cancel`", StringComparison.Ordinal)
            && door.Terminal.Contains("`daoris-driver update`", StringComparison.Ordinal)
            && new[] { "Settings → Driver", "*Update when idle*", "*Update now*", "*Not now*" }.All(word => door.Screen.Contains(word, StringComparison.Ordinal))
            && door.To.Contains("Ask Daoris never proposes it", StringComparison.Ordinal));
        Assert.Contains("D139", UpdateDoor.Reason);
        Assert.Same(UpdateDoor, Controls.Single(row => row.Hook == "useSayUpdate").Answer);
    }

    /// <summary>
    /// UPDATE1f: a hook no Settings domain presses is answered for one by one, and only while the bridge still exports it, it
    /// still changes something, and no domain presses it, where it would be read as a control instead.
    /// </summary>
    [Fact]
    public void Every_hook_answered_outside_settings_still_changes_something_no_domain_presses()
    {
        var hooks = BridgeHooks();
        var pressed = Domains().Values
            .SelectMany(files => files)
            .SelectMany(Pressed)
            .ToHashSet(StringComparer.Ordinal);

        var page = Presses().Select(press => press.Hook).ToHashSet(StringComparer.Ordinal);
        foreach (var (hook, _) in Elsewhere)
        {
            Assert.True(hooks.TryGetValue(hook, out var read) && read.Changes, $"`{hook}` is answered for as a hook that changes something, and the bridge exports none.");
            Assert.False(pressed.Contains(hook), $"`{hook}` is pressed by a Settings domain now, so it is a control there, not answered here.");
            // ENTRY1a: the page is read whole now, so a row whose press left it is said too.
            Assert.True(page.Contains(hook), $"`{hook}` is answered for as a press, and nothing on the page presses it.");
        }

        Assert.Equal(Elsewhere.Length, Elsewhere.Select(row => row.Hook).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// ENTRY1a (D161's ENTRY1 note, the ENTRY1 audit §1): every start the page offers is answered for, not only a Settings
    /// domain's. Each hook that changes something, pressed anywhere on the page outside the files that define the hooks, is a
    /// door, an exemption, a door owed or the quest's or session's own conversation, by where it is pressed, unless a domain's
    /// control or <see cref="Elsewhere"/> answers the same act already.
    /// </summary>
    [Fact]
    public void Every_start_on_the_page_is_a_door_owed_exempt_or_its_own_conversation()
    {
        var hooks = BridgeHooks();
        var found = new List<string>();
        foreach (var (where, hook) in Presses().Where(press => hooks.TryGetValue(press.Hook, out var read) && read.Changes))
        {
            IReadOnlyList<string?> actions = hooks[hook].Actions.Count == 0 ? [null] : [.. hooks[hook].Actions];
            found.AddRange(actions
                .Where(action => !Elsewhere.Any(row => row.Hook == hook) && !Controls.Any(row => row.Hook == hook && row.Action == action))
                .Select(action => Control(where, hook, action)));
        }

        Same("Starts on the page", found, Starts.Select(row => Control(row.Where, row.Hook, row.Action)));
    }

    /// <summary>
    /// ENTRY1a: the page's own service calls (`queries.ts`) are read as the bridge's hooks are, a hook that calls its own
    /// file's helper that changes something included (`useDeleteQuest` calls `useDelete`), so a quest's, an ask's and a
    /// repository's starts are answered for rather than unseen; a read changes nothing.
    /// </summary>
    [Fact]
    public void The_pages_own_service_calls_are_read_as_the_bridges_hooks_are()
    {
        var hooks = BridgeHooks();

        Assert.True(hooks.TryGetValue("useDeleteQuest", out var delete) && delete.Changes);
        Assert.True(hooks.TryGetValue("useAnswerGoAhead", out var goAhead) && goAhead.Changes);
        Assert.True(hooks.TryGetValue("useRespondQuest", out var respond) && respond.Changes);
        Assert.Equal(new[] { "take", "done", "decline" }, respond.Actions);
        Assert.True(hooks.TryGetValue("useQuests", out var quests) && !quests.Changes);
        Assert.DoesNotContain("useDelete", hooks.Keys);
    }

    /// <summary>
    /// ENTRY1a: a file that defines the hooks (the bridge, `queries.ts`, `shell.ts`) presses none of them, though its text
    /// names them all; read as a press, every domain importing `queries.ts` would press every service call there.
    /// </summary>
    [Fact]
    public void A_file_that_defines_the_hooks_presses_none_of_them()
    {
        var ai = Domains()["ai"];

        Assert.Contains(ai, file => Path.GetFileName(file) == "queries.ts");
        Assert.DoesNotContain("useDeleteQuest", ai.SelectMany(Pressed));
        Assert.Empty(Pressed(Path.Combine(Page, "queries.ts")));
        Assert.Contains(Presses(), press => press.Hook == "useRespondQuest");
    }

    /// <summary>
    /// ENTRY1a: a quest's or a session's own conversation names the view a go reaches it by (D161's ENTRY1 note), so Ask
    /// Daoris can take the person there; a go below the view is ENTRY1b's.
    /// </summary>
    [Fact]
    public void Every_own_conversation_names_a_view_a_go_reaches()
    {
        var conversations = Starts.Select(row => row.Answer).OfType<Conversation>().ToList();

        Assert.NotEmpty(conversations);
        foreach (var conversation in conversations)
        {
            Assert.True(HelpPlaces.Views.Any(view => view.Id == conversation.Place),
                $"`{conversation.Place}` is not a view a go reaches: one of {string.Join(", ", HelpPlaces.Views.Select(view => view.Id))}.");
        }
    }

    /// <summary>
    /// TRACE1: the headless host's <c>trace</c> is exempt from Ask Daoris (D110 §4, D143) while its usage spells it: a read that
    /// changes nothing, with no kind of that name to take it.
    /// </summary>
    [Fact]
    public void The_headless_hosts_trace_is_exempt_since_it_changes_nothing()
    {
        Assert.Contains("trace <commit|session|quest>  ·  trace commit|session|quest <id>", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("trace"));
        Assert.Contains("changes nothing", TraceDoor.Reason);
        Assert.Contains("D143", TraceDoor.Reason);
    }

    /// <summary>
    /// WORKFLOW1c2: the headless host's <c>workflow run</c> is exempt from Ask Daoris (D157, D110 §4) while its usage spells it: a
    /// read that changes nothing. The <c>workflow</c> kind WORKFLOW1h builds takes a named workflow's saves, and never this read.
    /// </summary>
    [Fact]
    public void The_headless_hosts_workflow_run_is_exempt_since_it_changes_nothing()
    {
        Assert.Contains("workflow run --session <id> | --quest <id> | --ask <id>", DriverCommand.Usage);
        Assert.DoesNotContain("run", HelpProposalKinds.Find("workflow")?.Doors ?? []);
        Assert.Contains("changes nothing", WorkflowRunDoor.Reason);
        Assert.Contains("D157", WorkflowRunDoor.Reason);
    }

    /// <summary>
    /// WORKFLOW1f: the headless host's <c>workflow keep</c> is exempt from Ask Daoris (D157 point 10, D110) while its usage spells
    /// it and the room names it: the person's say-so, never a card's.
    /// </summary>
    [Fact]
    public void The_headless_hosts_workflow_keep_is_exempt_since_it_is_the_persons_say_so()
    {
        Assert.Contains("workflow keep <session> [\"…\"]", DriverCommand.Usage);
        Assert.DoesNotContain("keep", HelpProposalKinds.Find("workflow")?.Doors ?? []);
        Assert.Contains("say-so", WorkflowKeepDoor.Reason);
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver workflow keep <session> [\"…\"]`", StringComparison.Ordinal));
    }

    /// <summary>
    /// GIT1c: the headless host's <c>git branches</c> is exempt from Ask Daoris (D147 §3.3, D110 §4) while its usage spells it:
    /// a read that changes nothing. The <c>git</c> kind GIT1k builds takes the acts, and never the read.
    /// </summary>
    [Fact]
    public void The_headless_hosts_git_branches_is_exempt_since_it_changes_nothing()
    {
        Assert.Contains("git branches [--repository <name>] [--all] [--json]", DriverCommand.Usage);
        Assert.DoesNotContain("branches", HelpProposalKinds.Find("git")?.Doors ?? []);
        Assert.Contains("changes nothing", GitBranchesDoor.Reason);
        Assert.Contains("D147", GitBranchesDoor.Reason);
    }

    /// <summary>
    /// PLUGHOOK1c: the headless host's <c>trees state</c> is exempt from Ask Daoris (D148, D110 §4) while its usage spells it: it
    /// asks and keeps, and changes nothing of the person's or on the platform. The room names it, marked exempt, so the helper
    /// points the person at it, and its room reads the kept answer for itself.
    /// </summary>
    [Fact]
    public void The_headless_hosts_trees_state_is_exempt_and_the_room_says_so()
    {
        Assert.Contains("state <session|branch> [--repository <name>]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("state"));
        Assert.DoesNotContain("state", HelpProposalKinds.Find("sync")!.Doors);
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver trees state <session|branch> [--repository <name>]`", StringComparison.Ordinal)
            && door.To.Contains("Ask Daoris never proposes it", StringComparison.Ordinal));
        Assert.Contains("changes nothing", TreesStateDoor.Reason);
        Assert.Contains("D148", TreesStateDoor.Reason);
    }

    /// <summary>
    /// SESSUX1a: the archive's bridge hook changes something, so it is a door owed to SESSUX1h's <c>session</c> kind (D126
    /// §7.3) while no kind of that name is built. When the kind lands, this owed door becomes its door; Sessions' press of it
    /// is read with every other start since ENTRY1a.
    /// </summary>
    [Fact]
    public void The_session_archive_is_a_door_owed_to_the_session_kind()
    {
        var hooks = BridgeHooks();

        Assert.True(hooks.TryGetValue("useArchiveSessions", out var archive) && archive.Changes);
        Assert.Null(HelpProposalKinds.Find("session"));
        Assert.Contains("SESSUX1h", SessionArchiveDoor.Reason);
    }

    /// <summary>
    /// UPDATE1e: a hook exported as a function is read as one exported as a constant is, whether it changes something
    /// included, so the update's words on Settings → Driver are answered for rather than unseen.
    /// </summary>
    [Fact]
    public void A_hook_exported_as_a_function_is_read_as_one_exported_as_a_constant_is()
    {
        var hooks = BridgeHooks();

        Assert.True(hooks.TryGetValue("useSayUpdate", out var say) && say.Changes);
        Assert.True(hooks.TryGetValue("useUpdateState", out var state) && !state.Changes);
        Assert.Contains("useSetNotify", hooks.Keys);
    }

    /// <summary>
    /// SESSUX1f: the delete's bridge hook changes something, and the `delete` kind takes no `session` door yet, so it is a door
    /// owed to SESSUX1h (D126 §7.3). When the door lands, this owed door becomes it.
    /// </summary>
    [Fact]
    public void The_session_delete_is_a_door_owed_to_the_delete_kinds_session_door()
    {
        var hooks = BridgeHooks();

        Assert.True(hooks.TryGetValue("useDeleteSession", out var delete) && delete.Changes);
        Assert.DoesNotContain("session", HelpProposalKinds.Find("delete")!.Doors);
        Assert.Contains("SESSUX1h", SessionDeleteDoor.Reason);
    }

    /// <summary>
    /// SESSUX1g: the headless host's <c>sessions</c> verbs are doors owed to SESSUX1h's <c>session</c> kind (D126 §7.3) while
    /// the host's usage spells them and no kind of that name is built.
    /// </summary>
    [Fact]
    public void The_headless_hosts_sessions_verbs_are_doors_owed_to_the_session_kind()
    {
        Assert.Contains("sessions stop <id>", DriverCommand.Usage);
        Assert.Contains("sessions archive <id>… | --ended [--yes]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("session"));
        Assert.Contains("SESSUX1h", SessionsVerbDoor.Reason);
    }

    /// <summary>
    /// HIST1d: the headless host's history clears are doors owed to HIST1f's <c>clear</c> kind (D153, design §6.4) while the
    /// host's usage spells them and no kind of that name is built. When the kind lands, this owed door becomes its doors.
    /// </summary>
    [Fact]
    public void The_headless_hosts_history_clears_are_doors_owed_to_the_clear_kind()
    {
        Assert.Contains("history clear --workspace <name> [--yes]", DriverCommand.Usage);
        Assert.Contains("quest clear <id> [--failed] [--yes]  ·  ask --clear <id> [--yes]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("clear"));
        Assert.Contains("HIST1f", ClearDoor.Reason);
        Assert.Contains("§6.4", ClearDoor.Reason);
    }

    /// <summary>
    /// LEFT3: a form of a control is owed only while its control is answered for, as a door its kind takes, the CLI's
    /// usage still spells its terminal door, and the room names that door so the helper can point at it meanwhile.
    /// </summary>
    [Fact]
    public void Every_form_owed_is_of_a_door_answered_for_and_named_in_the_room()
    {
        var usage = File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", "agent.ts"));
        var room = string.Join("\n", HelpRoomDoors.Doors.Select(door => door.Terminal));

        foreach (var (control, form, terminal, answer) in Forms)
        {
            Assert.IsType<Owed>(answer);
            Assert.IsType<Door>(Controls.Single(row => Control(row.Domain, row.Hook, row.Action) == control).Answer);
            var spelled = terminal.Replace("daoris agent ", "", StringComparison.Ordinal);
            Assert.True(usage.Contains(spelled, StringComparison.Ordinal), $"the CLI no longer spells `{terminal}` ({form}).");
            Assert.True(room.Contains(terminal, StringComparison.Ordinal), $"the room's doors do not name `{terminal}` ({form}).");
        }
    }

    /// <summary>
    /// ACCT1, ACCT2: the terminal's new account doors — <c>profile rename</c>, <c>profile join</c>, and a sign-in's
    /// <c>--join</c> and <c>--name</c> — are answered for while the CLI's usage spells them, and the room names each, so the
    /// helper points the person at them before any screen presses them. A sign-in stays exempt (D89).
    /// </summary>
    [Fact]
    public void The_account_name_and_join_doors_are_answered_for_and_named_in_the_room()
    {
        var usage = File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", "agent.ts"));
        var room = string.Join("\n", HelpRoomDoors.Doors.Select(door => door.Terminal));

        Assert.Contains("profile rename <agent> <account> <name>", usage);
        Assert.Contains("profile join <agent> <account> [<workspace>…] [--machine]", usage);
        Assert.Contains("login <agent> --new [--name N] [--join W] [--join-machine]", usage);
        Assert.Contains("`daoris agent profile rename <agent> <account> <name>`", room);
        Assert.Contains("`daoris agent profile join <agent> <account> <workspace>…|--machine`", room);
        Assert.Contains("`daoris agent login <agent> --new [--name <name>] [--join <workspace>] [--join-machine]`", room);
        Assert.Contains("D66", RenameDoor.Reason);
        Assert.Contains("daoris agent profile join", JoinDoor.Reason);
        Assert.IsType<Exempt>(Controls.Single(row => row.Hook == "useHarnessAction" && row.Action == "login-new").Answer);
        // UX7b: the page presses both, so each is its control's row.
        Assert.Same(RenameDoor, Controls.Single(row => row.Hook == "useHarnessAction" && row.Action == "profile-rename").Answer);
        Assert.Same(JoinDoor, Controls.Single(row => row.Hook == "useHarnessAction" && row.Action == "profile-join").Answer);
    }

    /// <summary>DRV8's <c>--share</c>, decided (D110): exempt, while the headless loop's usage still names it.</summary>
    [Fact]
    public void The_driver_locks_share_flag_is_decided()
    {
        Assert.Contains("[--share]", DriverCommand.Usage);
        Assert.Contains("not a setting", Share.Reason);
    }

    /// <summary>
    /// Every verb that changes something is named in the room's doors, a door owed included, so the helper can point
    /// the person at the screen and the command even where it cannot propose.
    /// </summary>
    [Fact]
    public void Every_driver_verb_that_changes_something_is_named_in_the_rooms_doors()
    {
        var named = HelpRoomDoors.Doors
            .SelectMany(door => RoomVerb().Matches(door.Terminal).SelectMany(match => match.Groups[1].Value.Split('|')))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (verb, _) in Verbs.Where(row => row.Answer is not Exempt))
        {
            var head = verb.Split(' ')[0];
            Assert.True(named.Contains(head), $"the room's doors name no `daoris driver {head}`.");
        }
    }

    private static string Control(string domain, string hook, string? action) =>
        action is null ? $"{domain}: {hook}" : $"{domain}: {hook} {action}";

    /// <summary>Two sets the same, with what one lacks named — a control found and answered for nowhere, or a row whose control is gone.</summary>
    private static void Same(string what, IEnumerable<string> found, IEnumerable<string> answered)
    {
        var have = found.ToHashSet(StringComparer.Ordinal);
        var rows = answered.ToList();
        var unanswered = have.Except(rows, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var stale = rows.Except(have, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(unanswered.Count == 0,
            $"{what} with no Ask Daoris door and no reason: {string.Join("; ", unanswered)}. Build the door, or say why not here.");
        Assert.True(stale.Count == 0, $"{what} answered for that no longer exist: {string.Join("; ", stale)}.");
        Assert.Equal(rows.Count, rows.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The verbs the CLI's command table spells, from its usage: each line at the verbs' column, its alternatives
    /// (<c>drive|undrive</c>), the verbs a <c>·</c> puts on one line, and a verb's second word where one follows its
    /// operand (<c>across &lt;repo&gt; read</c>).
    /// </summary>
    private static IReadOnlyList<string> DriverVerbs(string table)
    {
        var verbs = new List<string>();
        foreach (Match line in UsageVerbLine().Matches(table))
        {
            foreach (var spelled in line.Groups[1].Value.Split(" · "))
            {
                var words = spelled.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var second = words.Skip(1).FirstOrDefault(word => PlainWord().IsMatch(word));
                verbs.AddRange(words[0].Split('|').Select(verb => second is null ? verb : $"{verb} {second}"));
            }
        }

        Assert.NotEmpty(verbs);
        return verbs;
    }

    /// <summary>
    /// Each exported hook of the page's bridge and of its own service calls (`queries.ts`, ENTRY1a): whether it changes
    /// something, and the actions its payload names.
    /// </summary>
    private static Dictionary<string, (bool Changes, IReadOnlyList<string> Actions)> BridgeHooks()
    {
        var hooks = new Dictionary<string, (bool, IReadOnlyList<string>)>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Page, "bridge"), "*.ts").Where(file => !file.Contains(".test.", StringComparison.Ordinal))
            .Append(Path.Combine(Page, "queries.ts")))
        {
            var parts = TopLevel().Split(File.ReadAllText(file).Replace("\r\n", "\n"));
            // ENTRY1a: a hook that calls a helper of its own file that changes something changes something too: `useDeleteQuest`
            // is `() => useDelete(…)`, which neither `useMutation(` nor a `…Change` helper's name says.
            var helpers = parts
                .Where(part => Changes().IsMatch(part))
                .Select(part => LocalHook().Match(part))
                .Where(local => local.Success)
                .Select(local => local.Groups["hook"].Value)
                .ToList();
            foreach (var part in parts)
            {
                if (ExportedHook().Match(part) is not { Success: true } exported) continue;
                var actions = ActionUnion().Matches(part)
                    .SelectMany(union => Quoted().Matches(union.Groups[1].Value).Select(action => action.Groups[1].Value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var changes = Changes().IsMatch(part)
                    || helpers.Any(helper => Regex.IsMatch(part, $@"\b{helper}\s*[(<]", RegexOptions.None, TimeSpan.FromSeconds(1)));
                hooks[exported.Groups["hook"].Value] = (changes, actions);
            }
        }

        Assert.Contains("useSetReadAcross", hooks.Keys);
        Assert.Contains("usePublishQuest", hooks.Keys);
        return hooks;
    }

    /// <summary>
    /// ENTRY1a: the hooks a file presses, by name, and none for a file that defines them: the bridge, the page's own service
    /// calls (`queries.ts`) and the shell's names (`shell.ts`). Their text names every hook they hold, so read as presses,
    /// each domain that imports `queries.ts` (<see cref="Domains"/> keeps it among its files) would press every service call.
    /// </summary>
    private static IEnumerable<string> Pressed(string file) =>
        Defines(file) ? [] : HookUse().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value);

    private static bool Defines(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bridge{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || Path.GetFileName(file) is "shell.ts" or "queries.ts";

    /// <summary>
    /// ENTRY1a: every hook pressed on the page, by where: each source file but the tests, the stories, the test set-up and the
    /// files that define the hooks (<see cref="Pressed"/>), named by its folder under the page's source, or by its own name
    /// at the root (`App`, `QuestsView`), so a start added anywhere is read, a Settings domain's files among them.
    /// </summary>
    private static IReadOnlyList<(string Where, string Hook)> Presses()
    {
        var presses = new HashSet<(string, string)>();
        foreach (var file in Directory.GetFiles(Page, "*.ts*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Page, file);
            var name = Path.GetFileName(file);
            if (name.Contains(".test.", StringComparison.Ordinal) || name.Contains(".stories.", StringComparison.Ordinal)
                || name.EndsWith(".d.ts", StringComparison.Ordinal) || !(name.EndsWith(".ts", StringComparison.Ordinal) || name.EndsWith(".tsx", StringComparison.Ordinal))
                || relative.StartsWith("test" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            var segments = relative.Split(Path.DirectorySeparatorChar);
            var where = segments.Length == 1 ? Path.GetFileNameWithoutExtension(name) : segments[0];
            foreach (var hook in Pressed(file)) presses.Add((where, hook));
        }

        return [.. presses.Order()];
    }

    /// <summary>
    /// The places whose controls left a Settings domain for a place of their own, read as that domain was, under its id
    /// (UX6e, D150 §5): the Agents place, from its view's file and everything it imports, so the controls the Settings Agents domain held
    /// stay answered for where they are pressed now. And Repositories (HELPSETUP1), whose view presses a repository's Setup
    /// (UX6f, D150 §4.2): its line, landing, language and reach left Settings → Workspace and Permissions for it, beside its
    /// driving and standing answer, and its drawers' controls come with the view. Since UX6g it holds a workspace's page
    /// too (`projects/WorkspaceView`, §4.3), imported by the view, so the rest of those two domains' controls, a
    /// workspace's defaults, remote, rules and Branches, are read with it, and neither domain is in Settings any more.
    /// </summary>
    private static readonly (string Id, string File)[] Places =
    [
        ("agents", Path.Combine("agents", "AgentsView")),
        ("projects", "ProjectsView"),
        // UX6j (D150 §2.3): Settings → Plugins retired into the Plugins place, whose view presses the same eight hooks.
        ("plugins", Path.Combine("plugins", "PluginsView")),
    ];

    /// <summary>
    /// Each Settings domain, by its id, with its component's file and every file that file imports, and so on: not
    /// the bridge, whose hooks are read by name, and not a type-only import, which presses nothing. A place that took a
    /// domain's controls (<see cref="Places"/>) is read the same way.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> Domains()
    {
        var settings = Path.Combine(Page, "settings");
        var registry = File.ReadAllText(Path.Combine(settings, "domains.ts"));
        var imported = DomainImport().Matches(registry).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        var domains = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (Match row in DomainRow().Matches(registry))
        {
            domains[row.Groups[1].Value] = Closure(Path.GetFullPath(Path.Combine(settings, imported[row.Groups[2].Value])));
        }

        foreach (var (id, file) in Places)
        {
            domains[id] = Closure(Path.GetFullPath(Path.Combine(Page, file)));
        }

        // The registry was read: a domain it still lists (UX6g took Workspace and Permissions out of it).
        Assert.Contains("driver", domains.Keys);
        Assert.DoesNotContain("workspace", domains.Keys);
        Assert.DoesNotContain("permissions", domains.Keys);
        return domains;

        static IReadOnlyList<string> Closure(string start)
        {
            var seen = new List<string>();
            var next = new Stack<string>([start]);
            while (next.TryPop(out var named))
            {
                var file = new[] { named + ".tsx", named + ".ts", named }.FirstOrDefault(File.Exists);
                if (file is null || seen.Contains(file)) continue;
                seen.Add(file);
                var name = Path.GetFileName(file);
                if (file.Contains($"{Path.DirectorySeparatorChar}bridge{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || name is "shell.ts" or "queries.ts")
                {
                    continue;
                }

                foreach (Match import in ValueImport().Matches(File.ReadAllText(file)))
                {
                    next.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, import.Groups[1].Value)));
                }
            }

            return seen;
        }
    }

    // A usage line at the verbs' column: 25 spaces, then the verb's spelling up to the gap before its help.
    [GeneratedRegex(@"^\s*' {25}([a-z][^' ]*(?: [^ ']+)*?)(?: {2,}[^']*)?',?\s*$", RegexOptions.Multiline)]
    private static partial Regex UsageVerbLine();

    [GeneratedRegex("^[a-z][a-z-]*$")]
    private static partial Regex PlainWord();

    [GeneratedRegex(@"daoris driver ([a-z|-]+)")]
    private static partial Regex RoomVerb();

    [GeneratedRegex(@"\n(?=export |const |function |type |/\*\*|//)")]
    private static partial Regex TopLevel();

    // Both forms a hook is exported in (UPDATE1e): `export const useX =` and `export function useX(`, which the update's
    // bridge uses, and which this read alone missed, leaving Settings → Driver's three words answered for nowhere.
    [GeneratedRegex(@"^export (?:const (?<hook>use[A-Z]\w*)\s*=|function (?<hook>use[A-Z]\w*)\s*[(<])")]
    private static partial Regex ExportedHook();

    [GeneratedRegex(@"useMutation\(|\buse[A-Z]\w*Change\b")]
    private static partial Regex Changes();

    // ENTRY1a: a hook a file keeps to itself, read only for whether an exported hook calling it changes something.
    [GeneratedRegex(@"^(?:function (?<hook>use[A-Z]\w*)\s*[(<]|const (?<hook>use[A-Z]\w*)\s*=)")]
    private static partial Regex LocalHook();

    [GeneratedRegex(@"\baction\s*:\s*('[a-z-]+'(?:\s*\|\s*'[a-z-]+')*)")]
    private static partial Regex ActionUnion();

    [GeneratedRegex("'([a-z-]+)'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\b(use[A-Z]\w*)\b")]
    private static partial Regex HookUse();

    [GeneratedRegex(@"^import \{ (\w+) \} from '([^']+)';", RegexOptions.Multiline)]
    private static partial Regex DomainImport();

    [GeneratedRegex(@"\{ id: '(\w+)'[^}]*?component: (\w+) \}")]
    private static partial Regex DomainRow();

    [GeneratedRegex(@"^import (?!type )[^;]*?from '(\.{1,2}/[^']+)'", RegexOptions.Multiline)]
    private static partial Regex ValueImport();
}
