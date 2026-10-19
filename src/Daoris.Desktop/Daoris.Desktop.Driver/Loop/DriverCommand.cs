namespace Daoris.Driver;

/// <summary>How a loop runs, once one is asked for.</summary>
public enum LoopMode
{
    /// <summary>Tick until stopped, a poll apart.</summary>
    Watch,

    /// <summary>One tick, then exit.</summary>
    Once,

    /// <summary>Tick until nothing starts, then exit — the deterministic mode a gate drives.</summary>
    UntilIdle,
}

/// <summary>A loop, asked for by name.</summary>
/// <param name="Mode">How it runs.</param>
/// <param name="Share">Run beside a live driver on the same home, on purpose, rather than be refused (D104).</param>
public sealed record LoopRequest(LoopMode Mode, bool Share);

/// <summary>
/// What <c>daoris-driver</c> was asked for (DRV8a, D104): a loop only by its verb, and anything else the
/// usage. Run with no verb to read its usage, the host used to start a loop, which took a quest beside
/// the desktop's own. In the library rather than the host, so that is held by a test, as the plugin
/// kit's command is.
/// </summary>
public static class DriverCommand
{
    /// <summary>What a bare invocation prints. Every verb the host answers has a line, held by a test.</summary>
    public const string Usage =
        """
        usage: daoris-driver <verb> …

          drive [--once | --until-idle] [--share]
              drive this home: start a session for each quest a repository opted in here can take, and
              watch until Ctrl+C. --once ticks once; --until-idle ticks until nothing starts. A home
              another live driver drives is refused, naming it; --share runs beside it on purpose.
          chat --repository <name> [--adapter <name>] [--profile <name>] [--own-tree]
              a conversation in a repository: stdin is you, stdout the session.
          ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… "…"
          ask --publish <id> --to <repo>  ·  ask --close <id> --reason "…"  ·  ask --delete <id>
          ask --go-ahead <id> <n> approve|refuse ["…"]
              ask at a workspace, or turn an ask into a quest, close it, or delete one made by mistake; answer a
              go-ahead a session asked on it, which every session on the ask is handed from then on, and which
              sends a parked session on once none of its go-aheads waits.
          ask channel <id> [--to <repository>] [--re <message>] ["…"]
              read an ask's channel, where its sessions and you talk, and mark it read; or with words post them as
              yours, to everyone on the ask or one of its repositories, which each session reads.
          ask --pause <id>  ·  ask --resume <id>  ·  quest pause <id>  ·  quest resume <id>
              stop what of an ask's work, or a quest's, runs on this machine and start nothing of it until
              you resume it, which carries it on where it stood.
          ask --abandon <id> [--reason "…" --yes]  ·  quest abandon <id> [--reason "…" --yes]
              list what abandoning it would decline, discard and archive, and what it keeps and why; with
              --reason and --yes, give up what the list holds, each quest declined with your reason.
          setup <repository> [--plan]
              ask a repository's own session to set it up for every agent: the doctrine, its knowledge, its
              brief, on its own branch. The press adds the doctrine tool's exact verbs to its rules; --plan
              prints what was read, the rule and the quest, and publishes nothing.
          setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]
          setup --workspace <name> --pause | --resume | --stop
              set a workspace up one repository at a time, the ones other work touches first: a press
              writes a plan the loop works, adding the doctrine tool's verbs to the workspace's rules
              once, and pausing after a pilot of two. --plan prints the list and each refusal; --pause,
              --resume (which judges the skipped again) and --stop steer a plan already made.
          register [--repository <name>]
              register each repository with a checkout here, or the one named, from what its line declares,
              as `connect` would; a start and a line Daoris moves do the same on their own.
          result <id>
              how a press this terminal sent to the running Daoris ended, when its answer did not reach the
              verb ("Daoris was still working on it (request …)"): asked again with the key it was sent with,
              for a day. It sends nothing again.
          quest delete <id>  ·  quest accept <id>
              delete a quest nobody has started on, or accept a done's departure from what you required, so what
              it held (the chain's next step, a quest waiting on it) goes on.
          quest done <id> [--note "…"]
              mark a quest done as yours, as a finish at a checkpoint that left it taken says: its record says you
              marked it done, with your words, and answers none of its requirements one by one.
          quest reopen <id> [--note "…"]
              reopen what a quest submitted in a workflow that closes its quests itself, once the step it published
              stands on it: its agent makes it ready again, and the step waits.
          quest go-on <id>
              go on with a step its source's reopen held, on the new work, once that is ready again; `quest abandon`
              abandons it instead.
          quest check <id> [--commit <sha>]
              read a done's evidence again: at the commit a driven end here read, or the one named, which must come
              after it on the same history. 0 when all is found, 1 when anything is missing or unread.
          quest review <id> reviewed|not-yet|skip ["…"]
              say what you saw of a set-up step's showing: reviewed lets the work it holds land; not-yet, with your
              words, sends them to the step's session as its next turn; skip lets this work land without a review.
          quest review <id> off ["…"]  ·  quest review <id> on|<environment>
          ask … --short "…"
              name a new ask in a list by a short title, at most 40 characters on one line: the service judges it, as the
              composer's field is judged.
          ask … --review rule|on|<environment>|off [--review-words "…"]
          ask --set-review <id> on|<environment>|off ["…"]
              choose whether work is reviewed before it lands, and where: a done quest's at its gate, where off is the
              skip and on (the repository's default) or an environment it declares publishes a set-up step to show it
              there; a new ask's, where rule (the default) leaves it to each repository's rule; or an ask's, the latest
              standing, which applies its intake's proposal too.
          ask … --kind <kind> [--workflow <id>|current] [--workflow-words "…"]
          ask --set-workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear ["…"]
              say what kind of task an ask is and which workflow its work follows: a kind its workspace declares, a
              workflow saved here or current; the latest stands, and each run binds what is chosen at its first start.
              Yours alone: an agent proposes a kind, never sets one.
          opinion ask <session> [--reviewer <adapter>] [--same-agent] ["…"]  ·  opinion show <session|opinion>
          opinion stop <opinion>  ·  opinion anyway <session> ["…"]  ·  opinion myself <session> ["…"]
              a session's second opinion before its work lands: ask another agent to read it (one of its rule's reviewers by
              name, or the working agent fresh), say where it stands with its findings and their answers, stop a reviewer
              reading, go on without a settled one, or record your own reading in its place. `sessions say` sends it back.
          brainstorm "<question>" [--repository <name>] [--agents <a,b>]  ·  brainstorm show|send|clear <id>  ·  brainstorm list
              one question put to several agents: Daoris freezes a context packet from its own knowledge for you to read,
              and once you send it each agent proposes, one synthesizes, and the ranked document is kept on this machine.
          meeting ask "<question>" --agents <a,b> [--repository <name>] [--id <id>]  ·  meeting show|stop|clear <id>
          meeting send <id> [--round <n>]  ·  meeting list
              agents of different makers talking in one conversation Daoris runs: each round's packet is yours to read
              before it goes, each agent speaks once a round in your order, and a second round goes only when you name it.
          history [--workspace <name>] [--json]
              what this machine keeps of finished work, per workspace: quests, asks, sessions and bytes, what a
              clear would take, and what it keeps and why.
          history clear --workspace <name> [--yes]
          quest clear <id> [--failed] [--yes]  ·  ask --clear <id> [--yes]
              list what clearing a workspace's finished history, one closed quest's work (or, with --failed, its
              failed sessions) or one ask's work would take and keep; with --yes, clear it from this machine.
          answer <session> ["…"]
              answer a session that parked to ask you; the same session goes on with your words at the
              driver's next look.
          sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
              this machine's sessions by what they need: waiting on you first, then to review, working,
              resumes later and ended.
          sessions stop <id>  ·  sessions finish <id> [--note "…"]  ·  sessions decline <id> --reason "…"
              stop a live session (its quest is then held here until `daoris driver retry`), or finish or
              decline one that waits on you.
          sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>
              take ended sessions out of the list, or bring them back; delete a conversation that served no quest.
          sessions say <id> "…" [--file <path>]… [--now]
              say something to a session of this machine's: read at its next step or when its turn ends while it
              works, or, parked or ended, the same session goes on with it. Prints where the words stand. --now is
              the page's Send now: steered into the running turn where its agent steers, or the turn stopped so they
              go next; it says what the door did, and why where it could not.
          sessions take-back <id> [<word>]  ·  sessions reword <id> <word> "…"
              take back one of your words waiting on a parked or ended session before it goes on with them, or put
              new words in its place; with no word, list those waiting with their ids.
          sessions go-on-new <id>
              words that wait for the account a session ran on to cool: go on now in a new session at the driver's
              next look, handed them, without that session's conversation.
          sessions start-from <id>
              words a session cannot go on with: start a conversation here with them, as `chat` does, without that
              session's context; they leave that session, naming the conversation.
          sessions hand-over <id> --agent <adapter> [--account <name>]
              a conversation goes on with another agent (D163): its own ends, and a new conversation in its tree
              starts, told what Daoris kept of it. Conversations only; a quest's session comes later.
          sessions helper <id> "<question>" [--agent <name>]
              ask another agent, read-only, to answer one question about a session's work (D163): the driver takes
              it at its next look, and the answer comes back to that session as its next turn, marked as the helper's.
          sessions helper --approve|--decline <ask>
              your yes or no to an agent's ask for a helper that waits on you (the `helpers` rule's `ask`), as its
              session's page takes it: a yes starts it at the driver's next look; a no refuses it and says so.
          help list [--search "…"] [--json] [--offset <n>] [--limit <n>] [--workspace <name> | --all]
          help resume <id> "…" [--file <path>]…  ·  help rename <id> "…" | --clear  ·  help pin <id>  ·  help unpin <id>
          help delete <id> [--yes]
              Ask Daoris's conversations, kept on this machine only: list or search them a page at a time, one
              workspace's or every one's, each then labelled with its own; go on in one with your words (it goes
              on in the window), name, pin or unpin one, or delete one, listed first and deleted with --yes.
          help mode [restricted|full]
              Ask Daoris's access mode (D183), from its next start: restricted, the default, reads and proposes and
              you apply; full acts on this machine itself, in its agent's mode that asks nothing. Alone, says which.
          access [<repository> | --workspace <name>] [full | <pause>=<value>… | --clear]
              the access setup a repository's sessions start under, its workspace's, or this machine's (D184): full
              access, or each pause chosen (permissions, routing, landing, goAheads, questions). A pause a repository
              does not name comes from its workspace, then this machine, then today's behaviour; alone, says each
              pause with where it came from.
          trees [list | remove <path|session|branch> [--repository <name>] [--force]
                | clean [--workspace <name>] [--yes] | land <session> [--plan] [--branch <name>]
                | land --entry <id> | --ask <id> [--leave-out <session>]... [--plan]
                | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]
                | state [--entries]
                | state <session|branch> [--repository <name>]
                | sync [--repository <name>] [--workspace <name>] [--all] [--yes]]
              this machine's session trees and the branches its landings made; remove takes a failed or
              superseded attempt's branch, with its tree where it is still here, on --force; state asks a
              landed branch's plugin again whether its pull request completed, and says what that proves here;
              sync brings each repository up to date after a pull request merged, listed first (it fetches,
              never pushes): those holding Daoris's branches, every one with --all. For clean and sync,
              --workspace takes one workspace's checkouts alone, as its Branches tab does. state alone says each
              unit of work's milestone, as `work state` does (FABRIC-MILESTONE1a).
          work state [<unit>] [--entries]
              each unit of work's milestone, read from the records the tree lifecycle keeps and changing nothing:
              whether its end was reached, the result kept, the cleanup pending (what waits and why) or
              completed, and the recovery retained until its date; --entries lists what it is read from, and a
              unit named by its id, session or branch always does. The page's is the work's foot.
          work recovery drop <ref>... --repository <name> | --unheld [--repository <name>] [--plan]
              delete the recovery refs no branch or tag of yours holds, which Daoris keeps until you say so: each ref
              in one transaction that verifies the commit it names, so one that moved or that a holder now keeps is
              kept and said; its commits may then be lost from this machine once git collects them. Only the process
              holding the home acts. --plan lists and changes nothing. The page's is Branches' Kept for recovery.
          work held [--unit <id>]  |  work held <unit> <node> <attempt> happened|did-not|stop|record|resend
              the steps whose attempt is held for you, because nothing else can say what became of it, and why;
              with a word, yours on one of them. A plugin's step: it happened (the step is reached on your word),
              it did not (a new attempt with a new key), or stop the step. A quest's close, whose outcome is its
              door's answer and never your word: record that answer again, resend the same close (its key, never
              a new close), or stop the work's further steps. Only the process holding the home records it; the
              next look applies it. The page's is What needs you, and the work's foot.
          sync [status | dismiss <quest>] [--workspace <name>]
              one sync pass now, or where each workspace stands.
          sync forward <quest> <machine> <sequence>
              bring forward one side of two machines' work on a quest, as its conflict names it.
          logs [--since <30m|2h|3d>] [--source <name>] [--event <name>] [--level <warn|error>] [--json]
              the machine log, every source merged by time.
          run-heavy [--gate <name>]
              run this checkout's heavy gates, those its daoris.gates.json marks "heavy": true, one after another, or the
              one named, holding its repository's one heavy-run slot: a run from any tree of it waits, saying behind whom and
              how long. What a gate starts is ended before the slot is let go. 0 passed · 1 a gate failed · 2 could not run.
          heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]
              this machine's heavy gates for a repository whose own daoris.gates.json marks none, in the order they run:
              what run-heavy runs there, and what its sessions are told to run once, at their end. Kept here, never in
              the repository; a command may bound its own workers (npm test -- --maxWorkers=2). Alone, lists each
              repository's; with a repository alone, says its gates.
          trace <commit|session|quest>  ·  trace commit|session|quest <id>
              one read back to the ask: the person's words and go-aheads, the quest's requirements and answers, each
              session's agent, account, instruction, rules and landing, each from the store that keeps it. Reads only.
          workflow run --session <id> | --quest <id> | --ask <id>
              where a piece of work stands in its workflow, as the session's Workflow view draws it: each step with its
              state and what it says, the step the run stands at marked. A session's run is its quest's. Reads only.
          workflow keep <session> ["…"]
              keep the workflow a kind of task chose for a session's work that changed paths outside the kind's, where
              it asks less of you than the one it would follow without the kind: your say-so, with your words.
          git branches [--repository <name>] [--all] [--json]
              each repository's line and branches: the sessions', the landed (on origin or not, their pull
              request), yours, and how many only origin holds, each against the line; and the git each read
              ran. Those holding Daoris's branches, every one with --all. Reads only.
          update [--install <folder>]  ·  update --when-idle | --now | --cancel [--install <folder>]
              what is staged beside the install and how the last swap ended; or install it when idle (the
              default), now (ending what runs as a close does), or not now, as the window's banner does.
          wiring [--json] [--measure]
              how this Daoris is wired, Settings → This Daoris's twin: its folders, service, loop, settings files,
              variables and where each came from, versions and what a new terminal finds; what only the window
              knows says so. --measure walks the folders' sizes first. Never a key's value. Reads only.
          plugins install <file.nupkg>
              install a plugin package's plugin, checked first; nothing reaches a network.
          plugins new <id> --point <point>… [--in <folder>]  ·  plugins try <folder|id> [--point <point>]
              make a plugin, or try one as the driver would.
          plugins show <id> [--json]  ·  plugins activity <id> [--since <30m|2h|3d>] [--json]
              a plugin's page and its health, or what it did, from the machine log.
          plugins approve <id> <server> [--reads-only | --writes <tool,…>]  ·  plugins withdraw <id> <server>
              let Ask Daoris call the exact tools a plugin's server offers it, its writes too unless --reads-only,
              or only the writes --writes names, or take that back; an update that adds or widens a tool asks again.

        Only `drive` starts a loop. DAORIS_HOME names the home; DAORIS_SERVICE_URL the service, which
        `drive` and every verb that reads a record need.
        """;

