using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy (the agent-lyntai design §5.2, rounds 3 to 5): behind a bound kind no table stands, so each account reader has a trusted
/// entry taking the typed outcome and reading no sentence, the writes after it today's; and which readers a door's site calls is today's,
/// by door and site. Daoris's own types, never a proof that the layer supplies the outcome.
/// </summary>
public sealed class HostPolicyTrustedEntryTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-host-policy-" + Guid.NewGuid().ToString("N")[..8]);

    public HostPolicyTrustedEntryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessRoster Roster() =>
        new(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = TimeZoneInfo.Utc };

    /// <summary>
    /// A stated reset reaches cooling whole: today's sentence entry drops a stated limit its table cannot read, where the trusted entry
    /// cools the account until it, its window kept.
    /// </summary>
    [Fact]
    public void Host_policy_s_trusted_limit_cools_the_account_until_the_stated_reset_reading_no_sentence()
    {
        var roster = Roster();
        var stated = new LimitSeen("weekly", AccountWindows.Weekly, Seen.AddDays(2), Stated: true, AssumedZone: false);

        Assert.Null(roster.Limited("acp-stub", "account-1", "no sentence a table reads", "s1", byCode: stated));
        var (entry, seen) = roster.LimitedTrusted("acp-stub", "account-1", stated, "s1")!.Value;

        Assert.Equal(stated, seen);
        Assert.Equal(("stub", "account-1", Seen.AddDays(2), true, AccountWindows.Weekly, "s1"), (entry.Agent, entry.Account, entry.Until, entry.Stated, entry.Window, entry.Session));
        Assert.Equal(entry, roster.CoolingOf("acp-stub", "account-1"));
    }

    /// <summary>The trusted sign-out writes what a sign-in's words write, the tool's own sign-in held, and reads no words.</summary>
    [Fact]
    public void Host_policy_s_trusted_sign_out_writes_today_s_sign_in_line_reading_no_words()
    {
        var roster = Roster();
        var adapter = roster.Adapters.Door("acp-stub");

        Assert.Null(RefusedAccounts.SignInRefused(roster, adapter, null, "words no sign-in table reads"));
        var line = RefusedAccounts.SignOut(roster, adapter, null);

        Assert.Equal(RefusedAccounts.SignedOutNote("stub", null).Note, line.Note);
        Assert.Equal(RefusedAccounts.SignedOutNote("stub", "account-1").Note, RefusedAccounts.SignOut(roster, adapter, "account-1").Note);
    }

    /// <summary>
    /// Which readers each door's site has today (§5.2's round-4 and round-5 paragraphs, D125's recorded state): the protocol door's
    /// conclusion all four; the native door's driven conclusion is handed no failure, so the refused credential and the refused model act
    /// alone; a protocol conversation and a protocol brainstorm pass read their limit, the conversation its sign-in too; a native
    /// conversation and a native pass read nothing.
    /// </summary>
    [Theory]
    [InlineData(Transport.Protocol, ReaderSite.Conclusion, ReaderSet.Limit | ReaderSet.SignOut | ReaderSet.Refuse | ReaderSet.Model)]
    [InlineData(Transport.Native, ReaderSite.Conclusion, ReaderSet.Refuse | ReaderSet.Model)]
    [InlineData(Transport.Protocol, ReaderSite.Conversation, ReaderSet.Limit | ReaderSet.SignOut)]
    [InlineData(Transport.Native, ReaderSite.Conversation, ReaderSet.None)]
    [InlineData(Transport.Protocol, ReaderSite.BrainstormPass, ReaderSet.Limit)]
    [InlineData(Transport.Native, ReaderSite.BrainstormPass, ReaderSet.None)]
    public void Host_policy_routes_each_site_s_readers_by_door_as_today(Transport door, ReaderSite site, ReaderSet reads)
    {
        Assert.Equal(reads, AccountReaders.Of(door, site));
    }

    /// <summary>A bound kind takes the trusted entries; a Daoris-run kind reads as today, through its tables.</summary>
    [Fact]
    public void Host_policy_routes_a_bound_kind_to_the_trusted_entries_and_a_daoris_run_kind_to_its_tables()
    {
        Assert.Equal(AccountRoute.Trusted, AccountReaders.Route(bound: true));
        Assert.Equal(AccountRoute.Tables, AccountReaders.Route(bound: false));
    }
}
