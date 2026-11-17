using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where the saved workflows' floors are kept (<see cref="WorkflowRolesSince"/>): a text file each beside the workflows' files, which their
/// readers never take for one. Each judgement reads them and asks the rule; a floor named none is the roles'.
/// </summary>
public static class WorkflowRolesSinceStore
{
    public static string PathOf(string home, WorkflowRolesSince.Floor? floor = null) =>
        Path.Combine(WorkflowStore.FolderOf(home), (floor ?? WorkflowRolesSince.Roles).File);

    /// <summary>The floor's kept build, or null where no version closing it has been saved by a numbered build.</summary>
    public static string? Read(string home, WorkflowRolesSince.Floor? floor = null) =>
        File.Exists(PathOf(home, floor)) ? AtomicFile.ReadText(PathOf(home, floor)).Trim() : null;

    /// <summary>Keeps <paramref name="running"/> as each floor a save of <paramref name="file"/> closes (<see cref="WorkflowRolesSince.Closes"/>).</summary>
    public static void Keep(string home, JsonNode? file, string? running)
    {
        foreach (var floor in WorkflowRolesSince.Floors)
        {
            if (WorkflowRolesSince.Closes(Read(home, floor), file, running, floor) is not { } build) continue;
            Directory.CreateDirectory(WorkflowStore.FolderOf(home));
            // Whole or not at all: a floor torn by a stop is read as no floor, and the going back it closes is allowed.
            AtomicFile.WriteText(PathOf(home, floor), build);
        }
    }

    /// <summary>The build a save of <paramref name="file"/> on <paramref name="home"/> keeps as a floor where it closes any, which its confirm card says first.</summary>
    public static string? Closes(string home, JsonNode? file, string? running) =>
        WorkflowRolesSince.Floors.Select(floor => WorkflowRolesSince.Closes(Read(home, floor), file, running, floor)).FirstOrDefault(build => build is not null);

    /// <summary>Why installing <paramref name="staged"/> on <paramref name="home"/> goes back past a kept floor (<see cref="WorkflowRolesSince.Refused"/>), the highest first.</summary>
    public static (string Code, string Sentence)? Refused(string home, string? staged) =>
        WorkflowRolesSince.Floors
            .OrderByDescending(floor => HomeLastBuild.Numbered(Read(home, floor)))
            .Select(floor => WorkflowRolesSince.Refused(Read(home, floor), staged, floor))
            .FirstOrDefault(refused => refused is not null);
}
