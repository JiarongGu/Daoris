using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One entry of an actor's order (D205 §3.1): an agent, and its model and effort in the tool's own words; null is account default.</summary>
public sealed record ActorChoice(string Agent, string? Model = null, string? Effort = null)
{
    /// <summary>The entry in the terminal's words: <c>agent model|- effort|-</c>.</summary>
    public string Said => $"{Agent} {Model ?? "-"} {Effort ?? "-"}";
}

/// <summary>
/// One entry of an actor's effective order (D205 §4.0): who runs, with what, and <c>From</c>, what the record keeps as its <c>choice</c>:
/// <c>1</c> to <c>4</c>, <c>before</c>, <c>rule-default</c>, <c>pick</c> or <c>handed</c>.
/// </summary>
public sealed record ActorEffective(string Agent, string? Model, string? Effort, string From)
{
    /// <summary>What this entry asks a start to carry: a value the entry names is carried; one it leaves out is account default.</summary>
    public ActorAsked Asked => new(
        Agent,
        Model is { } model ? new("entry", model, true) : new("account-default", null, false),
        Effort is { } effort ? new("entry", effort, true) : new("account-default", null, false));
}

/// <summary>
/// The actors' one judge and one reading (D205 §3.1, §4.0, §6.1, MODELROLE1c): <see cref="Judge"/> refuses alike for the screen, the
/// terminal and a proposal, and <see cref="Effective"/> answers every start, the editor and the terminal the order a start walks.
/// </summary>
/// <remarks>
/// An actor's stored state is one of three (§4.0): absent, <i>as before</i>, read from today's keys at account default; <c>[]</c>,
/// <i>off</i>, for Intake and Ask Daoris alone; or one to four choices. A stored entry that does not read is kept as its error and holds
/// the start that reads it (<see cref="ActorUnreadableException"/>), never silently read as <i>as before</i> (§6.4).
/// </remarks>
public static class ActorChoices
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

    /// <summary><see cref="Judge"/>, thrown as the driver's refusal.</summary>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    public static void Judged(string actor, IReadOnlyList<ActorChoice>? choices)
    {
        if (Judge(actor, choices) is { } refused) throw new DriverException(refused);
    }

    private static string? Word(string? word) =>
        string.IsNullOrWhiteSpace(word) ? "is empty"
        : word != word.Trim() ? "has spaces around it"
        : word.Length > 200 ? "is longer than 200 characters"
        : word.Any(char.IsControl) ? "holds a control character"
        : null;

    /// <summary>
    /// <paramref name="actor"/>'s effective order (§4.0): its entries as stored, or, <i>as before</i>, read from today's keys at account
    /// default (§6.1). A <paramref name="pick"/> or a <paramref name="handed"/> agent overrides who runs, its model the actor's first
    /// entry for that agent; a <paramref name="fence"/>'s agents decide who, each taking the actor's entries for it, else
    /// <c>rule-default</c>. Second opinion and Partner have no order of their own, and Helper <i>as before</i> is
    /// <c>HelperChoice.Candidates</c>: each reads empty here, its caller's own rule deciding.
    /// </summary>
    /// <exception cref="ActorUnreadableException">The actor's stored entry does not read: the start that reads it holds.</exception>
    public static IReadOnlyList<ActorEffective> Effective(
        string actor, DriverConfig config, IReadOnlyList<string>? fence = null, string? pick = null, string? handed = null)
    {
        if (config.ActorErrors.TryGetValue(actor, out var error)) throw new ActorUnreadableException(actor, error);
        var stored = config.Actors.TryGetValue(actor, out var set) ? set : null;

        ActorEffective Named(string agent, string from)
        {
            var first = stored?.FirstOrDefault(choice => string.Equals(choice.Agent, agent, StringComparison.OrdinalIgnoreCase));
            return new(agent, first?.Model, first?.Effort, from);
        }

        if (pick is { Length: > 0 }) return [Named(pick, "pick")];
        if (handed is { Length: > 0 }) return [Named(handed, "handed")];
        if (fence is { Count: > 0 })
        {
            var fenced = new List<ActorEffective>();
            foreach (var agent in fence)
            {
                var entries = stored?
                    .Select((choice, i) => (choice, i))
                    .Where(each => string.Equals(each.choice.Agent, agent, StringComparison.OrdinalIgnoreCase))
                    .Select(each => new ActorEffective(agent, each.choice.Model, each.choice.Effort, (each.i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .ToList() ?? [];
                fenced.AddRange(entries.Count > 0 ? entries : [new ActorEffective(agent, null, null, "rule-default")]);
            }

            return fenced;
        }

        if (stored is not null)
        {
            return [.. stored.Select((choice, i) => new ActorEffective(
                choice.Agent, choice.Model, choice.Effort, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))];
        }

        string? before = actor switch
        {
            "build" or "chat" or "brainstorm" => config.Adapter,
            "intake" => config.IntakeAdapter,
            "ask-daoris" => config.HelperAdapter,
            _ => null,
        };
        return before is { Length: > 0 } legacy ? [new ActorEffective(legacy, null, null, "before")] : [];
    }

    /// <summary>The agent a start of <paramref name="actor"/> runs where nothing walks its order: its first entry's, or null where it is off.</summary>
    /// <exception cref="ActorUnreadableException">The actor's stored entry does not read.</exception>
    public static string? FirstAgent(string actor, DriverConfig config) => Effective(actor, config).FirstOrDefault()?.Agent;

    /// <summary>
    /// <paramref name="config"/> with <paramref name="actor"/>'s order set (or, null, back to <i>as before</i>), judged first, and written
    /// through (§6.4): Build's first agent is <c>adapter</c>, Intake's <c>intakeAdapter</c> and Ask Daoris's <c>helperAdapter</c> (absent
    /// while off). 🔴 Every write that changes <c>adapter</c> first makes Chat and Brainstorm explicit where they are <i>as before</i>, as
    /// the one-entry list of <c>adapter</c>'s agent as it stood, at account default (§4.0, round 3): no actor follows <c>adapter</c> across
    /// a change of it. <paramref name="kept"/> names the actors made explicit, for the write's sentence.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    public static DriverConfig With(DriverConfig config, string actor, IReadOnlyList<ActorChoice>? choices, out IReadOnlyList<string> kept)
    {
        Judged(actor, choices);
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(config.Actors, StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(config.ActorErrors, StringComparer.Ordinal);
        errors.Remove(actor);
        if (choices is null) actors.Remove(actor); else actors[actor] = [.. choices];

        var after = config with { Actors = actors, ActorErrors = errors };
        if (choices is not null)
        {
            var first = choices.FirstOrDefault()?.Agent;
            after = actor switch
            {
                "build" => after with { Adapter = first! },
                "intake" => after with { IntakeAdapter = first },
                "ask-daoris" => after with { HelperAdapter = first },
                _ => after,
            };
        }

        kept = Keep(config, ref after);
        return after;
    }

    /// <summary><see cref="With(DriverConfig, string, IReadOnlyList{ActorChoice}?, out IReadOnlyList{string})"/>, the actors it kept unsaid.</summary>
    public static DriverConfig With(DriverConfig config, string actor, IReadOnlyList<ActorChoice>? choices) => With(config, actor, choices, out _);

    /// <summary>
    /// Where <paramref name="after"/> changes <c>adapter</c> from <paramref name="before"/>'s, Chat and Brainstorm, each still <i>as
    /// before</i>, written as <c>adapter</c>'s agent as it stood (§4.0); the actors written. Every writer of <c>adapter</c> calls it.
    /// </summary>
    public static IReadOnlyList<string> Keep(DriverConfig before, ref DriverConfig after)
    {
        if (string.Equals(before.Adapter, after.Adapter, StringComparison.Ordinal)) return [];
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(after.Actors, StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (var follower in (string[])["chat", "brainstorm"])
        {
            if (actors.ContainsKey(follower) || after.ActorErrors.ContainsKey(follower)) continue;
            actors[follower] = [new ActorChoice(before.Adapter)];
            kept.Add(follower);
        }

        after = after with { Actors = actors };
        return kept;
    }

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

    /// <summary>
    /// A press's <c>choices</c> (<c>SET_ACTOR</c>'s payload): null where absent or null, as before; an empty list, off; else each entry
    /// <c>{ agent, model?, effort? }</c>, a blank model or effort read as account default.
    /// </summary>
    /// <exception cref="DriverException">A list whose entry is not one: nothing is read from it.</exception>
    public static IReadOnlyList<ActorChoice>? Payload(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named || !named.TryGetProperty("choices", out var list)
            || list.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (list.ValueKind != JsonValueKind.Array) throw new DriverException("the press's choices are not a list.");
        var choices = new List<ActorChoice>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "agent") is not { } agent)
            {
                throw new DriverException($"the press's choice {choices.Count + 1} names no agent.");
            }

            choices.Add(new ActorChoice(agent, Blank(Text(item, "model")), Blank(Text(item, "effort"))));
        }

        return choices;
    }

    private static string? Blank(string? word) => string.IsNullOrWhiteSpace(word) ? null : word;

    private static string? Text(JsonElement item, string name) =>
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

/// <summary>
/// One actor as the screen, the terminal and <c>ACTORS</c> say it (D205 §7, §9.4): <c>State</c> is <c>set</c>, <c>before</c>, <c>off</c> or
/// <c>unreadable</c>, with its effective order (empty for Second opinion and Partner, whose rule or ask names the agents, and for Helper as
/// before) and, where it does not read, why.
/// </summary>
public sealed record ActorView(string Id, string State, IReadOnlyList<ActorEffective> Choices, string? Error)
{
    /// <summary>Every actor of <paramref name="config"/>, in the screen's order.</summary>
    public static IReadOnlyList<ActorView> All(DriverConfig config) => [.. ActorChoices.Ids.Select(id => Of(id, config))];

    /// <summary><paramref name="id"/> as <paramref name="config"/> holds it.</summary>
    public static ActorView Of(string id, DriverConfig config)
    {
        if (config.ActorErrors.TryGetValue(id, out var error)) return new(id, "unreadable", [], error);
        var choices = ActorChoices.Effective(id, config);
        var state = config.Actors.TryGetValue(id, out var set) ? set.Count == 0 ? "off" : "set"
            : choices.Count == 0 && ActorChoices.MayBeOff(id) ? "off" : "before";
        return new(id, state, choices, null);
    }
}

/// <summary>An actor's stored entry in <c>driver.json</c> that does not read: the start that reads it holds with this sentence (§6.4).</summary>
public sealed class ActorUnreadableException(string actor, string why)
    : DriverException($"driver.json's `actors.{actor}` {why}, so {actor} starts nothing until it is set again (`daoris driver actor {actor}`).")
{
    public string Actor { get; } = actor;
}
