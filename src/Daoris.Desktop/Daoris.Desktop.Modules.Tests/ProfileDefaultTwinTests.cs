using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// 🔴 The twin's table (LEFT3 e): an account's default, set and cleared, as <c>HARNESS_ACTION</c>'s <c>profile-default</c>
/// writes it for the screen (<see cref="DriverModule.DefaultEdited"/>). The CLI's <c>daoris agent profile default …
/// [--clear]</c> writes the same file with its own code (<c>withDefault</c>), and <c>toolchain.test.ts</c>'s
/// <c>DEFAULT_EDITS</c> holds the same rows with the same answers: change one and the other changes with it
/// (<c>.claude/knowledge/twins.md</c>). A clear removes one entry, the machine's or one workspace's; a workspace left
/// with none is dropped; nothing else moves, and clearing what is not set changes nothing.
/// </summary>
public sealed class ProfileDefaultTwinTests
{
    /// <summary>A setting's defaults as the table spells them: <c>agent=account</c>, and <c>workspace/agent=account</c>.</summary>
    private static HarnessSettings Settings(params string[] entries)
    {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var workspaces = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var (key, account) = (entry[..entry.IndexOf('=')], entry[(entry.IndexOf('=') + 1)..]);
            if (key.IndexOf('/') is var slash and > 0)
            {
                var circle = workspaces.TryGetValue(key[..slash], out var held) ? held : workspaces[key[..slash]] = new(StringComparer.OrdinalIgnoreCase);
                circle[key[(slash + 1)..]] = account;
            }
            else
            {
                defaults[key] = account;
            }
        }

        return new HarnessSettings(defaults, workspaces.ToDictionary(
            each => each.Key, each => (IReadOnlyDictionary<string, string>)each.Value, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>The settings' defaults back in the table's spelling, sorted.</summary>
    private static IReadOnlyList<string> Spelled(HarnessSettings settings) =>
    [
        .. settings.Defaults.Select(each => $"{each.Key}={each.Value}")
            .Concat(settings.Workspaces.SelectMany(circle => circle.Value.Select(each => $"{circle.Key}/{each.Key}={each.Value}")))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary><c>toolchain.test.ts</c>'s <c>DEFAULT_EDITS</c>, row for row: before, the agent, the account (none clears), the workspace, after.</summary>
    public static TheoryData<string, string[], string, string?, string?, string[]> Edits => new()
    {
        { "a machine default set", [], "claude-code", "work", null, ["claude-code=work"] },
        { "a machine default cleared, another agent's kept", ["claude-code=work", "codex=play"], "claude-code", null, null, ["codex=play"] },
        { "a workspace default set, the machine's kept", ["claude-code=play"], "claude-code", "work", "aurora",
            ["aurora/claude-code=work", "claude-code=play"] },
        { "a workspace default cleared, another agent's there kept", ["aurora/claude-code=work", "aurora/codex=play"], "claude-code", null, "aurora",
            ["aurora/codex=play"] },
        { "a workspace left with none is dropped, the machine's default kept", ["claude-code=play", "aurora/claude-code=work", "lab/codex=play"],
            "claude-code", null, "aurora", ["claude-code=play", "lab/codex=play"] },
        { "clearing what is not set changes nothing", ["codex=play"], "claude-code", null, "aurora", ["codex=play"] },
    };

    [Theory]
    [MemberData(nameof(Edits))]
    public void An_accounts_default_is_set_and_cleared_as_the_terminals_verb_sets_and_clears_it(
        string why, string[] before, string owner, string? profile, string? workspace, string[] after)
    {
        var edited = DriverModule.DefaultEdited(Settings(before), owner, profile, workspace);

        Assert.True(after.SequenceEqual(Spelled(edited)), $"{why}: {string.Join(", ", Spelled(edited))}");
        Assert.DoesNotContain(edited.Workspaces, circle => circle.Value.Count == 0);
    }
}
