namespace Daoris.Driver;

/// <summary>
/// Commit attribution from the selected harness's maker and its running model's identity.
/// </summary>
internal static class CommitTrailers
{
    // The registration declares the maker; the harness knows the model actually selected for the turn.
    // Account settings may be aliases or defaults, and a continued session may have changed maker.
    // A carry-on whose earlier part ran on another maker and left work uncommitted is told that maker's trailer too.
    internal static string For(ProviderRegistry adapters, AgentDoor adapter, EarlierMaker? earlier = null)
    {
        var maker = adapters.MakerOf(adapter);
        return earlier is not null && !SameMaker(maker, adapter, earlier) ? $"{Own(maker, adapter)}\n\n{Left(earlier)}" : Own(maker, adapter);
    }

    private static string Own(string? maker, AgentDoor adapter)
    {
        if (string.Equals(maker, "Anthropic", StringComparison.OrdinalIgnoreCase))
            return $"This start runs on `{adapter.Id.Value}`, made by Anthropic. Use the model actually running this session for commit attribution. "
                + "Your Claude harness adds its Co-Authored-By trailer; let it do so and do not add a duplicate or another maker's trailer.";

        var identity = maker switch
        {
            "OpenAI" => "Codex (OpenAI, MODEL) <noreply@openai.com>",
            "DeepSeek" => "DeepSeek (MODEL) <noreply@deepseek.com>",
            _ => "AGENT (MAKER, MODEL) <your maker's attribution address>",
        };
        var on = maker is { Length: > 0 } ? $", made by {maker}" : "";
        return $"This start runs on `{adapter.Id.Value}`{on}. End every commit message you write with a Co-Authored-By trailer naming your own maker and the model actually running this session:\n\n"
            + $"Co-Authored-By: {identity}\n\n"
            + "Replace MODEL with the model your harness reports for this run, not an account default or a model from an earlier session's brief. "
            + "If no model identity is reported, say model unreported rather than invent one. "
            + "Replace any other placeholders with your own identity. If your harness adds that trailer itself, do not add a duplicate.";
    }

    /// <summary>
    /// The earlier maker's trailer, its model as that part's door reported it, or <c>model unreported</c>: never the
    /// new maker's, nor an account default.
    /// </summary>
    private static string Left(EarlierMaker earlier)
    {
        var model = earlier.Models.Count == 0 ? "model unreported" : string.Join(", ", earlier.Models);
        var identity = earlier.Maker switch
        {
            "Anthropic" => $"Claude (Anthropic, {model}) <noreply@anthropic.com>",
            "OpenAI" => $"Codex (OpenAI, {model}) <noreply@openai.com>",
            "DeepSeek" => $"DeepSeek ({model}) <noreply@deepseek.com>",
            _ => $"{earlier.Adapter} ({earlier.Maker ?? "maker unknown"}, {model}) <its maker's attribution address>",
        };
        var on = earlier.Maker is { Length: > 0 } maker ? $", made by {maker}" : "";
        return $"The part of this line before you ran on `{earlier.Adapter}`{on}, and left work uncommitted in this tree. "
            + "A commit holding any of that work carries its maker's trailer too, beside your own:\n\n"
            + $"Co-Authored-By: {identity}";
    }

    /// <summary>One maker where both harnesses name theirs; else one harness, since a maker this build does not know is told apart by its harness.</summary>
    private static bool SameMaker(string? own, AgentDoor adapter, EarlierMaker earlier) =>
        own is { Length: > 0 } maker && earlier.Maker is { Length: > 0 }
            ? string.Equals(maker, earlier.Maker, StringComparison.OrdinalIgnoreCase)
            : string.Equals(adapter.Id.Value, earlier.Adapter, StringComparison.OrdinalIgnoreCase);
}
