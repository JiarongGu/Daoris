using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git Daoris runs, by its version: git's answer read, asked of a
/// file once per change of it, and the two floors judged, each refusal naming the version it found and the floor it needs.
/// </summary>
public sealed class GitVersionsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-versions", Guid.NewGuid().ToString("N")[..8]);

    public GitVersionsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        GitSeed.Discard(_root);
    }

    [Theory]
    [InlineData("git version 2.53.0.windows.1\n", "2.53.0")]
    [InlineData("git version 2.30.1\n", "2.30.1")]
    [InlineData("git version 2.31.0.vfs.0.0", "2.31.0")]
    [InlineData("not a git", null)]
    [InlineData("", null)]
    public void Gits_answer_is_read_as_its_first_dotted_number(string said, string? version)
    {
        Assert.Equal(version, GitVersions.Parse(said)?.ToString());
    }

    [Fact]
    public async Task A_file_is_asked_once_and_again_only_once_it_changes()
    {
        var git = Path.Combine(_root, "git.exe");
        File.WriteAllText(git, "a");
        var asked = 0;
        Task<string?> Ask(string file, CancellationToken ct)
        {
            asked++;
            return Task.FromResult<string?>("git version 2.30.1\n");
        }

        Assert.Null(GitVersions.Known(git));
        Assert.Equal(new Version(2, 30, 1), await GitVersions.OfAsync(git, Ask, CancellationToken.None));
        Assert.Equal(new Version(2, 30, 1), await GitVersions.OfAsync(git, Ask, CancellationToken.None));
        Assert.Equal(new Version(2, 30, 1), GitVersions.Known(git));
        Assert.Equal(1, asked);

        File.WriteAllText(git, "a newer git");
        Assert.Null(GitVersions.Known(git));
        await GitVersions.OfAsync(git, Ask, CancellationToken.None);
        Assert.Equal(2, asked);
    }

    [Fact]
    public async Task An_answer_with_no_version_is_not_kept_so_the_next_act_asks_again()
    {
        var git = Path.Combine(_root, "silent.exe");
        File.WriteAllText(git, "a");
        var asked = 0;
        Task<string?> Ask(string file, CancellationToken ct)
        {
            asked++;
            return Task.FromResult<string?>(null);
        }

        Assert.Null(await GitVersions.OfAsync(git, Ask, CancellationToken.None));
        Assert.Null(await GitVersions.OfAsync(git, Ask, CancellationToken.None));
        Assert.Equal(2, asked);
        Assert.Null(await GitVersions.OfAsync(Path.Combine(_root, "gone.exe"), Ask, CancellationToken.None));
        Assert.Equal(2, asked);
    }

    [Theory]
    [InlineData("2.30.1", "2.31")]
    [InlineData("2.31.0", null)]
    [InlineData("2.56.0", null)]
    [InlineData(null, null)]
    public void Below_2_31_Daoriss_doors_cannot_stand_and_its_remote_acts_refuse_naming_both(string? version, string? floor)
    {
        var refusal = GitVersions.DoorsRefusal(version is null ? null : Version.Parse(version));
        if (floor is null)
        {
            Assert.Null(refusal);
            return;
        }

        Assert.Contains(version!, refusal, StringComparison.Ordinal);
        Assert.Contains(floor, refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2.31.8", "2.32")]
    [InlineData("2.30.1", "2.32")]
    [InlineData("2.32.0", null)]
    [InlineData(null, null)]
    public void Below_2_32_a_git_setting_refuses_naming_both(string? version, string? floor)
    {
        var refusal = GitVersions.SettingRefusal(version is null ? null : Version.Parse(version));
        if (floor is null)
        {
            Assert.Null(refusal);
            return;
        }

        Assert.Contains(version!, refusal, StringComparison.Ordinal);
        Assert.Contains(floor, refusal, StringComparison.Ordinal);
    }
}
