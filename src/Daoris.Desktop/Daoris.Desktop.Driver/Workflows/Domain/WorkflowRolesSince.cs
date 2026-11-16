using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The first build that saved a workflow version naming a step's role (<see cref="NodeDeclaration.Role"/>), kept beside the saved workflows
/// (<see cref="WorkflowRolesSinceStore"/>): a going back below it is refused (<see cref="InstallRollback"/>, code <see cref="Code"/>). A build
/// before the field reads such a version without its roles, takes another digest for it and holds every run bound to it as a version edited,
/// so it must not open the home. The rule alone, on the kept build as a value.
/// </summary>
public static class WorkflowRolesSince
{
    /// <summary>The going back's refusal code, the saved workflows'.</summary>
    public const string Code = "workflows";

    /// <summary>
    /// The build a save of <paramref name="file"/> keeps as the floor, which its confirm card says first: <paramref name="running"/> where the
    /// file names a step's role (a default one is never written, <see cref="StepRoles.Unwritten"/>) and none is kept yet
    /// (<paramref name="kept"/> null); else null. A build naming no numbered version keeps none, since no going back is judged against it.
    /// </summary>
    public static string? Closes(string? kept, JsonNode? file, string? running) =>
        HomeLastBuild.Numbered(running) is not null && kept is null && NamesRole(file) ? running : null;

    /// <summary>Why installing <paramref name="staged"/> is a going back past the field, or null: none kept, or a staged build at or above it.</summary>
    public static (string Code, string Sentence)? Refused(string? kept, string? staged) =>
        HomeLastBuild.Numbered(kept) is { } since && HomeLastBuild.Numbered(staged) is { } older && older < since
            ? (Code, $"a saved workflow names its steps' roles, which builds before {since} read without them, so going back to {staged} would hold every run bound to it.")
            : null;

    /// <summary>Whether any step of any version in <paramref name="file"/> names a role: an object in a <c>nodes</c> list holding <c>role</c>.</summary>
    private static bool NamesRole(JsonNode? node) => node switch
    {
        JsonObject named => named.Any(each => each.Key == "nodes" && each.Value is JsonArray nodes
                && nodes.Any(step => step is JsonObject declared && declared["role"] is JsonValue))
            || named.Any(each => NamesRole(each.Value)),
        JsonArray list => list.Any(NamesRole),
        _ => false,
    };
}
