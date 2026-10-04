using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4d (D125 §2.3): an account's cool-off, kept per account in <c>cooling.json</c> under the home, written when a
/// limit is read and read before any probe, so a start on a spent account is held with nothing spawned.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the file is read and written, and the roster's selection is asked of a harness
/// whose presence is a PATH look (<see cref="HarnessToolchain.ProbeByPresence"/>) for a command no machine has, so a
/// hold that came before the probe reads differently from the probe's own refusal.
/// </remarks>
public sealed class AccountCoolingTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    /// <summary>Observation 4's moment, in the test's zone: 1 October, 14:00.</summary>
    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>Observation 4's sentence on the protocol door, its zone the test's.</summary>
    private static readonly string Refusal =
        "the ACP agent refused the call: You've hit your individual spend limit · run /usage-credits to ask your admin "
        + $"for a higher limit · your weekly limit resets Oct 3, 4pm ({Zone.Id})";

    /// <summary>What observation 4's reset reads as: 3 October, 16:00 in the test's zone, plus the margin.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    /// <summary>A command no machine has on its PATH, so a probe that is reached says the harness is not installed.</summary>
    private const string Nowhere = "daoris-tool4d-no-such-command";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-cooling-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    public AccountCoolingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private static CoolingEntry Entry(
        string agent = "claude-code", string? account = "account-1", DateTimeOffset? until = null, bool stated = true,
        string? window = "weekly", bool assumedZone = false, bool notBelieved = false) =>
        new(agent, account, until ?? Until, stated, window, Seen, "3f9c2a71", assumedZone, notBelieved);

    private JsonObject File() => JsonNode.Parse(System.IO.File.ReadAllText(AccountCooling.PathOf(_home)))!.AsObject();

    // ——— The file (§2.3).

    [Fact]
    public void A_limit_writes_the_account_s_cool_off_under_the_home_with_no_words_and_no_key()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(account: null, stated: false, window: null), _now);

        Assert.Equal(Path.Combine(_home, "cooling.json"), AccountCooling.PathOf(_home));
        var written = File();
        var named = written["claude-code"]!["account-1"]!.AsObject();
        Assert.Equal("2026-10-03T10:17:00Z", named["until"]!.GetValue<string>());
        Assert.True(named["stated"]!.GetValue<bool>());
        Assert.Equal("weekly", named["window"]!.GetValue<string>());
        Assert.Equal("2026-10-01T08:15:00Z", named["seen"]!.GetValue<string>());
        Assert.Equal("3f9c2a71", named["session"]!.GetValue<string>());
        Assert.Equal(["until", "stated", "window", "seen", "session"], named.Select(field => field.Key));

        // The tool's own configuration home is `""`, AGT3b's key; a window it did not name is not written.
        var own = written["claude-code"]![""]!.AsObject();
        Assert.False(own["stated"]!.GetValue<bool>());
        Assert.False(own.ContainsKey("window"));
        Assert.DoesNotContain("hit your", System.IO.File.ReadAllText(AccountCooling.PathOf(_home)));
    }

    [Fact]
    public void What_the_reader_gives_back_is_what_was_written()
    {
        AccountCooling.Cool(_home, Entry(assumedZone: true), _now);
        AccountCooling.Cool(_home, Entry(agent: "codex", account: null, stated: false, window: null, notBelieved: true), _now);

        Assert.Equal(Entry(assumedZone: true), AccountCooling.Of(_home, "claude-code", "account-1", _now));
        Assert.Equal(
            Entry(agent: "codex", account: null, stated: false, window: null, notBelieved: true),
            AccountCooling.Of(_home, "codex", null, _now));
        Assert.Equal(2, AccountCooling.Read(_home, _now).Count);
        // Names compare as the wiring compares them, without case.
        Assert.NotNull(AccountCooling.Of(_home, "Claude-Code", "ACCOUNT-1", _now));
        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-2", _now));
        Assert.Null(AccountCooling.Of(_home, "claude-code", null, _now));
    }

    [Fact]
    public void A_later_limit_on_the_same_account_replaces_its_entry()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        var later = Entry(until: Until.AddDays(-1), window: "session");
        AccountCooling.Cool(_home, later, _now);

        Assert.Equal(later, AccountCooling.Of(_home, "claude-code", "account-1", _now));
        Assert.Single(File()["claude-code"]!.AsObject());
    }

    [Fact]
    public void An_entry_whose_reset_has_passed_is_ready_and_the_next_write_drops_it()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        var after = Until.AddMinutes(1);

        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-1", after));
        Assert.Empty(AccountCooling.Read(_home, after));

        AccountCooling.Cool(_home, Entry(agent: "codex", until: after.AddHours(1)), after);
        Assert.False(File().ContainsKey("claude-code"));
        Assert.NotNull(AccountCooling.Of(_home, "codex", "account-1", after));
    }

    /// <summary>
    /// TOOL6e: a cool-off that ended within the last day, which the file still holds, says when its account was offered
    /// again; one still cooling, one that ended longer ago, and one a later write dropped say nothing.
    /// </summary>
    [Fact]
    public void A_cool_off_that_ended_within_the_day_says_when_its_account_was_offered_again_while_the_file_holds_it()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(account: null, until: Until.AddHours(-30)), _now);
        AccountCooling.Cool(_home, Entry(account: "account-2", until: Until.AddHours(3)), _now);
        var after = Until.AddHours(2);

        var offered = Assert.Single(AccountCooling.Offered(_home, after));
        Assert.Equal(Entry(), offered);
        Assert.Empty(AccountCooling.Offered(_home, Until.AddMinutes(-1)).Where(entry => entry.Account == "account-1"));
        Assert.Empty(AccountCooling.Offered(_home, Until.AddHours(3) + AccountCooling.OfferedFor + TimeSpan.FromMinutes(1)));

        AccountCooling.Cool(_home, Entry(agent: "codex", until: after.AddHours(1)), after);
        Assert.Empty(AccountCooling.Offered(_home, after));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "claude-code": { "account-1": { "stated": true } } }""")]
    [InlineData("""{ "claude-code": { "account-1": { "until": "Oct 3" } } }""")]
    [InlineData("""{ "claude-code": [1] }""")]
    public void Missing_or_unreadable_is_no_account_cooling(string? text)
    {
        if (text is not null) System.IO.File.WriteAllText(AccountCooling.PathOf(_home), text);

        Assert.Empty(AccountCooling.Read(_home, _now));
        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-1", _now));
    }

    [Fact]
    public void A_write_over_an_unreadable_file_starts_it_again()
    {
        System.IO.File.WriteAllText(AccountCooling.PathOf(_home), "not json");

        AccountCooling.Cool(_home, Entry(), _now);

        Assert.Equal(Entry(), AccountCooling.Of(_home, "claude-code", "account-1", _now));
    }

    [Fact]
    public void A_writer_keeps_what_it_has_no_field_for()
    {
        System.IO.File.WriteAllText(AccountCooling.PathOf(_home), """
            { "codex": { "": { "until": "2026-10-05T00:00:00Z", "stated": false, "seen": "2026-10-01T00:00:00Z",
                               "session": "s9", "later": "kept" } },
              "claude-code": { "account-2": { "until": "2026-10-05T00:00:00Z", "stated": true, "seen": "2026-10-01T00:00:00Z" } } }
            """);

        AccountCooling.Cool(_home, Entry(), _now);

        var written = File();
        Assert.Equal("kept", written["codex"]![""]!["later"]!.GetValue<string>());
        Assert.NotNull(written["claude-code"]!["account-2"]);
        Assert.NotNull(written["claude-code"]!["account-1"]);
    }

    [Fact]
    public void Ending_a_cool_off_takes_that_account_and_no_other()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(account: "account-2"), _now);

        Assert.True(AccountCooling.End(_home, "claude-code", "account-1", _now));
        Assert.False(AccountCooling.End(_home, "claude-code", "account-1", _now));
        Assert.False(AccountCooling.End(_home, "codex", null, _now));

        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-1", _now));
        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-2", _now));
    }

    [Fact]
    public void Ending_the_own_homes_ends_every_agent_s_own_sign_in_and_no_named_account()
    {
        AccountCooling.Cool(_home, Entry(account: null), _now);
        AccountCooling.Cool(_home, Entry(agent: "codex", account: null), _now);
        AccountCooling.Cool(_home, Entry(), _now);

        Assert.Equal(2, AccountCooling.EndOwnHomes(_home, _now));

        Assert.Null(AccountCooling.Of(_home, "claude-code", null, _now));
        Assert.Null(AccountCooling.Of(_home, "codex", null, _now));
        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-1", _now));
    }

    // ——— What ends one early (§2.3): a sign-in or a key into that account.

    [Fact]
    public void A_sign_in_into_an_account_s_directory_ends_that_account_s_cool_off()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(account: "account-2"), _now);

        Assert.True(AccountCooling.SignedIn(HarnessSettings.ProfileHome(_home, "claude-code", "account-1"), _now));

        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-1", _now));
        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-2", _now));
    }

    [Fact]
    public void A_sign_in_anywhere_else_ends_nothing()
    {
        AccountCooling.Cool(_home, Entry(), _now);

        Assert.False(AccountCooling.SignedIn(Path.Combine(_home, "elsewhere", "claude-code", "account-1"), _now));
        Assert.False(AccountCooling.SignedIn(_home, _now));

        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-1", _now));
    }

    [Fact]
    public void A_key_made_into_an_account_ends_a_cool_off_its_name_still_carried()
    {
        AccountCooling.Cool(_home, Entry(agent: "fake", account: "account-1", until: DateTimeOffset.UtcNow.AddDays(2)), DateTimeOffset.UtcNow);

        var account = HarnessKeys.Add(_home, "fake", "sk-test-0000-wxyz");

        Assert.Equal("account-1", account);
        Assert.Null(AccountCooling.Of(_home, "fake", "account-1", DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// TOOL4e: an account removed takes its cool-off with it, as the CLI's <c>profile remove</c> does — the next account
    /// made takes the first free name, which may be this one's, and must not start out cooling.
    /// </summary>
    [Fact]
    public void Removing_an_account_ends_its_cool_off_and_no_other()
    {
        var now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "account-1"));
        AccountCooling.Cool(_home, Entry(until: now.AddDays(2)), now);
        AccountCooling.Cool(_home, Entry(account: "account-2", until: now.AddDays(2)), now);
        AccountCooling.Cool(_home, Entry(account: "account-3", until: now.AddDays(2)), now);

        Assert.True(HarnessSettings.RemoveProfile(_home, "claude-code", "account-1"));
        Assert.False(HarnessSettings.RemoveProfile(_home, "claude-code", "account-3"));

        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-1", now));
        Assert.Null(AccountCooling.Of(_home, "claude-code", "account-3", now));
        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-2", now));
    }

    // ——— The words (§2.4, §4): whose account, until when in the machine's zone, and why.

    [Fact]
    public void The_hold_says_whose_account_until_when_and_that_the_agent_said_so()
    {
        Assert.Equal(
            $"the `claude-code` account `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said. "
            + "Daoris starts nothing on it until then.",
            CoolingWords.Hold(Entry(), Zone));
    }

    [Fact]
    public void The_own_home_s_hold_says_a_sign_in_at_the_terminal_may_have_moved_it()
    {
        var said = CoolingWords.Hold(Entry(account: null), Zone);

        Assert.StartsWith($"`claude-code`'s own sign-in is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said.", said);
        // UX6e2: opening the Agents place asks nothing, so the refresh that ends the cool-off is its Read again.
        Assert.EndsWith(
            "If you have signed in to another account at your own terminal since, press Read again under Agents → the agent's "
            + "page → Accounts.",
            said);
    }

    /// <summary>The CLI's <c>cooling.test.ts</c> parses this theory: <c>daoris agent list</c> says why in the same words (TOOL4e).</summary>
    [Theory]
    [InlineData(true, false, false, "as the agent said")]
    [InlineData(false, false, false, "Daoris's default: the agent named no time")]
    [InlineData(false, false, true, "Daoris's default: the agent named a date more than 8 days off")]
    [InlineData(true, true, false, "as the agent said, in this machine's zone")]
    public void Why_says_whether_the_agent_named_the_time(bool stated, bool assumed, bool notBelieved, string why)
    {
        Assert.Contains(
            $"until Oct 3, 16:02 ({Zone.Id}), {why}.",
            CoolingWords.Hold(Entry(stated: stated, assumedZone: assumed, notBelieved: notBelieved), Zone));
    }

    [Fact]
    public void The_record_s_note_names_no_account_since_the_note_travels()
    {
        var note = CoolingWords.Note(Entry(), Zone);

        Assert.Equal(
            $"The account it ran on is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said, and nothing starts on it "
            + "until then.",
            note);
        Assert.DoesNotContain("account-1", note);
        Assert.DoesNotContain("claude-code", note);
    }

    // ——— The roster: read before any probe (§3.3, §4).

    private sealed class Adapter(string name, HarnessToolchain? toolchain, SessionWire wire = SessionWire.Pipe) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>A harness whose presence is a PATH look, so asking it starts nothing.</summary>
    private static HarnessToolchain Present(string? accountOf = null, LimitWords? limits = null) => new(
        Binary: [Nowhere], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
        AccountOf: accountOf, Limits: limits);

    private HarnessRoster Roster(params ISessionAdapter[] adapters) =>
        new(new AdapterSet(adapters.ToDictionary(a => a.Name, a => a, StringComparer.OrdinalIgnoreCase)), Settings)
        {
            Clock = () => _now,
            Zone = Zone,
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    [Fact]
    public async Task A_start_on_a_cooling_account_is_held_before_any_probe()
    {
        var roster = Roster(new Adapter("fake", Present()));
        AccountCooling.Cool(_home, Entry(agent: "fake", account: null), _now);

        var held = await roster.SelectAsync("fake", Config, null, null);

        Assert.False(held.Allowed);
        Assert.Equal(CoolingWords.Hold(Entry(agent: "fake", account: null), Zone), held.Refusal);
        Assert.Equal(Entry(agent: "fake", account: null), held.Cooling);

        // Once it is ready the selection goes on to the probe, which finds nothing on this PATH.
        Assert.True(roster.Ready("fake", null));
        var probed = await roster.SelectAsync("fake", Config, null, null);
        Assert.Null(probed.Cooling);
        Assert.Contains("is not installed on this machine", probed.Refusal);
    }

    [Fact]
    public async Task A_named_account_s_cool_off_holds_starts_on_it_and_not_on_another_account()
    {
        new HarnessSettings().WithDefault("fake", "account-1").WithWorkspaceDefault("work", "fake", "account-2").Save(Settings);
        var roster = Roster(new Adapter("fake", Present()));
        AccountCooling.Cool(_home, Entry(agent: "fake"), _now);

        var machine = await roster.SelectAsync("fake", Config, null, null);
        var work = await roster.SelectAsync("fake", Config, "work", null);
        var picked = await roster.SelectAsync("fake", Config, "work", "account-1");

        Assert.Equal(CoolingWords.Hold(Entry(agent: "fake"), Zone), machine.Refusal);
        Assert.Null(work.Cooling);
        Assert.Equal(Entry(agent: "fake"), picked.Cooling);
    }

    [Fact]
    public async Task A_cool_off_ends_at_its_reset_with_nothing_said()
    {
        var roster = Roster(new Adapter("fake", Present()));
        AccountCooling.Cool(_home, Entry(agent: "fake", account: null), _now);

        _now = Until.AddSeconds(1);

        Assert.Null((await roster.SelectAsync("fake", Config, null, null)).Cooling);
        Assert.False(roster.Ready("fake", null));
    }

    [Fact]
    public async Task A_door_s_cool_off_is_its_owner_s_and_so_are_its_limits()
    {
        var roster = Roster(
            new Adapter("fake", Present(limits: ClaudeLimits.Words)),
            new Adapter("fake-acp", Present(accountOf: "fake"), SessionWire.Acp));

        Assert.Same(ClaudeLimits.Words, roster.LimitsOf("fake-acp"));
        var limited = roster.Limited("fake-acp", null, Refusal, "s1");

        Assert.NotNull(limited);
        Assert.Equal(("fake", null, Until, true), (limited.Value.Entry.Agent, limited.Value.Entry.Account, limited.Value.Entry.Until, limited.Value.Entry.Stated));
        Assert.Equal("individual spend", limited.Value.Seen.Hit);
        // One account, spent: neither way onto it starts.
        Assert.NotNull((await roster.SelectAsync("fake", Config, null, null)).Cooling);
        Assert.NotNull((await roster.SelectAsync("fake-acp", Config, null, null)).Cooling);
    }

    [Fact]
    public void A_failure_no_table_recognises_cools_nothing()
    {
        var roster = Roster(new Adapter("fake", Present(limits: ClaudeLimits.Words)), new Adapter("other", Present()));

        Assert.Null(roster.Limited("fake", null, "the ACP agent refused the call: Internal error: Overloaded", "s1"));
        Assert.Null(roster.Limited("other", null, Refusal, "s1"));
        Assert.Null(roster.Limited("fake", null, null, "s1"));
        Assert.Empty(AccountCooling.Read(_home, _now));
    }

    [Fact]
    public void A_limit_s_reset_is_read_at_the_roster_s_moment_in_the_roster_s_zone()
    {
        var roster = Roster(new Adapter("fake", Present(limits: ClaudeLimits.Words)));

        var limited = roster.Limited("fake", "account-1", Refusal.Replace($" ({Zone.Id})", ""), "s1");

        Assert.Equal(Until, limited!.Value.Entry.Until);
        Assert.True(limited.Value.Entry.AssumedZone);
        Assert.Equal(Seen, limited.Value.Entry.Seen);
        Assert.Equal("s1", limited.Value.Entry.Session);
        Assert.Equal(limited.Value.Entry, AccountCooling.Of(_home, "fake", "account-1", _now));
    }

    /// <summary>TOOL4e: the default a limit naming no time takes is the one the caller hands over, the machine's <c>cooloff</c>.</summary>
    [Fact]
    public void A_limit_that_names_no_time_cools_for_the_cool_off_handed_over_and_an_hour_without_one()
    {
        var roster = Roster(new Adapter("fake", Present(limits: ClaudeLimits.Words)));
        const string noTime = "the ACP agent refused the call: Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit";

        var handed = roster.Limited("fake", "account-1", noTime, "s1", TimeSpan.FromMinutes(15));
        var none = roster.Limited("fake", "account-2", noTime, "s2");

        Assert.Equal((Seen.AddMinutes(15), false), (handed!.Value.Entry.Until, handed.Value.Entry.Stated));
        Assert.Equal(Seen + AccountLimits.DefaultCoolOff, none!.Value.Entry.Until);
        // A time the agent named is read, whatever the default.
        Assert.Equal(Until, roster.Limited("fake", "account-3", Refusal, "s3", TimeSpan.FromMinutes(15))!.Value.Entry.Until);
    }

    /// <summary>
    /// The roster's refresh ends the tool's own home's cool-off, since a sign-in there happens at the person's own
    /// terminal, where Daoris does not see it (§3.7); a named account's reset is a stated fact, and stands.
    /// </summary>
    [Fact]
    public async Task The_roster_s_refresh_ends_the_own_home_s_cool_off_and_no_named_account_s()
    {
        var roster = Roster(new Adapter("fake", Present()));
        AccountCooling.Cool(_home, Entry(agent: "fake", account: null), _now);
        AccountCooling.Cool(_home, Entry(agent: "fake"), _now);

        await roster.RosterAsync(Config with { Commands = new Dictionary<string, IReadOnlyList<string>> { ["fake"] = [Nowhere] } }, refresh: true);

        Assert.Null(roster.CoolingOf("fake", null));
        Assert.NotNull(roster.CoolingOf("fake", "account-1"));
    }

    // ——— The protocol stub (TOOL4j, D125 §1.3 rule 4): a door onto the stub, as Claude Code's protocol door is onto
    // Claude Code, so a limit is gated over the protocol door with no account behind it, on the stub's named accounts.

    /// <summary>
    /// It runs as the stub's accounts (AGT7): the stub's directories through the stub's variable, the stub's limit table,
    /// and nothing of its own to sign in with. Present by a file look, since the agent it runs waits on its stdin.
    /// </summary>
    [Fact]
    public void The_protocol_stub_is_a_door_onto_the_stub_s_accounts_present_by_a_file_look()
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => _now, Zone = Zone };
        var door = adapters.Resolve("acp-stub").Toolchain!;
        var stub = adapters.Resolve("stub").Toolchain!;

        Assert.Equal("stub", door.Owner("acp-stub"));
        Assert.Equal(stub.ProfileVariable, door.ProfileVariable);
        Assert.True(door.ProbeByPresence);
        Assert.Equal((0, (LimitWords?)null, (LoginQuestion?)null), (door.Binary.Count, door.Limits, door.LoginCheck));
        Assert.Same(stub.Limits, roster.LimitsOf("acp-stub"));
    }

    [Fact]
    public async Task A_limit_on_the_protocol_stub_cools_the_stub_account_it_ran_as_and_holds_both_doors_onto_it()
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => _now, Zone = Zone };
        new HarnessSettings().WithDefault("stub", "account-1").WithWorkspaceDefault("work", "stub", "account-2").Save(Settings);

        var limited = roster.Limited("acp-stub", "account-1", Refusal, "s1");

        Assert.Equal(("stub", "account-1", Until), (limited!.Value.Entry.Agent, limited.Value.Entry.Account, limited.Value.Entry.Until));
        Assert.Equal(limited.Value.Entry, AccountCooling.Of(_home, "stub", "account-1", _now));
        // One account, spent: neither way onto it starts, and another account of the stub is not held.
        var door = await roster.SelectAsync("acp-stub", DriverConfig.Empty with { Adapter = "acp-stub" }, null, null);
        var pipe = await roster.SelectAsync("stub", DriverConfig.Empty with { Adapter = "stub" }, null, null);
        Assert.Equal((CoolingWords.Hold(limited.Value.Entry, Zone), limited.Value.Entry), (door.Refusal, door.Cooling));
        Assert.Equal(limited.Value.Entry, pipe.Cooling);
        Assert.Null((await roster.SelectAsync("acp-stub", DriverConfig.Empty with { Adapter = "acp-stub" }, "work", null)).Cooling);
    }

    [Fact]
    public async Task A_limit_on_the_protocol_stub_with_no_account_named_cools_the_stub_s_own_sign_in()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), Settings) { Clock = () => _now, Zone = Zone };

        var limited = roster.Limited("acp-stub", null, Refusal, "s1");

        Assert.Equal(("stub", (string?)null), (limited!.Value.Entry.Agent, limited.Value.Entry.Account));
        var door = await roster.SelectAsync("acp-stub", DriverConfig.Empty with { Adapter = "acp-stub" }, null, null);
        var pipe = await roster.SelectAsync("stub", DriverConfig.Empty with { Adapter = "stub" }, null, null);
        Assert.Equal(CoolingWords.Hold(limited.Value.Entry, Zone), door.Refusal);
        Assert.Equal(limited.Value.Entry, pipe.Cooling);
    }

    [Fact]
    public async Task A_door_with_no_toolchain_and_no_owner_s_words_starts_as_it_always_did()
    {
        var roster = Roster(new Adapter("bare", toolchain: null, SessionWire.Acp));
        AccountCooling.Cool(_home, Entry(agent: "bare", account: null), _now);

        Assert.Null(roster.LimitsOf("bare"));
        Assert.True((await roster.SelectAsync("bare", Config, null, null)).Allowed);
    }
}
