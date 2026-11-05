using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row h's edges (the agent-provider design §2.7, §4.6): the transports' one shell sits in Agents and reaches the session's
/// git through the port Workplace installs, so it names no type of Workplace, Work orders or Context; and Platform names nothing of a spawn.
/// Each held twice: on the driver's tree, and by a planted breach the structure check refuses.
/// </summary>
public sealed class ShellStructureTests
{
    private static readonly Lazy<(StructureMap Map, StructureAnalysis Analysis)> Tree = new(() =>
    {
        var map = StructureMap.Read(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json")));
        var driver = SourceFiles.Project(SourceFiles.DriverProject);
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Select(path => (Path.GetRelativePath(driver, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .ToList();
        return (map, StructureAnalysis.Of(sources));
    });

    [Fact]
    public void The_shell_names_no_type_of_Workplace_Work_orders_or_Context()
    {
        var above = new[] { "Workplace", "Work orders", "Context" };
        Assert.Empty(Tree.Value.Analysis.Edges.Where(edge => edge.File.StartsWith("Agents/Transports/", StringComparison.Ordinal)
            && edge.DeclaredIn.Any(path => above.Contains(Tree.Value.Map.OwnerOf(path)))));
    }

    [Fact]
    public void Platform_names_no_spawn_of_Agents_and_no_tools_of_Install_or_git_of_Workplace()
    {
        var below = new[] { "Agents", "Install", "Workplace" };
        Assert.Empty(Tree.Value.Analysis.Edges.Where(edge => Tree.Value.Map.OwnerOf(edge.File) == "Platform"
            && new[] { "ProcessRecipe", "ISpawn", "Shell", "SessionGit", "ISessionGit", "SessionTarget", "Tools", "GitWiring" }.Contains(edge.Type)
            && edge.DeclaredIn.Any(path => below.Contains(Tree.Value.Map.OwnerOf(path)))));
    }

    [Fact]
    public void A_planted_git_call_in_the_shell_and_a_planted_recipe_in_Platform_each_fail_the_check()
    {
        var planted = new (string Path, string Text)[]
        {
            ("Workplace/GitWiring.cs", "namespace Daoris.Driver;\npublic static class GitWiring { public static void Untoken() { } }"),
            ("Agents/Contract/ProcessRecipe.cs", "namespace Daoris.Driver;\npublic sealed record ProcessRecipe(string Program);"),
            ("Agents/Transports/Shell.cs", "namespace Daoris.Driver;\ninternal static class Shell { static void Start() => GitWiring.Untoken(); }"),
            ("Platform/AgentStarts.cs", "namespace Daoris.Driver;\ninternal static class AgentStarts { static ProcessRecipe? Recipe; }"),
        };

        var breaches = StructureCheck.Check(Tree.Value.Map, planted).Select(breach => breach.ToString()).ToList();

        Assert.Contains(breaches, breach => breach.StartsWith("Agents/Transports/Shell.cs:2", StringComparison.Ordinal) && breach.Contains("GitWiring"));
        Assert.Contains(breaches, breach => breach.StartsWith("Platform/AgentStarts.cs:2", StringComparison.Ordinal) && breach.Contains("ProcessRecipe"));
    }
}
