using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LOOK2c: what sessions run as where a default was just set or cleared, which the Agents screen says after its press
/// (<see cref="DriverModule.DefaultStanding"/>). The terminal's <c>daoris agent profile default … [--clear]</c> prints the
/// same fact from the same resolution: an account, the machine's default a workspace falls back to, or the agent's own
/// configuration home. Found by LEFT3: the tool's own row's *use for a workspace* clears that workspace's entry, and with
/// a machine default set its sessions run as that default, not in the tool's own home.
/// </summary>
public sealed class ProfileDefaultStandingTests
{
    private static HarnessSettings Settings(Dictionary<string, string> machine, Dictionary<string, Dictionary<string, string>> circles) =>
        new(machine, circles.ToDictionary(
            each => each.Key, each => (IReadOnlyDictionary<string, string>)each.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>Before, the edit (the account, none clears; the workspace, none is the machine's), and what sessions there run as after.</summary>
    public static TheoryData<string, string?, string?, string?, string?, string> Standings => new()
    {
        // A workspace cleared while the machine names a default: its sessions run as that default.
        { "the tool's own row, for a workspace, with a machine default", null, null, "aurora", "work", DefaultFrom.Machine },
        // The same with no machine default: the tool's own home, as the row says.
        { "the tool's own row, for a workspace, with none", "none", null, "aurora", null, DefaultFrom.Own },
        { "an account, for a workspace", null, "play", "aurora", "play", DefaultFrom.Workspace },
        { "an account, for the machine", null, "play", null, "play", DefaultFrom.Machine },
        { "the tool's own row, for the machine", null, null, null, null, DefaultFrom.Own },
    };

    [Theory]
    [MemberData(nameof(Standings))]
    public void What_sessions_run_as_after_an_edit_is_what_the_terminal_says(
        string why, string? machine, string? profile, string? workspace, string? account, string from)
    {
        var before = Settings(
            machine == "none"
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = "work" },
            new(StringComparer.OrdinalIgnoreCase) { ["aurora"] = new(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = "lab" } });

        var standing = DriverModule.DefaultStanding(DriverModule.DefaultEdited(before, "claude-code", profile, workspace), "claude-code", workspace);

        Assert.True(standing.Account == account, $"{why}: {standing.Account}");
        Assert.True(standing.From == from, $"{why}: {standing.From}");
        Assert.Equal(workspace, standing.Workspace);
    }
}
