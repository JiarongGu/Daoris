using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account's limit, read from the agent's own words on the door that refused (TOOL4a, D125 §1.3, §1.5,
/// §2.1). The reader is pure, so this table is the whole contract: which failures are a limit, what the
/// reset they name reads as, and when the default stands in.
/// </summary>
/// <remarks>
/// The zones are this test's own, never a machine's: the recorded sentences carry <c>&lt;zone&gt;</c> where the
/// agent printed one, and each test writes in the zone it chose. Both have a fractional offset, so a reading
/// that slipped into UTC or into this machine's own zone lands on the wrong minute.
/// </remarks>
public sealed class AccountLimitsTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");
    private static readonly TimeZoneInfo Machine = TimeZoneInfo.FindSystemTimeZoneById("America/St_Johns");

    private static readonly LimitWords Claude = new ClaudeCodeAdapter().Toolchain!.Limits!;

    /// <summary>A wall-clock moment in a zone, written as the design's rows write it.</summary>
    private static DateTimeOffset At(string wall, TimeZoneInfo zone)
    {
        var local = DateTime.ParseExact(wall, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static LimitSeen? Read(string failure, DateTimeOffset seen, TimeSpan? coolOff = null) =>
        AccountLimits.Read(Claude, failure, seen, Machine, coolOff);

    // ——— §2.1's rows.

    /// <summary>
    /// A stated reset is the first such moment after the limit was seen, in the zone it names, plus the
    /// 2-minute margin: a time of day within a day, a date when days away, and the next year when the
    /// date has passed this one. The second row is the grace: a reset up to 15 minutes before it was seen
    /// is two clocks disagreeing, so the account waits only the margin.
    /// </summary>
    [Theory]
    [InlineData("your session limit resets 7am", "2026-10-02 03:10", "2026-10-02 07:02")]
    [InlineData("your session limit resets 7am", "2026-10-02 07:05", "2026-10-02 07:07")]
    [InlineData("your session limit resets 7:50am", "2026-10-02 08:30", "2026-10-03 07:52")]
    [InlineData("resets Oct 6, 10pm", "2026-10-01 14:00", "2026-10-06 22:02")]
    [InlineData("your weekly limit resets Oct 3, 4pm", "2026-10-02 09:00", "2026-10-03 16:02")]
    [InlineData("resets Jan 2, 9am", "2026-12-30 12:00", "2027-01-02 09:02")]
    public void A_stated_reset_is_read_in_its_zone_plus_the_margin(string reset, string seen, string until)
    {
        var limit = Read($"You've hit your weekly limit · {reset} ({Zone.Id})", At(seen, Zone));

        Assert.NotNull(limit);
        Assert.Equal(At(until, Zone), limit.Until);
        Assert.True(limit.Stated);
        Assert.False(limit.AssumedZone);
        Assert.False(limit.NotBelieved);
    }

    /// <summary>
    /// The longest window any sentence named is a week, so a reset further off than 8 days is a date
    /// already past carried into next year: the default stands, and says it did not believe the date.
    /// </summary>
    [Fact]
    public void A_reset_more_than_eight_days_ahead_is_not_believed()
    {
        var seen = At("2026-10-20 12:00", Zone);
        var limit = Read($"You've hit your weekly limit · resets Oct 6, 10pm ({Zone.Id})", seen);

        Assert.NotNull(limit);
        Assert.Equal(seen + AccountLimits.DefaultCoolOff, limit.Until);
        Assert.False(limit.Stated);
        Assert.True(limit.NotBelieved);
    }

    /// <summary>
    /// A zone that is not an IANA name, or none at all, is the machine's own: the harness printed the
    /// time for the machine it runs on, which is this one. The reading says the zone was assumed.
    /// </summary>
    [Theory]
    [InlineData("resets 7am (PST)")]
    [InlineData("resets 7am (CST)")]
    [InlineData("resets 7am (Pacific Standard Time)")]
    [InlineData("resets 7am (Mars/Olympus_Mons)")]
    [InlineData("resets 7am ()")]
    [InlineData("resets 7am")]
    public void A_zone_that_is_not_an_IANA_name_is_the_machine_s_own(string reset)
    {
        var limit = Read($"You've hit your weekly limit · {reset}", At("2026-10-02 03:00", Machine));

        Assert.NotNull(limit);
        Assert.Equal(At("2026-10-02 07:02", Machine), limit.Until);
        Assert.True(limit.Stated);
        Assert.True(limit.AssumedZone);
    }

    /// <summary>A named IANA zone is read as itself, whichever zone the machine is in; <i>UTC</i> is one.</summary>
    [Theory]
    [InlineData("UTC")]
    [InlineData("Asia/Kathmandu")]
    [InlineData("Europe/Lisbon")]
    public void A_named_IANA_zone_is_read_as_itself(string zone)
    {
        var named = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var limit = Read($"You've hit your weekly limit · resets 7am ({zone})", At("2026-10-02 03:00", named));

        Assert.NotNull(limit);
        Assert.Equal(At("2026-10-02 07:02", named), limit.Until);
        Assert.False(limit.AssumedZone);
    }

    /// <summary>No reset clause is Daoris's default, which a setting may replace (TOOL4e), and is not stated.</summary>
    [Fact]
    public void No_reset_clause_is_the_default()
    {
        var seen = At("2026-10-02 03:00", Zone);

        var limit = Read("Internal error: You've hit your weekly limit", seen);
        Assert.NotNull(limit);
        Assert.Equal(seen + TimeSpan.FromMinutes(60), limit.Until);
        Assert.Equal(TimeSpan.FromMinutes(60), AccountLimits.DefaultCoolOff);
        Assert.False(limit.Stated);
        Assert.False(limit.AssumedZone);
        Assert.Null(limit.Window);

        Assert.Equal(seen + TimeSpan.FromMinutes(15), Read("You've hit your weekly limit", seen, TimeSpan.FromMinutes(15))!.Until);
    }

    /// <summary>
    /// A reset this grammar does not read is the default, as no reset is: a word, a 24-hour clock no
    /// sentence used, a date no calendar has, an hour no clock shows, a month in another language.
    /// </summary>
    [Theory]
    [InlineData("resets soon")]
    [InlineData("resets 16:00")]
    [InlineData("resets Feb 30, 9am")]
    [InlineData("resets 13pm")]
    [InlineData("resets 7:75am")]
    [InlineData("resets Okt 6, 10pm")]
    public void A_reset_this_grammar_does_not_read_is_the_default(string reset)
    {
        var seen = At("2026-10-02 03:00", Zone);
        var limit = Read($"You've hit your weekly limit · your weekly limit {reset} ({Zone.Id})", seen);

        Assert.NotNull(limit);
        Assert.Equal(seen + AccountLimits.DefaultCoolOff, limit.Until);
        Assert.False(limit.Stated);
        Assert.False(limit.NotBelieved);
        Assert.Equal("weekly", limit.Window);
    }

    /// <summary>The grace looks back across midnight too: yesterday's 11:55pm, seen at 00:05, has landed.</summary>
    [Fact]
    public void The_grace_reaches_back_across_midnight()
    {
        var seen = At("2026-10-02 00:05", Zone);

        Assert.Equal(seen + AccountLimits.Margin, Read($"You've hit your weekly limit · resets 11:55pm ({Zone.Id})", seen)!.Until);
        Assert.Equal(TimeSpan.FromMinutes(2), AccountLimits.Margin);
    }

    /// <summary>Noon is 12pm and midnight is 12am, as a twelve-hour clock prints them.</summary>
    [Theory]
    [InlineData("resets 12pm", "2026-10-02 09:00", "2026-10-02 12:02")]
    [InlineData("resets 12am", "2026-10-02 21:00", "2026-10-03 00:02")]
    [InlineData("resets 12:30am", "2026-10-02 21:00", "2026-10-03 00:32")]
    public void Noon_and_midnight_read_as_a_twelve_hour_clock_prints_them(string reset, string seen, string until)
    {
        Assert.Equal(At(until, Zone), Read($"You've hit your weekly limit · {reset} ({Zone.Id})", At(seen, Zone))!.Until);
    }

    // ——— The marker.

    /// <summary>
    /// The marker is a clause that ENDS <i>You've hit your &lt;what&gt; limit</i>, whatever a door put before
    /// it; what was hit and the window the reset names are kept for the record.
    /// </summary>
    [Theory]
    [InlineData("Internal error: You've hit your individual spend limit · your session limit resets 7am", "individual spend", "session")]
    [InlineData("the ACP agent refused the call: Internal error: You've hit your weekly limit · resets 7am", "weekly", null)]
    [InlineData("You've hit your weekly limit · run /usage-credits · your weekly limit resets 7am", "weekly", "weekly")]
    public void The_marker_is_read_whatever_the_door_put_before_it(string failure, string hit, string? window)
    {
        var limit = Read($"{failure} ({Zone.Id})", At("2026-10-02 03:00", Zone));

        Assert.NotNull(limit);
        Assert.Equal(hit, limit.Hit);
        Assert.Equal(window, limit.Window);
        Assert.True(limit.Stated);
    }

    /// <summary>Compared case-insensitively, the apostrophe in either form, a run of white space as one.</summary>
    [Fact]
    public void Words_compare_whatever_their_case_apostrophe_or_spacing()
    {
        var limit = Read($"YOU’VE  HIT\tyour Weekly   limit  ·   your WEEKLY limit RESETS Oct 3,  4PM ({Zone.Id})", At("2026-10-02 09:00", Zone));

        Assert.NotNull(limit);
        Assert.Equal(At("2026-10-03 16:02", Zone), limit.Until);
        Assert.True(limit.Stated);
    }

    /// <summary>
    /// Anything else is not a limit, and stays a failure as today: another refusal, a clause that quotes the
    /// words without ending on them, a sentence in another language, an agent with no entry, no failure.
    /// </summary>
    [Theory]
    [InlineData("the ACP agent refused the call: Internal error: Overloaded")]
    [InlineData("Failed to authenticate. API Error: 401 API key is invalid.")]
    [InlineData("the test said You've hit your weekly limit when it should not have · resets 7am")]
    [InlineData("resets 7am · You've hit your weekly limit yesterday")]
    [InlineData("Vous avez atteint votre limite hebdomadaire · réinitialisation le 6 oct. à 22 h")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_failure_that_is_not_a_limit_is_none(string failure)
    {
        Assert.Null(Read(failure, At("2026-10-02 03:00", Zone)));
    }

    [Fact]
    public void An_agent_with_no_entry_and_a_failure_with_no_words_are_none()
    {
        var seen = At("2026-10-02 03:00", Zone);

        Assert.Null(AccountLimits.Read(null, "You've hit your weekly limit · resets 7am", seen, Machine));
        Assert.Null(AccountLimits.Read(Claude, null, seen, Machine));
    }

    // ——— The table: an entry grows only with a recorded sentence.

    /// <summary>
    /// Every recorded sentence, in the zone this test chose, is recognised by its own entry and its reset
    /// read as stated: the five of D125 §0.2 each named a time.
    /// </summary>
    [Fact]
    public void Every_recorded_sentence_is_recognised_and_its_reset_read()
    {
        Assert.Equal(5, Claude.Recorded.Count);
        foreach (var recorded in Claude.Recorded)
        {
            var seen = At($"{recorded.Seen} 12:00", Zone);
            var limit = AccountLimits.Read(Claude, InZone(recorded), seen, Machine);

            Assert.True(limit is { Stated: true, AssumedZone: false, NotBelieved: false }, $"not read: {recorded.Sentence}");
            Assert.InRange(limit!.Until - seen, TimeSpan.Zero, TimeSpan.FromDays(8));
        }
    }

    /// <summary>Both ways, over every entry a toolchain declares: nothing in an entry stands without a recorded sentence.</summary>
    [Fact]
    public void Every_entry_is_proven_by_its_recorded_sentences_both_ways()
    {
        var declared = Declared().ToList();
        Assert.NotEmpty(declared);
        foreach (var (agent, words) in declared)
        {
            Assert.True(Unproven(words).Count == 0, $"{agent}: {string.Join("; ", Unproven(words))}");
        }
    }

    /// <summary>The both-ways check refuses each kind of unproven entry, so its passing above means something.</summary>
    [Fact]
    public void The_both_ways_check_refuses_an_unproven_entry()
    {
        var marker = Claude with { Markers = [.. Claude.Markers, @"\byou've reached your (?<hit>.+?) quota$"] };
        var reset = Claude with { Resets = [.. Claude.Resets, @"^try again (?<when>.+)$"] };
        var sentence = Claude with
        {
            Recorded = [.. Claude.Recorded, new RecordedLimit("Usage limit reached · resets 7am (<zone>)", "2026-10-02", "made up")],
        };
        var empty = Claude with { Recorded = [] };

        Assert.Contains(Unproven(marker), p => p.Contains("quota", StringComparison.Ordinal));
        Assert.Contains(Unproven(reset), p => p.Contains("try again", StringComparison.Ordinal));
        Assert.Contains(Unproven(sentence), p => p.Contains("Usage limit reached", StringComparison.Ordinal));
        Assert.NotEmpty(Unproven(empty));
    }

    /// <summary>A recorded sentence carries the zone as a placeholder, once, and never a zone of its own.</summary>
    [Fact]
    public void A_recorded_sentence_carries_no_zone_of_its_own()
    {
        foreach (var recorded in Declared().SelectMany(d => d.Words.Recorded))
        {
            Assert.Single(recorded.Sentence.Split(AccountLimits.ZonePlaceholder)[1..]);
            Assert.DoesNotMatch(@"\((?!<zone>\))[^()]*\)\s*$", recorded.Sentence);
            Assert.False(string.IsNullOrWhiteSpace(recorded.Channel));
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", recorded.Seen);
        }
    }

    /// <summary>
    /// Only an agent seen hitting a limit declares an entry: Claude Code, and the stub that mirrors its words
    /// as it mirrors <c>Refused</c>, so a rehearsal can gate a limit with no account. A door onto Claude Code
    /// reads its owner's (AGT7); codex, dsh and a plugin have none, and read every failure as a failure.
    /// </summary>
    [Fact]
    public void Only_an_agent_seen_hitting_a_limit_declares_an_entry()
    {
        Assert.Equal(new[] { "claude-code", "stub" }, Declared().Select(d => d.Agent).Order(StringComparer.Ordinal));
        Assert.Same(Claude, new StubAdapter().Toolchain!.Limits);
    }

    private static IEnumerable<(string Agent, LimitWords Words)> Declared()
    {
        var adapters = AdapterSet.Built();
        foreach (var name in new[] { "stub", "acp-stub", "claude-code", "claude-code-acp", "dsh", "codex-acp" })
        {
            if (adapters.Resolve(name).Toolchain?.Limits is { } words) yield return (name, words);
        }
    }

    private static string InZone(RecordedLimit recorded) =>
        recorded.Sentence.Replace(AccountLimits.ZonePlaceholder, Zone.Id, StringComparison.Ordinal);

    /// <summary>
    /// What in an entry no recorded sentence proves: a marker or a reset no sentence matches, a sentence no
    /// marker matches, and an entry with no sentence at all.
    /// </summary>
    private static IReadOnlyList<string> Unproven(LimitWords words)
    {
        var problems = new List<string>();
        var sentences = words.Recorded.Select(InZone).ToList();
        if (sentences.Count == 0) problems.Add("no recorded sentence");

        problems.AddRange(words.Markers
            .Where(marker => !sentences.Any(s => AccountLimits.Matches(marker, s)))
            .Select(marker => $"marker `{marker}` matches no recorded sentence"));
        problems.AddRange(words.Resets
            .Where(reset => !sentences.Any(s => AccountLimits.Matches(reset, s)))
            .Select(reset => $"reset `{reset}` matches no recorded sentence"));
        problems.AddRange(words.Recorded
            .Where(recorded => !words.Markers.Any(marker => AccountLimits.Matches(marker, InZone(recorded))))
            .Select(recorded => $"recorded sentence `{recorded.Sentence}` matches no marker"));
        return problems;
    }
}
