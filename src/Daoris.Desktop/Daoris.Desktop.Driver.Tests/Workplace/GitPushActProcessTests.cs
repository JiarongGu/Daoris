using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2g and n's proof (D241 point 2; the git design §3.1 row G7), against the stand-in SSH host (D241 point 8): the person's press of
/// one branch, one act of the journal, pushes through Daoris's built ssh door under the grant it binds, while the same push made without the
/// grant is refused; a lightweight tag on the same act is judged Dangerous at the door, <c>deny</c> refusing it and <c>allow</c> passing it.
/// </summary>
/// <remarks>
/// <para><b>Its server</b> is <see cref="GitSshHost"/>, in this process on a free loopback port: nothing is installed or started. Every key is
/// generated here, under the repository's <c>_fixtures/</c>, and removed with it; no key, agent or known-hosts file of the person's is read.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitPushActProcessTests : IClassFixture<GitSshHost>, IDisposable
{
    private readonly GitSshHost _host;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-push", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Work => Path.Combine(_root, "work");

    private string Address => _host.Address(_path);

    public GitPushActProcessTests(GitSshHost host)
    {
        _host = host;
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

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
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
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(stop.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(' ', info.ArgumentList)} did not end within its bound");
        }

        return (process.ExitCode, await output, await error);
    }

    /// <summary>A git started as Daoris starts its own, kept from the person's configuration and home, with no grant.</summary>
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
    /// The bare repository with one commit, an RSA identity generated and authorized on the host, the host's key trusted, the doors laid, the
    /// person's checkout cloned through the door and one commit made on a branch of its own, which origin does not have.
    /// </summary>
    private async Task<string> ReadyAsync()
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
        _host.Authorize(GitKeys.PublicKey(Home, identity.Id)!);
        var tested = await GitIdentityActs.TestAsync(Home, identity.Id, GitKeys.Describe(_host.HostKey)!.Value.Fingerprint, CancellationToken.None);
        Assert.True(tested.Result == "reached", $"{tested.Result}: {tested.Words}");
        GitDoorsLaid.Lay(_root);

        await Ok(Git(_root, "clone", Address, Work));
        await Ok(Git(Work, "switch", "-c", "work/fix"));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "fixed\n");
        await Ok(Git(Work, "commit", "-am", "fixed"));
        return await Ok(Git(Work, "rev-parse", "HEAD"));
    }

    private ActRuns Runs() => new(new ActJournal(Home));

    /// <summary>The machine's Dangerous value as the person set it, Write left at its default, so only a grant lets a push through.</summary>
    private void Dangerous(string value)
    {
        var config = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        var root = File.Exists(config) ? (JsonObject)JsonNode.Parse(File.ReadAllText(config))! : new JsonObject();
        root["machineGitAccess"] = new JsonObject { ["dangerous"] = value };
        File.WriteAllText(config, root.ToJsonString());
    }

    /// <summary>What the host's repository holds at a ref; null where it holds none.</summary>
    private async Task<string?> RefAsync(string reference)
    {
        var (exit, output, _) = await RunAsync(Git(Bare, "rev-parse", "--verify", "--quiet", reference));
        return exit == 0 ? output.Trim() : null;
    }

    private static string[] Grants(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.GitGrants);
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }

    [Fact]
    public async Task A_branch_pushed_on_the_press_passes_the_door_under_its_grant_and_the_same_push_without_one_is_refused()
    {
        var tip = await ReadyAsync();

        // Without the press's grant, Write at its default refuses a push at the door, and origin hears no update.
        var (exit, _, error) = await RunAsync(Git(Work, "push", "origin", $"{tip}:refs/heads/work/fix"));
        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        Assert.Null(await RefAsync("refs/heads/work/fix"));

        var run = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "branch", "work/fix", tip), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(tip, await RefAsync("refs/heads/work/fix"));
        var journal = new ActJournal(Home);
        var act = journal.Act(run.Act!)!;
        Assert.Equal((GitPushActs.Kind, "window", false), (act.Kind, act.Press, act.Open));
        Assert.Equal([ActStepKinds.Push], journal.Steps(act.Id).Select(step => step.Kind));
        var plan = JsonNode.Parse(act.Plan)!;
        Assert.Equal(("refs/heads/work/fix", tip, GitPushActs.Absent), ((string?)plan["ref"], (string?)plan["at"], (string?)plan["old"]));
        // The grant was bound to this one update and went with the step.
        Assert.Empty(Grants(Home));
    }

    [Fact]
    public async Task A_branch_that_moved_since_the_page_showed_it_is_refused_before_anything_is_written()
    {
        var tip = await ReadyAsync();
        File.AppendAllText(Path.Combine(Work, "a.txt"), "again\n");
        await Ok(Git(Work, "commit", "-am", "again"));

        var run = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "branch", "work/fix", tip), "window", "window", CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Null(run.Act);
        Assert.Contains("no longer stands at the commit shown", run.Said, StringComparison.Ordinal);
        Assert.Null(await RefAsync("refs/heads/work/fix"));
    }

    [Fact]
    public async Task A_lightweight_tag_on_the_same_act_is_judged_Dangerous_at_the_door_deny_refusing_and_allow_passing()
    {
        var tip = await ReadyAsync();
        await Ok(Git(Work, "tag", "v1"));
        Dangerous(GitAccessKeys.Deny);

        var refused = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "tag", "v1", tip), "window", "window", CancellationToken.None);

        Assert.False(refused.Refused, refused.Said);
        Assert.Equal(ActOutcomes.NotDone, refused.Outcome);
        Assert.Contains(GitPushUpdates.DeniedLine(GitPushKind.Tag).TrimEnd('.'), refused.Notes, StringComparison.Ordinal);
        Assert.Null(await RefAsync("refs/tags/v1"));
        Assert.Empty(Grants(Home));

        Dangerous(GitAccessKeys.Allow);
        var passed = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "tag", "v1", tip), "window", "window", CancellationToken.None);

        Assert.False(passed.Refused, passed.Said);
        Assert.Equal(ActOutcomes.Done, passed.Outcome);
        Assert.Equal(tip, await RefAsync("refs/tags/v1"));
    }

    [Fact]
    public async Task An_annotated_tag_and_a_session_branch_are_refused_before_anything_is_written()
    {
        await ReadyAsync();
        await Ok(Git(Work, "tag", "-a", "v2", "-m", "annotated"));
        await Ok(Git(Work, "branch", "daoris/s-1"));

        var annotated = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "tag", "v2"), "window", "window", CancellationToken.None);
        var session = await GitPushActs.PushAsync(Runs(), new PushAsk("engine", Work, "branch", "daoris/s-1"), "window", "window", CancellationToken.None);

        Assert.Equal((true, true), (annotated.Refused, session.Refused));
        Assert.Contains("annotated", annotated.Said, StringComparison.Ordinal);
        Assert.Contains("daoris/", session.Said, StringComparison.Ordinal);
        Assert.Null(await RefAsync("refs/tags/v2"));
        Assert.Null(await RefAsync("refs/heads/daoris/s-1"));
    }
}
