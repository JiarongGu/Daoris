using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The start's words of a tree's build bases (the tree-pool design §36.3, 4, §38.2, 3 and 5): the bases named with the runners their
/// rule integrated, or the word that none refuses them; a release that set entries aside said once; and a development host passed over
/// for standing in a base named with the door that releases it.
/// </summary>
public sealed class TreeBaseWordsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-base-words", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Tree => Path.Combine(_root, "engine");

    public TreeBaseWordsTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Tree);
    }

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_root);

    [Fact]
    public void The_bases_are_named_with_the_runners_that_refuse_them_or_the_word_that_none_does()
    {
        Keep("bin/Debug/", "bin/Release/");
        var guarded = Rules(new FileRule(FileKind.Output, ["bin/"]) { Take = "base", Refuses = ["node tools/run.mjs"] });

        var (said, told) = TreeBaseWords.Say(Home, Tree, guarded, new FileFloorFacts([], []));

        var line = Assert.Single(said);
        Assert.Equal(StartNoteCodes.StartedTreeBase.Code, Assert.Single(line.Parts).Code);
        Assert.Equal(["bin/Debug/", "bin/Release/"], (IReadOnlyList<string>)line.Parts[0].Value("bases")!);
        Assert.Equal(["node tools/run.mjs"], (IReadOnlyList<string>)line.Parts[0].Value("runners")!);
        Assert.Contains("daoris tree build <path>", told);
        Assert.Contains("`node tools/run.mjs` refuse it", told);

        var (unguarded, _) = TreeBaseWords.Say(Home, Tree, Rules(new FileRule(FileKind.Output, ["bin/"]) { Take = "base" }), new FileFloorFacts([], []));
        Assert.Equal(StartNoteCodes.StartedTreeBaseUnguarded.Code, Assert.Single(Assert.Single(unguarded).Parts).Code);
    }

    [Fact]
    public void A_release_that_set_entries_aside_is_said_once_naming_its_producer_and_count()
    {
        var release = new TreeBaseRelease("bin/Debug/", "App/App.csproj", "Debug", [], ["dotnet build -c Debug App.csproj"], "abc", 0, DateTimeOffset.UtcNow, 3);
        TreeFilesRecord.Change(Home, Tree, state => state with { Released = [release, release with { Path = "bin/Release/", Trimmed = 0 }] });

        var (first, told) = TreeBaseWords.Say(Home, Tree, Rules(), new FileFloorFacts([], []));
        var (again, _) = TreeBaseWords.Say(Home, Tree, Rules(), new FileFloorFacts([], []));

        var part = Assert.Single(Assert.Single(first).Parts);
        Assert.Equal(StartNoteCodes.TreeBaseTrimmed.Code, part.Code);
        Assert.Equal(("bin/Debug/", "App/App.csproj", 3), ((string)part.Value("path")!, (string)part.Value("project")!, Convert.ToInt32(part.Value("count"))));
        Assert.Null(told);
        Assert.Empty(again);
    }

    [Fact]
    public void A_development_host_standing_in_a_base_is_named_with_the_door_and_one_that_stands_in_none_is_not()
    {
        var workspace = Path.Combine(_root, "workspace");
        var running = Path.Combine(workspace, "src", "Daoris.Desktop", "App", "net10.0");
        Directory.CreateDirectory(running);
        File.WriteAllText(Path.Combine(workspace, KnowledgeConnector.WorkspaceMarker), "{}");
        var marked = Path.Combine(workspace, "src", "Daoris.Service", KnowledgeConnector.Project, "bin", "Release");
        Directory.CreateDirectory(Path.Combine(marked, "net10.0"));
        Assert.Null(BaseHosts.PassedOver(running, KnowledgeConnector.Project));

        File.WriteAllText(Path.Combine(marked, BaseMarker.FileName), """{ "from": "s-aaaa0001", "says": "x" }""");

        var path = BaseHosts.PassedOver(running, KnowledgeConnector.Project);
        Assert.Equal($"src/Daoris.Service/{KnowledgeConnector.Project}/bin/Release", path);
        Assert.Null(BaseHosts.PassedOver(running, ServiceHostLocator.Project));
        var said = BaseHosts.Said(path!);
        Assert.Equal(StartNoteCodes.TreeBaseHost.Code, Assert.Single(said.Parts).Code);
        Assert.Contains($"daoris tree build {path}", said.Note);
    }

    private void Keep(params string[] paths) => TreeFilesRecord.Change(Home, Tree, state => state with
    {
        Bases = [.. paths.Select(path => new TreeBase(path, "s-aaaa0001", "abc", DateTimeOffset.UtcNow, "Engine.csproj", path.Split('/')[1]))],
    });

    private static FileRules Rules(params FileRule[] rules) => FileRules.Of([], rules, PersonFilesRead.Empty, "engine");
}
