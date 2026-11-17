using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The saved workflows' floors (<see cref="Floors"/>): for what a saved version may name that an earlier build misreads, the first build that
/// saved it, kept beside the saved workflows (<see cref="WorkflowRolesSinceStore"/>), and a going back below any is refused
/// (<see cref="InstallRollback"/>, code <see cref="Code"/>). Each is kept apart, since a roles' floor kept first lies below the builds that know
/// no <c>task</c> (<see cref="HumanFormerKind"/>). The rule alone, on a kept build as a value.
/// </summary>
public static class WorkflowRolesSince
{
    /// <summary>The going back's refusal code, the saved workflows'.</summary>
    public const string Code = "workflows";

    /// <summary>One floor: the file its first build is kept in, what in a saved file closes it, and why going back below it is refused (<c>{0}</c> the kept build).</summary>
    public sealed record Floor(string File, Func<JsonNode?, bool> Names, string Why);

    /// <summary>A step naming its role: an object in a <c>nodes</c> list holding <c>role</c> (a default one is never written, <see cref="StepRoles.Unwritten"/>).</summary>
    public static readonly Floor Roles = new("roles-since.txt", node => AnyStep(node, step => step["role"] is JsonValue),
        "a saved workflow names its steps' roles, which builds before {0} read without them");

    /// <summary>A step of the <c>task</c> kind, which a save writes where the former <c>human</c> kind stood.</summary>
    public static readonly Floor Tasks = new("tasks-since.txt",
        node => AnyStep(node, step => step["kind"] is JsonValue kind && kind.TryGetValue<string>(out var named) && named == NodeKinds.Task),
        "a saved workflow has a `task` step, which builds before {0} know only as the `human` kind");

    /// <summary>Every floor, each kept and judged apart.</summary>
    public static IReadOnlyList<Floor> Floors { get; } = [Roles, Tasks];

    /// <summary>
    /// The build a save of <paramref name="file"/> keeps as <paramref name="floor"/>'s (the roles' where none is named), which its confirm card
    /// says first: <paramref name="running"/> where the file names what closes it and none is kept yet (<paramref name="kept"/> null); else null.
    /// A build naming no numbered version keeps none, since no going back is judged against it.
    /// </summary>
    public static string? Closes(string? kept, JsonNode? file, string? running, Floor? floor = null) =>
        HomeLastBuild.Numbered(running) is not null && kept is null && (floor ?? Roles).Names(file) ? running : null;

    /// <summary>Why installing <paramref name="staged"/> goes back past <paramref name="floor"/>'s kept build, or null: none kept, or a staged build at or above it.</summary>
    public static (string Code, string Sentence)? Refused(string? kept, string? staged, Floor? floor = null) =>
        HomeLastBuild.Numbered(kept) is { } since && HomeLastBuild.Numbered(staged) is { } older && older < since
            ? (Code, $"{string.Format(System.Globalization.CultureInfo.InvariantCulture, (floor ?? Roles).Why, since)}, so going back to {staged} would hold every run bound to it.")
            : null;

    /// <summary>Whether any object in a <c>nodes</c> list anywhere in <paramref name="node"/> is a step <paramref name="step"/> holds for.</summary>
    private static bool AnyStep(JsonNode? node, Func<JsonObject, bool> step) => node switch
    {
        JsonObject named => named.Any(each => each.Key == "nodes" && each.Value is JsonArray nodes
                && nodes.Any(declared => declared is JsonObject one && step(one)))
            || named.Any(each => AnyStep(each.Value, step)),
        JsonArray list => list.Any(each => AnyStep(each, step)),
        _ => false,
    };
}
