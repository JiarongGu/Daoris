using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The choices Daoris suggests for one working role (the seeded role's id is its actor's): applied only by the person's press.</summary>
public sealed record SuggestedChoices(string Role, IReadOnlyList<ActorChoice> Choices);

/// <summary>
/// The choices Daoris suggests for the seeded roles (MODELROLE1g; D205 point 7 and its 2026-10-25 note, the actors design §6.2): one dated
/// table in Claude Code's own aliases, held to <c>docs/model-roles.md</c> by <see cref="Census"/>. Build is led by the <b>build-settled</b>
/// row (<c>sonnet</c> · high) and then the <b>build-judgement</b> row (<c>opus</c> · high); Second opinion, Helper, Partner and Brainstorm
/// are decision support and keep <c>opus</c> · high where they name Claude Code. Never a preset: the person's press copies it into their
/// own choices, unlinked, and it can spend more than the order it replaces.
/// </summary>
public static partial class ActorSuggestion
{
    /// <summary>The day the table was read from the reference.</summary>
    public const string Dated = "2026-10-31";

    /// <summary>The roles the table suggests for, in the screen's order.</summary>
    public static IReadOnlyList<SuggestedChoices> Table { get; } =
    [
        new("build", [new ActorChoice(RoleSuggestion.ClaudeCode, "sonnet", "high"), new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high")]),
        new("opinion", [new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high")]),
        new("helper", [new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high")]),
        new("partner", [new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high")]),
        new("brainstorm", [new ActorChoice(RoleSuggestion.ClaudeCode, "opus", "high")]),
    ];

    /// <summary>The table with each choice of an agent the machine has not set up left out, and a role left with none left out whole; null <paramref name="setUp"/> leaves nothing out.</summary>
    public static IReadOnlyList<SuggestedChoices> For(IReadOnlySet<string>? setUp) =>
    [
        .. Table
            .Select(each => each with { Choices = [.. each.Choices.Where(choice => setUp is null || setUp.Contains(choice.Agent))] })
            .Where(each => each.Choices.Count > 0),
    ];

    /// <summary>
    /// Where <see cref="Table"/> and the reference's roles table (<c>docs/model-roles.md</c>) differ, one sentence each; empty where they agree.
    /// Build reads the <b>build-settled</b> row and then the <b>build-judgement</b> row, each other role the <b>build-judgement</b> row's alias.
    /// </summary>
    public static IReadOnlyList<string> Census(IEnumerable<string> reference)
    {
        var rows = reference.Where(line => line.StartsWith("| **", StringComparison.Ordinal)).ToDictionary(
            line => line.Split('|', StringSplitOptions.TrimEntries)[1].Trim('*'),
            line => line.Split('|', StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        ActorChoice? Read(string role) =>
            rows.TryGetValue(role, out var cells) && cells.Length > 4 && Alias().Match(cells[3]) is { Success: true } alias
                ? new ActorChoice(RoleSuggestion.ClaudeCode, alias.Groups[1].Value.ToLowerInvariant(), cells[4])
                : null;

        var settled = Read("build-settled");
        var judgement = Read("build-judgement");
        var found = new List<string>();
        foreach (var each in Table)
        {
            var expected = each.Role == "build" ? new[] { settled, judgement } : [judgement];
            if (expected.Any(choice => choice is null)) found.Add($"the reference names no model for {each.Role}'s rows.");
            else if (!each.Choices.SequenceEqual(expected!)) found.Add($"{each.Role} is suggested as {Said(each.Choices)}, the reference says {Said(expected!)}.");
        }

        return found;
    }

    private static string Said(IEnumerable<ActorChoice> choices) => string.Join(", ", choices.Select(choice => choice.Said));

    [GeneratedRegex(@"Claude (\w+)")]
    private static partial Regex Alias();
}
