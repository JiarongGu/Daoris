namespace Daoris.Driver;

/// <summary>
/// What a start's walk over Build's order answered (D205 §4.1, MODELROLE1c2): the selection, the entry it took (null where it waits), and
/// each entry it passed before, in order, with its code: what the wait's sentence and the opening note's actor line say.
/// </summary>
public sealed record BuildWalk(HarnessSelection Selection, ActorEffective? Chosen, IReadOnlyList<BuildPass> Passes);

/// <summary>An actor's name as the screen says it in English (D205 §2.2), for the lines the driver writes; the page words it by its id.</summary>
public static class ActorWords
{
    public static string Name(string actor) => actor switch
    {
        "build" => "Build",
        "intake" => "Intake",
        "opinion" => "Second opinion",
        "helper" => "Helper",
        "partner" => "Partner",
        "brainstorm" => "Brainstorm",
        "ask-daoris" => "Ask Daoris",
        "chat" => "Chat",
        _ => actor,
    };
}

/// <summary>
/// One entry of Build's order a start passed (D205 §4.1): <c>unavailable</c>, <c>no-posture</c>, <c>no-connector</c>, <c>not-offered</c>,
/// <c>cooling</c>, <c>signed-out</c> or <c>held</c>, and the walk's own reason in English.
/// </summary>
public sealed record BuildPass(ActorEffective Entry, string Code, string? Why)
{
    /// <summary>The entry as the person's order says it: its agent, and its model or <i>account default</i>.</summary>
    public string Named => $"{Entry.Agent} · {Entry.Model ?? "account default"}";

    /// <summary>The pass in the wait's words: <c>choice n, agent · model: why</c>.</summary>
    public string Said => $"choice {Entry.From}, {Named}: {Why?.TrimEnd('.')}";
}
