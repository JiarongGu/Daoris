using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A remote that cannot authenticate is said once and left alone (GITAUTH1a, the git-auth design §5.2 and §14 row 1), with this
/// machine's real git against a code host on the loopback that refuses every sign-in: Daoris's fetch of the line fails once, holds
/// origin, and connects no more across the moments of the minutes after, each answered by the hold's sentence; <i>Try again</i> and a
/// changed address each let the next fetch connect, once.
/// </summary>
/// <remarks>
/// <para>Every git here is kept from the person's configuration and home: no system file (where Git for Windows names Git Credential
/// Manager), an empty global file, a scratch home. So nothing reads a credential store, nothing starts GCM, and nothing reaches a real
/// host.</para>
/// <para>Alone: it sets the process's environment (the home <c>TreeRefs</c> resolves, and the git variables above), which every class
/// beside it would share.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class GitHoldsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-holds-process-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "home");

    private string Repo => Path.Combine(_root, "engine");

    public GitHoldsProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        var global = Path.Combine(_root, "global.gitconfig");
        File.WriteAllText(global, "");
        var scratch = Path.Combine(_root, "user");
        Directory.CreateDirectory(scratch);
        Set(DaorisHome.Variable, Home);
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", global);
        Set("HOME", scratch);
        Set("XDG_CONFIG_HOME", scratch);
    }

    private void Set(string name, string value)
    {
        _was[name] = Environment.GetEnvironmentVariable(name);
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

    private static async Task<(string? Failed, string? Url, int Fetches)> FetchAsync(string repo)
    {
        var fetches = 0;
        WorkingTree.Started.Value = arguments => { if (arguments.Contains("fetch")) Interlocked.Increment(ref fetches); };
        try
        {
            var (failed, url) = await TreeRefs.FetchAsync(repo, "main", CancellationToken.None, "engine", "work");
            return (failed, url, fetches);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    [Fact]
    public async Task A_refused_fetch_is_held_once_and_no_moment_after_it_connects_until_try_again()
    {
        await using var host = new RefusingHttpHost(_ => 401);
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", host.Address);

        var first = await FetchAsync(Repo);
        Assert.Equal(1, first.Fetches);
        var asked = host.Requests;
        Assert.True(asked > 0, "the fetch never reached the loopback host");
        Assert.StartsWith("`origin` is held: 127.0.0.1:", first.Failed, StringComparison.Ordinal);
        Assert.Contains("terminal prompts disabled", first.Failed, StringComparison.Ordinal);
        var held = Assert.Single(GitHolds.Read(Home));
        Assert.Equal(("refused", "all", "engine", "work"), (held.Class, held.Holds, held.Repository, held.Workspace));

        // The moments of the next minutes: each calls the fetch, and the hold answers it without a git or a connection.
        for (var moment = 0; moment < 3; moment++)
        {
            var later = await FetchAsync(Repo);
            Assert.Equal(0, later.Fetches);
            Assert.Equal(first.Failed, later.Failed);
        }

        Assert.Equal(asked, host.Requests);

        // Try again: the next fetch connects, once, and holds again.
        Assert.Equal(1, GitHolds.LiftRepository(Home, "engine", "work"));
        var again = await FetchAsync(Repo);
        Assert.Equal(1, again.Fetches);
        Assert.True(host.Requests > asked);
        Assert.Single(GitHolds.Read(Home));
    }

    [Fact]
    public async Task A_changed_address_is_tried_at_once()
    {
        await using var refusing = new RefusingHttpHost(_ => 401);
        await using var moved = new RefusingHttpHost(_ => 401);
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", refusing.Address);
        await FetchAsync(Repo);
        Assert.Single(GitHolds.Read(Home));

        await Git("remote", "set-url", "origin", moved.Address);
        var tried = await FetchAsync(Repo);

        Assert.Equal(1, tried.Fetches);
        Assert.True(moved.Requests > 0);
        Assert.Equal(GitWords.HostOf(moved.Address), Assert.Single(GitHolds.Read(Home)).Host);
    }

    /// <summary>An install laid out with the ssh door in its <c>app/bin/</c> (an empty file: no act here starts it), made the home.</summary>
    private string Install(string name)
    {
        var install = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(install, "data"));
        Directory.CreateDirectory(Path.Combine(install, "app", "bin"));
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(install, "app", "bin", GitWiring.SshDoor), "");
        var home = Path.Combine(install, "data");
        Environment.SetEnvironmentVariable(DaorisHome.Variable, home);
        return home;
    }

    [Fact]
    public async Task A_repositorys_own_ssh_command_where_the_door_is_named_by_GIT_SSH_is_held_as_own_input_until_it_goes()
    {
        var home = Install("with space");
        Assert.Equal("git-ssh", GitWiring.SshForm(home));
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@code.example.invalid:org/engine.git");
        await Git("config", "core.sshCommand", "ssh -i C:/keys/engine_key");

        var held = await FetchAsync(Repo);

        Assert.Equal(0, held.Fetches);
        Assert.StartsWith("`origin` is held: this repository names its own ssh command (`core.sshCommand`)", held.Failed, StringComparison.Ordinal);
        Assert.DoesNotContain("engine_key", held.Failed, StringComparison.Ordinal);
        var hold = Assert.Single(GitHolds.Read(home));
        Assert.Equal(("own-input", "all", "", "code.example.invalid"), (hold.Class, hold.Holds, hold.Words, hold.Host));
        Assert.DoesNotContain("engine_key", File.ReadAllText(DaorisHome.Entry(home, HomeEntry.GitHeld)), StringComparison.Ordinal);
        Assert.Equal(held.Failed, (await FetchAsync(Repo)).Failed);

        // The setting removed: the hold goes before the next act, which is then tried.
        await Git("config", "--unset", "core.sshCommand");
        Assert.Null(await GitOwnInput.HeldAsync(home, Repo, "engine", "work", "origin", "git@code.example.invalid:org/engine.git", null, CancellationToken.None));
        Assert.Empty(GitHolds.Read(home));
    }

    [Theory]
    [InlineData("bare", "git@code.example.invalid:org/engine.git")]
    [InlineData("with space", "https://code.example.invalid/org/engine.git")]
    public async Task Neither_a_door_named_by_its_bare_path_nor_an_HTTPS_remote_is_held_for_a_repositorys_ssh_command(string install, string address)
    {
        var home = Install(install);
        await Git("init", "-q", "-b", "main");
        await Git("config", "core.sshCommand", "ssh -i C:/keys/engine_key");

        Assert.False(await GitOwnInput.FindsAsync(home, Repo, address, CancellationToken.None));
        Assert.Null(await GitOwnInput.HeldAsync(home, Repo, "engine", "work", "origin", address, null, CancellationToken.None));
        Assert.Empty(GitHolds.Read(home));
    }

    [Fact]
    public async Task An_unreachable_host_holds_nothing()
    {
        int port;
        using (var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }

        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", $"http://127.0.0.1:{port}/org/engine.git");

        var first = await FetchAsync(Repo);

        Assert.NotNull(first.Failed);
        Assert.Equal(GitFailureClass.Unreachable, GitFailures.Class(first.Failed, GitAct.Read));
        Assert.Empty(GitHolds.Read(Home));
        Assert.Equal(1, (await FetchAsync(Repo)).Fetches);
    }
}
