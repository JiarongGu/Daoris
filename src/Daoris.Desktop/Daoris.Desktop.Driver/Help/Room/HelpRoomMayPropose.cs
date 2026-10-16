using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the helper may propose (HELP6): each kind's tool with the rule its route judges it by, said so the
/// helper proposes what the route takes rather than learning it from a refusal — and each card's buttons, by
/// the names the card gives them (HELP7).
/// </summary>
internal sealed class HelpRoomMayPropose : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // HELP6: every door built since HELP1c, each with the rule its route judges it by, said so the
        // helper proposes what the route takes rather than learning it from a refusal.
        text.Append("## What you may propose\n\n");
        // HELP9: every `daoris driver` verb; across (D107) said form by form, and a write-to as the say-so the canon
        // asks for, which its card says too. HELP10: `retry` too, of a quest the driver parked, named from the list;
        // SESSUX1b: or one the person's stop holds, released from that stop.
        text.Append("- `setting_propose`: every door below that `daoris driver` spells.\n");
        text.Append("  `retry` takes a quest parked by its failed sessions, or held by the person's stop, from this machine's lists below,\n");
        text.Append("  as the target, as its page's *Try again* does; a quest on neither list is refused.\n");
        text.Append("  `across` takes `read on|off|--clear` for a repository or a whole workspace,\n");
        text.Append("  or `write-to <other> [--clear]` for a repository, to let its sessions write into the other's checkout, one way.\n");
        text.Append("  Applying a write-to is the person's standing say-so for writing across, and its card says so.\n");
        // HEAD2e: an ask's short title, at most 40, the person's own where they gave one (the card says it).
        text.Append("- `ask_propose`: something to start, as an ask at a workspace, with an optional short title (at most 40 characters,\n");
        text.Append("  the few words that tell it apart in a list, the person's own where they gave one).\n");
        // HELP10: an agent's default account, which HELP9 left owed. UX6e2: each is on the agent's page since UX6e.
        text.Append("- `agent_propose`: an agent's Update or a pin to one version, as Agents → the agent's page → Ways in offers them,\n");
        text.Append("  or which of its accounts it runs as by default, for the machine or one workspace (`default`,\n");
        text.Append("  the account one the room lists under that agent).\n");
        // TOOL4l-b (D130 §9): the account doors the screen's How accounts are used fold offers, each a card the person applies.
        text.Append("  `use` sets how an agent's list of accounts is used (`use` goal or order, `keep` one account of the list or none,\n");
        text.Append("  `early`, and `near` from 50 to 99); `order` writes the list in walk order, or with `clear` sends a workspace back\n");
        text.Append("  to this machine's accounts; `ready` ends an account's cool-off, as *Try now* does (`own` is the tool's own sign-in).\n");
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
        // SESSUX1h2b (D126 §5.4): a session's delete is this kind's `session` door, judged as Sessions' Delete… is; Ask Daoris's
        // own conversations are deleted in its History (ASKHIST1), never from here.
        text.Append("  A session goes only where Sessions offers Delete…: a conversation that served no quest, ended,\n");
        text.Append("  this machine's, named by no ask, quest or landing, and with no tree here. One that worked on a quest\n");
        text.Append("  is archived instead (`session_propose`), and Ask Daoris's own conversations are deleted in its History.\n");
        text.Append("- `go_propose`: take the person to a place on the window, from the list below. It changes nothing.\n");
        // NEW1d-a (D162's note): Ask Daoris is the general entry, so a go may begin a conversation: the composer opens with the
        // words and the repository, and the person sends them.
        text.Append("  A go may fill Add or Import with a workspace, or open New conversation with a repository and the person's words in the composer.\n");
        text.Append("- `plugin_propose`: add a plugin that has landed, from its folder in the checkout of the repository that\n");
        text.Append("  holds it (name the repository and the folder there), or switch one installed here on or off. Daoris copies\n");
        text.Append("  an added plugin into its home under its id, and never replaces one already installed. An `add` may name\n");
        text.Append("  one of Daoris's own plugins by its id instead (`offer`, below), and an `update` (the plugin's `id`) takes a\n");
        text.Append("  newer copy from where an installed one came from, the card saying what changes; one with no record of\n");
        text.Append("  where it came from cannot be updated.\n");
        // WSR5b: the review's own hand-off, for a ticket landed before its workspace named a plugin, or whose plugin failed.
        text.Append("- `hand_propose`: hand a branch a landing made (from the list below) to a landing plugin, which pushes it\n");
        text.Append("  and opens the pull request, signed in as the person. Name the session that landed it or the branch, and\n");
        text.Append("  a plugin only where the repository's landing rule names none. Only a branch a landing made and recorded\n");
        text.Append("  here can be handed on; the person's own branches are theirs to push.\n");
        // HELP10: the browser's settings, which HELP9 left owed, each as `daoris browser` spells it.
        text.Append("- `browser_propose`: Daoris's browser's settings, as Settings → Browser sets them: `use` `daoris` or `edge`,\n");
        text.Append("  `links` `system` or `daoris`, `extensions` `offer` or `refuse`,\n");
        text.Append("  or a `favorite` to `add` (its address, and a title if wanted) or `remove` (one the list below keeps).\n");
        text.Append("  Each but `links` holds from the browser's next start.\n");
        // HELP10: WSR6's Bring up to date (D109), whose look fetches and so is the person's press, as on the screen.
        text.Append("- `sync_propose`: bring repositories up to date after a pull request merged, as the workspace's page →\n");
        text.Append("  Branches → Updates does: each line fast-forwarded from `origin`, the branches still at\n");
        text.Append("  work replayed onto it, the landed branches whose work reached it deleted. Name a repository, a workspace\n");
        text.Append("  to look within, or none for every one with a checkout here. Its card asks the person to look first, which fetches each line as them,\n");
        text.Append("  then lists what the press would do; apply acts on those rows only. Daoris never pushes.\n");
        // DRIFT1d2 (D133 §4): the person's yes to a departure, as the quest page's *Accept the departure* gives it. The yes
        // is theirs, so the helper proposes it on their ask and never on its own reading of the departure.
        text.Append("- `accept_propose`: the person's yes to a quest a done's departure holds: it closed done departing from what\n");
        text.Append("  the person required, so the chain's next step and a quest waiting on it wait (`quest_list` marks it held for\n");
        text.Append("  the person's yes). Propose it only when the person asks to accept it, never on your own reading of the\n");
        text.Append("  departure. Its card shows each departure with the person's words it relied on, as the quest's page does.\n");
        // ENTRY1d1 (D161's ENTRY1d note): a move needs no path (D48 §7), so it is proposed by name; adding or importing needs a
        // folder, which stays the person's pick, so since ENTRY1d2a a go opens the drawer with the workspace filled.
        text.Append("- `repository_propose`: move a repository registered here to a workspace, as its page's Manage →\n");
        text.Append("  Move to workspace does. Name it as `registry` lists it, and the workspace: one the room lists,\n");
        text.Append("  or a new name, which the move starts. It changes one row of this machine's registry and touches no file.\n");
        text.Append("  Adding or importing a repository needs a folder, which stays the person's pick: `go_propose` opens\n");
        text.Append("  Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.\n");
        // SESSUX1h2b (D126 §5.2, §5.3): Sessions' Archive, Archive what ended… and Unarchive, judged as the list judges them. A stop
        // is the session's own conversation, which a go reaches (D161's SESSUX1h1 note).
        text.Append("- `session_propose`: archive sessions on this machine, or unarchive them, by id as Sessions lists them,\n");
        text.Append("  or `ended` to archive every session that ended, as the list's ⋯ → Archive what ended… does.\n");
        text.Append("  An archived session leaves the list with its record whole, and a teammate's may be archived here too.\n");
        text.Append("  A session still running, in Waiting on you or in To review is never archived: archive never hides what\n");
        text.Append("  needs the person, and the route refuses it. Stopping a session is never proposed: it is the person's own,\n");
        text.Append("  on the session's page, where `go_propose` takes them.\n");
        // PAUSE1f-b (D132 §7.4, design §2.1): the pages' Pause… and Resume, judged by what a pause does with each piece of the
        // work. Abandon is exempt: it declines quests with the person's reason, which is their answer (D37).
        text.Append("- `pause_propose`: pause one ask's work, or one quest's, on this machine, or resume it, naming the ask or\n");
        text.Append("  the quest by its id. A pause stops what of the work runs here as the person's own stop and starts nothing of it\n");
        text.Append("  until it is resumed; a session waiting on the person stays parked, a running intake keeps reading, and a\n");
        text.Append("  teammate's session is never reached. Resume lifts the pause and releases its stops; another pause over a quest\n");
        text.Append("  of the work stays.\n");
        text.Append("  Abandoning is never proposed: it declines quests with the person's reason, which is their answer, so name the\n");
        text.Append("  ask's or the quest's page → Abandon…, or `daoris-driver ask --abandon <id>`, and never write the reason.\n");
        // HIST1f-b, HIST1f-d (D153, design §6.4): the pages' clears, judged by their first press, a closed quest's failed
        // sessions alone being the `failed` door.
        text.Append("- `clear_propose`: clear finished history from this machine: one closed quest's work (`quest` and its id),\n");
        text.Append("  one ask's work with every quest it became (`ask` and its id), one closed quest's failed sessions alone, the quest\n");
        text.Append("  and its other sessions staying (`failed` and the quest's id), or a workspace's (`workspace` and its name).\n");
        text.Append("  Its card lists what would go and what stays and why; apply clears exactly what the card listed, each judged\n");
        text.Append("  again as it goes, and nothing brings it back. A quest an ask asked is cleared with its ask, never alone; work still\n");
        text.Append("  open or taken, a session still running and anything waiting on the person are never cleared, and the route\n");
        text.Append("  refuses them.\n");
        // WORKFLOW1h-b (D157 point 13, design §8): a whole version judged as the editor judges it; changes, kinds and imports are
        // WORKFLOW1h2's, and a merge with nobody is a decision of its own (§8.3, §12.1).
        text.Append("- `workflow_propose`: a named workflow's whole version for a repository or a workspace (`scope`): a new\n");
        text.Append("  workflow (an id no workflow here has, and no `base`) or an edit of one (`base` its newest version, as\n");
        text.Append("  `daoris driver workflow show <id>` says it), every step in `steps` as the workflow's own list, and\n");
        text.Append("  `choose` to make it the scope's workflow too. Its card says the person's part first, then each step by id,\n");
        text.Append("  what leaves this machine with no press of theirs, what is needed first, and that it applies to new work only.\n");
        text.Append("  A base that is not the newest is refused as stale. A declaration a step needs that is missing where it\n");
        text.Append("  applies, a landing plugin that cannot land here, or a kind this build does not run\n");
        text.Append("  makes the card a draft with no apply until it is. Changes against a base, a kind and an import are not\n");
        text.Append("  proposed yet.\n");
        text.Append("  Merging with nobody is never proposed: *land without my review* is a pull request opened with no press, which\n");
        text.Append("  the person still merges, and a merge at done is a decision of its own, which you say rather than propose.\n");
        // LAYOUT8 (D170 point 1): the set-up press as a card, where the owner's helper could only name the terminal command. The
        // room has no shell, by design (D89): the door is the press, judged by `--plan`'s refusals.
        text.Append("- `setup_propose`: set a repository registered here up for every agent, as `daoris-driver setup <repository>`\n");
        text.Append("  and its page's Set up for agents do: one quest to that repository's own session, which takes up the shared\n");
        text.Append("  doctrine and writes down what the repository owns and its instructions on a branch of its own, landed by its\n");
        text.Append("  workspace's rule, and a rule letting its sessions run the doctrine tool. Name it as `registry` lists it.\n");
        text.Append("  Its card shows what was read on its line and the quest's whole text; what the press refuses (not driven here,\n");
        text.Append("  no tree of its own, no doctrine tool, already set up, a set-up already open) comes back to you in its words,\n");
        text.Append("  each with the door that answers it. Daoris never writes into the repository, and you never run it yourself:\n");
        text.Append("  the person's Apply is the press.\n\n");
        // HELP7: the real helper said *press Apply* of a card whose button reads *go there*. HELP10: a delete's
        // press was *delete* all along, and a bring-up-to-date card's first is the look.
        text.Append("Each proposal reaches the person as a card with two buttons, and you name them as the card does:\n");
        text.Append("a go card reads **go there** and **not now** (in 中文 **前往** and **暂不**),\n");
        text.Append("a delete card **delete** and **not now** (in 中文 **删除** and **暂不**),\n");
        text.Append("a bring-up-to-date card **look for updates** until the person has looked, then **apply** (in 中文 **查看更新**, then **应用**),\n");
        // DRIFT1d2: an accept's press is the person's yes, worded as the quest page's.
        text.Append("an accept card **accept** and **not now** (in 中文 **采纳** and **暂不**),\n");
        text.Append("and every other card reads **apply** and **not now** (in 中文 **应用** and **暂不**).\n\n");
        return text.ToString();
    }
}
