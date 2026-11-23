using System.Collections.Concurrent;
using System.Text.Json;
using System.Reflection;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Sessions of one account reopened together after a restart do not spend its single-use refresh token twice, each write
/// to the account's credentials is said in the machine log without a value, and the files of writes that never finished are removed by
/// the provider's rule.
/// </summary>
/// <remarks>
/// The stand-in agent's status question does not refresh (as the real one may not), so the stored token is still expiring when starts go
/// on; a session it starts reads the store and refreshes the token it read, and its provider refuses a refresh with a token already
/// rotated, clearing the store, as Claude Code did on 2026-11-22.
/// </remarks>
public sealed class SignOutRaceTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 22, 18, 8, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-out-race-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _account;

    private readonly FakeAgent _agent;

    public SignOutRaceTests()
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

    private HarnessRoster Roster(TimeSpan stagger) =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = TestDoors.Door("agent", new TestAgent(
                Binary: [Binary],
                VersionArguments: ["--version"],
                ProfileVariable: "READ_HOME",
                LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
                ProbeByPresence: true,
                StoredSignIn: _agent)),
        }), Settings)
        {
            Clock = () => Ten,
            // The status question answers signed in and refreshes nothing: the token is still expiring when the starts go on.
            Asking = (_, _, _) => Task.FromResult(LoginState.In),
            RefreshStagger = stagger,
            RefreshPoll = TimeSpan.FromMilliseconds(20),
        };

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("agent") };

    /// <summary>
    /// The proof the row names: four sessions of one account reopened together after a restart, the stored token expiring and each
    /// session refreshing the token it reads as it starts, make one refresh, and the account stays signed in.
    /// </summary>
    [Fact]
    public async Task Four_sessions_of_one_account_reopened_together_refresh_once_and_stay_signed_in()
    {
        _agent.SignedIn(expires: Ten - TimeSpan.FromMinutes(1));
        var roster = Roster(TimeSpan.FromSeconds(5));

        var starts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            var start = await roster.SelectAsync("agent", Config, null, null);
            // The session's process starts as the start returns: it reads the store and refreshes the token it read.
            _agent.Session(_agent.Held(), Ten);
            return start;
        })).WaitAsync(Poll.Patience);

        Assert.All(starts, start => Assert.Equal("account-1", start.Profile));
        Assert.Equal(1, _agent.Refreshes);
        Assert.False(_agent.Read("")!.Cleared);
    }

    /// <summary>The control: with no pace the four read the one token together, and the account is signed out.</summary>
    [Fact]
    public async Task Without_the_pace_four_sessions_together_sign_the_account_out()
    {
        _agent.SignedIn(expires: Ten - TimeSpan.FromMinutes(1));
        var roster = Roster(TimeSpan.Zero);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => roster.SelectAsync("agent", Config, null, null))).WaitAsync(Poll.Patience);
        var read = Enumerable.Range(0, 4).Select(_ => _agent.Held()).ToList();
        foreach (var held in read) _agent.Session(held, Ten);

        Assert.True(_agent.Read("")!.Cleared);
    }

    /// <summary>A store far from its expiry is no race: the four go at once, with no gap between them.</summary>
    [Fact]
    public async Task A_fresh_store_holds_no_start()
    {
        _agent.SignedIn(expires: Ten + TimeSpan.FromHours(6));
        var roster = Roster(TimeSpan.FromMinutes(5));

        var starts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => roster.SelectAsync("agent", Config, null, null))).WaitAsync(Poll.Patience);

        Assert.All(starts, start => Assert.Equal("account-1", start.Profile));
    }

    /// <summary>A write of the store in flight (a fresh temp file) is waited out, and the start goes on when it is gone.</summary>
    [Fact]
    public async Task A_start_waits_while_a_write_of_the_store_is_in_flight()
    {
        _agent.SignedIn(expires: Ten + TimeSpan.FromHours(6));
        var temp = Path.Combine(_account, "store.json.tmp.7");
        File.WriteAllText(temp, "");
        File.SetLastWriteTimeUtc(temp, Ten.UtcDateTime);
        var roster = Roster(TimeSpan.FromSeconds(5));

        var start = roster.SelectAsync("agent", Config, null, null);
        await Assert.ThrowsAsync<TimeoutException>(() => start.WaitAsync(TimeSpan.FromMilliseconds(400)));

        File.Delete(temp);
        Assert.Equal("account-1", (await start.WaitAsync(Poll.Patience)).Profile);
    }

    /// <summary>A slot reached between two reads of the clock is no wait at all: never a zero, negative or infinite delay.</summary>
    [Theory]
    [InlineData(1000L, 990L, 10L, 10L)]
    [InlineData(1000L, 995L, 10L, 5L)]
    [InlineData(1000L, 1000L, 10L, null)]
    [InlineData(1000L, 1001L, 10L, null)]
    [InlineData(1000L, 1002L, 10L, null)]
    public void A_slot_that_has_come_is_not_waited_for(long slot, long now, long poll, long? expected) =>
        Assert.Equal(
            expected is { } ms ? TimeSpan.FromMilliseconds(ms) : null,
            RefreshPace.Pause(slot, now, TimeSpan.FromMilliseconds(poll)));

    /// <summary>The watch is armed in the look's first phase, before any start is planned or begun, so a start's first refresh is said.</summary>
    [Fact]
    public void The_watch_is_armed_before_the_look_plans_a_start()
    {
        Assert.Equal(LookPhase.Before, typeof(CredentialWritesLook).GetCustomAttribute<LookPassAttribute>()!.Phase);
    }

    private HarnessRoster Claude(out string account)
    {
        account = HarnessSettings.ProfileHome(_home, "claude-code", "account-1");
        Directory.CreateDirectory(account);
        return new HarnessRoster(ProviderRegistry.Built(), Settings) { Clock = () => Ten };
    }

    /// <summary>
    /// Each write to the store and to a temp file beside it is said: its file, the change, whether it is empty and whether each token
    /// is there, and the sessions running on the account then; no line holds a token's value, and the first says the watch began.
    /// </summary>
    [Fact]
    public async Task Each_write_to_a_claude_store_is_said_without_a_value()
    {
        var roster = Claude(out var account);
        roster._looked = [("claude-code", new SessionStarted("claude-code", "account-1", null, Running: true) { Id = "s-running" })];
        var lines = new ConcurrentQueue<AccountLine>();
        using var writes = new CredentialWrites(roster, lines.Enqueue);
        writes.Follow();
        writes.Follow();
        Assert.Equal(1, lines.Count(line => line.Event == "credentials.watching"));

        // The agent's write: a temp file, then a rename over the store; the tokens empty, as when a refresh is refused.
        var temp = Path.Combine(account, ".credentials.json.tmp.4242");
        await File.WriteAllTextAsync(temp, """{"claudeAiOauth":{"accessToken":"","refreshToken":"","expiresAt":0}}""");
        File.Move(temp, Path.Combine(account, ".credentials.json"), overwrite: true);
        await Poll.Until(
            () => Written(lines).Any(line => Value(line, "file") is "store" && Value(line, "change") is "renamed"),
            () => string.Join("; ", Written(lines).Select(line => $"{Value(line, "file")}/{Value(line, "change")}")));

        var store = Written(lines).First(line => Value(line, "file") is "store" && Value(line, "change") is "renamed");
        Assert.Equal(("claude-code", "account-1", false, false, false), (Value(store, "door"), Value(store, "account"), Value(store, "empty"), Value(store, "access"), Value(store, "refresh")));
        Assert.Equal((1, "s-running"), (Value(store, "running"), Value(store, "sessions")));
        Assert.Contains(Written(lines), line => Value(line, "file") is "temp" && Value(line, "change") is "created");

        // A write that held tokens, and one with no bytes: said as what they are, and no value in any line.
        await File.WriteAllTextAsync(
            Path.Combine(account, ".credentials.json.tmp.1"), """{"claudeAiOauth":{"accessToken":"SECRET-ACCESS","refreshToken":"SECRET-REFRESH","expiresAt":1}}""");
        await File.WriteAllTextAsync(Path.Combine(account, ".credentials.json.tmp.2"), "");
        await Poll.Until(
            () => Written(lines).Any(line => Value(line, "file") is "temp" && Value(line, "access") is true)
                  && Written(lines).Any(line => Value(line, "file") is "temp" && Value(line, "empty") is true));

        foreach (var line in lines)
        {
            Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(line.Data.Select(each => each.Value)), StringComparison.Ordinal);
        }
    }

    /// <summary>Reading a store never shuts it: one the agent still has open for writing reads, and renames over while read.</summary>
    [Fact]
    public void Reading_a_store_does_not_hold_it_shut_against_its_writer()
    {
        Claude(out var account);
        var reader = ProviderRegistry.Built().AgentNamed("claude-code")!.Accounts.StoredSignIn!;
        var temp = Path.Combine(account, ".credentials.json.tmp.9");
        using var writer = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write("""{"claudeAiOauth":{"accessToken":"a","refreshToken":"r","expiresAt":1}}"""u8);
        writer.Flush();

        Assert.True(reader.ReadFile(temp)!.Access);
        File.Move(temp, Path.Combine(account, ".credentials.json"));
        Assert.True(reader.Read(account)!.Refresh);
    }

    private static IEnumerable<AccountLine> Written(ConcurrentQueue<AccountLine> lines) => lines.Where(line => line.Event == "credentials.written");

    private static object? Value(AccountLine line, string key) => line.Data.First(each => each.Key == key).Value;

    /// <summary>The store's own rule: a temp file older than a write takes is a leftover and goes, an empty one too; a young one, the store and any other file stay.</summary>
    [Fact]
    public void A_look_removes_the_leftover_temp_files_by_the_stores_rule()
    {
        var roster = Claude(out var account);
        var old = Ten.UtcDateTime - TimeSpan.FromHours(3);
        foreach (var name in new[] { ".credentials.json.tmp.1", ".credentials.json.tmp.2" })
        {
            var path = Path.Combine(account, name);
            File.WriteAllText(path, name.EndsWith('1') ? """{"claudeAiOauth":{"accessToken":"","refreshToken":"","expiresAt":0}}""" : "");
            File.SetLastWriteTimeUtc(path, old);
        }

        File.WriteAllText(Path.Combine(account, ".credentials.json.tmp.young"), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(account, ".credentials.json.tmp.young"), Ten.UtcDateTime);
        File.WriteAllText(Path.Combine(account, ".credentials.json"), "{}");
        File.WriteAllText(Path.Combine(account, "notes.tmp.1"), "x");
        File.SetLastWriteTimeUtc(Path.Combine(account, "notes.tmp.1"), old);

        Assert.Equal(2, StoredLeftovers.Sweep(roster));

        Assert.Equal(
            [".credentials.json", ".credentials.json.tmp.young", "notes.tmp.1"],
            Directory.GetFiles(account).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(0, StoredLeftovers.Sweep(roster));
    }

    /// <summary>The stand-in agent: its store in the account's home, and its provider, whose refresh token is single-use.</summary>
    private sealed class FakeAgent(string account) : IStoredSignIn
    {
        private readonly object _gate = new();
        private string _issued = "refresh-0";
        private int _serial;

        public int Refreshes { get; private set; }

        private string Store => Path.Combine(account, "store.json");

        public string? FileName => "store.json";

        public string? LeftoverPattern => "store.json.tmp.*";

        public void SignedIn(DateTimeOffset expires)
        {
            lock (_gate) Write("access-0", "refresh-0", expires.ToUnixTimeMilliseconds());
        }

        public (string Refresh, DateTimeOffset Expires) Held()
        {
            lock (_gate)
            {
                var node = JsonNode.Parse(File.ReadAllText(Store))!;
                return ((string)node["refresh"]!, DateTimeOffset.FromUnixTimeMilliseconds((long)node["expiresAt"]!));
            }
        }

        /// <summary>A session starting: it renews the token it read where it finds it expiring.</summary>
        public void Session((string Refresh, DateTimeOffset Expires) held, DateTimeOffset now)
        {
            if (held.Expires - now > TimeSpan.FromMinutes(5)) return;
            lock (_gate)
            {
                if (held.Refresh != _issued)
                {
                    Write("", "", 0);
                    return;
                }

                _serial++;
                _issued = $"refresh-{_serial}";
                Refreshes++;
                Write($"access-{_serial}", _issued, (now + TimeSpan.FromHours(8)).ToUnixTimeMilliseconds());
            }
        }

        private void Write(string access, string refresh, long expiresAt) =>
            File.WriteAllText(Store, JsonSerializer.Serialize(new { access, refresh, expiresAt }));

        public StoredSignInShape? Read(string profileHome)
        {
            JsonNode node;
            lock (_gate)
            {
                if (!File.Exists(Store)) return null;
                node = JsonNode.Parse(File.ReadAllText(Store))!;
            }

            var expires = (long)node["expiresAt"]!;
            return new StoredSignInShape(
                ((string?)node["access"])?.Length > 0, ((string?)node["refresh"])?.Length > 0,
                expires > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(expires) : null,
                new DateTimeOffset(File.GetLastWriteTimeUtc(Store), TimeSpan.Zero));
        }
    }
}
