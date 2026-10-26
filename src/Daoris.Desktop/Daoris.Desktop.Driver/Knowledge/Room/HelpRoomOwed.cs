using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The doors Daoris still owes Ask Daoris (HELPOWED1, D170 point 2): each act a person can start that no kind of proposal
/// takes yet, the terminal command that does it today, and why its door is owed. Where the helper meets one, it names the
/// command and says the door is owed, rather than calling a missing door a limit of its own (the owner's report, D170).
/// </summary>
/// <remarks>
/// <see cref="All"/> is the one list: <c>HelpCoverageTests</c>' owed answers are these rows, and its test fails on a door owed
/// there that is not one here, or a row here nothing owes. A door built leaves both in the same change. Each command is spelled
/// as its usage spells it (<c>HelpRoomOwedTests</c>).
/// </remarks>
internal sealed class HelpRoomOwed : IHelpRoomSection
{
    /// <summary>WSSETUP6's <c>daoris-driver setup --workspace</c> (D124 §4.5): owed until WSSETUP7's doors of the <c>setup</c> kind.</summary>
    public static readonly HelpOwedDoor Workspace = new(
        "set every repository of a workspace up, one at a time, or pause, resume or stop that plan",
        ["daoris-driver setup --workspace <name> [--plan]", "daoris-driver setup --workspace <name> --pause | --resume | --stop"],
        "setting a workspace up writes a plan that publishes a quest to each repository in turn and adds a rule at the "
        + "workspace's scope, a widening the person applies as a card, and pausing, resuming or stopping it changes only what "
        + "the person already said, so Ask Daoris should propose each; that waits on WSSETUP7's `workspace`, `pause`, `resume` "
        + "and `stop` doors of LAYOUT8's `setup` kind, judged against the same plan (D124 §4.5).");

    /// <summary>WSSETUP5's <c>daoris-driver register</c> (D124 §3.1): owed until WSSETUP7's <c>register</c> door.</summary>
    public static readonly HelpOwedDoor Register = new(
        "register a repository from what its line declares",
        ["daoris-driver register [--repository <name>]"],
        "registering a repository from its line changes only what the line already says, so Ask Daoris should propose it and "
        + "the person apply it; that waits on WSSETUP7's `register` door of LAYOUT8's `setup` kind, judged against the same "
        + "line (D124 §4.5).");

    /// <summary>EVID1b's <c>daoris-driver quest check</c> (D144 §5): owed until EVID1c's check card.</summary>
    public static readonly HelpOwedDoor Check = new(
        "read a done's evidence again, so a commit that holds it now lifts its hold",
        ["daoris-driver quest check <id> [--commit <sha>]"],
        "reading a done's evidence again posts a verdict that can lift its hold and publish what it held, a move the person "
        + "applies as a card, so Ask Daoris should propose it; that is EVID1c's check card beside the quest page's Check again "
        + "(D144 §5), and until then `daoris-driver quest check` is the terminal's door.");

    /// <summary>An ask's review choice (REVIEWENV1b): owed until ENTRY1c widens the <c>ask</c> door with it.</summary>
    public static readonly HelpOwedDoor AskReview = new(
        "choose, as an ask is proposed, whether its work is reviewed before it lands and where (the ask's page sets it after)",
        ["daoris-driver ask … --review rule|on|<environment>|off", "daoris-driver ask --set-review <id> on|<environment>|off [\"…\"]"],
        "an ask's review choice says how its work is shown to the person before it lands, a choice the person applies, so "
        + "Ask Daoris should carry it when it proposes the ask; the `ask` door takes the sentence, workspace and why only, "
        + "which ENTRY1c widens with the review and the kind, and until then the ask's page is its door (REVIEWENV1b).");

    /// <summary>An ask's kind choice (WORKFLOW1i-c): owed as the review choice is.</summary>
    public static readonly HelpOwedDoor AskKind = new(
        "say, as an ask is proposed, what kind of task it is and which workflow its work follows (the ask's page sets it after)",
        [
            "daoris-driver ask … --kind <kind> [--workflow <id>|current]",
            "daoris-driver ask --set-workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]",
        ],
        "an ask's kind choice says which workflow its work follows (D157), a choice the person applies, here an intake's "
        + "proposal applied from the ask's page (WORKFLOW1i-c); Ask Daoris should carry the kind when it proposes the ask, "
        + "as the review choice is owed, and until then the ask's page is its door.");

