using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What sessions consumed (TOOL3/D57 §4) — measured before it is managed, on the owner's answer.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Machine-local by an inherited rule, not a new one.</b> Per-account usage names a
/// credential profile, and a profile name is already served only over loopback — so this joins the
/// transcript, the tree path and the profile name behind the shell's own bridge. There is no HTTP
/// route onto it, which is a structural guarantee rather than a policed one.</para>
///
/// <para><b>Reported, never computed.</b> Every number here was volunteered by somebody else's tool.
/// Daoris attaches no price: what a token costs is the deployment's business (D24).</para>
/// </remarks>
public sealed class UsageTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-usage-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionUsage Store() => new(_home);

    [Fact]
    public void A_machine_that_has_measured_nothing_says_so_rather_than_answering_zero()
    {
        var store = Store();

        Assert.Empty(store.Sessions);
        Assert.Empty(store.ByAccount());
        Assert.Null(store.Of("never-ran"));
    }

    [Fact]
    public void What_a_session_used_survives_the_file()
    {
        Store().Record(new UsageEntry(
            "s1", "engine", "claude-code", "work", Used: 48_000, Size: 200_000,
            When: DateTimeOffset.Parse("2026-09-22T10:00:00Z")));

        var read = Store().Of("s1");

        Assert.Equal(48_000, read?.Used);
        Assert.Equal(200_000, read?.Size);
        Assert.Equal("work", read?.Profile);
        Assert.Equal("engine", read?.Repository);
    }

    /// <summary>
    /// One session is measured once. A turn that reported a larger number replaces the reading — the
    /// same high-water rule the wire applies within a turn, applied across them.
    /// </summary>
    [Fact]
    public void A_later_larger_reading_replaces_an_earlier_one_and_a_smaller_one_does_not()
    {
        var store = Store();
        store.Record(new UsageEntry("s1", "engine", "claude-code", "work", 48_000, 200_000, When: Now()));
        store.Record(new UsageEntry("s1", "engine", "claude-code", "work", 120_000, 200_000, When: Now()));
        store.Record(new UsageEntry("s1", "engine", "claude-code", "work", 900, 200_000, When: Now()));

        Assert.Equal(120_000, Store().Of("s1")?.Used);
        // Still ONE session, not three readings pretending to be three sessions.
        Assert.Single(Store().Sessions);
    }

    /// <summary>
    /// The per-account answer is the question the owner actually asked — "which of my accounts is
    /// carrying the load". It is a derivation over the sessions, so the two can never disagree.
    /// </summary>
    [Fact]
    public void Accounts_are_totalled_from_the_sessions_rather_than_counted_separately()
    {
        var store = Store();
        store.Record(new UsageEntry("s1", "engine", "claude-code", "work", 48_000, 200_000, When: Now()));
        store.Record(new UsageEntry("s2", "tools", "claude-code", "work", 12_000, 200_000, When: Now()));
        store.Record(new UsageEntry("s3", "engine", "claude-code", "personal", 5_000, 200_000, When: Now()));

        var accounts = Store().ByAccount();

        var work = accounts.Single(a => a.Profile == "work");
        Assert.Equal(2, work.Sessions);
        Assert.Equal(60_000, work.Used);
        Assert.Equal(1, accounts.Single(a => a.Profile == "personal").Sessions);
    }

    /// <summary>
    /// A session that ran on the harness's own configuration home has no profile, and that is a
    /// state rather than a gap — it is still somebody's usage and still worth totalling.
    /// </summary>
    [Fact]
    public void A_session_with_no_named_account_is_still_measured()
    {
        Store().Record(new UsageEntry("s1", "engine", "claude-code", Profile: null, 9_000, 200_000, Now()));

        var account = Assert.Single(Store().ByAccount());
        Assert.Null(account.Profile);
        Assert.Equal(9_000, account.Used);
    }

    /// <summary>
    /// 🔴 The store is bounded, and it drops the OLDEST rather than the largest — a file that grew
    /// forever would be the transcript problem again, and dropping by size would quietly delete
    /// exactly the sessions worth looking at.
    /// </summary>
    [Fact]
    public void It_keeps_a_bounded_history_and_drops_the_oldest_first()
    {
        var store = Store();
        for (var n = 0; n < SessionUsage.Retained + 40; n++)
        {
            store.Record(new UsageEntry(
                $"s{n:D4}", "engine", "claude-code", "work", 1_000 + n, 200_000,
                When: DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(n)));
        }

        var read = Store();
        Assert.Equal(SessionUsage.Retained, read.Sessions.Count);
        Assert.Null(read.Of("s0000"));
        Assert.NotNull(read.Of($"s{SessionUsage.Retained + 39:D4}"));
    }

    /// <summary>
    /// A hand-mangled or half-written file is a machine that has measured nothing, never a crash.
    /// Usage is an observation, and losing it costs nothing — the same reading `driver.json` and the
    /// harness wiring already take (D21).
    /// </summary>
    [Fact]
    public void An_unreadable_file_is_a_machine_that_measured_nothing()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path.Combine(_home, "usage.json"), "{ not json at all");

        Assert.Empty(Store().Sessions);
        // And it is not overwritten by the act of reading it, which would destroy what was being typed.
        Assert.Equal("{ not json at all", File.ReadAllText(Path.Combine(_home, "usage.json")));
    }

    private static DateTimeOffset Now() => DateTimeOffset.UtcNow;
}
