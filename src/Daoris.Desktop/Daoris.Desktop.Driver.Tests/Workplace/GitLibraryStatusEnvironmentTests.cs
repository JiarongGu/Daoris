using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A tree's status where the process's environment says what git reads: the personal ignore file under <c>XDG_CONFIG_HOME</c>, ranked
/// below the repository's rules, and an index named by <c>GIT_INDEX_FILE</c>, which only git reads. On real git.
/// </summary>
/// <remarks>Alone: it sets variables of the process's environment, which every class beside it and each git they start would read.</remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class GitLibraryStatusEnvironmentTests : LandedFixture
{
    private static readonly string[] Status = ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"];

    [GitLegFact]
    public async Task The_personal_ignore_file_decides_only_what_the_repository_s_rules_leave()
    {
        var root = await RepositoryAsync("status-personal");
        var config = Path.Combine(Scratch, "status-personal-xdg");
        Directory.CreateDirectory(Path.Combine(config, "git"));
        await File.WriteAllTextAsync(Path.Combine(config, "git", "ignore"), "*.log\n!*.tmp\n");
        await File.WriteAllTextAsync(Path.Combine(root, "other.log"), "o\n");
        await File.WriteAllTextAsync(Path.Combine(root, "keep.log"), "k\n");
        await File.WriteAllTextAsync(Path.Combine(root, "scratch.tmp"), "s\n");
        await WithAsync("XDG_CONFIG_HOME", config, async () =>
        {
            await GitStamp.OfAsync(root, CancellationToken.None);

            // The personal file hides what no repository rule names; the repository's exclusion stands over its negation.
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.tmp\n");
            await SameAsync(root, Status);

            // A repository negation may re-include what the personal file excludes: git's to rank.
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.tmp\n!keep.log\n");
            await GitsAsync(root, Status);
        });
    }

    [GitLegFact]
    public async Task An_index_the_process_names_elsewhere_is_git_s()
    {
        var root = await RepositoryAsync("status-index");
        var other = Path.Combine(Scratch, "status-index-other");
        File.Copy(Path.Combine(root, ".git", "index"), other);
        await RunAsync(root, new Dictionary<string, string> { ["GIT_INDEX_FILE"] = other }, null, "rm", "-q", "--cached", "shared.txt");
        await GitStamp.OfAsync(root, CancellationToken.None);
        await SameAsync(root, Status);
        await WithAsync("GIT_INDEX_FILE", other, () => GitsAsync(root, Status));
    }

    private static async Task WithAsync(string name, string value, Func<Task> work)
    {
        var was = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            await work();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, was);
        }
    }
}