    /// <summary>Whether the words ask for the usage itself — an answer, exit 0, not a mistake.</summary>
    public static bool AskedForHelp(IReadOnlyList<string> args) =>
        args is ["help" or "--help" or "-h"];

    /// <summary>
    /// The loop the words ask for, or null for the usage — with what was not understood, where
    /// something was said. A bare invocation is null with nothing to say.
    /// </summary>
    public static LoopRequest? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0 || AskedForHelp(args)) return null;

        // The spelling before the verb — a mode flag first — is kept: it names a mode, which is an
        // explicit ask, and the rehearsals and people's scripts already use it.
        IEnumerable<string> flags;
        if (args[0] == "drive") flags = args.Skip(1);
        else if (args[0] is "--once" or "--until-idle") flags = args;
        else
        {
            problem = $"no verb `{args[0]}` — the loop is `drive`.";
            return null;
        }

        bool once = false, untilIdle = false, share = false;
        foreach (var flag in flags)
        {
            switch (flag)
            {
                case "--once": once = true; break;
                case "--until-idle": untilIdle = true; break;
                case "--share": share = true; break;
                default:
                    problem = $"`drive` takes --once, --until-idle and --share, not `{flag}`.";
                    return null;
            }
        }

        if (once && untilIdle)
        {
            problem = "`--once` and `--until-idle` are two modes — name one.";
            return null;
        }

        return new LoopRequest(once ? LoopMode.Once : untilIdle ? LoopMode.UntilIdle : LoopMode.Watch, share);
    }

    /// <summary>
    /// How the host ends a run a cancellation ended (DEV3b, D115's DEV3a note): a close only where the person's own Ctrl+C
    /// asked for one, <i>driver: stopped.</i>, exit 0. A request the service never answered within the client's own timeout
    /// is a failure that says so, exit 2, as a service it could not reach is; and a cancellation nothing asked for is one too.
    /// </summary>
    /// <remarks>
    /// 🔴 The host read every cancellation as Ctrl+C, so a look whose own request timed out printed <i>driver: stopped.</i>
    /// and exited 0, and a script or a gate read a stalled service as a person's close.
    /// </remarks>
    /// <param name="closed">Whether the host's own close was asked for: its Ctrl+C, and nothing else.</param>
    public static LoopEnded Cancelled(OperationCanceledException error, bool closed) =>
        closed ? new LoopEnded(0, "driver: stopped.", Failed: false)
        // The client's own timeout: HttpClient cancels with a TimeoutException inside, and its message names the timeout.
        : error.InnerException is TimeoutException ? new LoopEnded(2, $"driver: the service did not answer in time — {error.Message}", Failed: true)
        : new LoopEnded(2, $"driver: a request was cancelled though nothing closed the run — {error.Message}", Failed: true);
}

/// <summary>How the host ends a run (DEV3b): its exit code, and the line it prints, to the error stream where it failed.</summary>
/// <param name="Exit"><c>0</c> for the person's close, <c>2</c> for a failure.</param>
/// <param name="Said">The line the host prints.</param>
/// <param name="Failed">Whether the run failed rather than closed: its line goes to the error stream and its log as a failure.</param>
public sealed record LoopEnded(int Exit, string Said, bool Failed);
