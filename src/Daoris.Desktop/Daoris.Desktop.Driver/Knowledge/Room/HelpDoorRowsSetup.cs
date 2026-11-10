namespace Daoris.Driver;

/// <summary>The doors for setting a machine, a repository and its workflow up: what a place follows, where work is shown, and how it lands.</summary>
internal static class HelpDoorRowsSetup
{
    private const string Setup = HelpDoorPlaces.Setup;
    private const string Ws = HelpDoorPlaces.Ws;

    internal static readonly HelpDoor[] Rows =
    [
        // The page hands the helper a first message asking to be walked through it, naming the steps by these titles, so it is
        // told the guide exists and what each step is done on.
        ("walk through setting this machine up: an agent, Ask Daoris's agent, a workspace and its repositories, "
            + "setting a place up, what agents may do", "Settings → Get started (Help → *Get started*)",
            "(each step shows its own command there)"),
        // A repository's own values are on its Setup; a workspace's are on its page.
        ("drive a repository, or stop", Setup + "Driving", "`daoris place drive|undrive <repository>`"),
        // A repository's hold, never a pause; a pause is an ask's or a quest's, and stops what runs.
        ("hold one, so nothing new starts there, or resume it", Setup + "Driving", "`daoris place hold|resume <repository>`"),
        // One door adopts a repository: one quest to its own session, which takes up the doctrine, writes down what the repository
        // owns and its brief on its own branch. Ask Daoris's `setup` kind proposes it through its `place` door.
        new("set a repository up for every agent: one quest to its own session, which takes up the doctrine, writes down what "
            + "the repository owns and its brief on its own branch (`--plan` shows what was read and the quest, and asks nothing)",
            "Agents → Setup → *Set up…* (adoption ticked)",
            "`daoris place <repository>… --preset keep --adoption [--what-it-builds <kind>] --plan`, then `--apply`",
            new HelpDoorAsk.Proposes("setup", "place")),
        // The workspace plan's terminal door; its screen and Ask Daoris's doors are owed.
        ("set every repository of a workspace up, one at a time, the ones other work touches first: a plan the loop works, "
            + "pausing after a pilot of two (`--plan` shows the list and each refusal, and writes nothing); pause, resume or stop it",
            "(no screen yet)",
            "`daoris place --workspace <name> --adoption [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]`, "
            + "`daoris place --workspace <name> --pause|--resume|--stop`"),
        // Registration follows the line on its own (a start, a line Daoris moves); this is the person's ask.
        ("register a repository from what its line declares, as `connect` would, without running it: after a set-up's "
            + "branch is merged and brought up to date, or a declaration changed outside Daoris",
            "(no screen yet)", "`daoris repository register [--repository <name>]`"),
        ("set the line its work grows from and lands on", Setup + "Line and landing; a workspace's, " + Ws + "Setup → Defaults → Line",
            "`daoris place line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        // The ticket's key and the squash, which Ask Daoris's `landing` door takes too, keeping the ones set there that a card does
        // not name. The old landing rule serves only a place still on Current; converting retires it.
        ("set how accepted work lands: this serves only a place still on Current, which follows its old landing rule (a place that "
            + "follows a saved workflow lands as that workflow's `land` step says), and converting retires the rule",
            Setup + "Line and landing; a workspace's, " + Ws + "Setup → Defaults → How work lands",
            "`daoris place landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`, "
            + "`--auto-accept`: a quest's done lands it with no press, `--key <shape>|--no-key`: the shape a `{ticket}` in the "
            + "pattern is found by in the title or the request, the session's id where none is, and `--squash|--no-squash`: "
            + "Accept makes the pull request's branch one commit of the session's work)"),
        // How work is committed; Ask Daoris proposes it as the `setting` kind's `commits` door, its convention seeded from the
        // repository's own instructions where the person asks.
        new("set how a repository's work is committed, or a workspace's: as it goes (each piece that passes its checks; the "
            + "default), each commit waiting for your yes, or one commit when the work is done, and your words on how a commit "
            + "message is written",
            Setup + "Line and landing → Commits; a workspace's, " + Ws + "Setup → Defaults → Commits",
            "`daoris place commits <repository> --mode as-you-go|ask|at-end [--convention \"…\"|--no-convention]` "
            + "(`--workspace <name>` for a whole workspace), `daoris place commits <repository> --clear`",
            new HelpDoorAsk.Proposes("setting", "commits", "and may quote the convention from the repository's own instructions for you to apply")),
        // What a helper an agent asks for does; Ask Daoris proposes it as the `setting` kind's `helpers` door. Before the review and
        // the opinion, whose rows the workflow's follows.
        new("set what a helper a repository's agent asks for does, or a workspace's: wait for your yes (the default), start at once "
            + "within the limits, or not be offered, and the other agents it may ask, in the order they are tried",
            Setup + "Sessions → Helpers; a workspace's, " + Ws + "Setup → Defaults → Helpers",
            "`daoris place helpers <repository> --mode ask|on|off [--agents <adapter,adapter>]` (`--workspace <name>` for a "
            + "whole workspace), `daoris place helpers <repository> --clear`",
            new HelpDoorAsk.Proposes("setting", "helpers")),
        // Where work is shown to the person before it is offered to land; Ask Daoris proposes it as the `setting` kind's `review`
        // door. The landing's gate reads it, and the room says what lets work that waits go, and that the intake composes no set-up
        // step yet, as every door does.
        ("declare where a repository's work is shown to you before it is offered to land, or a workspace's: a review "
            + "environment, local (shown in Daoris's browser at the address where the app runs) or deployed (by the "
            + "repository's documented procedure), and whether work waits for your look (work that waits lands only once you "
            + "say it is reviewed, `daoris quest review <quest> reviewed`, or skip the review; no set-up step is composed "
            + "for you yet; production is never one)",
            Setup + "Line and landing → Review before landing; a workspace's, Repositories → the workspace's page → Setup → Defaults",
            "`daoris place review <repository> <environment> --kind local|deployed --procedure <path> [--address <url>] "
            + "[--run \"<command>\"] [--required|--not-required]` (`--workspace <name>` for a whole workspace), "
            + "`daoris place review <repository> none|--drop <environment>|--clear`"),
        // Which other agent reads work before it lands; Ask Daoris proposes it as the `setting` kind's `opinion` door. The room says
        // what the gate does in the sentences every door says after a rule's own, read from the twin, as the review's row says its gate's.
        ("declare which other agent reads a repository's work before it lands, or a workspace's: reviewers named in the order "
            + "they are tried, another maker's agent by your naming, each reading in a copy of its own that nothing is taken back "
            + "from, at landing or each step, whether work waits for you when none can, whether it may run what is declared safe, "
            + "and how long a pass may take (never on unless you name a reviewer). Each door then says what the gate does: "
            + KnowledgeReviews.Current.GateWords,
            Setup + "Line and landing → Second opinion before landing; a workspace's, Repositories → the workspace's page → Setup → Defaults",
            "`daoris place opinion <repository> --agents <adapter,adapter> [--on landing,steps] [--required|--not-required] "
            + "[--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]` (`--workspace <name>` for a whole workspace), "
            + "`daoris place opinion <repository> none|--clear`"),
        // The workflow a place follows drawn as it stands; a place still on Current is drawn from its old landing rule. Never
        // proposed, since it reads; a change is the workflow's, in the editor, or the Setup row a step opens for Current.
        new("see how a repository's work moves, or a workspace's, at each gate: the workflow it follows, or for a place still on "
            + "Current its old landing rule's, as they stand; each step (the work, a second opinion, your look, the landing, a pull "
            + "request a plugin opens after it) and the plugins that may hold a start, who takes part (agent alone, agent + you, "
            + "you, automatic) and who acts, where each was set, and what Daoris cannot do of it yet; a step opens the workflow "
            + "editor, or for Current the Setup row that sets it, where a change is made",
            "Repositories → the repository's page → Workflow; a workspace's, Repositories → the workspace's page → Workflow",
            "`daoris workflow show --repository <name>|--workspace <name>`",
            new HelpDoorAsk.Never("it reads and changes nothing")),
        // A named workflow, its versions never edited. Ask Daoris's `workflow` kind takes `new` and `edit`, a whole version; `apply`
        // and `import` are done at a terminal. Choosing one comes next, and every gate reads the version a run bound.
        new("name a workflow: a graph of steps from a default Daoris ships or another saved workflow, each change saved as its next "
            + "version, never edited, said step by step with your part first; a list of steps is no longer saved",
            "(no screen yet)",
            "`daoris workflow new <id> --from <default>|<id>[@<version>]`, `daoris workflow "
            + "apply|export|import …`, `daoris workflow list`, `daoris workflow show <id>[@<version>]`",
            new HelpDoorAsk.Proposes("workflow", "new", "a whole version, new or after the newest, which you apply; not an import yet")),
        // Which workflow work follows, by repository, workspace and kind of task, the first level that names one deciding; a run
        // binds it at its first start, and every gate reads that version. Ask Daoris's `workflow` kind takes `use`, with the version
        // it proposes.
        new("choose which workflow a repository's or a workspace's work follows, or a kind of task's, and declare a workspace's kinds "
            + "with the paths their work keeps to (a repository's choice replaces its workspace's whole; each run binds the "
            + "newest version at its first start, and how it lands, your look and its second opinion follow that version; "
            + "`--clear` follows this machine's default workflow). "
            + "`current` is no development route's choice any more, and is refused there; only the landed route takes it, for no verification",
            "(no screen yet)",
            "`daoris workflow use <id>|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed]`, "
            + "`daoris workflow kind <workspace> <kind> --label \"…\" [--paths <path,…>]|--drop`, "
            + "`daoris workflow show --repository <name>|--workspace <name> [--kind <kind>]`",
            new HelpDoorAsk.Proposes("workflow", "use", "the choice with a version it proposes, as a repository's or a workspace's default; not a kind yet")),
        // The home's one conversion door over every area's part, which is the person's press over the plan they read. Ask Daoris
        // opens the page and names the plan, and never proposes the apply.
        new("convert this home's old records, every area's part in one plan: in the workflow part, a workflow saved as steps or "
            + "holding a `handed` step, and a place still on Current, which follows its old landing rule, become saved graphs that each "
            + "place follows, with a home default workflow for a place that chooses none (`--plan` shows every operation and writes "
            + "nothing; the apply is your press over the plan you read, by its digest; a part that failed is pressed again or left; the "
            + "latest receipt is undone)",
            "Agents → Setup → the place's head *Convert…*, or *What needs you* → *Review conversion…*",
            "`daoris convert --plan`, then `--apply <digest>` (`--leave <part>` for a failed part) or `--undo`, through the running Daoris",
            new HelpDoorAsk.Never("it opens the page and names the plan, and the apply is your press")),
    ];
}
