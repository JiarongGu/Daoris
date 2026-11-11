using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing reads the lines the work adds against the private patterns file its repository declares, taken from the repository's own
/// checkout (a session's tree holds none of the ignored <c>local/</c>): a hit holds the landing with the path and the pattern's number, never
/// its text; a clean diff lands, and a repository declaring none is unaffected. Real git in a scratch repository (the suite's Process half).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SensitiveLandingTests : LandedFixture
{
    private const string Private = "secret-workspace-name";
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private static async Task DeclareAsync(string root)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.gates.json"), """{ "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");
        Directory.CreateDirectory(Path.Combine(root, "local"));
        await File.WriteAllTextAsync(Path.Combine(root, "local", "sensitive-patterns.txt"), $"# names\nunrelated-name\n{Private}\n");
    }

    [Fact]
    public async Task A_line_matching_a_declared_pattern_holds_the_landing_with_its_path_and_number_and_no_text()
    {
        var root = await RepositoryAsync("engine");
        await DeclareAsync(root);
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", $"- a row about {Private}\n", "a row");

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(landed.Landed);
        Assert.Equal(LandingCode.Refused, landed.Refusal);
        Assert.Contains("`TASKS.md` (pattern 2)", landed.Message);
        Assert.DoesNotContain(Private, landed.Message);
    }

    [Fact]
    public async Task A_clean_diff_lands_and_a_repository_declaring_no_patterns_file_is_unaffected()
    {
        var root = await RepositoryAsync("engine");
        await DeclareAsync(root);
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", "- a clean row\n", "a row");

        var first = await trees.LandAsync(tree.Path, Subject, gate: Lands);
        Assert.True(first.Landed, first.Message);

        var bare = await RepositoryAsync("plain");
        var other = await trees.OpenAsync(bare, "plain", "borealis");
        await CommitAsync(other.Path, "TASKS.md", $"- {Private}\n", "a row");

        var second = await trees.LandAsync(other.Path, Subject, gate: Lands);
        Assert.True(second.Landed, second.Message);
    }
}
