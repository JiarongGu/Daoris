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
        text.Append("- `ask_propose`: something to start, as an ask at a workspace.\n");
        // HELP10: an agent's default account, which HELP9 left owed. UX6e2: each is on the agent's page since UX6e.
        text.Append("- `agent_propose`: an agent's Update or a pin to one version, as Agents → the agent's page → Ways in offers them,\n");
        text.Append("  or which of its accounts it runs as by default, for the machine or one workspace (`default`,\n");
        text.Append("  the account one the room lists under that agent).\n");
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
        text.Append("- `go_propose`: take the person to a place on the window, from the list below. It changes nothing.\n");
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
        text.Append("  work replayed onto it, the landed branches whose work reached it deleted. Name a repository, or none for\n");
        text.Append("  every one with a checkout here. Its card asks the person to look first, which fetches each line as them,\n");
        text.Append("  then lists what the press would do; apply acts on those rows only. Daoris never pushes.\n");
        // DRIFT1d2 (D133 §4): the person's yes to a departure, as the quest page's *Accept the departure* gives it. The yes
        // is theirs, so the helper proposes it on their ask and never on its own reading of the departure.
        text.Append("- `accept_propose`: the person's yes to a quest a done's departure holds: it closed done departing from what\n");
        text.Append("  the person required, so the chain's next step and a quest waiting on it wait (`quest_list` marks it held for\n");
        text.Append("  the person's yes). Propose it only when the person asks to accept it, never on your own reading of the\n");
        text.Append("  departure. Its card shows each departure with the person's words it relied on, as the quest's page does.\n");
        // ENTRY1d1 (D161's ENTRY1d note): a move needs no path (D48 §7), so it is proposed by name; adding or importing needs a
        // folder, which stays the person's pick.
        text.Append("- `repository_propose`: move a repository registered here to a workspace, as its page's Manage →\n");
        text.Append("  Move to workspace does. Name it as `registry` lists it, and the workspace: one the room lists,\n");
        text.Append("  or a new name, which the move starts. It changes one row of this machine's registry and touches no file.\n");
        text.Append("  Adding or importing a repository needs a folder, which stays the person's pick in Repositories.\n\n");
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
