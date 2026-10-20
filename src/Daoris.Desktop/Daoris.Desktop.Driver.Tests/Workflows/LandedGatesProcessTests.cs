using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1a (D192 point 2, the pipeline design §4.2): the gates a landed route runs unasked are accepted by git's blob id of
/// <c>daoris.gates.json</c> at the line's tip, read with no checkout, and <c>none</c> where the tip holds none. Against real git: what the
/// checkout holds uncommitted, and another branch's declaration, are never read.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandedGatesProcessTests : IDisposable
{
    private const string Declared = """{ "gates": [{ "name": "suite", "run": "npm test", "heavy": true }] }""";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "landed-gates", Guid.NewGuid().ToString("N")[..8]);

    public LandedGatesProcessTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task<string> RepositoryAsync(string name, string? declaration)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Web app\n");
        if (declaration is not null) await File.WriteAllTextAsync(Path.Combine(root, LandedGates.File), declaration);
        await GitAsync(root, "add", ".");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "the line");
        return root;
    }

    [Fact]
    public async Task The_routes_blob_is_read_from_the_lines_tip_and_never_from_the_checkout()
    {
        var root = await RepositoryAsync("declared", Declared);
        var blob = (await GitAsync(root, "rev-parse", $"main:{LandedGates.File}")).Trim();
        var tip = (await GitAsync(root, "rev-parse", "main")).Trim();

        // In flight in the checkout, and committed on another branch: neither is the line's tip.
        await GitAsync(root, "checkout", "--quiet", "-b", "elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, LandedGates.File), """{ "gates": [] }""");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-am", "elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, LandedGates.File), """{ "gates": [{ "name": "other", "run": "x" }] }""");

        var (read, problem) = await LandedGates.ReadAsync(root, "main");

        Assert.Null(problem);
        Assert.Equal(new LandedTip("main", tip, blob), read);
        Assert.Equal(Declared, await LandedGates.TextAsync(root, blob));
    }

    [Fact]
    public async Task A_tip_with_no_declaration_reads_none()
    {
        var root = await RepositoryAsync("undeclared", null);

        var (read, problem) = await LandedGates.ReadAsync(root, "main");

        Assert.Null(problem);
        Assert.Equal(WorkflowSelection.NoGates, read!.Gates);
        Assert.Null(await LandedGates.TextAsync(root, WorkflowSelection.NoGates));
    }

    [Fact]
    public async Task A_line_that_names_no_commit_and_a_folder_that_is_no_repository_are_said()
    {
        var root = await RepositoryAsync("lined", Declared);

        var (none, noLine) = await LandedGates.ReadAsync(root, "release");
        Assert.Null(none);
        Assert.Equal("its line `release` names no commit here", noLine);

        var inside = Path.Combine(root, "sub");
        Directory.CreateDirectory(inside);
        var (outside, notTop) = await LandedGates.ReadAsync(inside, "main");
        Assert.Null(outside);
        Assert.Equal("its checkout here is not a git repository", notTop);
    }
}
