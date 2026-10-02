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
              ask at a workspace, or turn an ask into a quest, close it, or delete one made by mistake.
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
          trees [list | remove <path> [--force] | clean [--yes] | land <session> [--plan]
                | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]
                | sync [--repository <name>] [--all] [--yes]]
              this machine's session trees and the branches its landings made; sync brings each
              repository up to date after a pull request merged, listed first (it fetches, never pushes):
              those holding Daoris's branches, every one with --all.
          sync [status | dismiss <quest>] [--workspace <name>]
              one sync pass now, or where each workspace stands.
          logs [--since <30m|2h|3d>] [--source <name>] [--event <name>] [--level <warn|error>] [--json]
              the machine log, every source merged by time.
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
}
