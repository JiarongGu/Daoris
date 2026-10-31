using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITISOLATE1: a test's git reaches its fixture and no repository above it. The ceiling is a variable on the start, never the
/// process's: no git runs here but the one class below.
/// </summary>
public sealed class TestGitCeilingTests
{
    private const string Ceiling = "GIT_CEILING_DIRECTORIES";

    private static string Scratch => Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-ceiling-tests");

    private static string? Held(ProcessStartInfo info) => info.Environment.TryGetValue(Ceiling, out var value) ? value : null;

    [Fact]
    public void A_fixture_folder_that_is_no_repository_is_capped_at_its_parent()
    {
        var folder = Path.Combine(Scratch, "plain", "fixture");

        var info = TestGit.Ceiling(new ProcessStartInfo("git") { WorkingDirectory = folder });

        Assert.Equal(Path.GetDirectoryName(folder), info.Environment[Ceiling]);
    }

    [Fact]
    public void A_folder_inside_a_fixture_repository_is_capped_above_the_repository_so_it_still_finds_it()
    {
        var root = Path.Combine(Scratch, "with-repository", "repo");
        var folder = Path.Combine(root, "src", "deep");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        Directory.CreateDirectory(folder);
        try
        {
            var info = TestGit.Ceiling(new ProcessStartInfo("git") { WorkingDirectory = folder });

            Assert.Equal(Path.GetDirectoryName(root), info.Environment[Ceiling]);
        }
        finally
        {
            GitSeed.Discard(Path.GetDirectoryName(root)!);
        }
    }

    [Fact]
    public void A_start_that_names_its_repository_is_left_alone()
    {
        var folder = Path.Combine(Scratch, "named", "fixture");
        var argument = TestGit.Ceiling(new ProcessStartInfo("git") { WorkingDirectory = folder, ArgumentList = { "--git-dir=x", "status" } });
        var variable = new ProcessStartInfo("git") { WorkingDirectory = folder };
        variable.Environment.Remove(Ceiling);
        variable.Environment["GIT_DIR"] = "x";

        Assert.Equal(Held(new ProcessStartInfo("git")), Held(argument));
        Assert.Null(Held(TestGit.Ceiling(variable)));
    }

    [Fact]
    public void A_commands_own_git_dir_flag_names_no_repository()
    {
        var folder = Path.Combine(Scratch, "flag", "fixture");

        var info = TestGit.Ceiling(new ProcessStartInfo("git") { WorkingDirectory = folder, ArgumentList = { "-c", "a.b=c", "rev-parse", "--git-dir" } });

        Assert.Equal(Path.GetDirectoryName(folder), Held(info));
    }

    [Fact]
    public void Past_caps_the_start_it_points_at_git()
    {
        var info = TestGit.Past(new ProcessStartInfo("git") { WorkingDirectory = Path.Combine(Scratch, "past", "fixture") });

        Assert.Equal(Path.Combine(Scratch, "past"), info.Environment[Ceiling]);
    }
}

/// <summary>GITISOLATE1's proof against a real git: a command that would climb out of its fixture is refused.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TestGitCeilingProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-ceiling-process", Guid.NewGuid().ToString("N")[..8]);

    public TestGitCeilingProcessTests() => Directory.CreateDirectory(Path.Combine(_scratch, "fixture", "sub"));

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public async Task A_fixture_command_that_would_climb_to_the_checkout_is_refused_and_its_config_is_unchanged()
    {
        var common = (await GitFixture.GitAsync(GitSeed.RepoRoot(), "rev-parse", "--path-format=absolute", "--git-common-dir")).Trim();
        var config = Path.Combine(common, "config");
        var before = File.ReadAllText(config);

        // Each would act on the checkout's repository from a fixture that is none: the climb that once left core.bare set.
        var inside = await GitFixture.RunAsync(Path.Combine(_scratch, "fixture"), "rev-parse", "--git-dir");
        var read = await GitFixture.RunAsync(Path.Combine(_scratch, "fixture", "sub"), "config", "--local", "--get", "core.bare");

        Assert.NotEqual(0, inside.ExitCode);
        Assert.Contains("not a git repository", inside.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(128, read.ExitCode);
        Assert.Equal(before, File.ReadAllText(config));
    }

    [Fact]
    public async Task A_fixture_repository_still_answers_from_a_folder_inside_it()
    {
        var repository = Path.Combine(_scratch, "fixture");
        Assert.Equal(0, (await GitFixture.RunAsync(repository, "init", "--quiet", "-b", "main")).ExitCode);

        var run = await GitFixture.RunAsync(Path.Combine(repository, "sub"), "rev-parse", "--show-toplevel");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Path.GetFullPath(repository), Path.GetFullPath(run.Stdout.Trim()));
    }
}
