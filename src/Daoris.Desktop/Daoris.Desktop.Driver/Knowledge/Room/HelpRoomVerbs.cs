using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The verbs the helper presses, under <i>What you may do</i>: each with the rule its route's view judges it by, said so the
/// helper presses what the route takes rather than learning it from a refusal.
/// </summary>
internal sealed class HelpRoomVerbs : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("### The verbs you press\n\n");
        text.Append("- Every setting is pressed, its card the press's own sentence and command: `daoris place drive|undrive|hold|resume <repository>`,\n");
        text.Append("  `daoris place line|language <repository> …` (`--workspace <name>` for a whole workspace),\n");
        text.Append("  `daoris place review|opinion|helpers|commits <repository> …`, `daoris place standing <repository> …`,\n");
        text.Append("  `daoris place across <repository> read|write-to …`, `daoris quest retry <quest>`, `daoris git use …` and\n");
        text.Append("  `daoris settings notify|cap|cooloff|quiet|keep …`.\n");
        text.Append("  `daoris quest retry` takes a quest parked by its failed sessions, or held by the person's stop, from this machine's lists below,\n");
        text.Append("  as its page's *Try again* does; a quest on neither list is refused.\n");
        // A new ask is judged at the ask by the verb's own judges (ASK_NEW's view).
        text.Append("- Something to start is asked by pressing, as the page's composer asks it: `daoris ask --workspace <name> \"…\"`,\n");
        text.Append("  with `--short \"…\"` for a short title (at most 40 characters, the few words that tell it apart in a list, the person's own\n");
        text.Append("  where they gave one) and `--review rule|on|<environment>|off` for the review the person said they want. Its card shows the\n");
        text.Append("  person the ask; one the verb would refuse is answered to you at once and never shown.\n");
        text.Append(RoomsProposal.Ask());
        text.Append("- An agent's Update or a pin to one version is pressed, as Agents → the agent's page → Ways in offers them:\n");
        text.Append("  `daoris agent update <agent>`, `daoris agent pin <agent> <version>`. Update moves a pinned agent's pin to its newest\n");
        text.Append("  release, or runs an unpinned one's own updater, and is offered only where that screen shows Update; a pin names one\n");
        text.Append("  exact release, like 2.1.300, never `latest`.\n");
        text.Append("- Daoris's browser's settings are pressed, as Settings → Browser sets them: `daoris browser use daoris|edge`,\n");
        text.Append("  `daoris browser links system|daoris`, `daoris browser extensions offer|refuse` and `daoris browser favorite add|remove <address>`\n");
        text.Append("  (`--title` names a favorite added); a favorite removed is one the list below keeps. Each but `links` holds from the browser's next start.\n");
        // Daoris's accounts for an agent are one set that serves every workspace, so these name none.
        text.Append("- How Daoris's accounts for an agent are used, ordered and ended early are pressed, and name no workspace:\n");
        text.Append("  `daoris agent account use <agent> …` (a way to use them, `--keep`, `--switch`, `--curve`), `daoris agent account order <agent> …`\n");
        text.Append("  and `daoris agent account ready <agent> <account>|--own` (*Try now*; `--own` ends only an old cool-off of the tool's own sign-in).\n");
        text.Append("- An account's own model and effort are pressed, as Agents → the agent's page → Model and effort sets them:\n");
        text.Append("  `daoris agent settings <agent> --account <name> model <model> effort <effort>`, for a tool whose settings Daoris knows and one\n");
        text.Append("  of Daoris's accounts, never the tool's own sign-in. The values are the tool's own: one of its model aliases or a full model id,\n");
        text.Append("  and an effort of low, medium, high or xhigh. `max` is for one conversation, never an account's default; `unset` returns either\n");
        text.Append("  to the tool's own default.\n");
        text.Append("- A quest or an ask made by mistake (a duplicate, a test) is deleted by pressing:\n");
        text.Append("  `daoris quest delete <id>` or `daoris ask --delete <id>`, as the quest's drawer and the ask's record delete them. Only an open\n");
        text.Append("  quest nobody has started on can go, and an ask goes with every quest it became, or not at all. Never press the delete of a\n");
        text.Append("  taken, done or declined quest: its record stays, the press is answered to you at once and never shown, and declining it\n");
        text.Append("  with the reason is the way instead. A delete cannot be undone.\n");
        // A session's delete is judged as Sessions' Delete… is; Ask Daoris's own conversations are deleted in its History.
        text.Append("  A session goes by `daoris sessions delete <id>` only where Sessions offers Delete…: a conversation that served no quest, ended,\n");
        text.Append("  this machine's, named by no ask, quest or landing, and with no tree here. One that worked on a quest\n");
        text.Append("  is archived instead, and Ask Daoris's own conversations are deleted in its History.\n");
        text.Append("- A plugin is added, updated or switched by pressing, as the Plugins place does: `daoris plugin add <folder>`\n");
        text.Append("  (a plugin that has landed, its folder's whole path in the checkout of the repository that holds it), `daoris plugin add\n");
        text.Append("  --offer <id>` (one of Daoris's own plugins, below), `daoris plugin update <id> --yes` (a newer copy from where it came\n");
        text.Append("  from) and `daoris plugin enable|disable <id>`. Its card shows the person what it will run, or what an update changes.\n");
        text.Append("  `daoris plugins approve <id> <server>` asks the person's approval of what a plugin's server offers you, with\n");
        text.Append("  `--writes <tool>,…` naming only the writes outside Daoris they said they accept, or `--reads-only`; its card names every\n");
        text.Append("  tool, and you are handed it from your next conversation. `daoris plugins withdraw <id> <server>` takes one back.\n");
        // Bring up to date, judged at the ask by its list (TREES_SYNC's view).
        text.Append("- Repositories are brought up to date after a pull request merged by pressing, as the workspace's page →\n");
        text.Append("  Branches → Updates does: `daoris tree sync --yes`, with `--repository <name>` or `--workspace <name>` to take those alone,\n");
        text.Append("  or `--all` for every one with a checkout here. Each line is fetched from `origin` as the person and fast-forwarded, the\n");
        text.Append("  branches still at work replayed onto it, the landed branches whose work reached it deleted. Its card shows the person each\n");
        text.Append("  row that moves. Daoris never pushes.\n");
        // A quest's yes and a landed branch's hand-off stay in their own place, which a link reaches.
        text.Append("- Never pressed for the person: accepting a quest's departure, and handing a landed branch to a landing plugin. They are the\n");
        text.Append("  person's own presses, on the quest's page and in the review, where a link takes them.\n");
        // Adding or importing needs a folder, which stays the person's pick, so a link opens the drawer with the workspace filled.
        text.Append("- A repository registered here is moved to a workspace by pressing:\n");
        text.Append("  `daoris repository wire <repository> --workspace <name>`, a workspace the room lists or a new name, which the\n");
        text.Append("  move starts. Its card shows the person the move; one the registry would refuse is answered to you at once and never shown.\n");
        text.Append("  Adding or importing a repository needs a folder, which stays the person's pick: a link opens\n");
        text.Append("  Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.\n");
        // The role catalogue's change, judged at the ask by the role door's judges (SET_ROLE's view).
        text.Append("- A role of this machine's role catalogue is added, changed or removed by pressing, as Agents → Roles\n");
        text.Append("  does: `daoris role set <id> …` (its names, work, scope and choices, or a supervisor's kinds, acts, holder and window),\n");
        text.Append("  `daoris role remove <id>`, `daoris role default <id>|--all`, a role's policy `daoris role policy <id> …` and a place's\n");
        text.Append("  supervisor rules `daoris role rules --place <place> …`. A removed role's work waits until a role takes it; running work\n");
        text.Append("  goes on. Its card shows the person the change; one the role door would refuse is answered to you at once and never shown.\n");
        // Sessions' Archive and Unarchive, judged as the list judges them, pressed by id. A stop is the session's own
        // conversation, which a link reaches.
        text.Append("- Sessions are archived or unarchived by pressing: `daoris sessions archive <id>…` and\n");
        text.Append("  `daoris sessions unarchive <id>…`, by id as Sessions lists them. An archived session leaves the list with its record\n");
        text.Append("  whole, and a teammate's may be archived here too. A session still running, in Waiting on you or in To review is never\n");
        text.Append("  archived: archive never hides what needs the person, and the press is answered to you at once and never shown.\n");
        text.Append("  Stopping a session is never pressed for the person: it is their own, on the session's page, where a link takes them.\n");
        // The person's helper doors, as the page's Ask another agent…, Ask again… and Pass to the lead are.
        text.Append("  `daoris sessions helper <id> \"…\"` asks another agent, read-only, one question about one session's work, and\n");
        text.Append("  `daoris sessions pass <ask>` sends an answer kept for the person to the session that asked.\n");
        // The pages' Pause… and Resume. Abandon is exempt: it declines quests with the person's reason, which is their answer.
        text.Append("- One ask's work, or one quest's, is paused on this machine or resumed by pressing:\n");
        text.Append("  `daoris ask --pause <id>`, `daoris quest pause <id>` and their `--resume` and `resume`. A pause stops what of the work runs\n");
        text.Append("  here as the person's own stop and starts nothing of it until it is resumed; a session waiting on the person stays parked,\n");
        text.Append("  a running intake keeps reading, and a teammate's session is never reached. Resume lifts the pause and releases its stops;\n");
        text.Append("  another pause over a quest of the work stays.\n");
        text.Append("  Abandoning is never pressed for the person: it declines quests with the person's reason, which is their answer, so name the\n");
        text.Append("  ask's or the quest's page → Abandon…, or `daoris ask abandon <id>`, and never write the reason.\n");
        // The pages' clears, judged at the ask by their first press's list (HISTORY_CLEAR's view).
        text.Append("- Finished history is cleared from this machine by pressing, as the pages' *Clear from this machine…* do:\n");
        text.Append("  `daoris quest clear <id> --yes` (a closed quest's work), `daoris ask clear <id> --yes` (an ask's work with every quest it\n");
        text.Append("  became), `daoris quest clear <id> --failed --yes` (a closed quest's failed sessions alone, the quest and its other sessions\n");
        text.Append("  staying) and `daoris history clear --workspace <name> --yes`. Its card shows the person what would go; nothing brings it\n");
        text.Append("  back. A quest an ask asked is cleared with its ask, never alone; work still open or taken, a session still running and\n");
        text.Append("  anything waiting on the person are never cleared, and such a press is answered to you at once and never shown.\n");
        // A workflow's version is pressed from a draft the room keeps, its card the design (WORKFLOW_SAVE's view).
        text.Append($"- A workflow's new version is saved by pressing, as its editor's Save does: write its whole graph (the\n");
        text.Append($"  JSON object, schemaVersion 2, its nodes and wires) as a draft in this room's `{KnowledgeRoomPaths.Drafts}/` folder, the one place you\n");
        text.Append($"  may write, and press `daoris workflow apply <id> {KnowledgeRoomPaths.Drafts}/<id>.json --base <version>`, the base its newest version\n");
        text.Append("  as `daoris workflow show <id>` says it. Its card draws the graph for the person as the editor draws it, with each change\n");
        text.Append("  against the version it replaces; a save the route would refuse (a stale base, a graph that does not hold, a place it\n");
        text.Append("  reaches that cannot start it) is answered to you at once and never shown. A new workflow starts from a default or a\n");
        text.Append("  saved one, `daoris workflow new <id> --from <default>|<id> --name \"…\"`, and your draft is applied onto it. Where the\n");
        text.Append("  person has a workflow open in its editor, its draft goes beside their words as Daoris told it: write yours from it.\n");
        text.Append("  Where work follows it is chosen by `daoris workflow use <id> --repository|--workspace <name> [--kind <kind>|--landed]`.\n");
        text.Append("  Merging with nobody is never pressed: *land without my review* is a pull request opened with no press, which the\n");
        text.Append("  person still merges, and a merge at done is a decision of its own, which you say.\n");
        // A place's set-up is judged at the ask by its plan (PLACE_APPLY's view).
        text.Append("- A place's set-up is applied by pressing, as its page's *Set up…* does: `daoris place <repository>… --preset <id> --apply`,\n");
        text.Append("  the repositories named as `registry` lists them and all in one workspace, `keep` leaving how their work lands as it is,\n");
        text.Append("  with `--with opinion,look`, `--adoption [--what-it-builds <kind>]` and `--drive` as the person asks, and\n");
        text.Append("  `--accept <repository>:<purpose>,…` where the plan asks one. Adoption asks each repository's own session for one\n");
        text.Append("  quest, which takes up the shared doctrine and writes down what the repository owns and its instructions on a\n");
        text.Append("  branch of its own, landed by its workspace's rule, with a rule letting its sessions run the doctrine tool; where a\n");
        text.Append("  repository is not driven here, its quest starts alone and nothing else of it does. `--plan` in place of `--apply`\n");
        text.Append("  prints the plan and asks nothing. Its card shows the person each change the plan makes; a plan its own rules refuse\n");
        text.Append("  (no tree of its own, no doctrine tool, a set-up already open) is answered to you at once and never shown.\n");
        text.Append("  Daoris never writes into a repository.\n\n");
        return text.ToString();
    }
}
