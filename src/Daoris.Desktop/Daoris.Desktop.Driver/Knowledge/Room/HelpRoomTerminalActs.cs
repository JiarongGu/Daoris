using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Acts the person runs at a terminal that no proposal takes (D233 point 1: a door only where one is wanted): each with the commands
/// that do it, so the helper names the command rather than calling a missing card a limit of its own. Each command is spelled as its
/// usage spells it (<c>HelpRoomTerminalActsTests</c>).
/// </summary>
internal sealed class HelpRoomTerminalActs : IHelpRoomSection
{
    /// <summary>WSSETUP6's <c>daoris place --workspace</c> (D124 §4.5).</summary>
    public static readonly HelpTerminalAct Workspace = new(
        "set every repository of a workspace up, one at a time, or pause, resume or stop that plan",
        ["daoris place --workspace <name> --adoption [--plan]", "daoris place --workspace <name> --pause | --resume | --stop"]);

    /// <summary>WSSETUP5's <c>daoris repository register</c> (D124 §3.1).</summary>
    public static readonly HelpTerminalAct Register = new(
        "register a repository from what its line declares",
        ["daoris repository register [--repository <name>]"]);

    /// <summary>EVID1b's <c>daoris quest check</c> (D144 §5), beside the quest page's Check again.</summary>
    public static readonly HelpTerminalAct Check = new(
        "read a done's evidence again, so a commit that holds it now lifts its hold",
        ["daoris quest check <id> [--commit <sha>]"]);

    /// <summary>An ask's review choice (REVIEWENV1b), which the ask's page sets after.</summary>
    public static readonly HelpTerminalAct AskReview = new(
        "choose, as an ask is proposed, whether its work is reviewed before it lands and where (the ask's page sets it after)",
        ["daoris ask … --review rule|on|<environment>|off", "daoris ask review <id> on|<environment>|off [\"…\"]"]);

    /// <summary>An ask's kind choice (WORKFLOW1i-c), which the ask's page sets after.</summary>
    public static readonly HelpTerminalAct AskKind = new(
        "say, as an ask is proposed, what kind of task it is and which workflow its work follows (the ask's page sets it after)",
        [
            "daoris ask … --kind <kind> [--workflow <id>|current]",
            "daoris ask workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]",
        ]);

    /// <summary>A named workflow's version from a file, an import, or a kind of task (WORKFLOW1d, WORKFLOW1e; D157 point 13).</summary>
    public static readonly HelpTerminalAct Workflow = new(
        "apply a workflow's version from a file, import a workflow whole, or declare a workspace's kind of task",
        [
            "daoris workflow apply <id> <file> [--base <v>]",
            "daoris workflow import <file>",
            "daoris workflow kind <name> <kind> --label \"…\" [--paths <p,…>]",
        ]);

    /// <summary>Settings → Tools' *System* and *Use this version* (TOOLS7).</summary>
    public static readonly HelpTerminalAct ToolUse = new(
        "choose which version of a tool Daoris runs: the one on the PATH, or one kept in the home (Settings → Tools)",
        ["daoris tool use <tool> system", "daoris tool use <tool> managed [<version>]"]);

    /// <summary>Settings → Tools' location removal (TOOLS7).</summary>
    public static readonly HelpTerminalAct ToolLocation = new(
        "remove a location tool versions are offered from (Settings → Tools)",
        ["daoris tool locations [add|remove <address>]"]);

    /// <summary>Every act, in the order a person meets the tasks, as the doors table runs.</summary>
    public static readonly IReadOnlyList<HelpTerminalAct> All =
        [Workspace, Register, Check, AskReview, AskKind, Workflow, ToolUse, ToolLocation];

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## Done at a terminal\n\n");
        text.Append("Each act below is one the person runs at a terminal, and no proposal of yours takes it. When the person\n");
        text.Append("wants one, name its command so they can run it themselves, and its screen where the doors above name one.\n\n");
        foreach (var act in All)
        {
            text.Append($"- {act.Act}: {string.Join(", ", act.Terminal.Select(command => $"`{command}`"))}\n");
        }

        text.Append('\n');
        return text.ToString();
    }
}

/// <summary>An act the person runs at a terminal, as the room names it: what they would do, and each command that does it.</summary>
/// <param name="Act">What the person would do, as the room says it.</param>
/// <param name="Terminal">Each command that does it, spelled as its usage spells it.</param>
public sealed record HelpTerminalAct(string Act, IReadOnlyList<string> Terminal);
