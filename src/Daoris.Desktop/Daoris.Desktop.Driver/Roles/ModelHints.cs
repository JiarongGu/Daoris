namespace Daoris.Driver;

/// <summary>
/// A hint on a role's choice: its model is not among what its agent's open listed. Never a refusal: the agent's own answer at an open decides,
/// and a list that is only the stamped fallback, or no list at all, says nothing.
/// </summary>
public static class ModelHints
{
    /// <summary>The sentence for <paramref name="choice"/>, or null where its model is listed, names none, or no open has listed to read it against.</summary>
    public static string? Of(string home, ProviderRegistry registry, ActorChoice choice)
    {
        if (string.IsNullOrWhiteSpace(choice.Model)) return null;
        if (!registry.Doors.Any(door => string.Equals(door.Value, choice.Agent, StringComparison.OrdinalIgnoreCase))) return null;
        if (SessionChoices.Of(home, registry, choice.Agent) is not { Stamped: null, Models.Count: > 0 } offered) return null;
        if (offered.Models.Contains(choice.Model, StringComparer.OrdinalIgnoreCase)) return null;

        return $"`{choice.Agent}` did not list `{choice.Model}` at its last open (it listed {string.Join(", ", offered.Models.Select(model => $"`{model}`"))}); "
            + "the agent's own answer decides when the role runs.";
    }

    /// <summary>The hints of every choice in order, each naming its place.</summary>
    public static IReadOnlyList<string> Of(string home, ProviderRegistry registry, IReadOnlyList<ActorChoice> choices) =>
        [.. choices.Select((choice, at) => Of(home, registry, choice) is { } said ? $"choice {at + 1}: {said}" : null).OfType<string>()];
}
