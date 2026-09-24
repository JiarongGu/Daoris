using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The agent producer (MAP3d): a repository that keeps a code map is asked, by the session prompt, to
/// keep it current — the owner's answer (2026-09-24) over a canon skill, which would have made a
/// Daoris feature into doctrine that no two repositories had learned. The file is still written by
/// that repository's own session, in its own tree; the driver only frames the ask (D32).
/// </summary>
public sealed class CodeMapPromptTests : IDisposable
{
    private readonly string _tree = Path.Combine(
        Path.GetTempPath(), "daoris-code-map-prompt-" + Guid.NewGuid().ToString("N")[..8]);

    public CodeMapPromptTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionTarget Target(string? codeMap = null) => new(
        QuestId: "abc123",
        Title: "Split the loader",
        Body: "The loader does two jobs; make them two modules.",
        Asker: "Platform",
        Repository: "Game",
        Root: "C:/somewhere/Game",
        ServiceUrl: "http://localhost:5177")
    {
        CodeMap = codeMap,
    };

    private void Keep(string relative)
    {
        var path = Path.Combine(_tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"version\": 1, \"modules\": [], \"dependencies\": [] }\n");
    }

    /// <summary>
    /// Named by its file, asked to move with the work, and told how: the repository's own tool where it
    /// has one — which nothing here can see, and nothing in the file says — otherwise by hand, in the
    /// shape the reader judges whole. Project-agnostic like every word of the target.
    /// </summary>
    [Fact]
    public void A_repository_that_keeps_a_code_map_is_asked_to_keep_it_current()
    {
        var prompt = TargetPrompt.Compose(Target("docs/code-map.json"));

        Assert.Contains("keeps a code map in `docs/code-map.json`", prompt);
        Assert.Contains("in the same change", prompt);
        Assert.Contains("own tool", prompt);
        Assert.Contains("`id`", prompt);
        Assert.Contains("relative to the repository", prompt);
        Assert.DoesNotContain("MAP3", prompt);
        Assert.DoesNotContain("D32", prompt);
        // It is part of the work, never a reason to skip the claim or the close.
        Assert.True(prompt.IndexOf("First take the quest", StringComparison.Ordinal)
            < prompt.IndexOf("keeps a code map", StringComparison.Ordinal));
        Assert.Contains("Never write outside", prompt);
    }

    /// <summary>
    /// 🔴 Only where a map exists (design §3): a repository that keeps none is not asked to start one —
    /// a map an agent invents unasked is a claim nobody chose, and the tool producer or the person
    /// starts a map deliberately.
    /// </summary>
    [Fact]
    public void A_repository_that_keeps_none_is_told_nothing_about_one()
    {
        Assert.DoesNotContain("code map", TargetPrompt.Compose(Target()));
    }

    /// <summary>An intake answers an ask in a room under the home, not in a repository — its own words stand.</summary>
    [Fact]
    public void An_intake_is_never_asked_about_a_code_map()
    {
        var prompt = TargetPrompt.Compose(Target("docs/code-map.json") with { Prompt = "Answer the ask." });

        Assert.Equal("Answer the ask.", prompt);
    }

    [Fact]
    public void The_map_is_found_where_the_reader_looks_first_match_wins()
    {
        Assert.Null(CodeMapFile.Find(_tree));

        Keep("code-map.json");
        Assert.Equal("code-map.json", CodeMapFile.Find(_tree));

        Keep("docs/code-map.json");
        Assert.Equal("docs/code-map.json", CodeMapFile.Find(_tree));
    }

    /// <summary>
    /// The target a quest's session is handed names the map in the TREE it runs in — its own tree
    /// where the repository opted in (D51), which carries the committed map like the root does.
    /// </summary>
    [Fact]
    public void A_quests_target_names_the_map_its_tree_keeps_and_carries_the_quest()
    {
        Keep("docs/code-map.json");
        var quest = new QuestView("abc123", "Platform", "Game", "Split the loader", "Two jobs.", "Open")
        {
            Links = ["https://tickets.example/T-1"],
            Parent = "a1b2c3",
        };

        var target = SessionTarget.ForQuest(quest, _tree, "http://localhost:5177");

        Assert.Equal("docs/code-map.json", target.CodeMap);
        Assert.Equal(("abc123", "Platform", "Game", _tree), (target.QuestId, target.Asker, target.Repository, target.Root));
        Assert.Equal(["https://tickets.example/T-1"], target.Links);
        Assert.Equal("a1b2c3", target.Parent);
        var bare = Directory.CreateDirectory(Path.Combine(_tree, "a-tree-that-keeps-none")).FullName;
        Assert.Null(SessionTarget.ForQuest(quest, bare, "http://localhost:5177").CodeMap);
    }

    /// <summary>
    /// The driver shares no code with the service, so it restates the reader's candidates — held to the
    /// reader's own source, as the devkit's twin is. A map the reader finds and the prompt does not name
    /// would go stale unasked; one the prompt names and the reader ignores would be kept for nobody.
    /// </summary>
    [Fact]
    public void Its_candidates_are_the_readers()
    {
        var reader = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core", "CodeMap.cs"));

        Assert.Contains($"Candidates = [{string.Join(", ", CodeMapFile.Candidates.Select(c => $"\"{c}\""))}]", reader);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("could not find the repository root");
    }
}
