using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris driver workflow use … --landed</c> against real git (PUSH1f2b; FABRIC-PIPELINE1a, the pipeline design §1.3, §4.2), the part
/// of the family's text table (<c>DriverWorkflowsTextTests</c>) a repository git makes: the declaration at the line's tip shown and
/// accepted by its blob, accepted again when it changes, accepted as none where the tip holds none, and the start node's routes that
/// <c>show</c> says before the steps. The words are the CLI's, as its <c>workflowchoice-door.test.ts</c> held them.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DriverWorkflowsLandedProcessTests : IDisposable
{
    private const string Declared = """{ "gates": [{ "name": "suite", "run": "npm test", "heavy": true }] }""";

    private const string Verification = """
        { "id": "web-verify", "name": "Verify the line", "pipeline": "verification", "versions": [{ "version": 1, "at": "2026-10-21T09:00:00Z",
          "door": "terminal", "schema": 2, "graph": { "schemaVersion": 2, "entries": ["gates"], "end": "gates", "nodes": [{ "id": "gates",
          "kind": "check", "version": 1, "reach": "performed", "options": { "gates": "heavy", "failed": "wait" } }], "wires": [] } }] }
        """;

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "driver-workflows-landed", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public DriverWorkflowsLandedProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    /// <summary>A repository under the home whose line <c>main</c> holds the declaration given, or none.</summary>
    private async Task<string> RepositoryAsync(string? declaration)
    {
        var root = Path.Combine(_home, "web-app");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Web app\n");
        if (declaration is not null) await File.WriteAllTextAsync(Path.Combine(root, LandedGates.File), declaration);
        await GitAsync(root, "add", ".");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "the line");
        return root;
    }

    /// <summary><c>daoris driver workflow …</c> alone on this test's home, the terminal in it, the registry naming the repository's checkout.</summary>
    private async Task<(int Exit, IReadOnlyList<string> Out)> RunAsync(string root, params string[] words)
    {
        _out.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();
        var world = new DriverChoicesWorld
        {
            Instance = new InstanceWorld
            {
                Home = _home,
                Environment = name => name == DriverConfig.PathVariable ? Config : null,
                Out = _out,
                Error = _error,
                Verb = "daoris",
            },
            ConfigPath = Config,
            Folder = _home,
            Registry = _ => Task.FromResult(new ChoicesRegistry([("web-app", "work", root), ("notes-site", "work", null)], null)),
        };
        var exit = await DriverChoicesCommand.RunAsync(["workflow", .. words], world, CancellationToken.None);
        Assert.True(exit == 0, _error.ToString());
        return (exit, _out.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'));
    }

    private async Task VerificationAsync(string root)
    {
        await File.WriteAllTextAsync(Path.Combine(_home, "verify.json"), Verification);
        var (_, said) = await RunAsync(root, "import", "verify.json");
        Assert.StartsWith("daoris: `web-verify` — Verify the line, a verification pipeline, from verify.json", said[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_landed_route_shows_and_accepts_the_declaration_at_the_lines_tip_and_again_when_it_changes()
    {
        var root = await RepositoryAsync(Declared);
        await VerificationAsync(root);
        var blob = (await GitAsync(root, "rev-parse", $"main:{LandedGates.File}")).Trim();
        var tip = (await GitAsync(root, "rev-parse", "main")).Trim();

        var (_, plan) = await RunAsync(root, "use", "web-verify", "--repository", "web-app", "--landed", "--plan");
        Assert.Equal(
            [
                "daoris: `web-app` now verifies the work that lands with `web-verify` — Verify the line, v1 now: a graph (schema 2) of 1 node and "
                    + "0 wires, digest gph-33adad6e2b31c4063bc79327db9f9639.",
                $"  The gates it runs unasked, as `daoris.gates.json` declares them at `main`'s tip `{tip[..7]}` (blob `{blob[..7]}`):",
                $"    {Declared}",
                "  Accepted: these gates run unasked on the work that lands; an edit of `daoris.gates.json` asks you again, and nothing runs until you accept it.",
                "  The landed route resolves on its own: `web-app`'s development routes stay as they are.",
                "  --plan: nothing was written.",
                WorkflowWords.LandedReads,
            ],
            plan);
        Assert.False(File.Exists(Config));

        await RunAsync(root, "use", "web-verify", "--repository", "web-app", "--landed");
        Assert.Equal(new WorkflowLanded("web-verify", blob), DriverConfig.Load(Config).Workflows["web-app"].Landed);

        // Run again after the declaration changed, it accepts the new blob, and says what changed.
        await File.WriteAllTextAsync(Path.Combine(root, LandedGates.File), """{ "gates": [] }""");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-am", "fewer gates");
        var fewer = (await GitAsync(root, "rev-parse", $"main:{LandedGates.File}")).Trim();
        var (_, again) = await RunAsync(root, "use", "web-verify", "--repository", "web-app", "--landed");
        Assert.Contains($"  It replaces the gates accepted before, at blob `{blob[..7]}`: what changed is `git diff {blob} {fewer}`.", again);
        Assert.Equal(fewer, DriverConfig.Load(Config).Workflows["web-app"].Landed!.Gates);

        // Taken away, the work that lands goes unverified, said as what it lowers.
        var (_, cleared) = await RunAsync(root, "use", "--clear", "--repository", "web-app", "--landed");
        Assert.Equal(
            ["daoris: `web-app` no longer names a landed route: its workspace's reaches it, where it names one.", "  This asks less of you: it leaves the work that lands unverified."],
            cleared.Take(2));
        Assert.False(DriverConfig.Load(Config).Workflows.ContainsKey("web-app"));
    }

    [Fact]
    public async Task A_line_with_no_declaration_is_accepted_as_none()
    {
        var root = await RepositoryAsync(null);
        await VerificationAsync(root);
        var tip = (await GitAsync(root, "rev-parse", "main")).Trim();

        var (_, said) = await RunAsync(root, "use", "web-verify", "--repository", "web-app", "--landed");

        Assert.Contains($"  `main`'s tip `{tip[..7]}` holds no `daoris.gates.json`: accepted as none, so its checks find no declaration until one "
            + "lands and you accept it.", said);
        Assert.Equal(new WorkflowLanded("web-verify", WorkflowSelection.NoGates), DriverConfig.Load(Config).Workflows["web-app"].Landed);
    }

    [Fact]
    public async Task Show_says_the_start_nodes_routes_before_the_steps()
    {
        var root = await RepositoryAsync(Declared);
        await VerificationAsync(root);
        await RunAsync(root, "new", "docs-to-pr", "--from", "pull-request-no-press", "--name", "Documentation to a pull request");
        await RunAsync(root, "kind", "work", "docs", "--label", "Documentation", "--paths", "docs/**");
        await RunAsync(root, "kind", "work", "feature", "--label", "Feature");
        await RunAsync(root, "use", "docs-to-pr", "--workspace", "work", "--kind", "docs");
        await RunAsync(root, "use", "web-verify", "--repository", "web-app", "--landed");
        var blob = (await GitAsync(root, "rev-parse", $"main:{LandedGates.File}")).Trim();

        var (_, shown) = await RunAsync(root, "show", "--repository", "web-app");

        Assert.Matches("^daoris: `web-app` follows Current: in the workspace `work`, read from the rules as they stand at each gate \\(version [0-9a-f]{12}\\)\\.$", shown[0]);
        Assert.Equal(
            [
                "  Its start node routes the work that comes here:",
                "    the default → Current, since nothing names a workflow",
                "    kind `docs` (Documentation, `docs/**`) → `docs-to-pr`, chosen by the workspace `work` for Documentation",
                "    kind `feature` (Feature) → Current, since nothing names a workflow",
                $"    landed work → `web-verify`, as `web-app` chooses; its gates accepted at blob `{blob[..7]}`",
            ],
            shown.Skip(1).Take(5));
        Assert.Matches("^ {2}\\S", shown[6]);
    }
}
