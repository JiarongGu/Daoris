using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAUTH1b's proof (the git-auth design §14 row 2), with this machine's real git and Daoris's built doors laid out as an install's:
/// a fetch and a push through the remote helper against an HTTP host over <c>git http-backend</c>, and through the ssh door against a
/// stand-in ssh that runs the remote command on this machine; a session's push refused at each door with one line, and the landing's
/// grant letting one push through, once.
/// </summary>
/// <remarks>
/// <para>Each git is started as Daoris starts its own (<c>WorkingTree.GitStart</c> with the home), then kept from every configuration
/// of the person's: no system file, an empty global file and a scratch home. No real host, key, agent or credential store is touched.</para>
/// <para><b>An sshd</b> is used only where <c>DAORIS_TEST_SSHD</c> names one (<see cref="GitSshHoldsProcessTests"/>); the stand-in
/// proves the door's judgement, its argument contract and <c>BatchMode</c> given first, not OpenSSH's own transport.</para>
/// <para>In the Process half, beside a listener.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitDoorsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-doors-process-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Projects => Path.Combine(_root, "srv");

    private string Bare => Path.Combine(Projects, "org", "engine.git");

    private string Work => Path.Combine(_root, "work");

    public GitDoorsProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(_root, "scratch-home"));
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The doors' build beside this test's own, laid out as the install's <c>app/bin/</c>: one program, two names.</summary>
    private void LayDoors(string install)
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.GitDoor", "bin", build.Parent.Name, build.Name);
        Assert.True(File.Exists(Path.Combine(built, GitWiring.SshDoor)), "the doors are built before these tests: " + built);
        var bin = Path.Combine([install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)), overwrite: true);
        File.Copy(Path.Combine(built, GitWiring.SshDoor), Path.Combine(bin, GitWiring.RemoteHelper), overwrite: true);
    }

    /// <summary>A git started as Daoris starts its own, for <paramref name="home"/>, kept from the person's configuration and home.</summary>
    private ProcessStartInfo Git(string folder, string home, params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, home);
        Assert.Null(refusal);
        var scratch = Path.Combine(_root, "scratch-home");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = scratch;
        info.Environment["USERPROFILE"] = scratch;
        info.Environment["XDG_CONFIG_HOME"] = scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return info;
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private async Task<string> Ok(string folder, params string[] arguments) => await Ok(Git(folder, Home, arguments));

    private static async Task<string> Ok(ProcessStartInfo info)
    {
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    /// <summary>A served bare repository with one commit on <c>main</c>.</summary>
    private async Task ServeAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Bare, "init", "--bare", "--initial-branch=main");
        await Ok(Bare, "config", "http.receivepack", "true");
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(seed, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(seed, "add", "a.txt");
        await Ok(seed, "commit", "-m", "first");
        // Laid in on the disk, with no door in the way: the doors are not installed yet.
        await Ok(Git(seed, Path.Combine(_root, "no-install"), "push", Bare, "main"));
    }

    private async Task<string> CommitAsync(string message)
    {
        File.AppendAllText(Path.Combine(Work, "a.txt"), message + "\n");
        await Ok(Work, "commit", "-am", message);
        return await Ok(Work, "rev-parse", "HEAD");
    }

    private Task<string> TipAsync() => Ok(Bare, "rev-parse", "refs/heads/main");

    private static string Backend()
    {
        var (exit, output, _) = RunAsync(new ProcessStartInfo("git", "--exec-path") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })
            .GetAwaiter().GetResult();
        Assert.Equal(0, exit);
        return Path.Combine(output.Trim(), "git-http-backend.exe");
    }

    // ── HTTP, through the remote helper ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Over_HTTP_a_fetch_passes_the_helper_and_a_sessions_push_is_refused_at_it_before_the_host_hears_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        var address = host.Address("org/engine.git");

        await Ok(_root, "clone", address, Work);
        Assert.Equal(GitRouting.Prefix + address, await Ok(Work, "remote", "get-url", "origin"));
        var before = await TipAsync();
        await CommitAsync("second");
        var asked = host.Requests.Count;

        var (exit, _, error) = await RunAsync(Git(Work, Home, "push", "origin", "main"));

        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.DoesNotContain(host.Requests.Skip(asked), line => line.Contains("receive-pack", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Over_HTTP_the_landings_grant_lets_one_push_through_and_not_a_second()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        var address = host.Address("org/engine.git");
        await Ok(_root, "clone", address, Work);
        var grant = GitGrants.Write(Home, address, DateTimeOffset.UtcNow);

        var tip = await CommitAsync("landed");
        var push = Git(Work, Home, "push", "origin", "HEAD:refs/heads/main");
        push.Environment[GitRouting.GrantVariable] = grant;
        await Ok(push);
        Assert.Equal(tip, await TipAsync());

        await CommitAsync("again");
        var again = Git(Work, Home, "push", "origin", "HEAD:refs/heads/main");
        again.Environment[GitRouting.GrantVariable] = grant;
        var (exit, _, error) = await RunAsync(again);
        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        Assert.Equal(tip, await TipAsync());
    }

    [Fact]
    public async Task Every_push_form_a_rule_cannot_name_meets_the_same_door()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        await Ok(_root, "clone", host.Address("org/engine.git"), Work);
        await CommitAsync("forms");
        var before = await TipAsync();

        string[][] forms =
        [
            ["-C", ".", "push", "origin", "main"],
            ["-c", "push.default=current", "push"],
            ["-c", "alias.ship=push", "ship", "origin", "main"],
            ["push", "--no-verify", "origin", "main"],
            ["push", "--force", "origin", "main"],
        ];
        foreach (var form in forms)
        {
            var (exit, _, error) = await RunAsync(Git(Work, Home, form));
            Assert.True(exit != 0, string.Join(' ', form));
            Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        }

        Assert.Equal(before, await TipAsync());
    }

    [Fact]
    public async Task Where_the_person_switched_no_push_off_a_push_passes_the_door_as_it_did()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Permissions), """{"defaultsOff":["no-push"]}""");
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        await Ok(_root, "clone", host.Address("org/engine.git"), Work);
        var tip = await CommitAsync("pushed");

        await Ok(Work, "push", "origin", "main");

        Assert.Equal(tip, await TipAsync());
    }

    [Fact]
    public async Task A_submodules_recursive_fetch_passes_the_helper()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        var outer = Path.Combine(Projects, "org", "outer.git");
        Directory.CreateDirectory(outer);
        await Ok(Git(outer, Path.Combine(_root, "no-install"), "init", "--bare", "--initial-branch=main"));
        await Ok(Git(outer, Path.Combine(_root, "no-install"), "config", "http.receivepack", "true"));
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        var shaping = Path.Combine(_root, "shaping");
        await Ok(_root, "clone", host.Address("org/outer.git"), shaping);
        await Ok(Git(shaping, Home, "-c", "protocol.file.allow=never", "submodule", "add", host.Address("org/engine.git"), "engine"));
        await Ok(shaping, "commit", "-m", "with engine");
        var grant = GitGrants.Write(Home, host.Address("org/outer.git"), DateTimeOffset.UtcNow);
        var push = Git(shaping, Home, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = grant;
        await Ok(push);

        await Ok(_root, "clone", "--recurse-submodules", host.Address("org/outer.git"), Work);

        Assert.True(File.Exists(Path.Combine(Work, "engine", "a.txt")));
        Assert.Contains(host.Requests, line => line.Contains("/org/engine.git/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_repositorys_own_hooks_still_run_through_the_doors()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        await Ok(_root, "clone", host.Address("org/engine.git"), Work);
        var marker = Path.Combine(_root, "hooked").Replace('\\', '/');
        File.WriteAllText(Path.Combine(Work, ".git", "hooks", "pre-push"), $"#!/bin/sh\necho ran > '{marker}'\n");
        var tip = await CommitAsync("hooked");
        // A push the door allows: git runs pre-push after the remote's refs are listed, which a refused push never reaches.
        var push = Git(Work, Home, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(Home, host.Address("org/engine.git"), DateTimeOffset.UtcNow);

        await Ok(push);

        Assert.True(File.Exists(marker), "the repository's own pre-push hook did not run");
        Assert.Equal(tip, await TipAsync());
    }

    // ── SSH, through the ssh door ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A stand-in ssh for the repository's own <c>core.sshCommand</c>: it writes the words it was handed, then runs the remote command
    /// here, as a host would. Named by a quoted path, so git and the door run it through <c>sh</c>.
    /// </summary>
    private string StandInSsh()
    {
        var log = Path.Combine(_root, "ssh.log").Replace('\\', '/');
        var file = Path.Combine(_root, "stand-in-ssh.sh");
        File.WriteAllText(file, $$"""
            #!/bin/sh
            echo "$@" >> '{{log}}'
            while [ $# -gt 2 ]; do shift; done
            exec sh -c "$2"

            """.Replace("\r\n", "\n"));
        return $"\"{file.Replace('\\', '/')}\"";
    }

    /// <summary>
    /// The work cloned over the stand-in with no door in the way, its own <c>core.sshCommand</c> naming it: git unsets
    /// <c>GIT_CONFIG_PARAMETERS</c> for its ssh child (measured, GITAUTH1b), so a command-line <c>-c core.sshCommand</c> never reaches
    /// the door, and a repository's own setting does.
    /// </summary>
    private async Task<string> CloneOverSshAsync()
    {
        var address = "fixture@localhost:" + Bare.Replace('\\', '/');
        var ssh = StandInSsh();
        await Ok(Git(_root, Path.Combine(_root, "no-install"), "-c", $"core.sshCommand={ssh}", "clone", "--config", $"core.sshCommand={ssh}", address, Work));
        File.Delete(Path.Combine(_root, "ssh.log"));
        return address;
    }

    [Fact]
    public async Task Over_SSH_a_fetch_passes_the_door_with_BatchMode_first_and_a_sessions_push_is_refused_before_any_ssh_starts()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        var address = await CloneOverSshAsync();

        await Ok(Work, "fetch", "origin");
        Assert.Equal(address, await Ok(Work, "remote", "get-url", "origin"));
        var log = File.ReadAllLines(Path.Combine(_root, "ssh.log"));
        Assert.StartsWith("-o BatchMode=yes", log[0], StringComparison.Ordinal);
        Assert.Contains("git-upload-pack", log[0], StringComparison.Ordinal);

        var before = await TipAsync();
        await CommitAsync("over ssh");
        var (exit, _, error) = await RunAsync(Git(Work, Home, "push", "origin", "main"));

        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.Single(File.ReadAllLines(Path.Combine(_root, "ssh.log")));
    }

    [Fact]
    public async Task Over_SSH_the_landings_grant_lets_its_push_through()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        var address = await CloneOverSshAsync();
        var tip = await CommitAsync("landed over ssh");
        var push = Git(Work, Home, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(Home, address, DateTimeOffset.UtcNow);

        await Ok(push);

        Assert.Equal(tip, await TipAsync());
    }

    [Fact]
    public async Task Installed_under_a_folder_with_a_space_the_door_is_named_by_GIT_SSH_and_still_judges()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        var install = Path.Combine(_root, "with space");
        var home = Path.Combine(install, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        LayDoors(install);
        Assert.Equal("git-ssh", GitWiring.SshForm(home));
        // Through GIT_SSH git passes the door the remote command alone, with no repository command to run, so it starts git's own
        // ssh; the push is refused before that.
        var address = "fixture@localhost:" + Bare.Replace('\\', '/');
        await Ok(Git(_root, Path.Combine(_root, "no-install"), "clone", Bare, Work));
        await Ok(Work, "remote", "set-url", "origin", address);
        await CommitAsync("spaced");

        var (exit, _, error) = await RunAsync(Git(Work, home, "push", "origin", "main"));

        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
    }

    // ── The person's terminal (§4.5) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_persons_terminal_is_unwired_and_its_push_meets_no_door()
    {
        if (!OperatingSystem.IsWindows()) return;
        await ServeAsync();
        LayDoors(_root);
        await using var host = new GitHttpBackendHost(Backend(), Projects);
        await Ok(_root, "clone", host.Address("org/engine.git"), Work);
        var tip = await CommitAsync("the person's own");

        // The terminal's block: the tools' variables alone, over the person's own environment.
        var info = new ProcessStartInfo("git") { WorkingDirectory = Work, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var word in new[] { "push", "origin", "main" }) info.ArgumentList.Add(word);
        foreach (var (name, value) in Tools.ChildEnvironment(Tools.Read(Home), Home, Environment.GetEnvironmentVariable("PATH"))) info.Environment[name] = value;
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = Path.Combine(_root, "scratch-home");
        // This test may itself run under a Daoris that routes its children; the person's terminal starts from no such thing.
        foreach (var name in new[] { "GIT_SSH_COMMAND", "GIT_SSH", "GIT_CONFIG_COUNT", GitRouting.HomeVariable }) info.Environment.Remove(name);

        await Ok(info);

        Assert.Equal(tip, await TipAsync());
    }
}
