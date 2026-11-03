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
public sealed record LoopRequest(LoopMode Mode, bool Share)
{
    /// <summary>
    /// <c>--quiet-end &lt;minutes&gt;</c> (SESSWATCH1a, D200, the session-watch design §2): a session of this run unheard that long, busy or
    /// not, is ended and carried on, for a run nobody watches that must return. Only with <c>--once</c> or <c>--until-idle</c>; null waits.
    /// </summary>
    public int? QuietEnd { get; init; }
}

/// <summary>
/// The loop a person asks for by its verb (DRV8a, D104), and the tree's usage (ONEWAY1ai-i): a bare invocation, or a word no command
/// answers, is the usage and starts nothing. In the library rather than the host, so that is held by a test, as the plugin kit's command is.
/// </summary>
public static class DriverCommand
{
    /// <summary>
    /// What a bare <c>daoris</c> and <c>daoris help</c> print: the doctrine commands, then each group of the tree with its sub-verbs, a
    /// group's lines beginning with its name (<see cref="UsageOf"/>). Every group has a line, held by a test against the tree.
    /// </summary>
    public const string Usage =
        """
        usage: daoris <command> …

          analyze  ·  check  ·  doctor  ·  index  ·  init  ·  reconcile  ·  status  ·  sync  ·  upstream  ·  --version
              the doctrine commands, offline: run by the CLI this install carries, on the node PATH finds (Node.js 22
              or later); `daoris <command> --help` says each one's words. Every other command is the desktop's.
          help [<group>]
              this usage, or one group's.
          agent list  ·  agent install|update|pin|unpin|which <agent> …  ·  agent login|key <agent> …
          agent rules [list | allow|ask|deny|remove <rule> | default <id> on|off | proposals | accept|decline <id>]
          agent trust <agent> <folder> [--account <name>] [--yes]  ·  agent settings <agent> [--account <name>] [model|effort …]
          agent account list|add|remove|default|order|use|ready|rename|join <agent> …
              this machine's agents: their installs, pins and sign-ins, their rules, trust and settings, and each
              agent's accounts; a change a route takes goes to the running Daoris's window to confirm where one runs,
              and is written here where none does; `agent --help` says each verb's words.
          ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… "…"
          ask publish <id> --to <repo>  ·  ask close <id> --reason "…"  ·  ask delete <id>
          ask go-ahead <id> <n> approve|refuse ["…"]
              ask at a workspace, or turn an ask into a quest, close it, or delete one made by mistake; answer a
              go-ahead a session asked on it, which every session on the ask is handed from then on, and which
              sends a parked session on once none of its go-aheads waits.
          ask channel <id> [--to <repository>] [--re <message>] [--ordinary] ["…"]
              read an ask's channel, where its sessions and you talk, with each message's level where it is urgent
              and what it reached, and mark it read; or with words post them as yours, to everyone on the ask or
              one of its repositories, which each session reads at its next step, or with --ordinary when its turn ends.
          ask at-work <id> [--json]
              who is at work on an ask: each line of work counted by its parts and subagents, with the session
              that opens it, and the quests the ask asked with who took each; --json prints the answer whole. The
              channel and why a quest is not taken yet are not in it.
          ask pause <id>  ·  ask resume <id>
              stop what of an ask's work runs on this machine and start nothing of it until you resume it,
              which carries it on where it stood.
          ask abandon <id> [--reason "…" --yes]
              list what abandoning it would decline, discard and archive, and what it keeps and why; with
              --reason and --yes, give up what the list holds, each quest declined with your reason.
          ask … --short "…"
              name a new ask in a list by a short title, at most 40 characters on one line: the service judges it, as the
              composer's field is judged.
          ask … --review rule|on|<environment>|off [--review-words "…"]
          ask review <id> on|<environment>|off ["…"]
              choose whether work is reviewed before it lands, and where: a done quest's at its gate, where off is the
              skip and on (the repository's default) or an environment it declares publishes a set-up step to show it
              there; a new ask's, where rule (the default) leaves it to each repository's rule; or an ask's, the latest
              standing, which applies its intake's proposal too.
          ask … --kind <kind> [--workflow <id>|current] [--workflow-words "…"]
          ask workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear ["…"]
              say what kind of task an ask is and which workflow its work follows: a kind its workspace declares, a
              workflow saved here or current; the latest stands, and each run binds what is chosen at its first start.
              Yours alone: an agent proposes a kind, never sets one.
          ask clear <id> [--yes]
              list what clearing one ask's work from this machine would take and keep; with --yes, clear it.
          ask-daoris list [--search "…"] [--json] [--offset <n>] [--limit <n>] [--workspace <name> | --all]
          ask-daoris resume <id> "…" [--file <path>]…  ·  ask-daoris rename <id> "…" | --clear  ·  ask-daoris pin <id>  ·  ask-daoris unpin <id>
          ask-daoris delete <id> [--yes]
              Ask Daoris's conversations, kept on this machine only: list or search them a page at a time, one
              workspace's or every one's, each then labelled with its own; go on in one with your words (it goes
              on in the window), name, pin or unpin one, or delete one, listed first and deleted with --yes.
          ask-daoris mode [restricted|full]
              Ask Daoris's access mode (D183), from its next start: restricted, the default, reads and proposes and
              you apply; full acts on this machine itself, in its agent's mode that asks nothing. Alone, says which.
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
          chat --repository <name> [--agent <name>] [--account <name>] [--own-tree]
              a conversation in a repository: stdin is you, stdout the session.
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
          place <repository>… --preset <id> [--with opinion,look] [--adoption [--what-it-builds <kind>] [--at-once <n>] [--pilot <n>]] [--drive] (--plan | --apply)
              a quick set-up: a delivery preset (not-driven, merge-after-accept, branch-to-push,
              pull-request, integrated; keep leaves how it lands), its second opinion and your look,
              adoption (a repository's own session asked to set it up for every agent, on its own branch,
              the doctrine tool's exact verbs added to its rules; --what-it-builds library, service,
              desktop or tooling names the checks it is asked to declare) and general driving, read
              before and after through the driver's own readers, every refusal at once, what lowers your
              part, the work driving releases and each declaration to accept. --plan writes nothing;
              --apply is your press, and an adoption of more repositories than --at-once asks is paced
              by a workspace plan the loop works.
          place --workspace <name> --adoption [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]
          place --workspace <name> --pause | --resume | --stop
              adopt a workspace one repository at a time, the ones other work touches first: a press
              writes a plan the loop works, adding the doctrine tool's verbs to the workspace's rules
              once, and pausing after a pilot of two. --plan prints the list and each refusal; --pause,
              --resume (which judges the skipped again) and --stop steer a plan already made.
          place list  ·  place drive|undrive|hold|resume <repository>  ·  place trees <repository> on|off
          place line|landing|work-cycle|across|standing|language|review|opinion|helpers|commits <repository> …
              each place's standing choices: what is driven and held, its trees, its line and how work lands there, its
              work's cycle, what agents outside it read, its standing answer, its language, where its work is shown to you,
              its second opinion (--agents <a,b>), its helpers and how it commits; a change a route takes goes to the
              running Daoris's window to confirm where one runs, and is written here where none does.
          place access [<repository> | --workspace <name>] [full | <pause>=<value>… | --clear]
              the access setup a repository's sessions start under, its workspace's, or this machine's (D184): full
              access, or each pause chosen (permissions, routing, landing, goAheads, questions). A pause a repository
              does not name comes from its workspace, then this machine, then today's behaviour; alone, says each
              pause with where it came from.
          place heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]
              this machine's heavy gates for a repository whose own daoris.gates.json marks none, in the order they run:
              what `gate run` runs there, and what its sessions are told to run once, at their end. Kept here, never in
              the repository; a command may bound its own workers (npm test -- --maxWorkers=2). Alone, lists each
              repository's; with a repository alone, says its gates.
          place checks [<repository> [light | heavy-at-end | --clear]]
              which checks a repository's sessions run under Current: light, the gates its daoris.gates.json does not
              mark heavy and the checks a change reaches, its heavy gates refused to them and left to the verification;
              heavy-at-end, those gates once, at a session's end; none, as they did. Alone, lists each repository's.
          plugin list  ·  plugin add <folder>  ·  plugin add --offer <id>  ·  plugin update <id> [--yes]  ·  plugin remove <id>  ·  plugin enable|disable <id>
              this machine's plugins: a change a route takes goes to the running Daoris's window to confirm where one
              runs, and is made here where none does; an add over an installed plugin replaces it here alone.
          plugin install <file.nupkg>
              install a plugin package's plugin, checked first; nothing reaches a network.
          plugin new <id> --point <point>… [--in <folder>]  ·  plugin try <folder|id> [--point <point>]
              make a plugin, or try one as the driver would.
          plugin show <id> [--json]  ·  plugin activity <id> [--since <30m|2h|3d>] [--json]
              a plugin's page and its health, or what it did, from the machine log.
          plugin approve <id> <server> [--reads-only | --writes <tool,…>]  ·  plugin withdraw <id> <server>
              let Ask Daoris call the exact tools a plugin's server offers it, its writes too unless --reads-only,
              or only the writes --writes names, or take that back; an update that adds or widens a tool asks again.
          press result <id>
              how a press this terminal sent to the running Daoris ended, when its answer did not reach the
              verb ("Daoris was still working on it (request …)"): asked again with the key it was sent with,
              for a day, or up to thirty while it waits for your Confirm. It sends nothing again.
          press withdraw <id>
              take back a press this terminal sent that waits for your Confirm in Daoris's window: its card goes
              and nothing is done. One you confirmed meanwhile is never taken back; Ctrl+C takes nothing back.
          --wait <minutes> | --no-wait
              with any command that presses: how long it waits on its Confirm in Daoris's window, 0 to 30 (the
              default 30). --no-wait returns once it is asked, naming its id; `press result <id>` says how it ended.
          quest pause <id>  ·  quest resume <id>  ·  quest abandon <id> [--reason "…" --yes]
              the same for one quest's work: stop it and start nothing of it until you resume it, or list what
              abandoning it would decline, discard and archive, and with --reason and --yes give it up.
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
          quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear ["…"]
              the same for a quest no ask published, before it starts: its chain's later steps follow it too.
          quest clear <id> [--failed] [--yes]
              list what clearing one closed quest's work (or, with --failed, its failed sessions) would take and keep;
              with --yes, clear it from this machine.
          quest retry <quest> [--at <n>]  ·  quest retry <quest> --session <id>
              start a parked quest again, counted from this machine's records or from n failures; or release a quest
              your stop of that session holds.
          remote list  ·  remote add <workspace> --url <url> [--key <key>]  ·  remote remove <workspace>
              this machine's remotes, one per workspace: what is wired (keys redacted), a workspace's deployment wired
              (its key from --key, DAORIS_REMOTE_KEY or typed in, never printed back), or unwired here alone.
          remote sync [status | dismiss <quest>] [--workspace <name>]
              one sync pass now, or where each workspace stands.
          remote sync forward <quest> <machine> <sequence>
              bring forward one side of two machines' work on a quest, as its conflict names it.
          repository connect [--workspace <name>] [--dry-run]
              register this checkout's repository with a knowledge service, any address: what it owns and what it
              accepts, so siblings know what to ask of it; --workspace wires it on this machine, in no tracked file.
          repository register [--repository <name>]
              register each repository with a checkout here, or the one named, from what its line declares,
              as `repository connect` would; a start and a line Daoris moves do the same on their own.
          repository retire [<name>] [--dry-run]  ·  repository wire <repository> --workspace <name> [--dry-run]
          repository import [<folder>] [--workspace <name>] [--dry-run]
              take a repository off this machine's registry (the registration only, no file touched); move a
              registered one to a workspace; or register a folder's subdirectories in one go, safe to run again.
          role [<id>]  ·  role set|remove|default|policy|rules <id> …
              the role catalogue: a role takes a kind of work in a place, on its choices. `role supervisor` says the
              supervisor's rules, and `role rules --place <place> --set <count>=<at>:<do>` sets a place's.
          role brief
              the brief of a role's holder (D209): what each role does, never does and how its verbs answer, by its
              revision; names no key. In a role's terminal only its roles' verbs and the reads they need run.
          session list [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
              this machine's sessions by what they need: waiting on you first, then to review, working,
              resumes later and ended.
          session stop <id> [--turn]  ·  session finish <id> [--note "…"]  ·  session decline <id> --reason "…"
              stop a live session (its quest is then held here until `daoris quest retry`), or finish or
              decline one that waits on you. --turn stops only its turn, keeping the session, so the words
              waiting for its turn's end go next, where its door can.
          session archive <id>… | --ended [--yes]  ·  session unarchive <id>…  ·  session delete <id>
              take ended sessions out of the list, or bring them back; delete a conversation that served no quest.
          session say <id> "…" [--file <path>]… [--level ordinary|urgent|now]
              say something to a session of this machine's: read at its next step or when its turn ends while it
              works, or, parked or ended, the same session goes on with it: a park's answer is said here. Prints
              where the words stand. --level is when it reads them while it works: ordinary at its turn's end,
              urgent (the default) at its next step, now cutting its step where its agent steers; no level stops
              its turn. It says what the door did, and why where it could not.
          session take-back <id> [<word>]  ·  session reword <id> <word> "…"
              take back one of your words waiting on a parked or ended session before it goes on with them, or put
              new words in its place; with no word, list those waiting with their ids.
          session go-on-new <id>
              words that wait for the account a session ran on to cool: go on now in a new session at the driver's
              next look, handed them, without that session's conversation.
          session start-from <id>
              words a session cannot go on with: start a conversation here with them, as `chat` does, without that
              session's context; they leave that session, naming the conversation.
          session hand-over <id> --agent <adapter> [--account <name>]
              a conversation goes on with another agent (D163): its own ends, and a new conversation in its tree
              starts, told what Daoris kept of it. Conversations only; a quest's session comes later.
          session hand-over <id> --approve|--decline
              your yes or no to a session's agent's own ask to hand it over that waits on you (the `handOver` rule's
              `ask`), as its page takes it: a yes hands it to the agent named at the driver's next look; a no says so.
          session helper <id> "<question>" [--agent <name>]
              ask another agent, read-only, to answer one question about a session's work (D163): the driver takes
              it at its next look, and the answer comes back to that session as its next turn, marked as the helper's.
          session helper --approve|--decline <ask>
              your yes or no to an agent's ask for a helper that waits on you (the `helpers` rule's `ask`), as its
              session's page takes it: a yes starts it at the driver's next look; a no refuses it and says so.
          session partner <id> "<piece>" --agent <name> [--paths <path>…]
              ask another agent to write one piece for a session in a clone of its own (D199): the driver judges it
              at its next look and starts it only where that agent's confinement was measured; its answer comes back
              to that session naming the commit to merge.
          session partner <id> --list  ·  session partner <piece> --stop
              a session's pieces and where each stands; a working piece's stop, which keeps its work.
          session handback <session|quest> [--json]
              one quest's handback whole (D219), by the quest's id or the id of the session that served it, open or
              closed: the note its session closed it with, how it answered each requirement, when and by which
              session. --json prints the service's answer as it gave it.
          session opinion ask <session> [--agent <adapter>] [--same-agent] ["…"]  ·  session opinion show <session|opinion>
          session opinion stop <opinion>  ·  session opinion anyway <session> ["…"]  ·  session opinion myself <session> ["…"]
              a session's second opinion before its work lands: ask another agent to read it (one of its rule's reviewers by
              name, or the working agent fresh), say where it stands with its findings and their answers, stop a reviewer
              reading, go on without a settled one, or record your own reading in its place. `session say` sends it back.
          session supervise [<id>]  ·  session supervise <id> --question <qid> --generation <g> --choice <label>|--leave <bound> (--reason "…" --source <where>)…
              the supervisor's holder's verb (D208), only in the terminal Daoris started for it: what waits, one
              question's fenced print, and the answer or the question left to the owner. No alone path.
          settings notify on|off  ·  settings strikes <n>  ·  settings cooloff <minutes>  ·  settings cap <n>
          settings keep [<kind> <rule>]  ·  settings handover …  ·  settings brainstorms …
          settings timeout <minutes>  ·  settings actor export [<file>]|import <file>|found [take|dismiss <n>]
              this machine's standing choices: whether a park or an end you did not ask for is said, when a quest
              parks after failed sessions, how long an account cools, the cap, how long each kind is kept, a hand-over's
              and a brainstorm's rules, and the staffing copy's export, import and found changes; timeout is retired, no
              session is limited by time, and the agents each kind of work runs on are `role set`'s.
          space [--json] [--measure]
              the free space and each kind Daoris keeps, This Daoris's Disk space twin: its size and when it was
              counted, its rule, when it next goes and when it was last cleaned. --measure counts each kind first,
              each within its own bound. Reads only; removes nothing.
          space free [--kind <id>]... [--early <group>]... [--yes]
              Free up space…'s twin: lists what each rule takes now and what could go early, and changes
              nothing; with --yes removes what the rules take (--kind: set-aside, downloads, logs), and
              with --early a group too (set-aside, downloads, browser-cache, left-over, update-failed),
              each judged again, then reads the run to its end. Confirmed in the window where Daoris runs.
          steward acts  ·  steward drive|undrive|hold|resume <repo>  ·  steward trees <repo> on
          steward stop|finish <session>  ·  steward retry <quest>
              the steward's holder's verbs (D209), only in the terminal Daoris started for it: what the steward did
              this run, or one act sent to the running Daoris with a proof by the role's key, recorded as the
              steward's. No alone path.
          steward desk  ·  steward desk decline <request> "<reason>"
              the steward's desk (D209), read in its terminal: each request fenced as its caller's claims, and the
              holder's decline of one with its reason, sent as its acts are.
          steward request <act> <target> "<why>"
              asks the steward for one of its acts, from any terminal, with no key: kept at the running Daoris's
              desk (ten open per caller), it grants nothing, and the steward answers it by an act or declines it.
          tool list  ·  tool path <tool>  ·  tool use <tool> system|managed [<version>]|file <path>  ·  tool download|update <tool>
          tool delete <tool> <version>  ·  tool locations [add|remove <address>]  ·  tool look  ·  tool git …
              the programs Daoris runs beside its agents: a change goes to the running Daoris's window to confirm where
              one runs; a download fetches holding nothing and is published under the home's lease, waited for;
              `tool --help` says each verb's words.
          trace <commit|session|quest>  ·  trace commit|session|quest <id>
              one read back to the ask: the person's words and go-aheads, the quest's requirements and answers, each
              session's agent, account, instruction, rules and landing, each from the store that keeps it. Reads only.
          tree list  ·  tree remove <path|session|branch> [--repository <name>] [--force]
          tree remove <landing branch> [--repository <name>] --why "…" [--plan]
          tree clean [--workspace <name>] [--yes]  ·  tree land <session> [--plan] [--branch <name>]
          tree land --entry <id> | --ask <id> [--leave-out <session>]... [--plan]
          tree hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]
          tree state [--entries]  ·  tree state <session|branch> [--repository <name>]
          tree sync [--repository <name>] [--workspace <name>] [--all] [--yes]
              this machine's session trees and the branches its landings made; remove takes a failed or
              superseded attempt's branch, with its tree where it is still here, on --force, and a branch a landing
              made whose work the line will never hold is withdrawn with --why, its commits kept on a recovery ref
              and its record saying why (--plan says what it would do); state asks a
              landed branch's plugin again whether its pull request completed, and says what that proves here;
              sync brings each repository up to date after a pull request merged, listed first (it fetches,
              never pushes): those holding Daoris's branches, every one with --all. For clean and sync,
              --workspace takes one workspace's checkouts alone, as its Branches tab does. state alone says each
              unit of work's milestone, as `work state` does (FABRIC-MILESTONE1a).
          update [--install <folder>]  ·  update --when-idle | --now | --cancel [--install <folder>]
              what is staged beside the install and how the last swap ended; or install it when idle (the
              default), now (ending what runs as a close does), or not now, as the window's banner does.
          wiring [--json] [--measure]
              how this Daoris is wired, Settings → This Daoris's twin: its folders, service, loop, settings files,
              variables and where each came from, versions and what a new terminal finds; what only the window
              knows says so. --measure walks the folders' sizes first. Never a key's value. Reads only.
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
          workflow show --repository <repo>|--workspace <name> [--kind <kind>]  ·  workflow show <id>[@<version>]
          workflow list  ·  workflow defaults  ·  workflow new <id> --from <default>|<id>[@<v>] [--with-opinion] [--with-look] [--name "…"]
          workflow edit|apply <id> <file> [--base <v>]  ·  workflow export <id> [--to <file>]  ·  workflow import <file>
          workflow use <id>|--clear --repository <repo>|--workspace <name> [--kind <kind>|--landed]
          workflow kind <name> <kind> --label "…" [--paths <p,…>]
              the workflows you named, under the home's workflows/, each a list of versions never edited, and the
              defaults; which one a place's work follows, or a kind of task's; and the kinds of task a workspace
              declares. Each change saved as the next version, said step by step and your part first (--plan: say
              it, save nothing).
          workflow run --session <id> | --quest <id> | --ask <id>
              where a piece of work stands in its workflow, as the session's Workflow view draws it: each step with its
              state and what it says, the step the run stands at marked. A session's run is its quest's. Reads only.
          workflow keep <session> ["…"]
              keep the workflow a kind of task chose for a session's work that changed paths outside the kind's, where
              it asks less of you than the one it would follow without the kind: your say-so, with your words.
          workflow skip <run> <node> "…"
          workflow stop <run> <node> ["…"]
              pass a node of a run bound to a graph version over, with your words, or stop it: kept on the run's record,
              and Daoris's next look applies it. `workflow run` names the run and its nodes. Daoris's window confirms it.
          workflow changes <run> <node> "…"
              ask for changes on a run's review in place of its Accept, with your words: the work's session is told
              them as its next turn, and what it commits is reviewed again. Daoris's window confirms it.

        Only `drive` starts a loop. DAORIS_HOME names the home; DAORIS_SERVICE_URL the service, which
        `drive` and every command that reads a record need.
        """;

