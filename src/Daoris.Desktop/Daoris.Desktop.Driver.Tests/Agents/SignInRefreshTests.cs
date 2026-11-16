using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Sessions on one account never race its sign-in's refresh (CREDRACE1): Daoris renews the sign-in once, under the account's lock,
/// before starts on it go on together, and a store its agent cleared is read signed out at the next look.
/// </summary>
/// <remarks>
/// The agent is a fake whose provider rotates the refresh token, single-use: a refresh with a token already rotated is refused, and
/// the agent clears the account's store, as Claude Code did on 2026-11-16. Its status question, the roster's stand-in, renews an
/// expiring token as the real one does to answer; a session it starts renews one it finds expiring with the token it read.
/// </remarks>
public sealed class SignInRefreshTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-in-refresh-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeAgent _agent;

    public SignInRefreshTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Binary, "");
        var account = HarnessSettings.ProfileHome(_home, "agent", "account-1");
        Directory.CreateDirectory(account);
        _agent = new FakeAgent(account);
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Binary => Path.Combine(_home, "agent-binary");

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = TestDoors.Door("agent", new TestAgent(
                Binary: [Binary],
                VersionArguments: ["--version"],
                ProfileVariable: "READ_HOME",
                LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
                ProbeByPresence: true,
                StoredSignIn: _agent)),
        }), Settings) { Clock = () => Ten, Asking = (_, _, _) => Task.FromResult(_agent.Status(Ten)) };

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("agent") };

    /// <summary>
    /// The proof the row names: the store's access token expired, two starts on the one account together make one refresh, and the
    /// two sessions they start, each reading the store as it starts, find it fresh and stay signed in.
    /// </summary>
    [Fact]
    public async Task Two_starts_on_one_account_make_one_refresh_and_stay_signed_in()
    {
        _agent.SignedIn(expires: Ten - TimeSpan.FromMinutes(1));
        var roster = Roster();

        var starts = await Task.WhenAll(roster.SelectAsync("agent", Config, null, null), roster.SelectAsync("agent", Config, null, null));

        Assert.All(starts, start => Assert.Equal("account-1", start.Profile));
        Assert.Equal(1, _agent.Refreshes);

        // The two sessions start together: each reads the store, then renews the token it read where it finds it expiring.
        var read = new[] { _agent.Held(), _agent.Held() };
        foreach (var held in read) _agent.Session(held, Ten);

        Assert.False(_agent.Read("")!.Cleared);
        Assert.Equal(1, _agent.Refreshes);
    }

    /// <summary>A token far from its expiry is not renewed: a start asks nothing.</summary>
    [Fact]
    public async Task A_fresh_token_is_not_renewed()
    {
        _agent.SignedIn(expires: Ten + TimeSpan.FromHours(6));

        var start = await Roster().SelectAsync("agent", Config, null, null);

        Assert.Equal("account-1", start.Profile);
        Assert.Equal(0, _agent.Refreshes);
    }

    /// <summary>
    /// A store its agent cleared is read at the next look: the account reads signed out, through the one sign-in state, as one
    /// nobody at Daoris signed out, and the walk passes it.
    /// </summary>
    [Fact]
    public async Task A_cleared_store_is_read_signed_out_at_the_next_look()
    {
        // Read signed in before the store is written: the store's moment is the file's, on this machine's clock, after this one.
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromHours(1));
        _agent.SignedIn(expires: Ten - TimeSpan.FromMinutes(1));
        _agent.Refuse();
        var roster = Roster();

        Assert.Equal(1, StoredSignIns.Read(roster));
        Assert.Equal(0, StoredSignIns.Read(roster));

        var state = SignInStates.Of(AccountReads.Of(_home, "agent").Accounts["account-1"]);
        Assert.Equal((SignInStanding.Out, true), (state.Standing, state.NotByPerson));
        // The notice as the look makes it at the moment the store read signed out.
        Assert.Single(SignOutNotices.Occurrences(_home, state.Since!.Value));
        Assert.False((await roster.SelectAsync("agent", Config, null, null)).Allowed);
    }

    /// <summary>
    /// Claude Code's store read for its shape alone, as the 2026-11-16 file was: both tokens there with their expiry, then both
    /// emptied and the expiry 0, which is a cleared sign-in. The shape holds no text, so no token can reach a log or a page.
    /// </summary>
    [Fact]
    public void Claude_Code_s_store_is_read_for_its_shape_alone()
    {
        var home = HarnessSettings.ProfileHome(_home, "claude-code", "account-1");
        Directory.CreateDirectory(home);
        var store = Path.Combine(home, ".credentials.json");
        var expires = Ten + TimeSpan.FromHours(8);
        var reader = ProviderRegistry.Built().AgentNamed("claude-code")!.Accounts.StoredSignIn!;

        File.WriteAllText(store, $$$"""{"claudeAiOauth":{"accessToken":"a-value","refreshToken":"r-value","expiresAt":{{{expires.ToUnixTimeMilliseconds()}}},"scopes":[]}}""");
        Assert.Equal((true, true, expires, false), Shape(reader.Read(home)!));

        File.WriteAllText(store, """{"claudeAiOauth":{"accessToken":"","refreshToken":"","expiresAt":0}}""");
        Assert.Equal((false, false, (DateTimeOffset?)null, true), Shape(reader.Read(home)!));

        File.WriteAllText(store, "not json");
        Assert.Null(reader.Read(home));
        Assert.DoesNotContain(typeof(StoredSignInShape).GetProperties(), property => property.PropertyType == typeof(string));

        static (bool, bool, DateTimeOffset?, bool) Shape(StoredSignInShape shape) => (shape.Access, shape.Refresh, shape.ExpiresAt, shape.Cleared);
    }

    /// <summary>
    /// The fake agent: its store in the account's home (a token's presence and its expiry, as the real store's shape is read), and
    /// its provider, whose refresh token is single-use.
    /// </summary>
    private sealed class FakeAgent(string account) : IStoredSignIn
    {
        private readonly object _gate = new();
        private string _issued = "refresh-0";
        private int _serial;

        public int Refreshes { get; private set; }

        private string Store => Path.Combine(account, "store.json");

        public void SignedIn(DateTimeOffset expires) => Write("access-0", "refresh-0", expires.ToUnixTimeMilliseconds());

        /// <summary>The token the store holds and its expiry, as a session reads them when it starts.</summary>
        public (string Refresh, DateTimeOffset Expires) Held()
        {
            var node = JsonNode.Parse(File.ReadAllText(Store))!;
            return ((string)node["refresh"]!, DateTimeOffset.FromUnixTimeMilliseconds((long)node["expiresAt"]!));
        }

        /// <summary>A session starting: it renews the token it read where it finds it expiring.</summary>
        public void Session((string Refresh, DateTimeOffset Expires) held, DateTimeOffset now)
        {
            if (held.Expires - now <= TimeSpan.FromMinutes(5)) Refresh(held.Refresh, now);
        }

        /// <summary>The status question: it renews an expiring token to answer, under the lock its asker holds.</summary>
        public LoginState Status(DateTimeOffset now)
        {
            var held = Held();
            if (held.Expires - now <= TimeSpan.FromMinutes(5) && !Refresh(held.Refresh, now)) return LoginState.Out;
            return LoginState.In;
        }

        /// <summary>Another session renewed first: this one's refresh with the token it read is refused, and the store cleared.</summary>
        public void Refuse()
        {
            lock (_gate) _issued = "rotated-elsewhere";
            Refresh(Held().Refresh, Ten);
        }

        private bool Refresh(string presented, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (presented != _issued)
                {
                    // Refused: the agent clears the account's store.
                    Write("", "", 0);
                    return false;
                }

                _serial++;
                _issued = $"refresh-{_serial}";
                Refreshes++;
                Write($"access-{_serial}", _issued, (now + TimeSpan.FromHours(8)).ToUnixTimeMilliseconds());
                return true;
            }
        }

        private void Write(string access, string refresh, long expiresAt) =>
            File.WriteAllText(Store, JsonSerializer.Serialize(new { access, refresh, expiresAt }));

        public StoredSignInShape? Read(string profileHome)
        {
            if (!File.Exists(Store)) return null;
            var node = JsonNode.Parse(File.ReadAllText(Store))!;
            var expires = (long)node["expiresAt"]!;
            return new StoredSignInShape(
                ((string?)node["access"])?.Length > 0, ((string?)node["refresh"])?.Length > 0,
                expires > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(expires) : null,
                new DateTimeOffset(File.GetLastWriteTimeUtc(Store), TimeSpan.Zero));
        }
    }
}
