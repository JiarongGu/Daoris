using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D241 point 6's proof (the git design §4.1's HTTPS row), with this machine's real git and Daoris's built doors laid out as an
/// install's, against <see cref="GitTlsHost"/> asking HTTP Basic, and with nothing found beside git: no system file, an empty global
/// file, a scratch home, no credential manager and no store. An HTTPS identity's token, kept in <c>profiles/git/tokens/&lt;id&gt;/</c>,
/// fetches and pushes through the remote helper and the credential door; the credential door answers the identity's own host and no
/// other; a <c>_netrc</c> in the account's home is not read under an identity; an address carrying a password is refused; a removed
/// identity leaves no token and the next fetch is refused. No fact is skipped.
/// </summary>
/// <remarks>
/// <para>Every token is a throwaway value of a throwaway user, kept in this test's own home under <c>_fixtures/</c>.</para>
/// <para>In the Process half, beside a listener.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public class GitHttpsProcessTests : IClassFixture<GitTlsHost>, IDisposable
{
    /// <summary>Whether this run's git is the MinGit the toolchain pins (<see cref="PinnedMinGit"/>) rather than the runner's.</summary>
    protected virtual bool Pinned => false;

    private bool _pinnedLaid;

    private const string User = "fixture-user";
    private const string Token = "fixture-token-not-a-secret";

    private readonly GitTlsHost _host;
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-https-process", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>This test's own repository on the shared host.</summary>
    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Work => Path.Combine(_root, "work");

    private string Scratch => Path.Combine(_root, "scratch-home");

    private string Address => _host.Address(_path);

    private string Host => GitHttps.HostOf(Address)!;

    public GitHttpsProcessTests(GitTlsHost host)
    {
        _host = host;
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Scratch);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public void Dispose()
    {
        _host.Basic = null;
        GitSeed.Discard(_root);
        GitSeed.Discard(Path.GetDirectoryName(Bare)!);
    }

    private ProcessStartInfo Git(string folder, params string[] arguments)
    {
        if (Pinned && !_pinnedLaid)
        {
            PinnedMinGit.Lay(Home);
            _pinnedLaid = true;
        }

        var (info, refusal) = WorkingTree.GitStart(folder, arguments, Home);
        Assert.Null(refusal);
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = Scratch;
        info.Environment["USERPROFILE"] = Scratch;
        info.Environment["XDG_CONFIG_HOME"] = Scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return _host.TrustedBy(TestGit.Ceiling(info));
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        return (process.ExitCode, await stdout, await stderr);
    }

    private async Task<string> Ok(ProcessStartInfo info)
    {
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    /// <summary>The bare repository with one commit, the host asking Basic, and the doors laid.</summary>
    private async Task ServeAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Git(Bare, "init", "--bare", "--initial-branch=main"));
        await Ok(Git(Bare, "config", "http.receivepack", "true"));
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(Git(seed, "init", "--initial-branch=main"));
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(Git(seed, "add", "a.txt"));
        await Ok(Git(seed, "commit", "-m", "first"));
        await Ok(Git(seed, "push", Bare, "main"));
        GitDoorsLaid.Lay(_root);
        _host.Basic = (User, Token);
    }

    private GitIdentity Signed(string token = Token) =>
        GitIdentityHttps.AddHttps(Home, Host, "Fixture host", User, null, null, token);

    [Fact]
    public async Task An_identitys_token_fetches_and_pushes_over_TLS_with_nothing_found_beside_git()
    {
        await ServeAsync();
        var identity = Signed();

        await Ok(Git(_root, "clone", Address, Work));
        Assert.True(File.Exists(Path.Combine(Work, "a.txt")));
        Assert.Contains(User, _host.Users);
        Assert.All(_host.Users, user => Assert.Contains(user, new[] { User, "-" }));
        await Ok(Git(Work, "fetch", "origin"));

        File.AppendAllText(Path.Combine(Work, "a.txt"), "pushed\n");
        await Ok(Git(Work, "commit", "-am", "pushed"));
        var push = Git(Work, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(
            Home, Address, [new GitGrantRef("refs/heads/main", await Ok(Git(Work, "rev-parse", "HEAD")))], DateTimeOffset.UtcNow);
        await Ok(push);

        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await Ok(Git(Bare, "rev-parse", "refs/heads/main")));
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(Home, HomeEntry.GitUsed), identity.Id)), "the identity's use is touched");
        Assert.DoesNotContain(Token, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.GitIdentities)), StringComparison.Ordinal);
        Assert.True(GitPrivateFolder.IsPrivate(GitTokens.Folder(Home, identity.Id)));
    }

    [Fact]
    public async Task A_wrong_token_is_refused_by_the_host_and_nothing_is_cloned()
    {
        await ServeAsync();
        Signed("another-throwaway-token");

        var (exit, _, error) = await RunAsync(Git(_root, "clone", Address, Work));

        Assert.NotEqual(0, exit);
        Assert.True(error.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) || error.Contains("401", StringComparison.Ordinal), error);
        Assert.False(File.Exists(Path.Combine(Work, "a.txt")));
    }

    /// <summary>The built credential door, started as git starts it: the identity's token for its own host and nothing for any other.</summary>
    [Fact]
    public async Task The_credential_door_answers_the_identitys_own_host_and_any_other_host_gets_nothing()
    {
        GitDoorsLaid.Lay(_root);
        var identity = Signed();
        var door = Path.Combine(Tools.InstallBin(Home)!, GitCredentialDoor.Exe);

        async Task<(int Exit, string Out)> AskAsync(string host, string? asIdentity = null)
        {
            var info = new ProcessStartInfo(door, "get") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            info.Environment[GitRouting.HomeVariable] = Home;
            info.Environment[GitRouting.IdentityVariable] = asIdentity ?? identity.Id;
            using var process = Process.Start(info)!;
            await process.StandardInput.WriteAsync($"protocol=https\nhost={host}\n\n");
            process.StandardInput.Close();
            var said = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
            return (process.ExitCode, said);
        }

        var own = await AskAsync(Host);
        Assert.Equal(0, own.Exit);
        Assert.Equal($"username={User}\npassword={Token}\n", own.Out.Replace("\r\n", "\n"));

        foreach (var other in new[] { "github.com", "127.0.0.1:1", "evil.127.0.0.1", Host + ".evil.example" })
        {
            var refused = await AskAsync(other);
            Assert.Equal(0, refused.Exit);
            Assert.Equal("", refused.Out);
        }

        Assert.Equal("", (await AskAsync(Host, asIdentity: "gi-00000000000f")).Out);
    }

    [Fact]
    public async Task A_netrc_in_the_accounts_home_is_not_read_under_an_identity_and_is_read_without_one()
    {
        await ServeAsync();
        var netrc = $"machine 127.0.0.1\nlogin {User}\npassword {Token}\n";
        File.WriteAllText(Path.Combine(Scratch, "_netrc"), netrc);
        File.WriteAllText(Path.Combine(Scratch, ".netrc"), netrc);

        // The person's own sign-ins chosen for the host: what git would have run, which reads curl's netrc.
        var config = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$$"""{"machineGitIdentity":{"{{{Host}}}":"own"}}""");
        await Ok(Git(_root, "clone", Address, Path.Combine(_root, "ambient")));
        Assert.Contains(User, _host.Users);

        // An identity serves, with no token kept for it: the netrc must not be read, so the host refuses.
        File.WriteAllText(config, "{}");
        GitIdentities.Change(Home, all => [.. all, new GitIdentity(GitIdentities.NewId(), "Unsigned", Host, GitIdentity.Https, User, null, null, null, null, false, null, true)]);
        var asked = _host.Users.Count;
        var (exit, _, error) = await RunAsync(Git(_root, "clone", Address, Work));

        Assert.NotEqual(0, exit);
        Assert.True(
            error.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase) || error.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase), error);
        Assert.All(_host.Users.Skip(asked), user => Assert.Equal("-", user));
    }

    [Fact]
    public async Task An_address_carrying_a_password_is_refused_with_one_line_before_the_host_hears_it()
    {
        await ServeAsync();
        Signed();
        var asked = _host.Requests.Count;
        var address = Address.Replace("https://", $"https://{User}:{Token}@", StringComparison.Ordinal);

        var (exit, _, error) = await RunAsync(Git(_root, "clone", address, Work));

        Assert.NotEqual(0, exit);
        Assert.Contains("refuses an address carrying a password", error, StringComparison.Ordinal);
        Assert.Equal(asked, _host.Requests.Count);
    }

    [Fact]
    public async Task Removing_an_identity_deletes_its_token_so_the_next_fetch_is_refused()
    {
        await ServeAsync();
        var identity = Signed();
        await Ok(Git(_root, "clone", Address, Work));

        GitIdentityActs.Remove(Home, identity.Id);
        Assert.False(Directory.Exists(GitTokens.Folder(Home, identity.Id)));
        // The identity is gone: a second identity with no token stands in for "nothing left".
        GitIdentities.Change(Home, all => [.. all, new GitIdentity(GitIdentities.NewId(), "Again", Host, GitIdentity.Https, User, null, null, null, null, false, null, true)]);
        var asked = _host.Users.Count;
        var (exit, _, _) = await RunAsync(Git(Work, "fetch", "origin"));

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain(User, _host.Users.Skip(asked));
    }

    /// <summary>A test reads over plain HTTP: git is not handed the TLS host's authority there.</summary>
    [Fact]
    public async Task A_test_reads_through_the_identitys_token_and_records_it()
    {
        await ServeAsync();
        GitDoorsLaid.Lay(_root);
        await using var plain = new GitHttpBackendHost(GitHttpsBackend.Path(), _host.Projects, (User, Token));
        var host = GitHttps.HostOf(plain.Address(_path))!;
        var identity = GitIdentityHttps.AddHttps(Home, host, "Plain fixture", User, null, null, Token);

        var answer = await GitIdentityHttps.TestHttpsAsync(Home, identity.Id, plain.Address(_path), CancellationToken.None);

        Assert.Equal("reached", answer.Result);
        Assert.Equal("reached", GitIdentities.Find(Home, identity.Id)!.Test!.Result);
        Assert.Contains(User, plain.Users);
    }
}

/// <summary>This machine's git's own <c>git-http-backend</c>, found as <see cref="GitTlsHost"/> finds it.</summary>
internal static class GitHttpsBackend
{
    public static string Path()
    {
        var info = new ProcessStartInfo("git", "--exec-path") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(info)!;
        var folder = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return System.IO.Path.Combine(folder, OperatingSystem.IsWindows() ? "git-http-backend.exe" : "git-http-backend");
    }
}