    /// <summary>
    /// One group's lines of <see cref="Usage"/> (<c>daoris help &lt;group&gt;</c>): each line that begins with its name, and the lines
    /// that describe them; null for a group with none.
    /// </summary>
    public static string? UsageOf(string group)
    {
        List<string> lines = [];
        var taking = false;
        foreach (var line in Usage.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal))
            {
                var first = line[2..].Split(' ', 2)[0];
                taking = first == group;
            }
            else if (!line.StartsWith("      ", StringComparison.Ordinal))
            {
                taking = false;
            }

            if (taking) lines.Add(line);
        }

        return lines.Count == 0 ? null : $"usage: daoris {group} …\n\n{string.Join("\n", lines)}";
    }

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

        // One spelling (ONEWAY1ai-i): a mode flag before the verb is the tree's refusal, naming `drive`.
        if (args[0] != "drive")
        {
            problem = $"no verb `{args[0]}` — the loop is `drive`.";
            return null;
        }

        var flags = args.Skip(1);

        bool once = false, untilIdle = false, share = false;
        int? quietEnd = null;
        using var words = flags.GetEnumerator();
        while (words.MoveNext())
        {
            var flag = words.Current;
            switch (flag)
            {
                case "--once": once = true; break;
                case "--until-idle": untilIdle = true; break;
                case "--share": share = true; break;
                // SESSWATCH1a: a whole number of minutes, at least one.
                case "--quiet-end":
                    if (!words.MoveNext() || !int.TryParse(words.Current, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
                    {
                        problem = "`--quiet-end` takes a whole number of minutes, at least 1 — e.g. `--quiet-end 30`.";
                        return null;
                    }

                    quietEnd = minutes;
                    break;
                default:
                    problem = $"`drive` takes --once, --until-idle, --quiet-end and --share, not `{flag}`.";
                    return null;
            }
        }

        if (once && untilIdle)
        {
            problem = "`--once` and `--until-idle` are two modes — name one.";
            return null;
        }

        if (quietEnd is not null && !once && !untilIdle)
        {
            problem = "`--quiet-end` is for a run that must return, `--once` or `--until-idle`; the watch waits for the person.";
            return null;
        }

        return new LoopRequest(once ? LoopMode.Once : untilIdle ? LoopMode.UntilIdle : LoopMode.Watch, share) { QuietEnd = quietEnd };
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
