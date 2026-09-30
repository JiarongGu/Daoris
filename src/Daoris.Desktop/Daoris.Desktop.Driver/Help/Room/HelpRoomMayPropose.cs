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
        // asks for, which its card says too. HELP10: `retry` too, of a quest the driver parked, named from the list.
        text.Append("- `setting_propose`: every door below that `daoris driver` spells.\n");
        text.Append("  `retry` takes a quest parked by its failed sessions, from this machine's list below, as the target,\n");
        text.Append("  as its drawer's *try it again* does; a quest not on that list is refused.\n");
        text.Append("  `across` takes `read on|off|--clear` for a repository or a whole workspace,\n");
        text.Append("  or `write-to <other> [--clear]` for a repository, to let its sessions write into the other's checkout, one way.\n");
        text.Append("  Applying a write-to is the person's standing say-so for writing across, and its card says so.\n");
        text.Append("- `ask_propose`: something to start, as an ask at a workspace.\n");
        // HELP10: an agent's default account, which HELP9 left owed.
        text.Append("- `agent_propose`: an agent's Update or a pin to one version, as Settings → Agents & accounts offers them,\n");
        text.Append("  or which of its accounts it runs as by default, for the machine or one workspace (`default`,\n");
        text.Append("  the account one the room lists under that agent).\n");
        text.Append("  Update moves a pinned agent's pin to its newest release, or runs an unpinned one's own updater, and is\n");
        text.Append("  offered only where that screen shows Update; a pin names one exact release, like 2.1.300, never `latest`.\n");
        text.Append("- `agent_settings_propose`: an account's own model and effort, as Settings → Agents & accounts → Model &\n");
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
        text.Append("  here can be handed on; the person's own branches are theirs to push.\n\n");
        // HELP7: the real helper said *press Apply* of a card whose button reads *go there*.
        text.Append("Each proposal reaches the person as a card with two buttons, and you name them as the card does:\n");
        text.Append("every card but a go reads **apply** and **not now** (in 中文 **应用** and **暂不**), and\n");
        text.Append("a go card reads **go there** and **not now** (in 中文 **前往** and **暂不**).\n\n");
        return text.ToString();
    }
}
