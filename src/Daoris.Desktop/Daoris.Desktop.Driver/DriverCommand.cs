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
          quest delete <id>  ·  quest accept <id>
              delete a quest nobody has started on, or accept a done's departure from what you required, so what
              it held (the chain's next step, a quest waiting on it) goes on.
          quest done <id> [--note "…"]
              mark a quest done as yours, as a finish at a checkpoint that left it taken says: its record says you
              marked it done, with your words, and answers none of its requirements one by one.
          quest check <id> [--commit <sha>]
              read a done's evidence again: at the commit a driven end here read, or the one named, which must come
              after it on the same history. 0 when all is found, 1 when anything is missing or unread.
          quest review <id> reviewed|not-yet|skip ["…"]
              say what you saw of a set-up step's showing: reviewed lets the work it holds land; not-yet, with your
              words, sends them to the step's session as its next turn; skip lets this work land without a review.
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
          sessions say <id> "…" [--file <path>]…
              say something to a session of this machine's: read at its next step or when its turn ends while it
              works, or, parked or ended, the same session goes on with it. Prints where the words stand.
          sessions go-on-new <id>
              words that wait for the account a session ran on to cool: go on now in a new session at the driver's
              next look, handed them, without that session's conversation.
          sessions start-from <id>
              words a session cannot go on with: start a conversation here with them, as `chat` does, without that
              session's context; they leave that session, naming the conversation.
          help list [--search "…"] [--json]  ·  help resume <id> "…" [--file <path>]…
          help rename <id> "…" | --clear  ·  help pin <id>  ·  help unpin <id>  ·  help delete <id> [--yes]
              Ask Daoris's conversations, kept on this machine only: list or search them, go on in one with your
              words (it goes on in the window), name, pin or unpin one, or delete one, listed first and deleted
              with --yes.
          trees [list | remove <path|session|branch> [--repository <name>] [--force]
                | clean [--workspace <name>] [--yes] | land <session> [--plan]
                | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]
                | state <session|branch> [--repository <name>]
                | sync [--repository <name>] [--workspace <name>] [--all] [--yes]]
              this machine's session trees and the branches its landings made; remove takes a failed or
              superseded attempt's branch, with its tree where it is still here, on --force; state asks a
              landed branch's plugin again whether its pull request completed, and says what that proves here;
              sync brings each repository up to date after a pull request merged, listed first (it fetches,
              never pushes): those holding Daoris's branches, every one with --all. For clean and sync,
              --workspace takes one workspace's checkouts alone, as its Branches tab does.
          sync [status | dismiss <quest>] [--workspace <name>]
              one sync pass now, or where each workspace stands.
          logs [--since <30m|2h|3d>] [--source <name>] [--event <name>] [--level <warn|error>] [--json]
              the machine log, every source merged by time.
          trace <commit|session|quest>  ·  trace commit|session|quest <id>
              one read back to the ask: the person's words and go-aheads, the quest's requirements and answers, each
              session's agent, account, instruction, rules and landing, each from the store that keeps it. Reads only.
          git branches [--repository <name>] [--all] [--json]
              each repository's line and branches: the sessions', the landed (on origin or not, their pull
              request), yours, and how many only origin holds, each against the line; and the git each read
              ran. Those holding Daoris's branches, every one with --all. Reads only.
          update [--install <folder>]  ·  update --when-idle | --now | --cancel [--install <folder>]
              what is staged beside the install and how the last swap ended; or install it when idle (the
              default), now (ending what runs as a close does), or not now, as the window's banner does.
          plugins install <file.nupkg>
              install a plugin package's plugin, checked first; nothing reaches a network.
          plugins new <id> --point <point>… [--in <folder>]  ·  plugins try <folder|id> [--point <point>]
              make a plugin, or try one as the driver would.
          plugins show <id> [--json]  ·  plugins activity <id> [--since <30m|2h|3d>] [--json]
              a plugin's page and its health, or what it did, from the machine log.

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
