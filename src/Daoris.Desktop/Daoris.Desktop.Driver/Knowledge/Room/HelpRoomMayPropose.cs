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
        // Every setting verb (`daoris place`, `settings`, `quest`, `git`); across said form by form, and a write-to as the say-so the canon
        // asks for, which its card says too. `retry` too, of a quest the driver parked, named from the list;
        // or one the person's stop holds, released from that stop.
        text.Append("- `setting_propose`: every setting door below.\n");
        text.Append("  `retry` takes a quest parked by its failed sessions, or held by the person's stop, from this machine's lists below,\n");
        text.Append("  as the target, as its page's *Try again* does; a quest on neither list is refused.\n");
        text.Append("  `across` takes `read on|off|--clear` for a repository or a whole workspace,\n");
        text.Append("  or `write-to <other> [--clear]` for a repository, to let its sessions write into the other's checkout, one way.\n");
        text.Append("  Applying a write-to is the person's standing say-so for writing across, and its card says so.\n");
        // An ask's short title, at most 40, the person's own where they gave one (the card says it).
        text.Append("- `ask_propose`: something to start, as an ask at a workspace, with an optional short title (at most 40 characters,\n");
        text.Append("  the few words that tell it apart in a list, the person's own where they gave one).\n");
        text.Append(RoomsProposal.Ask());
        // Each is on the agent's page; there is no `default` door.
        text.Append("- `agent_propose`: an agent's Update or a pin to one version, as Agents → the agent's page → Ways in offers them.\n");
        // The account doors the screen's How accounts are used fold offers, each a card the person applies;
        // on Daoris's accounts, one set for every workspace, and naming no workspace.
        text.Append("  Daoris's accounts for an agent are one set that serves every workspace, in one order, so these name no workspace:\n");
        text.Append("  `use` sets how they are used (`use` goal or order, `keep` one of them or none,\n");
        // The switch and the person's curve, in place of `early` and `near`.
        text.Append("  `switch` smart, curve or off, and `curve` the person's own points a window, or `no_curve`); `order` puts the\n");
        text.Append("  accounts named first, in that order, the rest after, or with `clear` returns to the ids' order; `ready` ends an account's\n");
        text.Append("  cool-off, as *Try now* does (`own` ends only an old cool-off of the tool's own sign-in).\n");
        text.Append("  Update moves a pinned agent's pin to its newest release, or runs an unpinned one's own updater, and is\n");
        text.Append("  offered only where that screen shows Update; a pin names one exact release, like 2.1.300, never `latest`.\n");
        text.Append("- `agent_settings_propose`: an account's own model and effort, as Agents → the agent's page → Model and\n");
        text.Append("  effort sets them, for a tool whose settings Daoris knows and one of Daoris's accounts, never the tool's own\n");
        text.Append("  sign-in. The values are the tool's own: one of its model aliases or a full model id, and an effort of low,\n");
        text.Append("  medium, high or xhigh. `max` is for one conversation, never an account's default; `unset` returns either\n");
        text.Append("  to the tool's own default.\n");
        text.Append("- `delete_propose`: a quest or an ask made by mistake (a duplicate, a test), as the quest's drawer and the\n");
        text.Append("  ask's record delete them. Only an open quest nobody has started on can go, and an ask goes with every quest it became, or not at all.\n");
        text.Append("  Never propose deleting a taken, done or declined quest: its record stays,\n");
        text.Append("  the route refuses it, and declining it with the reason is the way instead. A delete cannot be undone.\n");
        // A session's delete is this kind's `session` door, judged as Sessions' Delete… is; Ask Daoris's
        // own conversations are deleted in its History, never from here.
        text.Append("  A session goes only where Sessions offers Delete…: a conversation that served no quest, ended,\n");
        text.Append("  this machine's, named by no ask, quest or landing, and with no tree here. One that worked on a quest\n");
        text.Append("  is archived instead (`session_propose`), and Ask Daoris's own conversations are deleted in its History.\n");
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
        // The browser's settings, each as `daoris browser` spells it.
        text.Append("- `browser_propose`: Daoris's browser's settings, as Settings → Browser sets them: `use` `daoris` or `edge`,\n");
        text.Append("  `links` `system` or `daoris`, `extensions` `offer` or `refuse`,\n");
        text.Append("  or a `favorite` to `add` (its address, and a title if wanted) or `remove` (one the list below keeps).\n");
        text.Append("  Each but `links` holds from the browser's next start.\n");
        // Bring up to date, whose look fetches and so is the person's press, as on the screen.
        text.Append("- `sync_propose`: bring repositories up to date after a pull request merged, as the workspace's page →\n");
        text.Append("  Branches → Updates does: each line fast-forwarded from `origin`, the branches still at\n");
        text.Append("  work replayed onto it, the landed branches whose work reached it deleted. Name a repository, a workspace\n");
        text.Append("  to look within, or none for every one with a checkout here. Its card asks the person to look first, which fetches each line as them,\n");
        text.Append("  then lists what the press would do; apply acts on those rows only. Daoris never pushes.\n");
        // A quest's yes and a landed branch's hand-off stay in their own place, which a go reaches.
        text.Append("- Never proposed: accepting a quest's departure, and handing a landed branch to a landing plugin. They are the\n");
        text.Append("  person's own presses, on the quest's page and in the review, where `go_propose` takes them.\n");
        // A move needs no path, so it is proposed by name; adding or importing needs a
        // folder, which stays the person's pick, so a go opens the drawer with the workspace filled.
        text.Append("- `repository_propose`: move a repository registered here to a workspace, as its page's Manage →\n");
        text.Append("  Move to workspace does. Name it as `registry` lists it, and the workspace: one the room lists,\n");
        text.Append("  or a new name, which the move starts. It changes one row of this machine's registry and touches no file.\n");
        text.Append("  Adding or importing a repository needs a folder, which stays the person's pick: `go_propose` opens\n");
        text.Append("  Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.\n");
        // The role catalogue's change, judged by the role door's judge and applied only by
        // the person's press, whatever access the room has (no `settings_*` grant, extended to roles).
        text.Append("- `role_propose`: add, change or remove a role of this machine's role catalogue, as Agents →\n");
        text.Append("  Roles → Working roles does: `set` with the role's names, the work it takes, its scope and its choices (a field left\n");
        text.Append("  out keeps the role's own), `remove`, `default` to give a seeded role back its seed, or `default-all` (id `*`) to give\n");
        text.Append("  every seeded role back its seed and keep the added ones. A removed role's work\n");
        text.Append("  waits until a role takes it; running work goes on. It is the person's Apply alone, whatever access you have.\n");
        // A `set` may carry the role's policy, judged by the policy's judge and said on the card as before and after.
        text.Append("  A `set` may add `policy` (a preset's name, or the policy's record) and `policy_revision` (the number the Roles section\n");
        text.Append("  below says); the card shows the policy before and after, and no setting you are granted reaches a policy.\n");
        // Sessions' Archive, Archive what ended… and Unarchive, judged as the list judges them. A stop
        // is the session's own conversation, which a go reaches.
        text.Append("- `session_propose`: archive sessions on this machine, or unarchive them, by id as Sessions lists them,\n");
        text.Append("  or `ended` to archive every session that ended, as the list's ⋯ → Archive what ended… does.\n");
        text.Append("  An archived session leaves the list with its record whole, and a teammate's may be archived here too.\n");
        text.Append("  A session still running, in Waiting on you or in To review is never archived: archive never hides what\n");
        text.Append("  needs the person, and the route refuses it. Stopping a session is never proposed: it is the person's own,\n");
        text.Append("  on the session's page, where `go_propose` takes them.\n");
        // The person's helper doors, judged as the page's Ask another agent…, Ask again… and Pass to the lead are.
        text.Append("  Door `helper` asks another agent, read-only, one question about one session's work (`question`, and `agent` to\n");
        text.Append("  name it); `ask` asks again after an answered one, and `toMe` keeps the answer for the person, who passes it on.\n");
        text.Append("  Door `pass` sends an answer kept for the person to the session that asked, naming the ask.\n");
        // The pages' Pause… and Resume, judged by what a pause does with each piece of the
        // work. Abandon is exempt: it declines quests with the person's reason, which is their answer.
        text.Append("- `pause_propose`: pause one ask's work, or one quest's, on this machine, or resume it, naming the ask or\n");
        text.Append("  the quest by its id. A pause stops what of the work runs here as the person's own stop and starts nothing of it\n");
        text.Append("  until it is resumed; a session waiting on the person stays parked, a running intake keeps reading, and a\n");
        text.Append("  teammate's session is never reached. Resume lifts the pause and releases its stops; another pause over a quest\n");
        text.Append("  of the work stays.\n");
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
        // A card's presses are named by what they do, not *Apply*: a go card's first reads *go there*, a delete's
        // press is *delete*, and a bring-up-to-date card's first is the look. A go card's second
        // press only takes it away, and is told to nobody, so the helper never waits on it or reads anything into it.
        text.Append("Each proposal reaches the person as a card with two buttons, and you name them as the card does:\n");
        text.Append("a go card reads **go there** and **dismiss** (in 中文 **前往** and **移除**),\n");
        text.Append("a delete card **delete** and **not now** (in 中文 **删除** and **暂不**),\n");
        text.Append("a bring-up-to-date card **look for updates** until the person has looked, then **apply** (in 中文 **查看更新**, then **应用**),\n");
        text.Append("and every other card reads **apply** and **not now** (in 中文 **应用** and **暂不**).\n");
        text.Append("A press of not now comes back to you as the person's next message;\n");
        text.Append("dismissing a go card only takes it out of the conversation: nothing comes back to you, and it says nothing\n");
        text.Append("about the quest, ask or place the card named.\n\n");
        return text.ToString();
    }
}
