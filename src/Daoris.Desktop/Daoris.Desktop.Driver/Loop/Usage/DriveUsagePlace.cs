namespace Daoris.Driver;

/// <summary>The usage's groups from <c>place</c> to <c>role</c>.</summary>
internal static class DriveUsagePlace
{
    public const string Text =
        """
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
          place <repository> --room <path> --purpose "…" (--plan | --apply)
              a room: a folder of an adopted repository with an AGENTS.md of its own, asked of that
              repository's own session as one quest carrying the doctrine's room convention, the
              repository's own declaration and your reason; Daoris writes none of the room. It rides
              with no other part, and is refused for a path outside the repository, a room already
              there, a repository not registered, held or not adopted. --apply is your press.
          place --workspace <name> --adoption [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]
          place --workspace <name> --pause | --resume | --stop
              adopt a workspace one repository at a time, the ones other work touches first: a press
              writes a plan the loop works, adding the doctrine tool's verbs to the workspace's rules
              once, and pausing after a pilot of two. --plan prints the list and each refusal; --pause,
              --resume (which judges the skipped again) and --stop steer a plan already made.
          place list  ·  place drive|undrive|hold|resume <repository>
          place line|across|standing|language|review|opinion|helpers|commits <repository> …
              each place's standing choices: what is driven and held, its line, what agents outside it read, its
              standing answer, its language, where its work is shown to you, its second opinion (--agents <a,b>),
              its helpers and how it commits; a change a route takes goes to the
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
          plugin try-again <id>
              an unsound plugin started again by the running Daoris, its count of exits afresh.
          plugin switch <id> <what> on|off [--place <place>]
              your word on one of a plugin's interfaces or points at one place, the running Daoris looking at once.
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
          repository add <folder> [--workspace <name>] [--exclude <glob>]... [--dry-run]
              register a folder that is no git repository as a department worked in place, with its own daoris.json
              read from disk where it holds one; --exclude names files its manifest passes by beside the defaults.
          repository register [--repository <name>]
              register each repository with a checkout here, or the one named, from what its line declares,
              as `repository connect` would; a start and a line Daoris moves do the same on their own.
          repository retire [<name>] [--dry-run]  ·  repository wire <repository> --workspace <name> [--dry-run]  ·  repository excludes <folder> [--add <glob>]… [--remove <glob>]…
          repository import [<folder>] [--workspace <name>] [--dry-run]
              take a repository off this machine's registry (the registration only, no file touched); move a
              registered one to a workspace; or register a folder's subdirectories in one go, safe to run again.
          repository create <name> --purpose "…" [--kind git|folder] [--workspace <name>] [--under <folder>] [--line <branch>] [--work "…"] [--plan]
              open a new department: its folder beside the workspace's repositories (or under --under), an empty
              history with one commit as the identity they use, its registration, its drive and its set-up quest,
              and nothing pushed. --plan says what it would do and writes nothing; a press of the same words goes
              on from a receipt, and a failure leaves the folder, which retire does not remove.
          role [<id>]  ·  role set|remove|default|policy|rules <id> …
              the role catalogue: a role takes a work in a place, on its choices.
          role brief
              the brief of a role's holder (D209): what each role does, never does and how its verbs answer, by its
              revision; names no key. In a role's terminal only its roles' verbs and the reads they need run.
        """;
}
