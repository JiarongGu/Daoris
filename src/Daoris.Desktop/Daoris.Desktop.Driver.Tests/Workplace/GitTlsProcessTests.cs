using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git design's §4.1 HTTPS row as far as today's doors carry it (D241 point 8), against <see cref="GitTlsHost"/>: a remote nothing
/// serves is cloned and fetched anonymously through the remote helper over TLS; a session's push is refused at the helper before the
/// host hears it, and a push under the landing's grant lands; a git not handed the host's authority refuses its certificate.
/// </summary>
/// <remarks>
/// <para>Every git is started as Daoris starts its own, then kept from the person's configuration: no system file, an empty global file
/// and a scratch home; the host's certificate is trusted by each command's own configuration variables alone.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitTlsProcessTests : IClassFixture<GitTlsHost>, IDisposable
{
    private readonly GitTlsHost _host;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-tls", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>This test's own repository on the shared host.</summary>
    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Work => Path.Combine(_root, "work");

    private string Address => _host.Address(_path);

    public GitTlsProcessTests(GitTlsHost host)
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

    /// <summary>A git started as Daoris starts its own, kept from the person's configuration and home, trusting the host.</summary>
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
        return _host.TrustedBy(TestGit.Ceiling(info));
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
        return (process.ExitCode, await output, await error);
    }

    private async Task<string> Ok(ProcessStartInfo info)
    {
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    /// <summary>The bare repository with one commit, and the doors laid.</summary>
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
    }

    private Task<string> TipAsync() => Ok(Git(Bare, "rev-parse", "refs/heads/main"));

    [Fact]
    public async Task Over_TLS_a_remote_nothing_serves_is_read_through_the_helper_and_only_a_granted_push_lands()
    {
        await ServeAsync();

        await Ok(Git(_root, "clone", Address, Work));
        Assert.Equal(GitRouting.Prefix + Address, await Ok(Git(Work, "remote", "get-url", "origin")));
        await Ok(Git(Work, "fetch", "origin"));
        Assert.Equal(await TipAsync(), await Ok(Git(Work, "rev-parse", "refs/remotes/origin/main")));

        File.AppendAllText(Path.Combine(Work, "a.txt"), "landed\n");
        await Ok(Git(Work, "commit", "-am", "landed"));
        var before = await TipAsync();
        var asked = _host.Requests.Count;
        var (exit, _, error) = await RunAsync(Git(Work, "push", "origin", "main"));
        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.DeniedLine, error, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.DoesNotContain(_host.Requests.Skip(asked), line => line.Contains("receive-pack", StringComparison.Ordinal));

        var push = Git(Work, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = await GrantAsync("refs/heads/main");
        await Ok(push);
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await TipAsync());
        Assert.Contains(_host.Requests, line => line.StartsWith("POST", StringComparison.Ordinal) && line.Contains("git-receive-pack", StringComparison.Ordinal));
    }

    /// <summary>
    /// GITREADONLY1 at the remote helper: once an HTTPS identity marked read only on its host serves the address, its push is refused at
    /// <c>list for-push</c> in its words, the landing's grant included, before the host hears it. The identity is written as its file
    /// holds it, with no sign-in: the refusal comes before any credential is asked.
    /// </summary>
    [Fact]
    public async Task Over_TLS_a_read_only_identitys_push_is_refused_at_the_helper_in_its_words()
    {
        await ServeAsync();
        await Ok(Git(_root, "clone", Address, Work));
        var identities = DaorisHome.Entry(Home, HomeEntry.GitIdentities);
        Directory.CreateDirectory(Path.GetDirectoryName(identities)!);
        File.WriteAllText(identities, $$"""
            {"identities":[{"id":"gi-00000000000b","label":"Reader","host":"{{GitHttps.HostOf(Address)}}","way":"https","readOnly":true,"default":true}]}
            """);
        File.AppendAllText(Path.Combine(Work, "a.txt"), "refused\n");
        await Ok(Git(Work, "commit", "-am", "refused"));
        var before = await TipAsync();
        var asked = _host.Requests.Count;
        var push = Git(Work, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = await GrantAsync("refs/heads/main");

        var (exit, _, error) = await RunAsync(push);

        Assert.NotEqual(0, exit);
        Assert.Contains(GitAccessRules.ReadOnlyLine("Reader"), error, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.DoesNotContain(_host.Requests.Skip(asked), line => line.Contains("receive-pack", StringComparison.Ordinal));
    }

    // ── GITAUTH1g at the remote helper: the remote's refs and the sources' objects judged, then forwarded as judged ──

    /// <summary>The machine's git access as the person set it: Write, and Dangerous where given.</summary>
    private void Access(string write, string dangerous)
    {
        var config = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, new System.Text.Json.Nodes.JsonObject
        {
            ["machineGitAccess"] = new System.Text.Json.Nodes.JsonObject { ["write"] = write, ["dangerous"] = dangerous },
        }.ToJsonString());
    }

    private async Task<string?> GrantAsync(string reference, string? commit = null) =>
        GitGrants.Write(Home, Address, [new GitGrantRef(reference, commit ?? await Ok(Git(Work, "rev-parse", "HEAD")))], DateTimeOffset.UtcNow);

    private async Task<string?> RefAsync(string reference)
    {
        var (exit, output, _) = await RunAsync(Git(Bare, "rev-parse", "--verify", "--quiet", reference));
        return exit == 0 ? output.Trim() : null;
    }

    /// <summary>A clone with one commit on main that the host does not hold; the host's tip before it.</summary>
    private async Task<string> CommittedAsync()
    {
        await ServeAsync();
        var before = await TipAsync();
        await Ok(Git(_root, "clone", Address, Work));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "held\n");
        await Ok(Git(Work, "commit", "-am", "held"));
        return before;
    }

    private async Task<string> ErrorOfAsync(params string[] push)
    {
        var (exit, _, error) = await RunAsync(Git(Work, push));
        Assert.NotEqual(0, exit);
        return error;
    }

    [Fact]
    public async Task Over_TLS_a_grant_for_one_ref_refuses_another_and_moves_nothing()
    {
        var before = await CommittedAsync();
        var push = Git(Work, "push", "origin", "main:refs/heads/other");
        push.Environment[GitRouting.GrantVariable] = await GrantAsync("refs/heads/main");
        var asked = _host.Requests.Count;

        var (exit, _, error) = await RunAsync(push);

        Assert.NotEqual(0, exit);
        Assert.Contains(GitPushJudge.NotCarriedLine("refs/heads/other"), error, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.Null(await RefAsync("refs/heads/other"));
        Assert.DoesNotContain(_host.Requests.Skip(asked), line => line.Contains("POST", StringComparison.Ordinal) && line.Contains("receive-pack", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Over_TLS_a_tag_is_refused_by_deny_and_passed_by_allow()
    {
        await CommittedAsync();
        await Ok(Git(Work, "tag", "v1"));
        Access("allow", "deny");

        var error = await ErrorOfAsync("push", "origin", "v1");

        Assert.Contains(GitPushUpdates.DeniedLine(GitPushKind.Tag), error, StringComparison.Ordinal);
        Assert.Null(await RefAsync("refs/tags/v1"));
        Access("allow", "allow");
        await Ok(Git(Work, "push", "origin", "v1"));
        Assert.Equal(await Ok(Git(Work, "rev-parse", "v1")), await RefAsync("refs/tags/v1"));
    }

    [Fact]
    public async Task Over_TLS_a_mirror_and_a_prune_are_refused_by_deny_and_passed_by_allow()
    {
        await ServeAsync();
        await Ok(Git(Bare, "branch", "stale", "main"));
        await Ok(Git(_root, "clone", Address, Work));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "held\n");
        await Ok(Git(Work, "commit", "-am", "held"));
        await Ok(Git(Work, "update-ref", "refs/custom/x", "HEAD"));
        var before = await TipAsync();
        Access("allow", "deny");

        var mirrored = await ErrorOfAsync("push", "--mirror", "origin");
        var pruned = await ErrorOfAsync("push", "--prune", "origin", "refs/heads/*:refs/heads/*");

        Assert.Contains(GitPushUpdates.DeniedLine(GitPushKind.Mirror), mirrored, StringComparison.Ordinal);
        Assert.Contains(GitPushUpdates.DeniedLine(GitPushKind.Prune), pruned, StringComparison.Ordinal);
        Assert.Equal(before, await TipAsync());
        Assert.Null(await RefAsync("refs/custom/x"));
        Assert.NotNull(await RefAsync("refs/heads/stale"));
        Access("allow", "allow");
        await Ok(Git(Work, "push", "--prune", "origin", "refs/heads/*:refs/heads/*"));
        Assert.Null(await RefAsync("refs/heads/stale"));
        await Ok(Git(Work, "push", "--mirror", "origin"));
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await RefAsync("refs/custom/x"));
    }

    [Fact]
    public async Task Over_TLS_a_force_is_judged_by_history_and_a_batch_with_one_refused_ref_moves_none()
    {
        var before = await CommittedAsync();
        Access("allow", "deny");
        await Ok(Git(Work, "push", "origin", "main"));
        var pushed = await TipAsync();
        Assert.NotEqual(before, pushed);
        await Ok(Git(Work, "commit", "--amend", "--allow-empty", "-m", "rewritten"));
        await Ok(Git(Work, "tag", "v1"));

        var forced = await ErrorOfAsync("push", "--force", "origin", "main");
        var batch = await ErrorOfAsync("push", "--force", "origin", "main", "v1");

        Assert.Contains(GitPushUpdates.DeniedLine(GitPushKind.Force), forced, StringComparison.Ordinal);
        Assert.Contains("daoris:", batch, StringComparison.Ordinal);
        Assert.Equal(pushed, await TipAsync());
        Assert.Null(await RefAsync("refs/tags/v1"));
        Access("allow", "allow");
        await Ok(Git(Work, "push", "--force", "origin", "main", "v1"));
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await TipAsync());
    }

    [Fact]
    public async Task A_git_not_handed_the_hosts_authority_refuses_its_certificate()
    {
        await ServeAsync();
        var clone = Git(_root, "clone", Address, Work);
        clone.Environment.Remove("GIT_CONFIG_COUNT");

        var (exit, _, error) = await RunAsync(clone);

        Assert.NotEqual(0, exit);
        Assert.Contains("certificate", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(Work, ".git")));
    }
}
