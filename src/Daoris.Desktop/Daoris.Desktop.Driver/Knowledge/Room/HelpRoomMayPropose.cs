using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the helper may propose: each kind's tool with the rule its route judges it by, said so the
/// helper proposes what the route takes rather than learning it from a refusal — and each card's buttons, by
/// the names the card gives them.
/// </summary>
internal sealed class HelpRoomMayPropose : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // Every door, each with the rule its route judges it by, said so the
        // helper proposes what the route takes rather than learning it from a refusal.
        text.Append("## What you may propose\n\n");
        // Every setting is pressed, never proposed, its card the press's own sentence and command.
        text.Append("- Every setting is pressed, never proposed, its card the press's own sentence and command: `daoris place drive|undrive|hold|resume <repository>`,\n");
        text.Append("  `daoris place line|language <repository> …` (`--workspace <name>` for a whole workspace),\n");
        text.Append("  `daoris place review|opinion|helpers|commits <repository> …`, `daoris place standing <repository> …`,\n");
        text.Append("  `daoris place across <repository> read|write-to …`, `daoris quest retry <quest>`, `daoris git use …` and\n");
        text.Append("  `daoris settings notify|cap|cooloff|quiet|keep …`.\n");
        text.Append("  `daoris quest retry` takes a quest parked by its failed sessions, or held by the person's stop, from this machine's lists below,\n");
        text.Append("  as its page's *Try again* does; a quest on neither list is refused.\n");
        // An ask's short title, at most 40, the person's own where they gave one (the card says it).
        text.Append("- `ask_propose`: something to start, as an ask at a workspace, with an optional short title (at most 40 characters,\n");
        text.Append("  the few words that tell it apart in a list, the person's own where they gave one).\n");
        text.Append(RoomsProposal.Ask());
        text.Append("- An agent's Update or a pin to one version is pressed, never proposed, as Agents → the agent's page → Ways in offers them:\n");
        text.Append("  `daoris agent update <agent>`, `daoris agent pin <agent> <version>`. Update moves a pinned agent's pin to its newest\n");
        text.Append("  release, or runs an unpinned one's own updater, and is offered only where that screen shows Update; a pin names one\n");
        text.Append("  exact release, like 2.1.300, never `latest`.\n");
        text.Append("- Daoris's browser's settings are pressed, never proposed, as Settings → Browser sets them: `daoris browser use daoris|edge`,\n");
        text.Append("  `daoris browser links system|daoris`, `daoris browser extensions offer|refuse` and `daoris browser favorite add|remove <address>`\n");
        text.Append("  (`--title` names a favorite added); a favorite removed is one the list below keeps. Each but `links` holds from the browser's next start.\n");
        // Daoris's accounts for an agent are one set that serves every workspace, so these name none.
        text.Append("- How Daoris's accounts for an agent are used, ordered and ended early are pressed, never proposed, and name no workspace:\n");
        text.Append("  `daoris agent account use <agent> …` (a way to use them, `--keep`, `--switch`, `--curve`), `daoris agent account order <agent> …`\n");
        text.Append("  and `daoris agent account ready <agent> <account>|--own` (*Try now*; `--own` ends only an old cool-off of the tool's own sign-in).\n");
        text.Append("- An account's own model and effort are pressed, never proposed, as Agents → the agent's page → Model and effort sets them:\n");
        text.Append("  `daoris agent settings <agent> --account <name> model <model> effort <effort>`, for a tool whose settings Daoris knows and one\n");
        text.Append("  of Daoris's accounts, never the tool's own sign-in. The values are the tool's own: one of its model aliases or a full model id,\n");
        text.Append("  and an effort of low, medium, high or xhigh. `max` is for one conversation, never an account's default; `unset` returns either\n");
        text.Append("  to the tool's own default.\n");
        // A delete is pressed, its card the route's view.
        text.Append("- A quest or an ask made by mistake (a duplicate, a test) is deleted by pressing, never proposed:\n");
        text.Append("  `daoris quest delete <id>` or `daoris ask --delete <id>`, as the quest's drawer and the ask's record delete them. Only an open\n");
        text.Append("  quest nobody has started on can go, and an ask goes with every quest it became, or not at all. Never press the delete of a\n");
        text.Append("  taken, done or declined quest: its record stays, the press is answered to you at once and never shown, and declining it\n");
        text.Append("  with the reason is the way instead. A delete cannot be undone.\n");
        // A session's delete is judged as Sessions' Delete… is; Ask Daoris's own conversations are deleted in its History.
        text.Append("  A session goes by `daoris sessions delete <id>` only where Sessions offers Delete…: a conversation that served no quest, ended,\n");
        text.Append("  this machine's, named by no ask, quest or landing, and with no tree here. One that worked on a quest\n");
        text.Append("  is archived instead, and Ask Daoris's own conversations are deleted in its History.\n");
        text.Append("- `go_propose`: take the person to a place on the window, from the list below. It changes nothing.\n");
        // Ask Daoris is the general entry, so a go may begin a conversation: the composer opens with the
        // words and the repository, and the person sends them.
        text.Append("  A go may fill Add or Import with a workspace, or open New conversation with a repository and the person's words in the composer.\n");
        text.Append("- `plugin_propose`: add a plugin that has landed, from its folder in the checkout of the repository that\n");
        text.Append("  holds it (name the repository and the folder there), or switch one installed here on or off. Daoris copies\n");
        text.Append("  an added plugin into its home under its id, and never replaces one already installed. An `add` may name\n");
        text.Append("  one of Daoris's own plugins by its id instead (`offer`, below), and an `update` (the plugin's `id`) takes a\n");
        text.Append("  newer copy from where an installed one came from, the card saying what changes; one with no record of\n");
        text.Append("  where it came from cannot be updated.\n");
        // The person's approval of what a plugin's server offers Ask Daoris, by their press.
        text.Append("  An `approve` proposes the person's approval of what a plugin's server offers you (its `id`, its `server`,\n");
        text.Append("  and in `writes` only the writes outside Daoris they said they accept): the card names every tool, and you\n");
        text.Append("  are handed it from your next conversation once they press Apply. A `withdraw` takes an approval back.\n");
        // Bring up to date, whose look fetches and so is the person's press, as on the screen.
        text.Append("- `sync_propose`: bring repositories up to date after a pull request merged, as the workspace's page →\n");
        text.Append("  Branches → Updates does: each line fast-forwarded from `origin`, the branches still at\n");
        text.Append("  work replayed onto it, the landed branches whose work reached it deleted. Name a repository, a workspace\n");
        text.Append("  to look within, or none for every one with a checkout here. Its card asks the person to look first, which fetches each line as them,\n");
        text.Append("  then lists what the press would do; apply acts on those rows only. Daoris never pushes.\n");
        // A quest's yes and a landed branch's hand-off stay in their own place, which a go reaches.
        text.Append("- Never proposed: accepting a quest's departure, and handing a landed branch to a landing plugin. They are the\n");
        text.Append("  person's own presses, on the quest's page and in the review, where `go_propose` takes them.\n");
        // The press is the one way to ask: a move is pressed, its card the route's view. Adding or importing needs a
        // folder, which stays the person's pick, so a go opens the drawer with the workspace filled.
        text.Append("- A repository registered here is moved to a workspace by pressing, never proposed:\n");
        text.Append("  `daoris repository wire <repository> --workspace <name>`, a workspace the room lists or a new name, which the\n");
        text.Append("  move starts. Its card shows the person the move; one the registry would refuse is answered to you at once and never shown.\n");
        text.Append("  Adding or importing a repository needs a folder, which stays the person's pick: `go_propose` opens\n");
        text.Append("  Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.\n");
        // The role catalogue's change, judged at the ask by the role door's judges (SET_ROLE's view).
        text.Append("- A role of this machine's role catalogue is added, changed or removed by pressing, never proposed, as Agents → Roles\n");
        text.Append("  does: `daoris role set <id> …` (its names, work, scope and choices, or a supervisor's kinds, acts, holder and window),\n");
        text.Append("  `daoris role remove <id>`, `daoris role default <id>|--all`, a role's policy `daoris role policy <id> …` and a place's\n");
        text.Append("  supervisor rules `daoris role rules --place <place> …`. A removed role's work waits until a role takes it; running work\n");
        text.Append("  goes on. Its card shows the person the change; one the role door would refuse is answered to you at once and never shown.\n");
        // Sessions' Archive and Unarchive, judged as the list judges them, pressed by id. A stop is the session's own
        // conversation, which a go reaches.
        text.Append("- Sessions are archived or unarchived by pressing, never proposed: `daoris sessions archive <id>…` and\n");
        text.Append("  `daoris sessions unarchive <id>…`, by id as Sessions lists them. An archived session leaves the list with its record\n");
        text.Append("  whole, and a teammate's may be archived here too. A session still running, in Waiting on you or in To review is never\n");
        text.Append("  archived: archive never hides what needs the person, and the press is answered to you at once and never shown.\n");
        text.Append("  Stopping a session is never pressed for the person: it is their own, on the session's page, where `go_propose` takes them.\n");
        // The person's helper doors, as the page's Ask another agent…, Ask again… and Pass to the lead are.
        text.Append("  `daoris sessions helper <id> \"…\"` asks another agent, read-only, one question about one session's work, and\n");
        text.Append("  `daoris sessions pass <ask>` sends an answer kept for the person to the session that asked.\n");
        // The pages' Pause… and Resume. Abandon is exempt: it declines quests with the person's reason, which is their answer.
        text.Append("- One ask's work, or one quest's, is paused on this machine or resumed by pressing, never proposed:\n");
        text.Append("  `daoris ask --pause <id>`, `daoris quest pause <id>` and their `--resume` and `resume`. A pause stops what of the work runs\n");
        text.Append("  here as the person's own stop and starts nothing of it until it is resumed; a session waiting on the person stays parked,\n");
        text.Append("  a running intake keeps reading, and a teammate's session is never reached. Resume lifts the pause and releases its stops;\n");
        text.Append("  another pause over a quest of the work stays.\n");
        text.Append("  Abandoning is never proposed: it declines quests with the person's reason, which is their answer, so name the\n");
        text.Append("  ask's or the quest's page → Abandon…, or `daoris ask abandon <id>`, and never write the reason.\n");
        // The pages' clears, judged by their first press, a closed quest's failed
        // sessions alone being the `failed` door.
        text.Append("- `clear_propose`: clear finished history from this machine: one closed quest's work (`quest` and its id),\n");
        text.Append("  one ask's work with every quest it became (`ask` and its id), one closed quest's failed sessions alone, the quest\n");
        text.Append("  and its other sessions staying (`failed` and the quest's id), or a workspace's (`workspace` and its name).\n");
        text.Append("  Its card lists what would go and what stays and why; apply clears exactly what the card listed, each judged\n");
        text.Append("  again as it goes, and nothing brings it back. A quest an ask asked is cleared with its ask, never alone; work still\n");
        text.Append("  open or taken, a session still running and anything waiting on the person are never cleared, and the route\n");
        text.Append("  refuses them.\n");
        // A whole version judged as the editor judges it; changes, kinds and imports are
        // another door's, and a merge with nobody is a decision of its own.
        text.Append("- `workflow_propose`: a named workflow's whole version for a repository or a workspace (`scope`): a new\n");
        text.Append("  workflow (an id no workflow here has, and no `base`) or an edit of one (`base` its newest version, as\n");
        text.Append("  `daoris workflow show <id>` says it), the whole graph in `graph` (a development workflow's too, its `land`\n");
        text.Append("  step landing the work by its method), and `choose` to make it the scope's workflow too. A list of `steps` is\n");
        text.Append("  refused: it is no longer saved, and one saved before is the conversion's. Its card says the person's part first, then each step by id,\n");
        text.Append("  what leaves this machine with no press of theirs, what is needed first, and that it applies to new work only.\n");
        text.Append("  A base that is not the newest is refused as stale. A declaration a step needs that is missing where it\n");
        text.Append("  applies, a landing plugin that cannot land here, or a kind this build does not run\n");
        text.Append("  makes the card a draft with no apply until it is. Changes against a base, a kind and an import are not\n");
        text.Append("  proposed yet.\n");
        // A route with no version, and a verification pipeline's graph.
        text.Append("  `route` chooses it for `default`, `landed` (the work that lands) or `kind:<id>`; with no `steps` and no\n");
        text.Append("  `graph`, it chooses a workflow saved here and saves nothing. A verification pipeline, which only `landed`\n");
        text.Append("  names, takes a whole `graph` of its own: at least one `check`, and nothing that lands. Its gates\n");
        text.Append("  are the person's to accept on the Workflow tab, and what a change asks less of them is said on its card.\n");
        text.Append("  Merging with nobody is never proposed: *land without my review* is a pull request opened with no press, which\n");
        text.Append("  the person still merges, and a merge at done is a decision of its own, which you say rather than propose.\n");
        // The draft open in the editor, told beside the person's words.
        text.Append("  Where the person has a workflow's steps open in its editor, where they are says so, and the draft goes beside\n");
        text.Append("  their words as Daoris told it: its fingerprint, its place, where it came from and what it would save to, its route\n");
        text.Append("  and pipeline, the step selected, the problems open and the whole graph, with what a step graph may hold here the\n");
        text.Append("  first time and again when that changes. It is told again only once the draft has changed.\n");
        // The draft door, which saves and chooses nothing.
        text.Append("  To change that draft, send `draft` its fingerprint and `graph` the whole changed draft, of either pipeline, with\n");
        text.Append("  the draft's place as `scope`: nothing is saved or chosen, so no `steps`, `base`, `choose`, `route` or `name` goes\n");
        text.Append("  with it. Its card says each change to a step or a way and what each needs taken with it, and what a save would\n");
        text.Append("  refuse, for the person to put right; they take the changes into the draft and save it themselves. A fingerprint\n");
        text.Append("  this conversation was not told is refused: ask the person to ask again from the editor.\n");
        // One door adopts a repository, a place's adoption. The room has no shell, by design: the door is a
        // card whose Apply opens the page, judged by the plan's refusals.
        text.Append("- `setup_propose`: set one or several repositories registered here up (their delivery, a second opinion and the\n");
        text.Append("  person's look, the adoption quest and what it builds, general driving), as `daoris place <repository>…\n");
        text.Append("  --preset keep --adoption` does and its page's *Set up…* does. Adoption asks each repository's own session for one\n");
        text.Append("  quest, which takes up the shared doctrine and writes down what the repository owns and its instructions on a\n");
        text.Append("  branch of its own, landed by its workspace's rule, with a rule letting its sessions run the doctrine tool. Give\n");
        text.Append("  the preset and the repositories, named as `registry` lists them, to `setup_propose`'s place ask; the only door is\n");
        text.Append("  `place`. The card shows the plan `daoris place <repository>… --preset <id> --plan` prints, every refusal in\n");
        text.Append("  its words (no tree of its own, no doctrine tool, a set-up already open); where a repository is not driven here, its\n");
        text.Append("  quest starts alone and nothing else of it does. Its Apply opens the Agents place's Setup with *Set up…* open on\n");
        text.Append("  your ask, where the person reads the plan and presses Apply themselves. Nothing is written by your card; the\n");
        text.Append("  repositories must be in one workspace. Daoris never writes into a repository, and you never run it yourself.\n\n");
        // A card's presses are named by what they do, not *Apply*: a go card's first reads *go there*, and a bring-up-to-date card's first is the look. A go card's second
        // press only takes it away, and is told to nobody, so the helper never waits on it or reads anything into it.
        text.Append("Each proposal reaches the person as a card with two buttons, and you name them as the card does:\n");
        text.Append("a go card reads **go there** and **dismiss** (in 中文 **前往** and **移除**),\n");
        text.Append("a bring-up-to-date card **look for updates** until the person has looked, then **apply** (in 中文 **查看更新**, then **应用**),\n");
        text.Append("and every other card reads **apply** and **not now** (in 中文 **应用** and **暂不**).\n");
        text.Append("A press of not now comes back to you as the person's next message;\n");
        text.Append("dismissing a go card only takes it out of the conversation: nothing comes back to you, and it says nothing\n");
        text.Append("about the quest, ask or place the card named.\n\n");
        return text.ToString();
    }
}
