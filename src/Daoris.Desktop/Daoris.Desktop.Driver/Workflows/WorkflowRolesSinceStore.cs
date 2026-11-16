using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where the first build that saved a workflow version naming a step's role is kept (<see cref="WorkflowRolesSince"/>): a text file beside the
/// workflows' files, which their readers never take for one. Each judgement reads it and asks the rule.
/// </summary>
public static class WorkflowRolesSinceStore
{
    public static string PathOf(string home) => Path.Combine(WorkflowStore.FolderOf(home), "roles-since.txt");

    /// <summary>The kept build, or null where no version naming a role has been saved by a numbered build.</summary>
    public static string? Read(string home) => File.Exists(PathOf(home)) ? File.ReadAllText(PathOf(home)).Trim() : null;

    /// <summary>Keeps <paramref name="running"/> where a save of <paramref name="file"/> closes the floor (<see cref="Closes"/>).</summary>
    public static void Keep(string home, JsonNode? file, string? running)
    {
        if (Closes(home, file, running) is not { } build) return;
        Directory.CreateDirectory(WorkflowStore.FolderOf(home));
        File.WriteAllText(PathOf(home), build);
    }

    /// <summary>The build a save of <paramref name="file"/> on <paramref name="home"/> keeps as the floor (<see cref="WorkflowRolesSince.Closes"/>).</summary>
    public static string? Closes(string home, JsonNode? file, string? running) => WorkflowRolesSince.Closes(Read(home), file, running);

    /// <summary>Why installing <paramref name="staged"/> on <paramref name="home"/> is a going back past the field (<see cref="WorkflowRolesSince.Refused"/>).</summary>
    public static (string Code, string Sentence)? Refused(string home, string? staged) => WorkflowRolesSince.Refused(Read(home), staged);
}
