namespace Daoris.Driver;

/// <summary>The usage's groups from <c>space</c> to <c>wiring</c>, and its closing lines.</summary>
internal static class DriveUsageMachine
{
    public const string Text =
        """
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
}
