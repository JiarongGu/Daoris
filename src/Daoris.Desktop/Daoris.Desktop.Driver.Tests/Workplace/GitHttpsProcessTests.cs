using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAUTH1d's proof (the git-auth design §14 row 4), with this machine's real git, its real Git Credential Manager and Daoris's built
/// doors laid out as an install's: an HTTPS identity's sign-in stored through GCM in Daoris's own namespace and handed to git by GCM
/// alone, a clone through the remote helper against a local host that asks HTTP Basic, a <c>_netrc</c> in the account's home not
/// read under an identity, an address carrying a password refused, and a removal that erases the stored sign-in.
/// </summary>
/// <remarks>
/// <para>The host is a loopback one (<see cref="GitHttpBackendHost"/>), so the credential is keyed <c>daoris:http://127.0.0.1:&lt;port&gt;</c>:
/// a test token of a throwaway user, in Daoris's namespace alone, erased at the end of each test. Nothing of the person's own store is
/// read or written, and nothing is signed in to. Where no Git Credential Manager is found each fact is skipped, saying so.</para>
/// <para>In the Process half, beside a listener.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitHttpsProcessTests : IDisposable
{
    private const string User = "fixture-user";
    private const string Token = "fixture-token-not-a-secret";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-https-process-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<(string Host, string User)> _stored = [];

    private string Home => Path.Combine(_root, "data");

    private string Projects => Path.Combine(_root, "srv");

    private string Bare => Path.Combine(Projects, "org", "engine.git");

    private string Work => Path.Combine(_root, "work");

    private string Scratch => Path.Combine(_root, "scratch-home");

    public GitHttpsProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Scratch);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public void Dispose()
    {
        // Whatever a test left in the store, erased: the namespace is Daoris's, the user a throwaway one.
        foreach (var (host, user) in _stored)
        {
            try { GitIdentityHttps.EraseAsync(Home, "http", host, user, CancellationToken.None).GetAwaiter().GetResult(); } catch (DriverException) { }
        }

        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void LayDoors()
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.GitDoor", "bin", build.Parent.Name, build.Name);
        Assert.True(File.Exists(Path.Combine(built, GitWiring.SshDoor)), "the doors are built before these tests: " + built);
        var bin = Path.Combine([_root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)), overwrite: true);
        File.Copy(Path.Combine(built, GitWiring.SshDoor), Path.Combine(bin, GitWiring.RemoteHelper), overwrite: true);
    }

    private ProcessStartInfo Git(string folder, params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, Home);
        Assert.Null(refusal);
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = Scratch;
        info.Environment["USERPROFILE"] = Scratch;
        info.Environment["XDG_CONFIG_HOME"] = Scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return info;
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(TestGit.Ceiling(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private async Task<string> Ok(string folder, params string[] arguments)
    {
        var info = Git(folder, arguments);
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    private async Task ServeAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Bare, "init", "--bare", "--initial-branch=main");
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(seed, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(seed, "add", "a.txt");
        await Ok(seed, "commit", "-m", "first");
        await Ok(seed, "push", Bare, "main");
    }

    private static string Backend()
    {
        var (exit, output, _) = RunAsync(new ProcessStartInfo("git", "--exec-path") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true })
            .GetAwaiter().GetResult();
        Assert.Equal(0, exit);
        return Path.Combine(output.Trim(), "git-http-backend.exe");
    }

    private static string HostOf(GitHttpBackendHost host) => GitHttps.HostOf(host.Address("org/engine.git"))!;

    /// <summary>An HTTPS identity for the host with its token stored through GCM (keyed by <c>http</c>, as this loopback host is reached).</summary>
    private async Task<GitIdentity> SignedInAsync(string host)
    {
        _stored.Add((host, User));
        return await GitIdentityHttps.AddHttpsAsync(Home, host, "Fixture host", User, null, null, Token, "http", CancellationToken.None);
    }

    [GcmFact]
    public async Task An_identitys_stored_token_is_handed_to_git_by_GCM_and_the_host_sees_the_user_and_no_other()
    {
        await ServeAsync();
        LayDoors();
        await using var host = new GitHttpBackendHost(Backend(), Projects, (User, Token));
        var identity = await SignedInAsync(HostOf(host));

        await Ok(_root, "clone", host.Address("org/engine.git"), Work);

        Assert.True(File.Exists(Path.Combine(Work, "a.txt")));
        Assert.Contains(User, host.Users);
        Assert.All(host.Users, user => Assert.Contains(user, new[] { User, "-" }));
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(Home, HomeEntry.GitUsed), identity.Id)), "the identity's use is touched");
        Assert.DoesNotContain(Token, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.GitIdentities)), StringComparison.Ordinal);
    }

    [GcmFact]
    public async Task A_netrc_in_the_accounts_home_is_not_read_under_an_identity_and_is_read_without_one()
    {
        await ServeAsync();
        LayDoors();
        await using var host = new GitHttpBackendHost(Backend(), Projects, (User, Token));
        var address = host.Address("org/engine.git");
        var netrc = $"machine 127.0.0.1\nlogin {User}\npassword {Token}\n";
        File.WriteAllText(Path.Combine(Scratch, "_netrc"), netrc);
        File.WriteAllText(Path.Combine(Scratch, ".netrc"), netrc);

        // Nothing serves: today's ambient credentials, which include curl's netrc.
        await Ok(_root, "clone", address, Path.Combine(_root, "ambient"));
        Assert.Contains(User, host.Users);

        // An identity serves, with no token stored for it: the netrc must not be read, so the host refuses.
        GitIdentities.Change(Home, all => [.. all, new GitIdentity(GitIdentities.NewId(), "Unsigned", HostOf(host), GitIdentity.Https, User, null, null, null, null, false, null, true)]);
        var asked = host.Users.Count;
        var (exit, _, error) = await RunAsync(Git(_root, "clone", address, Work));

        Assert.NotEqual(0, exit);
        // GCM's own refusal to prompt, or git's: the sign-in is missing and nothing asks for it.
        Assert.True(
            error.Contains("Cannot prompt", StringComparison.OrdinalIgnoreCase) || error.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase), error);
        Assert.All(host.Users.Skip(asked), user => Assert.Equal("-", user));
    }

    [GcmFact]
    public async Task An_address_carrying_a_password_is_refused_with_one_line_before_the_host_hears_it()
    {
        await ServeAsync();
        LayDoors();
        await using var host = new GitHttpBackendHost(Backend(), Projects, (User, Token));
        await SignedInAsync(HostOf(host));
        var asked = host.Requests.Count;
        var address = host.Address("org/engine.git").Replace("http://", $"http://{User}:{Token}@", StringComparison.Ordinal);

        var (exit, _, error) = await RunAsync(Git(_root, "clone", address, Work));

        Assert.NotEqual(0, exit);
        Assert.Contains("refuses an address carrying a password", error, StringComparison.Ordinal);
        Assert.Equal(asked, host.Requests.Count);
    }

    [GcmFact]
    public async Task Removing_an_identity_erases_its_stored_sign_in_so_the_next_fetch_is_refused()
    {
        await ServeAsync();
        LayDoors();
        await using var host = new GitHttpBackendHost(Backend(), Projects, (User, Token));
        var identity = await SignedInAsync(HostOf(host));
        await Ok(_root, "clone", host.Address("org/engine.git"), Work);

        await GitIdentityHttps.RemoveAsync(Home, identity.Id, CancellationToken.None);
        // The identity is gone: its host is served by nothing, so a second identity with no token stands in for "no sign-in left".
        GitIdentities.Change(Home, all => [.. all, new GitIdentity(GitIdentities.NewId(), "Again", HostOf(host), GitIdentity.Https, User, null, null, null, null, false, null, true)]);
        var asked = host.Users.Count;
        var (exit, _, _) = await RunAsync(Git(Work, "fetch", "origin"));

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain(User, host.Users.Skip(asked));
    }

    [GcmFact]
    public async Task A_test_reads_through_the_identitys_sign_in_and_records_it()
    {
        await ServeAsync();
        await using var host = new GitHttpBackendHost(Backend(), Projects, (User, Token));
        var identity = await SignedInAsync(HostOf(host));

        var answer = await GitIdentityHttps.TestHttpsAsync(Home, identity.Id, host.Address("org/engine.git"), CancellationToken.None);

        Assert.Equal("reached", answer.Result);
        Assert.Equal("reached", GitIdentities.Find(Home, identity.Id)!.Test!.Result);
        Assert.Contains(User, host.Users);
    }
}

/// <summary>A fact that runs where a Git Credential Manager is found beside this machine's git, and is skipped saying so elsewhere.</summary>
public sealed class GcmFactAttribute : FactAttribute
{
    public GcmFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || GitHttps.Manager(Environment.GetEnvironmentVariable) is null)
            Skip = "no Git Credential Manager is found beside this machine's git, so the sign-in cannot be stored";
    }
}
