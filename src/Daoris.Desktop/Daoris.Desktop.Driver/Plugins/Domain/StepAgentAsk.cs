using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What an agent performing a plugin's step is handed: the step's goal and declaration, the run's record (its unit, node and attempt),
/// and the work's place, branch, origin's address, line and landed commit. Kept beside the attempt as its ask, which the step's
/// <c>step_answer</c> reads for the schemas it names its evidence and outputs by.
/// </summary>
public sealed record StepAgentAsk(
    string Invocation, string Unit, string Node, int Attempt, string Plugin, string Step, string Goal, string Repository, string? Workspace,
    string? Branch, string? Origin, string? Line, string Tip)
{
    /// <summary>The routes it may reach with, as the step declares them.</summary>
    public IReadOnlyList<string> Outcomes { get; init; } = [];

    /// <summary>Each declared output's schema, by name: an answered output's id is named under it.</summary>
    public IReadOnlyDictionary<string, string> Outputs { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The step's evidence schema, which the answer's evidence is named under; null for none.</summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// The step's options as its graph writes them, each name with its JSON value's text: what the plugin's own <c>work/perform</c> would
    /// have been given, which the agent passes on to the plugin's tools.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string? Root { get; init; }

    public string? Landing { get; init; }

    /// <summary>The words the session is started with: the goal, then each input it works from, then how it answers.</summary>
    public string Prompt() => string.Join("\n", new[]
    {
        $"You perform the workflow step `{Step}` of the plugin `{Plugin}` for Daoris. Its goal:",
        Goal,
        "",
        "What you work from:",
        $"- the run's record: unit `{Unit}`, node `{Node}`, attempt {Attempt}",
        $"- the repository `{Repository}`{(Workspace is { Length: > 0 } workspace ? $" in workspace `{workspace}`" : "")}",
        $"- the branch `{Branch ?? "(none named)"}` at {Tip}",
        $"- origin's address `{Origin ?? "(none read)"}`",
        $"- the line `{Line ?? "(none named)"}`",
        $"- the landing: {Landing ?? "(none yet)"}",
        $"- the step's options: {OptionsTold()}",
        "",
        $"Use the plugin's tools (`{Plugin}_<command>`) for its platform, and nothing else outside Daoris. Finish by calling `step_answer` once:"
            + " `reached` with the evidence's address and a message where the goal is met"
            + (Outcomes.Count > 0 ? $" and its route as `decision` (one of {string.Join(", ", Outcomes)})" : "")
            + (Outputs.Count > 0 ? $" and the id of each output ({string.Join(", ", Outputs.Keys)})" : "")
            + ", or `failed` with a code and a message where it cannot be.",
    });

    private string OptionsTold() =>
        Options.Count == 0
            ? "(none given)"
            : "{" + string.Join(",", Options.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{JsonSerializer.Serialize(pair.Key)}:{pair.Value}")) + "}";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Write() => JsonSerializer.Serialize(this, Json);

    /// <summary>An ask as written, or null where it does not read.</summary>
    public static StepAgentAsk? Read(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<StepAgentAsk>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
