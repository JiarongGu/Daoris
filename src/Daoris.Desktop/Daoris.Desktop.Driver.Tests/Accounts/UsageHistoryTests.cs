using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGECAP1a1 (D179 point 2): every usage reading kept, appended to a bounded log under the home's <c>usage-history/</c>,
/// beside a small file of each account's binding epoch. History first, so data gathers while the estimator is built: what a
/// reading carries, an exact duplicate written once, the epoch a plan change or a sign-out and in bumps, the bound of 90 days
/// and 64 MiB pruned oldest first with the gap said where a reader meets it, and a write that never fails its caller.
/// </summary>
/// <remarks>Every moment is this test's own: the writer is handed its clock, so no margin of real time is asked of it.</remarks>
public sealed class UsageHistoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-history-" + Guid.NewGuid().ToString("N")[..8]);

    public UsageHistoryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly WindowSeen Week =
        new("seven_day", "weekly", null, 10080, 0.14, 0.01, Now.AddHours(100), UsageHistory.ResetSaid);

    private static ReadingFrom From(
        DateTimeOffset? seen = null, string account = "account-1", string source = UsageHistory.SourceFrame, string? plan = null,
        string? session = "3f9c2a71") =>
        new("claude-code", account, "claude-code-acp", source, Reader: 1, seen ?? Now, session, plan, plan is null ? null : "get_usage");

    private void Keep(ReadingFrom from, params WindowSeen[] windows) => UsageHistory.Kept(_home, from, windows, from.Seen);

    private string Segment(string name) => DaorisHome.Entry(_home, HomeEntry.UsageHistory, name);

    private string[] Lines(string name = "readings-2026-10.jsonl") =>
        File.ReadAllText(Segment(name)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    // ——— A reading.

    [Fact]
    public void A_reading_is_one_line_of_every_field_a_null_written_as_null_and_it_reads_back_whole()
    {
        var unopened = new WindowSeen("five_hour", "session", null, 300, null, null, null, null);
        Keep(From(plan: "max"), Week, unopened);

        Assert.Equal("readings-2026-10.jsonl", UsageHistory.SegmentOf(Now));
        var bytes = File.ReadAllBytes(Segment("readings-2026-10.jsonl"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no byte-order mark");
        Assert.DoesNotContain((byte)'\r', bytes);
        var lines = Lines();
        Assert.Equal(2, lines.Length);

        var first = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal(
            [
                "v", "kind", "id", "written", "agent", "account", "epoch", "door", "window", "walk", "scope", "minutes", "used",
                "precision", "reset", "resetFrom", "source", "seen", "session", "plan", "planFrom", "reader",
            ],
            first.Select(pair => pair.Key));
        Assert.Equal(1, (int)first["v"]!);
        Assert.Equal("reading", (string)first["kind"]!);
        Assert.Matches("^[0-9a-f]{32}$", (string)first["id"]!);
        Assert.Equal("2026-10-19T12:00:00.000Z", (string)first["written"]!);
        Assert.Equal("2026-10-23T16:00:00.000Z", (string)first["reset"]!);

        // Absent is never zero (D57): a window that said no use and no reset says null, never 0.
        var second = JsonNode.Parse(lines[1])!.AsObject();
        Assert.Null(second["used"]);
        Assert.True(second.ContainsKey("used") && second.ContainsKey("reset") && second.ContainsKey("precision"));

        var read = UsageHistory.Read(_home);
        Assert.Empty(read.Gaps);
        Assert.Equal(0, read.Unread);
        Assert.Equal(
            [
                new KeptReading("claude-code", "account-1", 1, "claude-code-acp", Week, UsageHistory.SourceFrame, Now, "3f9c2a71", "max", "get_usage", 1),
                new KeptReading("claude-code", "account-1", 1, "claude-code-acp", unopened, UsageHistory.SourceFrame, Now, "3f9c2a71", "max", "get_usage", 1),
            ],
            read.Readings);
    }

    [Fact]
    public void An_exact_duplicate_is_written_once_and_another_source_or_moment_is_another_reading()
    {
        Keep(From(), Week);
        Keep(From(), Week);
        Keep(From(source: "get_usage", session: null), Week);
        Keep(From(seen: Now.AddMilliseconds(1)), Week);
        Keep(From(account: "account-2"), Week);

        Assert.Equal(
            [("account-1", "frame", Now), ("account-1", "get_usage", Now), ("account-1", "frame", Now.AddMilliseconds(1)), ("account-2", "frame", Now)],
            UsageHistory.Read(_home).Readings.Select(each => (each.Account, each.Source, each.Seen)));
    }

    // ——— The binding epoch.

    [Fact]
    public void An_account_s_first_reading_mints_its_epoch_and_a_plan_read_otherwise_bumps_it()
    {
        Keep(From(seen: Now), Week);
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // A plan first learned is the epoch's own, no rebinding; the same plan again changes nothing.
        Keep(From(seen: Now.AddMinutes(1), plan: "max"), Week);
        Keep(From(seen: Now.AddMinutes(2), plan: "max"), Week);
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // Another plan as read is another binding.
        Keep(From(seen: Now.AddMinutes(3), plan: "team"), Week);
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // Where no plan was read the epoch is the last one, and the reading's plan is null.
        Keep(From(seen: Now.AddMinutes(4)), Week);

        Assert.Equal(
            [(1, null), (1, "max"), (1, "max"), (2, "team"), (2, null)],
            UsageHistory.Read(_home).Readings.Select(each => (each.Epoch, each.Plan)));
        Assert.Null(UsageHistory.EpochOf(_home, "claude-code", "account-2"));
    }

    [Fact]
    public void Signing_out_and_in_again_bumps_the_epoch_once_whether_the_sign_in_or_a_reading_says_it()
    {
        Keep(From(plan: "max"), Week);

        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now.AddMinutes(1));
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now.AddMinutes(2));
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "max", Now.AddMinutes(3));
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "max", Now.AddMinutes(4));
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // Signed out, then a reading: it ran again, so it signed in again, wherever that was.
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now.AddMinutes(5));
        Keep(From(seen: Now.AddMinutes(6)), Week);
        Assert.Equal(3, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // An account removed from the machine and made again under its name is another binding.
        UsageHistory.Unbound(_home, "claude-code", "account-1", Now.AddMinutes(7));
        Keep(From(seen: Now.AddMinutes(8)), Week);
        Assert.Equal(4, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        // The new binding's plan is first learned by a sign-in, as the epoch's own; one that differs after it bumps it as a
        // reading's does.
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "pro", Now.AddMinutes(9));
        Assert.Equal(4, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "max", Now.AddMinutes(10));
        Assert.Equal(5, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Unknown, null, Now.AddMinutes(11));
        Assert.Equal(5, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
    }

    [Fact]
    public void A_sign_in_of_an_account_the_history_never_met_writes_nothing()
    {
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "max", Now);
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now);
        UsageHistory.Unbound(_home, "claude-code", "account-1", Now);

        Assert.False(Directory.Exists(UsageHistory.FolderOf(_home)));
    }

    [Fact]
    public void An_epochs_file_that_does_not_read_is_left_as_found_and_its_readings_say_no_epoch()
    {
        Directory.CreateDirectory(UsageHistory.FolderOf(_home));
        File.WriteAllText(Segment("epochs.json"), "{ not json");

        Keep(From(plan: "max"), Week);
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now);

        Assert.Equal("{ not json", File.ReadAllText(Segment("epochs.json")));
        Assert.Null(Assert.Single(UsageHistory.Read(_home).Readings).Epoch);
        Assert.Null(UsageHistory.EpochOf(_home, "claude-code", "account-1"));
    }

    // ——— The bound: 90 days and 64 MiB, whole months pruned oldest first, the gap said.

    [Fact]
    public void The_bound_is_ninety_days_and_sixty_four_mebibytes()
    {
        Assert.Equal(90, UsageHistory.KeptDays);
        Assert.Equal(64L * 1024 * 1024, UsageHistory.CapBytes);
    }

    [Fact]
    public void Months_ended_ninety_days_ago_are_pruned_oldest_first_and_the_gap_is_said_at_the_head_of_the_next_write()
    {
        Directory.CreateDirectory(UsageHistory.FolderOf(_home));
        foreach (var month in new[] { "2026-05", "2026-06", "2026-07", "2026-10" }) File.WriteAllText(Segment($"readings-{month}.jsonl"), "");

        Keep(From(), Week);

        // Ninety days before 19 October is 21 July: June ended before it, July after.
        Assert.False(File.Exists(Segment("readings-2026-05.jsonl")));
        Assert.False(File.Exists(Segment("readings-2026-06.jsonl")));
        Assert.True(File.Exists(Segment("readings-2026-07.jsonl")));
        var lines = Lines();
        Assert.Equal(2, lines.Length);
        var gap = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal("gap", (string)gap["kind"]!);
        Assert.Equal("age", (string)gap["why"]!);

        var read = UsageHistory.Read(_home);
        Assert.Equal(
            new HistoryGap(new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), "age",
                ["readings-2026-05.jsonl", "readings-2026-06.jsonl"]),
            Assert.Single(read.Gaps), new GapComparer());
        Assert.Single(read.Readings);

        // Said once: the next write finds nothing to prune.
        Keep(From(seen: Now.AddMinutes(1)), Week);
        Assert.Single(UsageHistory.Read(_home).Gaps);
    }

    [Fact]
    public void Over_its_size_the_oldest_months_are_pruned_first_until_the_write_fits_and_the_gap_is_said()
    {
        Directory.CreateDirectory(UsageHistory.FolderOf(_home));
        File.WriteAllText(Segment("readings-2026-08.jsonl"), new string('x', 900) + "\n");
        File.WriteAllText(Segment("readings-2026-09.jsonl"), new string('x', 900) + "\n");
        File.WriteAllText(Segment("readings-2026-10.jsonl"), "");

        UsageHistory.Kept(_home, From(), [Week], Now, cap: 2000);

        Assert.False(File.Exists(Segment("readings-2026-08.jsonl")));
        Assert.True(File.Exists(Segment("readings-2026-09.jsonl")));
        var read = UsageHistory.Read(_home);
        Assert.Equal(
            new HistoryGap(new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "size", ["readings-2026-08.jsonl"]),
            Assert.Single(read.Gaps), new GapComparer());
        Assert.Single(read.Readings);
        // The filler line September holds is no reading, and is counted as one that did not read, never guessed at.
        Assert.Equal(1, read.Unread);
        var total = Directory.EnumerateFiles(UsageHistory.FolderOf(_home), "readings-*.jsonl").Sum(path => new FileInfo(path).Length);
        Assert.InRange(total, 0, 2000);
    }

    [Fact]
    public void A_month_alone_over_the_cap_is_marked_full_once_and_what_follows_is_dropped_and_said_as_a_gap()
    {
        Directory.CreateDirectory(UsageHistory.FolderOf(_home));
        File.WriteAllText(Segment("readings-2026-10.jsonl"), new string('x', 900) + "\n");

        UsageHistory.Kept(_home, From(), [Week], Now, cap: 1000);
        var marked = new FileInfo(Segment("readings-2026-10.jsonl")).Length;
        UsageHistory.Kept(_home, From(seen: Now.AddMinutes(1)), [Week], Now.AddMinutes(1), cap: 1000);

        Assert.Equal(marked, new FileInfo(Segment("readings-2026-10.jsonl")).Length);
        Assert.Equal("full", (string)JsonNode.Parse(Lines()[^1])!["kind"]!);
        var read = UsageHistory.Read(_home);
        Assert.Empty(read.Readings);
        Assert.Equal(
            new HistoryGap(Now, new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), "full", ["readings-2026-10.jsonl"]),
            Assert.Single(read.Gaps), new GapComparer());
    }

    // ——— Never a reason to fail.

    [Fact]
    public void A_history_that_cannot_be_written_or_read_never_throws()
    {
        // A file where the folder goes: nothing can be made under it.
        File.WriteAllText(UsageHistory.FolderOf(_home), "x");

        Keep(From(plan: "max"), Week);
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now);

        var read = UsageHistory.Read(_home);
        Assert.Empty(read.Readings);
        Assert.Null(UsageHistory.EpochOf(_home, "claude-code", "account-1"));
    }

    // ——— What is never written (D179 point 2): a prompt, a word, a raw frame, a key, an email, an organisation, who signed in.

    [Fact]
    public void A_maker_s_name_that_is_not_a_plain_name_never_reaches_the_file()
    {
        Keep(
            From(plan: "max for someone@example.com"),
            Week with { Scope = "the agent's own words" },
            Week with { Window = "seven_day_someone@example.com" },
            Week with { Window = "seven_day_opus", Scope = "opus" });

        var text = File.ReadAllText(Segment("readings-2026-10.jsonl"));
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("words", text);
        Assert.Equal(
            [("seven_day", null, null), ("seven_day_opus", "opus", null)],
            UsageHistory.Read(_home).Readings.Select(each => (each.Window.Window, each.Window.Scope, each.Plan)));
    }

    /// <summary>A gap compared by its moments, its reason and the months it names.</summary>
    private sealed class GapComparer : IEqualityComparer<HistoryGap>
    {
        public bool Equals(HistoryGap? x, HistoryGap? y) =>
            x is not null && y is not null && x.From == y.From && x.To == y.To && x.Why == y.Why && x.Segments.SequenceEqual(y.Segments);

        public int GetHashCode(HistoryGap obj) => obj.Why.GetHashCode(StringComparison.Ordinal);
    }
}
