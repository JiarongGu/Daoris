using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git-auth design §14 row 3's proof over SSH (GITAUTH1c, D198 point 4), against the stand-in host (D241 point 8): an SSH identity
/// Daoris generated, the host's key trusted at a test's press, a clone, a fetch and a push through Daoris's built ssh door, and a fetch
/// in another repository by <c>git -C</c>; a branch pushed so and opened as a pull request on <see cref="PullRequestApiHost"/>; Windows'
/// OpenSSH, chosen as the ssh, reading the key under its protected folder; and under <c>own</c>, a repository's <c>BatchMode=no</c>
/// losing to the door's first value.
/// </summary>
/// <remarks>
/// <para><b>Its server</b> is <see cref="GitSshHost"/>, in this process on a free loopback port: nothing is installed or started. It
/// verifies RSA and ECDSA keys, so the identity here is made as RSA.</para>
/// <para>Every git is started as Daoris starts its own, then kept from the person's configuration: no system file, an empty global file
/// and a scratch home. Every key is generated here, under the repository's <c>_fixtures/</c>, and removed with it; no key, agent or
/// known-hosts file of the person's is read.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitSshProcessTests : IClassFixture<GitSshHost>, IClassFixture<PullRequestApiHost>, IDisposable
{
    private readonly GitSshHost _host;

    private readonly PullRequestApiHost _api;

    private readonly ITestOutputHelper _output;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-ssh", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>This test's own repository on the shared host.</summary>
    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Work => Path.Combine(_root, "work");

    private string Address => _host.Address(_path);

    public GitSshProcessTests(GitSshHost host, PullRequestApiHost api, ITestOutputHelper output)
    {
        _host = host;
        _api = api;
        _output = output;
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(_root, "scratch-home"));
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public void Dispose()
    {
        GitSeed.Discard(_root);
        GitSeed.Discard(Path.GetDirectoryName(Bare)!);
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info, TimeSpan? bound = null)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.RedirectStandardInput = true;
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var stop = new CancellationTokenSource(bound ?? TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(stop.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(info.FileName)} {string.Join(' ', info.ArgumentList)} did not end within its bound");
        }

        return (process.ExitCode, await output, await error);
    }

    private static string Slashed(string path) => path.Replace('\\', '/');

    // ── Daoris's side ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A git started as Daoris starts its own, kept from the person's configuration and home.</summary>
    private ProcessStartInfo Git(string folder, params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, Home);
        Assert.Null(refusal);
        var scratch = Path.Combine(_root, "scratch-home");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = scratch;
        info.Environment["USERPROFILE"] = scratch;
        info.Environment["XDG_CONFIG_HOME"] = scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return TestGit.Ceiling(info);
    }

    private async Task<string> Ok(ProcessStartInfo info)
    {
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    /// <summary>
    /// The bare repository with one commit, the identity generated as <c>git identity add-ssh --rsa</c> makes it and authorized on the
    /// host, the host's key trusted as <c>git identity test --trust-host</c> trusts it, and the doors laid.
    /// </summary>
    private async Task<GitIdentity> ReadyAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Git(Bare, "init", "--bare", "--initial-branch=main"));
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(Git(seed, "init", "--initial-branch=main"));
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(Git(seed, "add", "a.txt"));
        await Ok(Git(seed, "commit", "-m", "first"));
        await Ok(Git(seed, "push", Bare, "main"));

        var identity = await GitIdentityActs.AddSshAsync(Home, _host.Host, "Fixture", null, null, null, rsa: true, CancellationToken.None);
        Assert.True(GitKeys.IsPrivate(GitKeys.Folder(Home, identity.Id)));
        _host.Authorize(GitKeys.PublicKey(Home, identity.Id)!);
        var fingerprint = GitKeys.Describe(_host.HostKey)!.Value.Fingerprint;
        var tested = await GitIdentityActs.TestAsync(Home, identity.Id, fingerprint, CancellationToken.None);
        Assert.True(tested.Result == "reached", $"{tested.Result}: {tested.Words}");
        GitDoorsLaid.Lay(_root);
        return identity;
    }

    private Task<string> TipAsync() => Ok(Git(Bare, "rev-parse", "refs/heads/main"));

    // ── The cases ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_identity_fetches_and_pushes_through_the_door_and_fetches_in_another_repository_by_git_C()
    {
        var identity = await ReadyAsync();

        await Ok(Git(_root, "clone", Address, Work));
        Assert.Equal(await TipAsync(), await Ok(Git(Work, "rev-parse", "HEAD")));
        Assert.NotNull(GitSshFiles.LastUsed(Home, identity.Id));

        File.AppendAllText(Path.Combine(Work, "a.txt"), "landed\n");
        await Ok(Git(Work, "commit", "-am", "landed"));
        var push = Git(Work, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(Home, Address, DateTimeOffset.UtcNow);
        await Ok(push);
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await TipAsync());

        var other = Path.Combine(_root, "other");
        await Ok(Git(_root, "clone", Address, other));
        await Ok(Git(_root, "-C", other, "fetch", "origin"));
        Assert.Equal(await TipAsync(), await Ok(Git(other, "rev-parse", "refs/remotes/origin/main")));

        var pushed = Assert.Single(_host.Runs, run => run.Command?.StartsWith("git-receive-pack", StringComparison.Ordinal) == true
            && run.Command.Contains(_path, StringComparison.Ordinal));
        Assert.Equal(0, pushed.Exit);
        Assert.StartsWith("SSH-2.0-OpenSSH", pushed.Client, StringComparison.Ordinal);
        _output.WriteLine($"the door's ssh: {pushed.Client}");
    }

    /// <summary>The design's workflow row in small: the branch pushed through the door under its grant, then opened and read on the API.</summary>
    [Fact]
    public async Task A_branch_pushed_through_the_door_is_opened_and_read_as_a_pull_request_on_the_stand_in_API()
    {
        await ReadyAsync();
        await Ok(Git(_root, "clone", Address, Work));
        await Ok(Git(Work, "switch", "-c", "work/fix"));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "fixed\n");
        await Ok(Git(Work, "commit", "-am", "fixed"));
        var push = Git(Work, "push", "origin", "work/fix");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(Home, Address, DateTimeOffset.UtcNow);
        await Ok(push);
        var pushed = await Ok(Git(Bare, "rev-parse", "refs/heads/work/fix"));
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), pushed);

        var token = "fixture-api-token-" + Guid.NewGuid().ToString("N")[..8];
        _api.Accept(token);
        using var http = new HttpClient { BaseAddress = new Uri(_api.Address) };
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var opened = await http.PostAsJsonAsync("/repos/org/engine/pulls", new { title = "Fixed", head = "work/fix", @base = "main", body = "From the door." });
        Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);
        var number = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("number").GetInt32();
        Assert.Equal("open", (await http.GetFromJsonAsync<JsonElement>($"/repos/org/engine/pulls/{number}")).GetProperty("state").GetString());

        _api.Complete(number, pushed);
        var merged = await http.GetFromJsonAsync<JsonElement>($"/repos/org/engine/pulls/{number}");
        Assert.Equal(("closed", true, pushed),
            (merged.GetProperty("state").GetString(), merged.GetProperty("merged").GetBoolean(), merged.GetProperty("merge_commit_sha").GetString()));
        Assert.All(_api.Calls, call => Assert.True(call.Admitted));
    }

    [Fact]
    public async Task Windows_OpenSSH_chosen_as_the_ssh_reads_the_key_under_its_protected_folder()
    {
        var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        var ssh = File.Exists(windows) ? windows : Path.Combine(GitIdentityActs.SshTools(Home), OperatingSystem.IsWindows() ? "ssh.exe" : "ssh");
        Tools.SetGit(Home, "core.sshCommand", ssh);
        await ReadyAsync();

        await Ok(Git(_root, "clone", Address, Work));

        Assert.Equal(await TipAsync(), await Ok(Git(Work, "rev-parse", "HEAD")));
        var cloned = _host.Runs.Last(run => run.Command?.Contains(_path, StringComparison.Ordinal) == true);
        if (File.Exists(windows)) Assert.Contains("OpenSSH_for_Windows", cloned.Client, StringComparison.Ordinal);
        _output.WriteLine($"the chosen ssh: {cloned.Client}");
    }

    [Fact]
    public async Task Under_own_a_repositorys_BatchMode_no_loses_to_the_doors_first_value()
    {
        var identity = await ReadyAsync();
        await Ok(Git(_root, "clone", Address, Work));
        GitIdentityActs.UseMachine(Home, _host.Host, own: true);
        var empty = Path.Combine(_root, "empty");
        File.WriteAllText(empty, "");
        // Its own command knows no host key and asks: with BatchMode=no ssh would wait on the console for a yes, so an answer at once,
        // refused, is BatchMode=yes standing first.
        await Ok(Git(Work, "config", "core.sshCommand",
            $"ssh -o BatchMode=no -o StrictHostKeyChecking=ask -F '{Slashed(empty)}' -i '{Slashed(GitKeys.KeyPath(Home, identity.Id))}' "
            + $"-o IdentitiesOnly=yes -o IdentityAgent=none -o UserKnownHostsFile='{Slashed(empty)}' -o GlobalKnownHostsFile=none"));

        var (exit, _, error) = await RunAsync(Git(Work, "fetch", "origin"), TimeSpan.FromSeconds(60));

        Assert.NotEqual(0, exit);
        Assert.Contains("Host key verification failed", error, StringComparison.Ordinal);
    }
}
