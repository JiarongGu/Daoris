using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A read never starts a refresh of an account's sign-in, and a refresh refused at a limit never leaves its store empty.</summary>
public sealed class SignOutSharedTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 7, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-out-shared-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _account;
    private readonly FakeAgent _agent;
    private readonly ConcurrentQueue<AccountLine> _lines = new();
    private readonly List<string> _usageAsked = [];

    public SignOutSharedTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Binary, "");
        _account = HarnessSettings.ProfileHome(_home, "agent", "account-1");
        Directory.CreateDirectory(_account);
        _agent = new FakeAgent(_account);
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Binary => Path.Combine(_home, "agent-binary");

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("agent") };

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = TestDoors.Door("agent", new TestAgent(
                Binary: [Binary],
                VersionArguments: ["--version"],
                ProfileVariable: "READ_HOME",
                LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
                ProbeByPresence: true,
                StoredSignIn: _agent,
                Usage: new CodexUsageQuestion("agent", ["fake-agent", "app-server"], CodexUsage.Question.Recorded)), transport: Transport.Protocol),
        }), Settings)
        {
            Clock = () => Start,
            Asking = (_, _, _) => Task.FromResult(_agent.Status(Start)),
            AskingUsage = (_, account, _) =>
            {
                lock (_usageAsked) _usageAsked.Add(account);
                _agent.Status(Start);
                return Task.FromResult<IReadOnlyList<WindowReading>?>(
                    [new WindowReading("session", 0.5, Start.AddHours(3)), new WindowReading("weekly", 0.2, Start.AddDays(4))]);
            },
        };

    private string[] UsageAsked()
    {
        lock (_usageAsked) return [.. _usageAsked];
    }

    /// <summary>The account's own reading puts its session window at 100% until after now: at its limit.</summary>
    private void AtItsLimit() =>
        AccountWindows.Said(
            _home, "agent", "account-1",
            [new WindowReading("session", 1.0, Start.AddHours(1)), new WindowReading("weekly", 0.6, Start.AddDays(3))],
            Start - TimeSpan.FromMinutes(30), session: null, source: AccountWindows.SourceRead);

    private static object? Value(AccountLine line, string key) => line.Data.First(each => each.Key == key).Value;

    /// <summary>The read of an account whose access token has expired starts no refresh: the account keeps its last reading and nothing is asked of its agent.</summary>
    [Fact]
    public async Task A_status_read_of_an_expiring_account_starts_no_refresh()
    {
        _agent.SignedIn(expires: Start - TimeSpan.FromMinutes(1));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Start - TimeSpan.FromHours(1));
        var roster = Roster();

        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None);

        Assert.False(answer.Asked);
        Assert.Equal(LoginState.In, answer.Login);
        Assert.Equal(0, _agent.Refreshes);
        Assert.Equal(0, _agent.Asks);
        Assert.False(_agent.Read("")!.Cleared);
    }

    /// <summary>A read of an account's windows does not either: a usage read of an expiring account asks nothing and keeps the reading it had.</summary>
    [Fact]
    public async Task A_usage_read_of_an_expiring_account_starts_no_refresh()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromMinutes(3));
        var roster = Roster();

        var read = await roster.AskUsageAsync("agent", "agent", "account-1", Config, CancellationToken.None);

        Assert.Null(read);
        Assert.Empty(UsageAsked());
        Assert.Equal(0, _agent.Refreshes);
    }

    /// <summary>The renewal a counted start makes is the one ask that may refresh: a start on an expiring account still renews it once.</summary>
    [Fact]
    public async Task A_start_still_renews_an_expiring_account_once()
    {
        _agent.SignedIn(expires: Start - TimeSpan.FromMinutes(1));

        var start = await Roster().SelectAsync("agent", Config, null, null);

        Assert.Equal("account-1", start.Profile);
        Assert.Equal(1, _agent.Refreshes);
    }

    /// <summary>
    /// The 2026-11-25 07:12:45Z write: a read of an account with a fresh token and a full window, whose agent forces a refresh and is refused
    /// at its limit, and writes its store empty. The store is put back as the read found it, the account reads signed in, and the line says it.
    /// </summary>
    [Fact]
    public async Task A_limit_refusal_during_a_status_read_keeps_the_store()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Start - TimeSpan.FromHours(1));
        _agent.RefuseReads();
        AtItsLimit();
        var before = _agent.Bytes();
        var roster = Roster();
        roster.Writes = new CredentialWrites(roster, _lines.Enqueue);
        roster.Writes.Follow();

        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None);

        // The refusal's word (signed out) is unsaid with the store put back: no reading is made of it.
        Assert.False(answer.Asked);
        Assert.True(answer.Kept);
        Assert.Equal(1, _agent.Asks);
        Assert.False(_agent.Read("")!.Cleared);
        Assert.Equal(before, _agent.Bytes());
        Assert.Contains(_lines, line => line.Event == "credentials.kept" && Value(line, "account") is "account-1");
        await Poll.Until(() => _lines.Any(line => line.Event == "credentials.written" && Value(line, "file") is "store" && Value(line, "change") is "renamed"), () => "no write said");
        Assert.NotEqual(LoginState.Out, AccountReads.Of(_home, "agent").Accounts["account-1"].Login);
        Assert.DoesNotContain(_lines, line => line.Event == "credentials.written" && Value(line, "marked") is "out");
        Assert.Equal(0, SignOutNotices.Claim(_home, Start));
    }

    /// <summary>The same refusal in a read of the account's windows.</summary>
    [Fact]
    public async Task A_limit_refusal_during_a_usage_read_keeps_the_store()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        _agent.RefuseReads();
        AtItsLimit();
        var before = _agent.Bytes();

        var read = await Roster().AskUsageAsync("agent", "agent", "account-1", Config, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(["account-1"], UsageAsked());
        Assert.False(_agent.Read("")!.Cleared);
        Assert.Equal(before, _agent.Bytes());
    }

    /// <summary>A refresh a read makes that succeeds is left as the agent wrote it, and a store a person emptied before the read is not filled.</summary>
    [Fact]
    public async Task A_refresh_that_succeeds_and_a_store_already_empty_are_left_alone()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        var roster = Roster();
        var held = _agent.Bytes();

        Assert.True((await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None)).Asked);
        Assert.Equal(held, _agent.Bytes());

        _agent.Empty();
        _agent.RefuseReads();
        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None);

        Assert.False(answer.Kept);
        Assert.True(_agent.Read("")!.Cleared);
    }

    /// <summary>Daoris's own sign-in into the account writes its own store: a read beside it puts nothing back.</summary>
    [Fact]
    public async Task A_store_emptied_beside_Daoris_s_own_sign_in_is_not_put_back()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        _agent.RefuseReads();
        AtItsLimit();
        using var signing = SignInMarks.Begin(_account);

        var roster = Roster();
        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None);

        Assert.False(answer.Kept);
        Assert.True(_agent.Read("")!.Cleared);
    }

    /// <summary>
    /// An account whose own reading puts a window at 100% before its reset is not read again by a start's or a turn's read: it can say nothing
    /// new until the reset. An account whose windows have room is read, and a press reads either.
    /// </summary>
    [Fact]
    public async Task A_full_account_is_not_read_again_before_its_reset()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        var roster = Roster();
        AccountWindows.Said(
            _home, "agent", "account-1",
            [new WindowReading("session", 1.0, Start.AddHours(1)), new WindowReading("weekly", 0.6, Start.AddDays(3))],
            Start - TimeSpan.FromMinutes(30), session: null, source: AccountWindows.SourceRead);

        await roster.ReadUsageAsync("agent", "agent", ["account-1"], Config);
        Assert.Empty(UsageAsked());

        AccountWindows.Said(
            _home, "agent", "account-1",
            [new WindowReading("session", 0.5, Start.AddHours(1)), new WindowReading("weekly", 0.6, Start.AddDays(3))],
            Start - TimeSpan.FromMinutes(30), session: null, source: AccountWindows.SourceRead);
        await roster.ReadUsageAsync("agent", "agent", ["account-1"], Config);
        Assert.Equal(["account-1"], UsageAsked());
    }

    private void ClaudeStore(string account, string refresh)
    {
        var home = HarnessSettings.ProfileHome(_home, "claude-code", account);
        Directory.CreateDirectory(home);
        File.WriteAllText(
            Path.Combine(home, ".credentials.json"),
            $$$"""{"claudeAiOauth":{"accessToken":"a-{{{account}}}","refreshToken":"{{{refresh}}}","expiresAt":{{{Start.AddHours(8).ToUnixTimeMilliseconds()}}}}}""");
    }

    /// <summary>
    /// The net beside the two fixes: two accounts whose stores hold one refresh token (a store copied from another sign-in) are said once, each
    /// naming the other, by a one-way fingerprint made inside the provider's reader; accounts with their own tokens, a cleared store and a
    /// token-less one say nothing, and the fingerprint prints no digest.
    /// </summary>
    [Fact]
    public void Two_stores_sharing_a_refresh_token_are_said_once_each_naming_the_other()
    {
        ClaudeStore("account-1", "one-token");
        ClaudeStore("copy", "one-token");
        ClaudeStore("own", "another-token");
        ClaudeStore("empty-1", "");
        ClaudeStore("empty-2", "");
        var store = ProviderRegistry.Built().AgentNamed("claude-code")!.Accounts.StoredSignIn!;

        var shared = SharedSignIns.Of(_home, "claude-code", store);

        Assert.Equal(["copy"], shared["account-1"]);
        Assert.Equal(["account-1"], shared["copy"]);
        Assert.False(shared.ContainsKey("own"));
        Assert.False(shared.ContainsKey("empty-1"));
        var print = store.Fingerprint(HarnessSettings.ProfileHome(_home, "claude-code", "account-1"))!;
        Assert.DoesNotContain("one-token", print.ToString());
        Assert.DoesNotContain(Convert.ToHexString(print.Digest), print.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(store.Fingerprint(HarnessSettings.ProfileHome(_home, "claude-code", "empty-1")));

        // The list says it with the way out.
        var line = AgentAccountLines.SharedWith(["copy"], "claude-code", "account-1", deviceCode: false);
        Assert.Contains("shares its sign-in with `copy`: its store looks copied from another sign-in", line);
        Assert.Contains("`daoris agent login claude-code --account account-1` signs this one in on its own", line);
        Assert.DoesNotContain("--device-code", line);
        Assert.Contains("`daoris agent login codex --account a --device-code` signs this one in on its own, with a code finished on another device",
            AgentAccountLines.SharedWith(["copy"], "codex", "a", deviceCode: true));
        Assert.DoesNotContain("one-token", line);
    }

    /// <summary>A store emptied by a read where the account is not at its limit is no refusal at a limit: it is left, and the account is signed out.</summary>
    [Fact]
    public async Task A_store_emptied_by_a_read_away_from_its_limit_is_left_and_read_signed_out()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Start - TimeSpan.FromHours(1));
        _agent.RefuseReads();
        var roster = Roster();

        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None);

        Assert.False(answer.Kept);
        Assert.True(_agent.Read("")!.Cleared);
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "agent").Accounts["account-1"].Login);
    }

    /// <summary>Only the first emptying in a limit window is put back: a second one in the same window is a real one and reads signed out.</summary>
    [Fact]
    public async Task A_second_emptying_in_one_limit_window_is_left_and_read_signed_out()
    {
        _agent.SignedIn(expires: Start + TimeSpan.FromHours(7));
        AtItsLimit();
        _agent.RefuseReads();
        var roster = Roster();

        var first = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None, pressed: true);
        Assert.True(first.Kept);
        var second = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None, pressed: true);

        Assert.False(second.Kept);
        Assert.True(_agent.Read("")!.Cleared);
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "agent").Accounts["account-1"].Login);
    }

    /// <summary>The person's press renews an expiring account as a start does, so a re-read of one idle past its token's life is not silently refused.</summary>
    [Fact]
    public async Task A_press_on_an_expiring_account_renews_it_and_reads_it()
    {
        _agent.SignedIn(expires: Start - TimeSpan.FromMinutes(1));
        var roster = Roster();

        var answer = await roster.AskOneAsync("agent", roster.AgentOf("agent")!, "agent", "account-1", Config, CancellationToken.None, pressed: true);
        var windows = await roster.AskUsageAsync("agent", "agent", "account-1", Config, CancellationToken.None, pressed: true);

        Assert.True(answer.Asked);
        Assert.Equal(1, _agent.Refreshes);
        Assert.NotNull(windows);
    }

    /// <summary>The roster's report carries the accounts a store shares its refresh token with, and the list says it.</summary>
    [Fact]
    public async Task The_roster_and_the_list_say_a_shared_sign_in()
    {
        ClaudeStore("account-1", "one-token");
        ClaudeStore("copy", "one-token");
        ClaudeStore("own", "another-token");
        var roster = new HarnessRoster(ProviderRegistry.Built(), Settings) { Clock = () => Start };

        var report = (await roster.ReportAsync("claude-code", DriverConfig.Empty))!;

        Assert.Equal(["copy"], report.Profiles.Single(profile => profile.Name == "account-1").SharedWith);
        Assert.Null(report.Profiles.Single(profile => profile.Name == "own").SharedWith);
        var lines = AgentListCommand.AccountLines(
            "claude-code", roster.Adapters.AgentNamed("claude-code")!, report, roster.Settings, _home, roster.Adapters, Start, TimeZoneInfo.Utc);
        Assert.Contains(lines, line => line.Contains("shares its sign-in with `copy`", StringComparison.Ordinal));
        Assert.Equal(1, lines.Count(line => line.Contains("shares its sign-in with `account-1`", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The fake agent: its store in the account's home as a file of the shape the real one has, its token's expiry, and its provider's
    /// single-use refresh. A read that it is told to refuse forces a refresh the provider refuses, and clears the store, as the 07:12:45Z write.
    /// </summary>
    private sealed class FakeAgent(string account) : IStoredSignIn
    {
        private readonly object _gate = new();
        private bool _refusing;
        private int _serial;

        public int Refreshes { get; private set; }

        /// <summary>How many times its status question or windows were asked.</summary>
        public int Asks { get; private set; }

        private string Store => Path.Combine(account, "store.json");

        string? IStoredSignIn.FileName => "store.json";

        /// <summary>The store read as the real reader reads it, sharing writing and deletion, so a rename over it is never held off.</summary>
        private string Shared()
        {
            using var stream = new FileStream(Store, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public byte[] Bytes() => File.ReadAllBytes(Store);

        public void SignedIn(DateTimeOffset expires) => Write("access-0", "refresh-0", expires.ToUnixTimeMilliseconds());

        public void Empty() => Write("", "", 0);

        StoredSignInShape? IStoredSignIn.ReadFile(string path) => Read("");

        public void RefuseReads() => _refusing = true;

        /// <summary>The status question or the windows' read: it refreshes an expiring token to answer; told to refuse, it forces one and is refused.</summary>
        public LoginState Status(DateTimeOffset now)
        {
            lock (_gate)
            {
                Asks++;
                var node = JsonNode.Parse(Shared())!;
                var expires = DateTimeOffset.FromUnixTimeMilliseconds((long)node["expiresAt"]!);
                if (_refusing)
                {
                    Write("", "", 0);
                    return LoginState.Out;
                }

                if (expires - now <= TimeSpan.FromMinutes(5))
                {
                    _serial++;
                    Refreshes++;
                    Write($"access-{_serial}", $"refresh-{_serial}", (now + TimeSpan.FromHours(8)).ToUnixTimeMilliseconds());
                }

                return LoginState.In;
            }
        }

        private void Write(string access, string refresh, long expiresAt) =>
            File.WriteAllText(Store, JsonSerializer.Serialize(new { access, refresh, expiresAt }));

        public StoredSignInShape? Read(string profileHome)
        {
            if (!File.Exists(Store)) return null;
            var node = JsonNode.Parse(Shared())!;
            var expires = (long)node["expiresAt"]!;
            return new StoredSignInShape(
                ((string?)node["access"])?.Length > 0, ((string?)node["refresh"])?.Length > 0,
                expires > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(expires) : null,
                new DateTimeOffset(File.GetLastWriteTimeUtc(Store), TimeSpan.Zero));
        }
    }
}
