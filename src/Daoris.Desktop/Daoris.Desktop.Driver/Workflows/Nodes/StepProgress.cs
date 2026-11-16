using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What a performed step's plugin last said of its attempt while it runs (<see cref="WorkStates.Progress"/>), kept on the attempt and never
/// settling it: the actor whose act the step waits on, where one does, its words and its page.
/// </summary>
/// <param name="Actor">The actor the step waits on (a plugin's word: <c>approver</c>, <c>deployer</c>), or null where it waits on none and goes on.</param>
/// <param name="Words">What the plugin said, as written.</param>
/// <param name="Url">The page where the act is taken.</param>
/// <param name="Environment">Where it acts (a stage), as the plugin names it.</param>
/// <param name="At">When the plugin said it: since when it waits.</param>
public sealed record StepProgress(string? Actor, string? Words, string? Url, string? Environment, DateTimeOffset At)
{
    /// <summary>A progress frame's outcome as the attempt keeps it, at the frame's time.</summary>
    public static StepProgress Of(WorkOutcome outcome, DateTimeOffset at) =>
        new(outcome.Actor, outcome.Evidence?.Message, outcome.Evidence?.Url, outcome.Evidence?.Environment, at);

    /// <summary>Written as one attempt's <c>progress</c> object, a part not said not written.</summary>
    public static void Write(Utf8JsonWriter writer, StepProgress progress)
    {
        writer.WriteStartObject("progress");
        if (progress.Actor is { } actor) writer.WriteString("actor", actor);
        if (progress.Words is { } words) writer.WriteString("words", words);
        if (progress.Url is { } url) writer.WriteString("url", url);
        if (progress.Environment is { } environment) writer.WriteString("environment", environment);
        writer.WriteString("at", Moments.Write(progress.At));
        writer.WriteEndObject();
    }

    /// <summary>An attempt's <c>progress</c>, or null where none is kept or it does not read: a progress is words, never a state.</summary>
    public static StepProgress? Read(JsonElement attempt) =>
        attempt.TryGetProperty("progress", out var kept) && kept.ValueKind == JsonValueKind.Object && Moments.Read(kept, "at") is { } at
            ? new StepProgress(Text(kept, "actor"), Text(kept, "words"), Text(kept, "url"), Text(kept, "environment"), at)
            : null;
}

/// <summary>Who fills an actor a step waits on: its kind and, for an agent, the name it goes by.</summary>
/// <param name="Kind">One of <see cref="StepHolders"/>' kinds.</param>
/// <param name="Name">The agent's name; null for the person.</param>
public sealed record StepHolder(string Kind, string? Name = null);

/// <summary>
/// Who fills the actor a step waits on. The step names the actor, never who fills it: the place's roles and its risk choose, so the same
/// step is held by the person or by an agent a role appoints, said alike. No role takes an actor yet, so the person fills each one; an
/// approver at a destination marked as the person's stays theirs whatever a role says.
/// </summary>
public static class StepHolders
{
    /// <summary>The person: their act, in the window or on the platform.</summary>
    public const string Person = "person";

    /// <summary>An agent a role appoints, named.</summary>
    public const string Agent = "agent";

    /// <summary>The holder filling <paramref name="actor"/> now.</summary>
    public static StepHolder Of(string actor) => new(Person);
}
