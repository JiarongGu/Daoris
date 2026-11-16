using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The first build that saved a workflow version naming a step's role (<see cref="NodeDeclaration.Role"/>), kept beside the saved workflows: a
/// going back below it is refused (<see cref="InstallRollback"/>, code <see cref="Code"/>). A build before the field reads such a version without
/// its roles, takes another digest for it and holds every run bound to it as a version edited, so it must not open the home.
/// </summary>
public static class WorkflowRolesSince
{
    /// <summary>The going back's refusal code, the saved workflows'.</summary>
    public const string Code = "workflows";

    /// <summary>Where the build is kept: a text file beside the workflows' files, which their readers never take for one.</summary>
    public static string PathOf(string home) => Path.Combine(WorkflowStore.FolderOf(home), "roles-since.txt");

    /// <summary>The kept build, or null where no version naming a role has been saved by a numbered build.</summary>
    public static string? Read(string home) => File.Exists(PathOf(home)) ? File.ReadAllText(PathOf(home)).Trim() : null;

    /// <summary>
    /// Keeps <paramref name="running"/> where <paramref name="file"/> (a saved workflow's file) names a step's role and no build is kept yet; a
    /// build naming no numbered version keeps none, since no going back is judged against it.
    /// </summary>
    public static void Keep(string home, JsonNode? file, string? running)
    {
        if (HomeLastBuild.Numbered(running) is null || Read(home) is not null || !NamesRole(file)) return;
        Directory.CreateDirectory(WorkflowStore.FolderOf(home));
        File.WriteAllText(PathOf(home), running);
    }

    /// <summary>Why installing <paramref name="staged"/> is a going back past the field, or null: none kept, or a staged build at or above it.</summary>
    public static (string Code, string Sentence)? Refused(string home, string? staged) =>
        HomeLastBuild.Numbered(Read(home)) is { } since && HomeLastBuild.Numbered(staged) is { } older && older < since
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
