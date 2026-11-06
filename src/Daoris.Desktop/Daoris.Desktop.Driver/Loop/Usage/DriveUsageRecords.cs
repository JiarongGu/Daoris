namespace Daoris.Driver;

/// <summary>The usage's groups from <c>brainstorm</c> to <c>notices</c>.</summary>
internal static class DriveUsageRecords
{
    public const string Text =
        """
          brainstorm "<question>" [--repository <name>] [--agents <a,b>] [--source <path>]...  ·  brainstorm show|send|stop|clear <id>  ·  brainstorm list
              one question put to several agents: Daoris freezes a context packet from its own knowledge for you to read,
              and once you send it each agent proposes, one synthesizes, and the ranked document is kept on this machine.
          brainstorm "<question>" --kind meeting --agents <a,b> [--repository <name>] [--id <id>]  ·  brainstorm show|stop|clear <id> --kind meeting
          brainstorm send <id> --kind meeting [--round <n>]  ·  brainstorm list --kind meeting
              a meeting: agents of different makers talking in one conversation Daoris runs: each round's packet is yours
              to read before it goes, each agent speaks once a round in your order, and a second round goes only when you
              name it.
          brainstorm measure <agent> [--run]
              the account a pass would take, or with --run one probe pass on it, its reads and writes judged by their effects.
          browser favorite list  ·  browser favorite add <address> [--title "…"]  ·  browser favorite remove <address>
          browser extensions [offer|refuse]  ·  browser use [daoris|edge]  ·  browser links [system|daoris]
              Daoris's browser: its favorites, kept in a Daoris folder on its bar; other software's Chrome extensions,
              offered for your approval or refused; Daoris's own browser or your Edge on a profile of Daoris's; and
              whether links on Daoris's page open in the system's browser or Daoris's. Each is read when it starts.
          chat --repository <name> [--agent <name>] [--account <name>] [--own-tree]  ·  chat --session <id>
              a conversation of the running Daoris, started in a repository or already open, which this terminal
              holds once you confirm it in Daoris's window: stdin is you, stdout the session. Ctrl+C at rest lets
              go, and the conversation goes on in Daoris. Needs the running Daoris.
          convert --plan | --apply <digest> [--leave <part>] | --undo [<digest>]
              convert this home's old records, every area's part in one plan: the plan reads and writes nothing,
              `--apply` is your press, confirmed in Daoris's window, a failed part is pressed again or left with
              `--leave`, and `--undo` takes the latest back. Needs the running Daoris.
          drive [--once | --until-idle] [--quiet-end <minutes>] [--share]
              drive this home: start a session for each quest a repository opted in here can take, and
              watch until Ctrl+C. --once ticks once; --until-idle ticks until nothing starts. A home
              another live driver drives is refused, naming it; --share runs beside it on purpose. No
              session is ended on time: a quiet one is said, and --quiet-end ends one of this run
              unheard that long and carries it on, for a run nobody watches.
          events relay --where  ·  events relay --once --home <home> [--since <run>:<seq>] [--kinds <kind,...>] [--session-pid <pid>]
              the work cycle's events for a session outside Daoris, through the event-relay plugin: --where lays
              this build's copy out in the home and prints the command to start in the background; --once,
              run from that copy, prints one batch of fixed lines (ids and codes, never words) and ends, its
              last line the bracket the next one's --since takes. Holds no key, opens no door of Daoris's.
          gate run [--gate <name> | --look | -- <command>]
              run this checkout's heavy gates, those its daoris.gates.json marks "heavy": true, one after another, or the
              one named, holding its repository's one heavy-run slot: a run from any tree of it waits, saying behind whom and
              how long. What a gate starts is ended before the slot is let go. 0 passed · 1 a gate failed · 2 could not run.
              --look runs nothing: who holds the slot, at which gate and for how long, and who waits.
              Every run also takes a place in the machine's heavy-run admission, the one the merge queue's heavy gates take
              theirs in, saying what it waits for. -- <command> runs a command of your own (a filtered dotnet test, a test
              file's run) as such a run, with no repository slot: 0 it passed · 1 it failed or was stopped.
          gate verify <repository> [--branch <name> | --commit <sha>]
              verify a branch or a commit of this checkout's repository: its declared gates run by the heavy executor as a
              verification run, holding the home's verification slot and the repository's heavy-run slot, the verdict kept
              with the run (FABRIC-PIPELINE1d).
          git branches [--repository <name>] [--all] [--json]
              each repository's line and branches: the sessions', the landed (on origin or not, their pull
              request), yours, and how many only origin holds, each against the line; and the git each read
              ran. Those holding Daoris's branches, every one with --all. Reads only.
          git branch|tag <repository> <name> [<commit>] [--plan | --yes]
              make a branch or a lightweight tag at a commit (HEAD without one), as the Branches tab's New branch and
              New tag do. --plan says what it would make; --yes makes it, after your yes in Daoris's window where
              Daoris runs. Refused from a session's terminal. --delete takes the branch or tag away instead (the
              commit, if given, is the one it must stand at), keeping its commits on a recovery ref; annotated
              tags, daoris/ branches, a landing's, the line and an occupied branch are refused. --rename <new-name>
              moves it instead, keeping its old tip on a recovery ref; the new name starts a new reflog.
          git fetch <repository> [--plan | --yes]
              fetch origin's branches into refs/remotes/origin/* alone (nothing else moves, no tags, FETCH_HEAD kept),
              removing those origin no longer has and naming them, as the Branches tab's Fetch does. --plan says what
              it would do; --yes does it, after your yes in Daoris's window where Daoris runs. Only where Daoris's git
              doors are installed and origin is not set to run another program; refused from a session's terminal.
          git push <repository> branch|tag <name> [<commit>] [--plan | --yes]
              push one branch or lightweight tag to origin, never forced and never with -u, as the Branches tab's Push
              and Push tag do: under a grant for that one update, from where origin had it at the last fetch. A tag
              is a Dangerous push, which the place's Dangerous value decides at the door. --plan says what it would
              push; --yes pushes it, after your yes in Daoris's window where Daoris runs. Refused from a session's
              terminal, where Write denies, or where the identity serving origin is read only.
          git pick|revert <repository> <branch> <commit|A..B> [--plan | --yes]
          git rebase <repository> <branch> <onto> [--plan | --yes]
              edit a branch no tree has out without a working tree, as a branch page's Edit history does: pick or
              revert commits onto it, or rebase its own commits onto another. Each says its plan; --yes makes it
              after your yes in Daoris's window where Daoris runs, keeping the old tip under refs/daoris/discarded/.
              Refused from a session's terminal.
          git review <session> [--json] [--time]
              a session's review as its Review page reads it: the files and their base, the uncommitted ones, the line
              and the branch; --time each git it started, its node and budget, the gate's wait, the creation and the run,
              with the `review.read` line written to the machine log. Reads only.
          git identity|use|access|asks|answer …
              the identities and accesses Daoris's git doors use for each host, and the asks they wait on you for.
          history [--workspace <name>] [--json]
              what this machine keeps of finished work, per workspace: quests, asks, sessions and bytes, what a
              clear would take, and what it keeps and why.
          history clear --workspace <name> [--yes]
              list what clearing a workspace's finished history would take and keep; with --yes, clear it.
          integrate <repository> [--request <branch> | --drop <request> | --recheck | --decide <candidate> split|hand-back|land | --order <request> first|last]
              a repository's integration as the window's Integration view draws it: what waits to land, in what order, and what
              holds it. The acts (--request, --drop, --recheck, --decide, --order) are the owner's, Person routes in the window.
          logs [--since <30m|2h|3d>] [--source <name>] [--event <name>] [--level <warn|error>] [--json]
              the machine log, every source merged by time.
          notices [--json]
              what came to you, changing nothing: what waits for your confirmation (by act and session), what needs
              you by group, unread messages by ask, and Done and failed, as the running Daoris holds them; without
              it, the notices' record alone, and the rest said not read. The window's Notifications is the other door.
        """;
}
