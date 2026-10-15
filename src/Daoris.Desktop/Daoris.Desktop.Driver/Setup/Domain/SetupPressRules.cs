using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The set-up press's rules (LAYOUT7; D124 §2.1, §2.4), pure: the doctrine tool's verbs a set-up's session runs and the rule
/// that allows them, which set-up a line calls for, the refusals the tools a child finds earn, and how the work lands in
/// words. <see cref="SetupPress"/> applies them against the world; its callers name them here.
/// </summary>
public static partial class SetupPressRules
{
    /// <summary>The doctrine tool's verbs a set-up's session runs, exactly (D124 §2.4). 🔴 Each is a verb the body asks for.</summary>
    public static readonly IReadOnlyList<string> Verbs =
    [
        "daoris --version", "daoris init --harness agents", "daoris analyze --json", "daoris sync --dry-run",
        "daoris sync", "daoris sync --force", "daoris check", "daoris status --json", "daoris doctor",
    ];

    /// <summary>The rule the press adds to the repository's scope: each verb as an exact <c>Bash</c> allow.</summary>
    public static IReadOnlyList<string> Rules { get; } = [.. Verbs.Select(verb => $"Bash({verb})")];

    /// <summary>The oldest <c>node</c> the doctrine tool runs on: its package's <c>engines</c>.</summary>
    public const int NodeFloor = 22;

    /// <summary>
    /// The refusals the tools a child finds earn (D124 §2.1): the machine's, not any one repository's, so a workspace plan
    /// pauses on them rather than skip every repository in turn (WSSETUP6).
    /// </summary>
    public static IReadOnlyList<SetupRefusal> ToolRefusals(SetupTools tools) =>
        [.. new[] { NodeRefusal(tools), ToolRefusal(tools) }.OfType<SetupRefusal>()];

    /// <summary>Whether a refusal is the machine's tools' rather than the repository's (<see cref="ToolRefusals"/>).</summary>
    public static bool IsToolRefusal(SetupRefusal refusal) => refusal.Code is SetupRefusals.NoNode or SetupRefusals.NoTool;

    /// <summary>
    /// Which set-up the line calls for: none adopted is the whole; the older layout, or a move the lock has not
    /// followed, is a move; the agents layout finished and declaring nothing is the knowledge alone; declaring too is done.
    /// </summary>
    public static SetupCase CaseOf(LayoutFacts facts) =>
        !facts.Adopted ? SetupCase.Whole
        : !facts.Clean ? SetupCase.Move
        : !facts.Declares ? SetupCase.Declare
        : SetupCase.Done;

    /// <summary>How the work will land, in the words the press says it in before it is pressed.</summary>
    public static string LandsAs(Landing landing) =>
        landing.Rule.Form == LandingForm.Branch
            ? "as a branch, for your review" + (landing.Rule.Plugin is { } plugin ? $", which `{plugin}` pushes and opens the pull request for" : "")
            : "merged into its line, where the landed history is the review";

    /// <summary>The <c>node</c> a child finds, refused where it is missing, does not answer, or is older than <see cref="NodeFloor"/>.</summary>
    internal static SetupRefusal? NodeRefusal(SetupTools tools)
    {
        string? found = tools.Node is null
            ? tools.NodeProblem ?? "no `node` on the PATH Daoris's children start with"
            : Major(tools.NodeVersion) is not { } major
                ? $"the `node` a child finds did not answer its version (it said `{Said(tools.NodeVersion)}`)"
                : major < NodeFloor
                    ? $"the `node` a child finds is {tools.NodeVersion!.Trim()}, older than {NodeFloor}"
                    : null;
        return found is null
            ? null
            : new(SetupRefusals.NoNode,
                $"the doctrine tool cannot run here: {found}. Name one in Settings → Tools, or with `daoris tool use node …`.");
    }

    /// <summary>The <c>daoris</c> a child finds, refused where it is missing or does not answer a version.</summary>
    internal static SetupRefusal? ToolRefusal(SetupTools tools)
    {
        string? found = tools.Daoris is null
            ? "no `daoris` on the PATH Daoris's children start with" + (tools.DaorisProblem is { } why ? $" ({why})" : "")
            : !IsVersion(tools.DaorisVersion)
                ? $"the `daoris` a child finds did not answer its version (it said `{Said(tools.DaorisProblem ?? tools.DaorisVersion)}`)"
                : null;
        return found is null
            ? null
            : new(SetupRefusals.NoTool,
                $"the doctrine tool cannot run here: {found}. An install that does not carry the doctrine tool puts none "
                + "there, and a republish of Daoris that carries it does.");
    }

    /// <summary>The major version <c>node --version</c> printed (<c>v22.11.0</c>), or null for anything else.</summary>
    private static int? Major(string? version) =>
        version is not null && NodeVersion().Match(version.Trim()) is { Success: true } match
            && int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major)
            ? major
            : null;

    /// <summary>
    /// Whether <c>daoris --version</c> printed a version: numbers and dots, as the CLI prints its own. Public since LAYOUT8, so
    /// the screen's plan says a version only where one was answered, as the terminal's does.
    /// </summary>
    public static bool IsVersion(string? version) => version is not null && DaorisVersion().IsMatch(version.Trim());

    private static string Said(string? text)
    {
        var first = (text ?? "").Trim().Split('\n')[0].Trim();
        return first.Length == 0 ? "nothing" : TextBounds.Clip(first, 80);
    }

    [GeneratedRegex(@"^v?(\d+)\.\d+\.\d+")]
    private static partial Regex NodeVersion();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex DaorisVersion();
}