    /// <summary>
    /// A named workflow's version from a file, an import, or a kind of task (WORKFLOW1d, WORKFLOW1e; D157 point 13): owed to
    /// WORKFLOW1h2 (the workflow design §8.1).
    /// </summary>
    public static readonly HelpOwedDoor Workflow = new(
        "apply a workflow's version from a file, import a workflow whole, or declare a workspace's kind of task",
        [
            "daoris driver workflow apply <id> <file>|--as-graph [--base <v>]",
            "daoris driver workflow import <file>",
            "daoris driver workflow kind <name> <kind> --label \"…\" [--paths <p,…>]",
        ],
        "a version applied from a file, a workflow imported whole and a kind of task are changes to how work moves that the person "
        + "applies, so Ask Daoris should propose them as the `workflow` kind's card, its part said first (D157 point 13, the "
        + "workflow design §8, §4.7), which WORKFLOW1h2 builds; until then `daoris driver workflow` is its terminal's door, and every "
        + "gate reads the version each run bound (WORKFLOW1f).");

    /// <summary>Settings → Tools' *System* and *Use this version* (TOOLS7): the <c>tool</c> kind's <c>use</c> door, TOOLS8's.</summary>
    public static readonly HelpOwedDoor ToolUse = new(
        "choose which version of a tool Daoris runs: the one on the PATH, or one kept in the home (Settings → Tools)",
        ["daoris tool use <tool> system", "daoris tool use <tool> managed [<version>]"],
        "it narrows what runs to the machine's own, or to a version the lists name, so Ask Daoris should propose it, judged "
        + "against the lists as last fetched; that is the `tool` kind's `use` door, which TOOLS8 builds (D121 §4.3), and "
        + "until then `daoris tool use` is the terminal's door.");

    /// <summary>Settings → Tools' location removal (TOOLS7): the <c>tool</c> kind's <c>location</c> door, TOOLS8's.</summary>
    public static readonly HelpOwedDoor ToolLocation = new(
        "remove a location tool versions are offered from (Settings → Tools)",
        ["daoris tool locations [add|remove <address>]"],
        "removing a location only narrows what is offered, so Ask Daoris should propose it, its card listing the versions "
        + "it stops offering; that is the `tool` kind's `location` door, which TOOLS8 builds (D121 §4.3), and until then "
        + "`daoris tool locations remove` is the terminal's door.");

    /// <summary>
    /// MODELROLE1c's <c>daoris driver actor</c> and Settings → AI features' *Actors* (D205 §8.1): the <c>actor_propose</c> tool,
    /// MODELROLE1h's.
    /// </summary>
    public static readonly HelpOwedDoor Actor = new(
        "choose each actor's order of agents, models and efforts (Settings → AI features → Actors)",
        ["daoris driver actor [<actor>] [\"<agent> [<model>|-] [<effort>|-]\" …]"],
        "an actor's order says which models this machine's accounts spend on, so Ask Daoris should propose it and the person apply "
        + "it, judged by the one judge the screen and the terminal take; that is the `actor_propose` tool, which MODELROLE1h builds "
        + "(D205 §8.1), and until then the *Actors* card and `daoris driver actor` are its doors.");

    /// <summary>
    /// Every door owed, in the order a person meets the tasks, as the doors table runs. LEFT3's clear of an account's default left with
    /// the default itself (POOL1b, D197 point 6): the order says where starts begin.
    /// </summary>
    public static readonly IReadOnlyList<HelpOwedDoor> All =
        [Workspace, Register, Check, AskReview, AskKind, Workflow, ToolUse, ToolLocation, Actor];

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## Doors Daoris still owes\n\n");
        text.Append("Each act below is one a person can start at a terminal and you cannot propose yet: Daoris still owes you\n");
        text.Append("its door. That is a door missing from Daoris, not a limit of yours and not how the act is meant to be done.\n");
        text.Append("When the person wants one, say that Daoris still owes this door, name its command so they can run it\n");
        text.Append("themselves, and its screen where the doors above name one; never offer a way around it as if it were the\n");
        text.Append("design.\n\n");
        foreach (var owed in All)
        {
            text.Append($"- {owed.Act}: {string.Join(", ", owed.Terminal.Select(command => $"`{command}`"))}\n");
        }

        text.Append('\n');
        return text.ToString();
    }
}

/// <summary>A door Daoris still owes Ask Daoris (HELPOWED1): the act, the commands that do it today, and why the door is owed.</summary>
/// <param name="Act">What the person would do, as the room says it.</param>
/// <param name="Terminal">Each command that does it today, spelled as its usage spells it.</param>
/// <param name="Reason">Why Ask Daoris should propose it and what its door waits on, as <c>HelpCoverageTests</c> holds it.</param>
public sealed record HelpOwedDoor(string Act, IReadOnlyList<string> Terminal, string Reason);
