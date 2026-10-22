using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A remote that cannot authenticate is held until something changes (GITAUTH1a, D198 point 7; the git-auth design §5.2): the classes
/// that hold and those that do not, the key (the checkout, the remote, its address, the git, reads or writes), what lifts a hold, what
/// is kept in <c>run/git/held.json</c> and what the machine log says. Files only; the real fetch is <see cref="GitHoldsProcessTests"/>.
/// </summary>
public sealed class GitHoldsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-git-holds-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private string Root => Path.Combine(_home, "engine");

    private const string Address = "git@ssh.dev.azure.com:v3/org/project/engine";

    public GitHoldsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private GitHold? Hold(GitFailureClass failure, string address = Address, string git = "git-a", string words = "git@ssh.dev.azure.com: Permission denied (password,publickey).\nfatal: Could not read from remote repository.") =>
        GitHolds.Hold(_home, Root, "engine", "work", "origin", address, git, failure, words, Now);

    private JsonElement[] Lines(string @event)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl")
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    [Theory]
    [InlineData(GitFailureClass.Refused, "all")]
    [InlineData(GitFailureClass.HostUnknown, "all")]
    [InlineData(GitFailureClass.HostChanged, "all")]
    [InlineData(GitFailureClass.WriteRefused, "writes")]
    [InlineData(GitFailureClass.Unreachable, null)]
    [InlineData(GitFailureClass.Unclassed, null)]
    public void A_class_holds_what_the_design_says_and_the_networks_and_the_unclassed_hold_nothing(GitFailureClass failure, string? holds)
    {
        var hold = Hold(failure);

        Assert.Equal(holds, hold?.Holds);
        Assert.Equal(holds is null ? 0 : 1, GitHolds.Read(_home).Count);
    }

    [Fact]
    public void A_held_remote_answers_every_read_and_write_at_its_key_and_is_kept_for_a_restart()
    {
        Hold(GitFailureClass.Refused);

        Assert.NotNull(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Read));
        Assert.NotNull(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Write));
        Assert.True(File.Exists(DaorisHome.Entry(_home, HomeEntry.GitHeld)));
        var kept = Assert.Single(GitHolds.Read(_home));
        Assert.Equal(("engine", "work", "ssh.dev.azure.com", "ambient", "refused"), (kept.Repository, kept.Workspace, kept.Host, kept.Served, kept.Class));
        Assert.Equal("git@ssh.dev.azure.com: Permission denied (password,publickey).", kept.Words);
        Assert.Null(GitHolds.Holding(_home, Root, "upstream", Address, "git-a", GitAct.Read));
    }

    [Fact]
    public void A_push_refused_holds_writes_alone()
    {
        Hold(GitFailureClass.WriteRefused);

        Assert.Null(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Read));
        Assert.NotNull(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Write));
    }

    [Theory]
    [InlineData("a changed address", "git@ssh.dev.azure.com:v3/org/project/engine2", "git-a", "address")]
    [InlineData("a changed git", Address, "git-b", "git")]
    public void A_changed_address_or_git_is_another_key_so_it_is_tried_and_the_hold_lifted(string name, string address, string git, string why)
    {
        Hold(GitFailureClass.Refused);

        Assert.True(GitHolds.Holding(_home, Root, "origin", address, git, GitAct.Read) is null, name);
        Assert.Empty(GitHolds.Read(_home));
        Assert.Equal(why, Assert.Single(Lines("git.lifted")).GetProperty("why").GetString());
    }

    [Fact]
    public void Try_again_lifts_every_hold_on_the_repository_and_says_so_once()
    {
        Hold(GitFailureClass.Refused);

        Assert.Equal(1, GitHolds.LiftRepository(_home, "engine", "work"));
        Assert.Null(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Read));
        Assert.Equal(0, GitHolds.LiftRepository(_home, "engine", "work"));
        Assert.Equal("try-again", Assert.Single(Lines("git.lifted")).GetProperty("why").GetString());
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.GitHeld)));
    }

    [Fact]
    public void A_fetch_that_passes_lifts_a_hold_over_reads_and_never_one_over_writes()
    {
        Hold(GitFailureClass.WriteRefused);
        GitHolds.Passed(_home, Root, "origin", GitAct.Read);
        Assert.Single(GitHolds.Read(_home));

        GitHolds.Passed(_home, Root, "origin", GitAct.Write);
        Assert.Empty(GitHolds.Read(_home));
    }

    [Fact]
    public void Held_again_at_its_key_keeps_its_first_time_and_logs_once()
    {
        Hold(GitFailureClass.Refused);
        GitHolds.Hold(_home, Root, "engine", "work", "origin", Address, "git-a", GitFailureClass.Refused, "again", Now.AddMinutes(5));

        Assert.Equal(Now, Assert.Single(GitHolds.Read(_home)).Since);
        var held = Assert.Single(Lines("git.held"));
        Assert.Equal("refused", held.GetProperty("class").GetString());
        Assert.Equal("engine", held.GetProperty("repository").GetString());
        Assert.DoesNotContain(Root, held.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_address_is_kept_without_its_user_info()
    {
        Hold(GitFailureClass.Refused, address: "https://x-access-token:s3cretvalue@github.com/org/engine.git");

        var kept = Assert.Single(GitHolds.Read(_home));
        Assert.Equal("https://github.com/org/engine.git", kept.Address);
        Assert.Equal("github.com", kept.Host);
        Assert.DoesNotContain("s3cret", File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.GitHeld)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_held_remote_is_said_as_its_row_names_it_never_as_a_retry()
    {
        var said = GitHolds.Said(Hold(GitFailureClass.Refused)!);

        Assert.Equal("`origin` is held: ssh.dev.azure.com refused the sign-in git has here (git@ssh.dev.azure.com: Permission denied "
            + "(password,publickey).). Daoris does not try it again until you press Try again on its row in What needs you, or its address "
            + "or the git Daoris runs changes", said);
    }

    [Fact]
    public void An_unreadable_file_holds_nothing()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.GitHeld);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ not json");

        Assert.Empty(GitHolds.Read(_home));
        Assert.Null(GitHolds.Holding(_home, Root, "origin", Address, "git-a", GitAct.Read));
    }
}
