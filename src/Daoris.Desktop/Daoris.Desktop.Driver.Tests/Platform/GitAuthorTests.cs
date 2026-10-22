using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// §3.6's author rule (GITAUTH1c, D198 point 5): a commit carries origin's identity's <c>commitsAs</c> only where the repository's own
/// configuration names no author; a repository that names one keeps it; and the own-input hold gives way to the person's <c>own</c>.
/// Against a real git in a scratch repository, its system and global files kept from the person's.
/// </summary>
[Collection(Alone.Name)]
public sealed class GitAuthorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-author-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    public GitAuthorTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var empty = Path.Combine(_root, "global");
        File.WriteAllText(empty, "");
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", empty);
    }

    private void Set(string name, string value)
    {
        if (!_was.ContainsKey(name)) _was[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task Git(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(Repo, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private async Task WithIdentityAsync()
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@github.com:org/engine.git");
        GitFolders.Remember(Home, [(Repo, "engine", "games")]);
        GitIdentities.Change(Home, _ =>
        [
            new GitIdentity("gi-00000000000a", "Work", "github.com", GitIdentity.Ssh, null, "Work Name", "work@example.invalid", "ed25519",
                "SHA256:x", false, null, Default: true),
        ]);
    }

    [Fact]
    public async Task A_repository_naming_no_author_commits_as_origins_identity()
    {
        await WithIdentityAsync();

        Assert.Equal(("Work Name", "work@example.invalid"), await GitAuthor.OfAsync(Home, Repo, CancellationToken.None));
        Assert.Equal(["-c", "user.name=Work Name", "-c", "user.email=work@example.invalid"], GitAuthor.Arguments(("Work Name", "work@example.invalid")));
        var info = new ProcessStartInfo("x");
        GitAuthor.Hand(info.Environment, ("Work Name", "work@example.invalid"));
        Assert.Equal(("user.name", "Work Name"), (info.Environment["GIT_CONFIG_KEY_0"], info.Environment["GIT_CONFIG_VALUE_0"]));
    }

    [Fact]
    public async Task A_repository_that_names_its_author_keeps_it()
    {
        await WithIdentityAsync();
        await Git("config", "user.email", "own@example.invalid");

        Assert.Null(await GitAuthor.OfAsync(Home, Repo, CancellationToken.None));
    }

    [Fact]
    public async Task Own_or_no_identity_leaves_gits_own_answer()
    {
        await WithIdentityAsync();
        GitIdentityActs.UseMachine(Home, "github.com", own: true);

        Assert.Null(await GitAuthor.OfAsync(Home, Repo, CancellationToken.None));
        Assert.True(GitOwnInput.ChoseOwn(Home, "engine", "games", "origin", "git@github.com:org/engine.git"));
        Assert.False(GitOwnInput.ChoseOwn(Home, "engine", "games", "origin", "git@gitlab.com:org/engine.git"));
        Assert.Null(await GitAuthor.OfAsync(null, Repo, CancellationToken.None));
    }

    [Theory]
    [InlineData("local\tuser.name A\n", true)]
    [InlineData("worktree\tuser.email a@b\n", true)]
    [InlineData("global\tuser.name A\nsystem\tuser.email a@b\n", false)]
    [InlineData("", false)]
    public void Only_the_repositorys_own_scopes_name_its_author(string scoped, bool names)
    {
        Assert.Equal(names, GitAuthor.Names(scoped));
    }
}
