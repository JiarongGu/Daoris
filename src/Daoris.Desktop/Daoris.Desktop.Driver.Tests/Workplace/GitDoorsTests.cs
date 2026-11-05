using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAUTH1b's tables (D198 points 2, 8, 11 and 13; the git-auth design §4.1-§4.3, §6.1, §6.2 and §6.7): what <see cref="GitWiring"/>
/// hands a start, the ssh door's contract, the helper's commands by class, the judge's default door by door, the address's key and
/// the landing's grant.
/// </summary>
public sealed class GitDoorsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-doors-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Bin => Path.Combine(_root, "app", "bin");

    public GitDoorsTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Lay(params string[] doors)
    {
        Directory.CreateDirectory(Bin);
        foreach (var door in doors) File.WriteAllText(Path.Combine(Bin, door), "");
    }

    // ── What every start is handed (§4.1) ──────────────────────────────────────────────────────────

    [Fact]
    public void With_both_doors_installed_a_start_is_routed_through_them_with_its_home()
    {
        Lay(GitWiring.SshDoor, GitWiring.RemoteHelper);
        var info = new ProcessStartInfo("x");
        // A start inherits this process's git settings, and a Daoris session hands its agents `core.longpaths` (GitLongPaths), so a
        // run inside one would count them: each case starts from none, as GitLongPathsTests' do.
        info.Environment.Remove("GIT_CONFIG_COUNT");
        info.Environment[GitRouting.HomeVariable] = "C:/elsewhere";

        GitWiring.Hand(info, Home);

        Assert.Equal(Path.GetFullPath(Home), info.Environment[GitRouting.HomeVariable]);
        Assert.Equal(Path.Combine(Bin, GitWiring.SshDoor).Replace('\\', '/'), info.Environment["GIT_SSH_COMMAND"]);
        Assert.Equal("ssh", info.Environment["GIT_SSH_VARIANT"]);
        Assert.Equal("3", info.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("url.daoris::https://.insteadOf", info.Environment["GIT_CONFIG_KEY_0"]);
        Assert.Equal("https://", info.Environment["GIT_CONFIG_VALUE_0"]);
        Assert.Equal("url.daoris::http://.insteadOf", info.Environment["GIT_CONFIG_KEY_1"]);
        Assert.Equal("protocol.daoris.allow", info.Environment["GIT_CONFIG_KEY_2"]);
        Assert.Equal("always", info.Environment["GIT_CONFIG_VALUE_2"]);
        Assert.Equal("command", GitWiring.SshForm(Home));
    }

    [Fact]
    public void With_no_door_installed_a_start_is_routed_nowhere_and_still_told_its_home()
    {
        var info = new ProcessStartInfo("x");
        info.Environment.Remove("GIT_CONFIG_COUNT");

        GitWiring.Hand(info, Home);

        Assert.Equal(Path.GetFullPath(Home), info.Environment[GitRouting.HomeVariable]);
        Assert.False(info.Environment.ContainsKey("GIT_SSH_VARIANT"));
        Assert.False(info.Environment.ContainsKey("GIT_CONFIG_COUNT"));
        Assert.Null(GitWiring.SshForm(Home));
    }

    [Fact]
    public void Each_door_is_handed_on_its_own()
    {
        Lay(GitWiring.SshDoor);
        var info = new ProcessStartInfo("x");
        info.Environment.Remove("GIT_CONFIG_COUNT");

        GitWiring.Hand(info, Home);

        Assert.True(info.Environment.ContainsKey("GIT_SSH_COMMAND"));
        Assert.False(info.Environment.ContainsKey("GIT_CONFIG_COUNT"));
    }

    [Fact]
    public void The_rewrite_is_appended_after_a_command_scope_the_start_already_holds()
    {
        Lay(GitWiring.RemoteHelper);
        var info = new ProcessStartInfo("x");
        info.Environment["GIT_CONFIG_COUNT"] = "1";
        info.Environment["GIT_CONFIG_KEY_0"] = "core.autocrlf";
        info.Environment["GIT_CONFIG_VALUE_0"] = "false";

        GitWiring.Hand(info, Home);

        Assert.Equal("4", info.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("core.autocrlf", info.Environment["GIT_CONFIG_KEY_0"]);
        Assert.Equal("url.daoris::https://.insteadOf", info.Environment["GIT_CONFIG_KEY_1"]);

        GitRouting.Unroute(info.Environment);

        Assert.Equal("1", info.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("core.autocrlf", info.Environment["GIT_CONFIG_KEY_0"]);
        Assert.Equal("false", info.Environment["GIT_CONFIG_VALUE_0"]);
        Assert.False(info.Environment.ContainsKey("GIT_CONFIG_KEY_1"));
    }

    [Fact]
    public void Unrouting_a_start_that_held_only_the_rewrite_leaves_no_command_scope()
    {
        var environment = new Dictionary<string, string?>();
        GitRouting.Scope(environment, GitRouting.Rewrites);

        GitRouting.Unroute(environment);

        Assert.Empty(environment);
    }

    [Theory]
    [InlineData("C:/Daoris/app/bin/daoris-git-ssh.exe", "C:/Daoris/app/bin/daoris-git-ssh.exe")]
    [InlineData(@"X:\daoris\app\bin\daoris-git-ssh.exe", "X:/daoris/app/bin/daoris-git-ssh.exe")]
    [InlineData(@"C:\Program Files\Daoris\app\bin\daoris-git-ssh.exe", null)]
    [InlineData(@"C:\PROGRA~1\Daoris\app\bin\daoris-git-ssh.exe", null)]
    [InlineData(@"X:\person\a&b\Daoris\app\bin\daoris-git-ssh.exe", null)]
    [InlineData(@"X:\person\x(1)\Daoris\app\bin\daoris-git-ssh.exe", null)]
    [InlineData(@"X:\person\100%\Daoris\app\bin\daoris-git-ssh.exe", null)]
    public void The_ssh_door_is_a_bare_forward_slash_path_or_none(string path, string? bare) => Assert.Equal(bare, GitWiring.Bare(path));

    [Fact]
    public void Where_the_install_path_holds_a_shell_character_the_door_is_named_by_GIT_SSH_and_an_inherited_command_goes()
    {
        var spaced = Path.Combine(_root, "with space");
        Directory.CreateDirectory(Path.Combine(spaced, "data"));
        Directory.CreateDirectory(Path.Combine(spaced, "app", "bin"));
        File.WriteAllText(Path.Combine(spaced, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(spaced, "app", "bin", GitWiring.SshDoor), "");
        var info = new ProcessStartInfo("x");
        info.Environment["GIT_SSH_COMMAND"] = "ssh -i elsewhere";

        GitWiring.Hand(info, Path.Combine(spaced, "data"));

        Assert.False(info.Environment.ContainsKey("GIT_SSH_COMMAND"));
        Assert.Equal(Path.Combine(spaced, "app", "bin", GitWiring.SshDoor), info.Environment["GIT_SSH"]);
        Assert.Equal("git-ssh", GitWiring.SshForm(Path.Combine(spaced, "data")));
    }

    [Fact]
    public void A_sessions_start_loses_the_token_variables_and_another_start_keeps_them()
    {
        var info = new ProcessStartInfo("x");
        foreach (var name in GitWiring.SessionTokens) info.Environment[name] = "inherited";

        GitWiring.Hand(info, Home);
        Assert.All(GitWiring.SessionTokens, name => Assert.Equal("inherited", info.Environment[name]));

        GitWiring.Untoken(info.Environment);
        Assert.All(GitWiring.SessionTokens, name => Assert.False(info.Environment.ContainsKey(name)));
    }

    [Fact]
    public void The_terminals_environment_takes_no_door()
    {
        Lay(GitWiring.SshDoor, GitWiring.RemoteHelper);

        var variables = Tools.ChildEnvironment(Tools.Read(Home), Home, "C:/inherited");

        Assert.Equal([Tools.PathVariable], variables.Keys);
    }

    // ── The ssh door's contract (§4.2) ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("git@github.com|git-upload-pack 'org/engine.git'", "git-upload-pack", "org/engine.git", null)]
    [InlineData("-p|2222|-o|SendEnv=GIT_PROTOCOL|-4|git@host|git-receive-pack '/srv/a b.git'", "git-receive-pack", "/srv/a b.git", null)]
    [InlineData("host|git-upload-pack 'it'\\''s.git'", "git-upload-pack", "it's.git", null)]
    [InlineData("host|git-lfs-authenticate 'org/engine.git' upload", "git-lfs-authenticate", "org/engine.git", "upload")]
    [InlineData("-6|host|git-lfs-authenticate 'org/engine.git' download", "git-lfs-authenticate", "org/engine.git", "download")]
    public void The_door_takes_what_git_passes_an_OpenSSH_ssh(string arguments, string program, string path, string? operation)
    {
        var (call, refusal) = GitDoors.ReadSsh(arguments.Split('|'));

        Assert.Null(refusal);
        Assert.Equal(program, call!.Program);
        Assert.Equal(path, call.Path);
        Assert.Equal(operation, call.Operation);
    }

    [Theory]
    [InlineData("-G|host", "the option -G")]
    [InlineData("-o|ProxyCommand=evil|host|git-upload-pack 'x'", "the option -o")]
    [InlineData("-i|key|host|git-upload-pack 'x'", "the option -i")]
    [InlineData("host|bash -c id", "the remote command bash")]
    [InlineData("host|git-upload-pack", "the remote command git-upload-pack")]
    [InlineData("host|git-lfs-authenticate 'x' delete", "the remote command git-lfs-authenticate")]
    [InlineData("host|git-upload-pack 'x'|extra", "one host and one remote command")]
    [InlineData("host", "one host and one remote command")]
    public void Anything_else_is_refused_with_one_line_naming_it(string arguments, string named)
    {
        var (call, refusal) = GitDoors.ReadSsh(arguments.Split('|'));

        Assert.Null(call);
        Assert.Contains(named, refusal);
        Assert.DoesNotContain('\n', refusal);
    }

    [Fact]
    public void Started_with_no_home_the_door_refuses_and_starts_nothing()
    {
        var error = new StringWriter();

        var code = GitDoors.Ssh(["host", "git-upload-pack 'x'"], _ => null, error, _root);

        Assert.Equal(GitDoors.Refused, code);
        Assert.Contains("outside Daoris's wiring", error.ToString());
    }

    [Fact]
    public void A_sessions_push_is_refused_at_the_ssh_door_before_any_ssh_starts()
    {
        var error = new StringWriter();
        var variables = new Dictionary<string, string?> { [GitRouting.HomeVariable] = Home, ["PATH"] = "" };

        var code = GitDoors.Ssh(["git@host", "git-receive-pack 'org/engine.git'"], name => variables.GetValueOrDefault(name), error, _root);

        Assert.Equal(GitDoors.Refused, code);
        Assert.Equal(GitAccessRules.DeniedLine, error.ToString().Trim());
    }

    /// <summary>The one ref the grants of these cases carry.</summary>
    private static readonly GitGrantRef Carried = new("refs/heads/main", new string('a', 40));

    private string Put(HomeEntry entry, string json)
    {
        var path = DaorisHome.Entry(Home, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>GITREADONLY1: a push through an identity read only on its host is refused at the ssh door in its words, a grant's included.</summary>
    [Fact]
    public void A_push_through_a_read_only_identity_is_refused_at_the_ssh_door_in_its_words()
    {
        Put(HomeEntry.GitIdentities, """{"identities":[{"id":"gi-00000000000a","label":"Deploy","host":"host","way":"ssh","readOnly":true,"default":true}]}""");
        Put(HomeEntry.DriverConfig, """{"machineGitAccess":{"write":"allow"}}""");
        var error = new StringWriter();
        var variables = new Dictionary<string, string?>
        {
            [GitRouting.HomeVariable] = Home, ["PATH"] = "",
            [GitRouting.GrantVariable] = GitGrants.Write(Home, "git@host:org/engine.git", [Carried], DateTimeOffset.UtcNow),
        };

        var code = GitDoors.Ssh(["git@host", "git-receive-pack 'org/engine.git'"], name => variables.GetValueOrDefault(name), error, _root);

        Assert.Equal(GitDoors.Refused, code);
        Assert.Equal(GitAccessRules.ReadOnlyLine("Deploy"), error.ToString().Trim());
        Assert.Contains(Lines("git.refused"), line => Field(line, "code") == "read-only");
    }

    /// <summary>GITAUTH1f: where Write says ask and no instance of Daoris holds the home, the door refuses at once and leaves no go-ahead.</summary>
    [Fact]
    public void Where_Daoris_is_not_running_a_door_that_would_ask_refuses_at_once()
    {
        Put(HomeEntry.DriverConfig, """{"machineGitAccess":{"write":"ask"}}""");
        var error = new StringWriter();
        var variables = new Dictionary<string, string?> { [GitRouting.HomeVariable] = Home, ["PATH"] = "" };
        var clock = Stopwatch.StartNew();

        var code = GitDoors.Ssh(["git@host", "git-receive-pack 'org/engine.git'"], name => variables.GetValueOrDefault(name), error, _root);

        Assert.Equal(GitDoors.Refused, code);
        Assert.Equal(GitDoorAsks.NotRunningLine, error.ToString().Trim());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"the door took {clock.Elapsed}");
        Assert.Empty(GitAsks.List(Home, DateTimeOffset.UtcNow));
        Assert.Contains(Lines("git.refused"), line => Field(line, "code") == "not-running");
    }

    [Fact]
    public void Daoris_runs_for_a_door_while_an_instance_holds_the_home_and_not_while_a_verb_does()
    {
        Assert.False(GitDoorAsks.Running(Home));
        using (HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "git"))
        {
            Assert.False(GitDoorAsks.Running(Home));
        }

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            Assert.True(GitDoorAsks.Running(Home));
        }

        Assert.False(GitDoorAsks.Running(Home));
    }

    // ── The coded lines (§11) ──────────────────────────────────────────────────────────────────────

    private System.Text.Json.JsonElement[] Lines(string @event)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.driver.jsonl")
                .SelectMany(StubFile.Lines)
                .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    private static string? Field(System.Text.Json.JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;

    [Fact]
    public void A_refusal_at_the_door_is_a_coded_line_naming_who_acted_and_never_the_user()
    {
        var variables = new Dictionary<string, string?>
        {
            [GitRouting.HomeVariable] = Home, ["PATH"] = "", [GitLog.SessionVariable] = "s-1234", ["DAORIS_REPOSITORY"] = "engine",
        };

        GitDoors.Ssh(["git@github.com", "git-receive-pack 'org/engine.git'"], name => variables.GetValueOrDefault(name), new StringWriter(), _root);

        var line = Assert.Single(Lines("git.refused"));
        Assert.Equal(("engine", "github.com", "write", "denied", "session", "s-1234"),
            (Field(line, "repository"), Field(line, "host"), Field(line, "class"), Field(line, "code"), Field(line, "door"), Field(line, "session")));
        Assert.DoesNotContain("git@", line.GetRawText(), StringComparison.Ordinal);
        Assert.Empty(Lines("git.grant.used"));
    }

    [Fact]
    public void The_landings_grant_is_said_when_a_push_it_carries_takes_it_and_who_used_it_is_the_landing()
    {
        var grant = GitGrants.Write(Home, "git@github.com:org/engine.git", [Carried], DateTimeOffset.UtcNow)!;
        var variables = new Dictionary<string, string?> { [GitRouting.HomeVariable] = Home, ["PATH"] = "", [GitRouting.GrantVariable] = grant };
        var write = new GitVerdict(true, "granted", null);
        var updates = new[] { new GitPushUpdate(new string('1', 40), Carried.New, Carried.Ref) };

        // A grant stands for the door's first judgement but is taken only by the push it carries, once its commands are read.
        var line = GitPushDoor.Refusal(Home, write, updates, (_, _) => true, "github.com/org/engine", name => variables.GetValueOrDefault(name), null, "github.com", _root);

        Assert.Null(line);
        var used = Assert.Single(Lines("git.grant.used"));
        Assert.Equal(("landing", "write", "github.com"), (Field(used, "door"), Field(used, "class"), Field(used, "host")));
        Assert.Empty(Lines("git.refused"));
        Assert.Null(GitGrants.Read(Home, grant, "github.com/org/engine", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_push_the_grant_does_not_carry_is_refused_in_a_coded_line_and_leaves_the_grant_for_its_own_push()
    {
        var grant = GitGrants.Write(Home, "git@github.com:org/engine.git", [Carried], DateTimeOffset.UtcNow)!;
        var variables = new Dictionary<string, string?> { [GitRouting.HomeVariable] = Home, ["PATH"] = "", [GitRouting.GrantVariable] = grant };
        var other = new[] { new GitPushUpdate(new string('1', 40), Carried.New, "refs/heads/other") };

        var line = GitPushDoor.Refusal(Home, new(true, "granted", null), other, (_, _) => true, "github.com/org/engine", name => variables.GetValueOrDefault(name), null, "github.com", _root);

        Assert.Equal(GitPushJudge.NotCarriedLine("refs/heads/other"), line);
        Assert.Equal("grant-bound", Field(Assert.Single(Lines("git.refused")), "code"));
        Assert.NotNull(GitGrants.Read(Home, grant, "github.com/org/engine", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_grant_written_and_the_wiring_at_the_drivers_start_are_coded_lines()
    {
        GitLog.Granted(Home, "engine", "origin", "s-1234", "press");
        DriverWatch.Wired(Home);

        var granted = Assert.Single(Lines("git.granted"));
        Assert.Equal(("engine", "origin", "write", "press", "landing"),
            (Field(granted, "repository"), Field(granted, "remote"), Field(granted, "class"), Field(granted, "by"), Field(granted, "door")));
        var wired = Assert.Single(Lines("git.wired"));
        Assert.Null(Field(wired, "ssh"));
        Assert.False(wired.GetProperty("helper").GetBoolean());

        Lay(GitWiring.SshDoor, GitWiring.RemoteHelper);
        DriverWatch.Wired(Home);
        var laid = Lines("git.wired")[^1];
        Assert.Equal("command", Field(laid, "ssh"));
        Assert.True(laid.GetProperty("helper").GetBoolean());
    }

    [Theory]
    [InlineData("origin", "origin")]
    [InlineData("https://github.com/org/engine.git", null)]
    [InlineData("https://someone@github.com/org/engine.git", null)]
    [InlineData("git@github.com:org/engine.git", null)]
    [InlineData("", null)]
    public void The_helpers_remote_is_logged_by_its_name_alone(string given, string? logged) => Assert.Equal(logged, GitDoors.RemoteName(given));

    [Theory]
    [InlineData("ssh -i C:/keys/deploy", "ssh", "-i C:/keys/deploy")]
    [InlineData("\"C:/Program Files/OpenSSH/ssh.exe\" -i k", "C:/Program Files/OpenSSH/ssh.exe", "-i k")]
    [InlineData("C:/ssh.exe", "C:/ssh.exe", "")]
    public void A_commands_first_word_is_read_as_the_shell_reads_it(string command, string first, string after) =>
        Assert.Equal((first, after), GitDoors.FirstWord(command));

    // ── The helper's commands (§4.3) ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("capabilities", null)]
    [InlineData("option progress true", null)]
    [InlineData("list", GitAct.Read)]
    [InlineData("fetch 0123 refs/heads/main", GitAct.Read)]
    [InlineData("stateless-connect git-upload-pack", GitAct.Read)]
    [InlineData("list for-push", GitAct.Write)]
    [InlineData("push refs/heads/x:refs/heads/x", GitAct.Write)]
    [InlineData("connect git-receive-pack", GitAct.Write)]
    public void The_helper_judges_each_command_by_its_class(string line, GitAct? act) => Assert.Equal(act, GitDoors.ClassOfHelperCommand(line));

    [Fact]
    public void The_helper_refuses_an_address_that_is_not_http_and_one_started_with_no_home()
    {
        var error = new StringWriter();
        Assert.Equal(GitDoors.Refused, GitDoors.Helper(["origin", "git://host/x"], _ => Home, Stream.Null, Stream.Null, error));
        Assert.Contains("only http:// and https://", error.ToString());

        error = new StringWriter();
        Assert.Equal(GitDoors.Refused, GitDoors.Helper(["origin", "https://host/x"], _ => null, Stream.Null, Stream.Null, error));
        Assert.Contains("outside Daoris's wiring", error.ToString());
    }

    // ── The judge, Daoris's default door by door (§6.1, §6.2) ─────────────────────────────────────

    [Theory]
    [InlineData(GitAct.Read, false, true, "read")]
    [InlineData(GitAct.Write, false, false, "denied")]
    [InlineData(GitAct.Write, true, true, "granted")]
    public void The_default_reads_and_lets_only_the_landing_push(GitAct act, bool granted, bool allowed, string code)
    {
        var verdict = GitAccessRules.Judge(act, granted);

        Assert.Equal(allowed, verdict.Allowed);
        Assert.Equal(code, verdict.Code);
        Assert.Equal(allowed ? null : GitAccessRules.DeniedLine, verdict.Line);
    }

    // ── The address's key and the landing's grant (§6.7) ──────────────────────────────────────────

    [Theory]
    [InlineData("git@github.com:org/engine.git", "github.com/org/engine")]
    [InlineData("ssh://git@GitHub.com:22/org/engine", "github.com/org/engine")]
    [InlineData("https://user:secret@github.com/org/engine.git/", "github.com/org/engine")]
    [InlineData("daoris::https://github.com/org/engine.git", "github.com/org/engine")]
    [InlineData("ssh://git@ssh.dev.azure.com/v3/org/project/engine", "ssh.dev.azure.com/v3/org/project/engine")]
    [InlineData("C:/repositories/engine.git", null)]
    [InlineData("../engine", null)]
    [InlineData("", null)]
    public void An_address_is_keyed_by_its_host_and_path_alone(string address, string? key) => Assert.Equal(key, GitAddressKeys.Of(address));

    [Fact]
    public void The_ssh_doors_host_and_path_key_as_the_address_does()
    {
        Assert.Equal(GitAddressKeys.Of("git@github.com:org/engine.git"), GitAddressKeys.Of("git@github.com", "org/engine.git"));
        Assert.Equal(GitAddressKeys.Of("ssh://git@github.com/org/engine.git"), GitAddressKeys.Of("git@github.com", "/org/engine.git"));
    }

    [Fact]
    public void A_grant_is_taken_once_for_its_address_within_its_bound()
    {
        var now = DateTimeOffset.UtcNow;
        var id = GitGrants.Write(Home, "https://github.com/org/engine.git", [Carried], now)!;

        Assert.False(GitGrants.Take(Home, id, "github.com/org/other", now));
        Assert.True(GitGrants.Take(Home, id, "github.com/org/engine", now));
        Assert.False(GitGrants.Take(Home, id, "github.com/org/engine", now));

        var late = GitGrants.Write(Home, "git@github.com:org/engine.git", [Carried], now)!;
        Assert.False(GitGrants.Take(Home, late, "github.com/org/engine", now + GitGrants.Bound + TimeSpan.FromSeconds(1)));

        var dropped = GitGrants.Write(Home, "git@github.com:org/engine.git", [Carried], now)!;
        GitGrants.Drop(Home, dropped);
        Assert.False(GitGrants.Take(Home, dropped, "github.com/org/engine", now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../../permissions")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    public void A_grant_id_that_is_not_one_takes_nothing(string? id) => Assert.False(GitGrants.Take(Home, id, "github.com/org/engine", DateTimeOffset.UtcNow));
}
