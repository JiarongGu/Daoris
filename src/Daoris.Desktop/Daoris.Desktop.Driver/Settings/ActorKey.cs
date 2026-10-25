using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One entry of an actor's order (D205 §3.1): an agent, and its model and effort in the tool's own words; null is account default.</summary>
public sealed record ActorChoice(string Agent, string? Model = null, string? Effort = null)
{
    /// <summary>The entry in the terminal's words: <c>agent model|- effort|-</c>.</summary>
    public string Said => $"{Agent} {Model ?? "-"} {Effort ?? "-"}";
}

/// <summary>
/// The actors' key as <c>driver.json</c> keeps it, <c>actors</c> (D205 §3.1, MODELROLE1c; FOUNDATION1 mod-iii): the ids it may hold, what
/// an actor's order may be, and its reading and writing by the settings' own parser. What an order means, its effective reading and its
/// write-through, stays with the actors (<see cref="ActorChoices"/>), which answers through this for what it shares.
/// </summary>
public static class ActorKey
{
    /// <summary>The eight actors' wire ids, in the screen's order (§2.2).</summary>
    public static IReadOnlyList<string> Ids { get; } = ["build", "intake", "opinion", "helper", "partner", "brainstorm", "ask-daoris", "chat"];

    /// <summary>The most choices an actor holds: the most voices a brainstorm takes (<c>BrainstormDesk.MostAgents</c>).</summary>
    public const int Most = 4;

    /// <summary>Whether <paramref name="actor"/> may be <i>off</i>: Intake and Ask Daoris, whose absent key is off today.</summary>
    public static bool MayBeOff(string actor) => actor is "intake" or "ask-daoris";

    /// <summary>
    /// Whether <paramref name="choices"/> may be <paramref name="actor"/>'s order: null (back to <i>as before</i>) always; otherwise the
    /// refusal's sentence, or null where it may.
    /// </summary>
    public static string? Judge(string actor, IReadOnlyList<ActorChoice>? choices)
    {
        if (!Ids.Contains(actor, StringComparer.Ordinal)) return $"`{actor}` is not an actor — one of {string.Join(", ", Ids)}.";
        if (choices is null) return null;
        if (choices.Count == 0)
        {
            return MayBeOff(actor) ? null : $"{actor} cannot be off: only intake and ask-daoris have no agent; give it one to {Most} choices.";
        }

        if (choices.Count > Most) return $"{actor} takes at most {Most} choices, not {choices.Count}.";
        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            if (Word(choice.Agent) is { } agentWhy) return $"choice {i + 1}'s agent {agentWhy}.";
            if (choice.Model is { } model && Word(model) is { } modelWhy) return $"choice {i + 1}'s model {modelWhy}.";
            if (choice.Effort is { } effort && Word(effort) is { } effortWhy) return $"choice {i + 1}'s effort {effortWhy}.";
            for (var j = 0; j < i; j++)
            {
                if (choices[j] == choice) return $"choice {i + 1} is choice {j + 1} again: no two choices are the same.";
                // Brainstorm's entries are voices that run together (§4.4): two of one agent would be one voice.
                if (actor == "brainstorm" && string.Equals(choices[j].Agent, choice.Agent, StringComparison.OrdinalIgnoreCase))
                {
                    return $"brainstorm's choices are voices, one each: choice {i + 1} names `{choice.Agent}` as choice {j + 1} does.";
                }
            }
        }

        return null;
    }

    private static string? Word(string? word) =>
        string.IsNullOrWhiteSpace(word) ? "is empty"
        : word != word.Trim() ? "has spaces around it"
        : word.Length > 200 ? "is longer than 200 characters"
        : word.Any(char.IsControl) ? "holds a control character"
        : null;

    /// <summary>The stored actors, as <c>driver.json</c>'s <c>actors</c> holds them: each readable entry, and each that does not read with why.</summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors, IReadOnlyDictionary<string, string> Errors) Read(JsonElement root)
    {
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("actors", out var element) || element.ValueKind != JsonValueKind.Object) return (actors, errors);

        foreach (var property in element.EnumerateObject())
        {
            // An id this build does not know is not read, and the writer keeps it where it stands (JsonKeeping).
            if (!Ids.Contains(property.Name, StringComparer.Ordinal) || actors.ContainsKey(property.Name) || errors.ContainsKey(property.Name)) continue;
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                errors[property.Name] = "is not a list of choices";
                continue;
            }

            var choices = new List<ActorChoice>();
            string? why = null;
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "agent") is not { } agent)
                {
                    why = $"choice {choices.Count + 1} names no agent";
                    break;
                }

                var model = Text(item, "model");
                var effort = Text(item, "effort");
                if ((item.TryGetProperty("model", out var m) && m.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    || (item.TryGetProperty("effort", out var e) && e.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
                {
                    why = $"choice {choices.Count + 1}'s model or effort is not text";
                    break;
                }

                choices.Add(new ActorChoice(agent, model, effort));
            }

            why ??= Judge(property.Name, choices);
            if (why is not null) errors[property.Name] = why;
            else actors[property.Name] = choices;
        }

        return (actors, errors);
    }

    internal static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The stored actors written as <c>driver.json</c> keeps them, only where any is set, in the screen's order.</summary>
    public static void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> actors)
    {
        if (actors.Count == 0) return;
        writer.WriteStartObject("actors");
        foreach (var id in Ids)
        {
            if (!actors.TryGetValue(id, out var choices)) continue;
            writer.WriteStartArray(id);
            foreach (var choice in choices)
            {
                writer.WriteStartObject();
                writer.WriteString("agent", choice.Agent);
                if (choice.Model is { } model) writer.WriteString("model", model);
                if (choice.Effort is { } effort) writer.WriteString("effort", effort);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
