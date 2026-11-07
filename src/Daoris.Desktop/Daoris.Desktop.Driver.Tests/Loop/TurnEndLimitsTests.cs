using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A protocol turn's last message read for its account's limit by the one reader, as a refused prompt's words are: the install's
/// sentence (2026-11-07) is the account's limit, its reset read in the zone it names; a limit in the agent's own other words, or any other
/// reading, leaves the turn's end alone.
/// </summary>
public sealed class TurnEndLimitsTests
{
    private static readonly ProviderRegistry Built = ProviderRegistry.Built();

    /// <summary>The frame's time on the install.</summary>
    private static readonly DateTimeOffset Seen = new(2026, 11, 7, 14, 8, 34, TimeSpan.Zero);

    private const string Sentence = "You've hit your session limit · resets 3am (Australia/Sydney)";

    private static (bool Limit, AgentError? Read) Read(string words, string door = "claude-code-acp")
    {
        AgentError? read = null;
        var limit = TurnEndLimits.For(Built, Built.Door(door), () => Seen, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu"), error => read = error)(words);
        return (limit, read);
    }

    [Fact]
    public void The_install_s_sentence_is_the_limit_until_the_next_3am_in_its_named_zone_plus_the_margin()
    {
        var (limit, read) = Read(Sentence);

        Assert.True(limit);
        Assert.Equal(("owner:words:limit", AgentErrorKind.Limit), (read!.By, read.Kind));
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 3, 2, 0, TimeSpan.FromHours(11)), read.Limit!.Until);
        Assert.Equal(("session", true, false), (read.Limit.Hit, read.Limit.Stated, read.Limit.AssumedZone));
    }

    [Theory]
    [InlineData("I hit a limit while running the tests, so I stopped there.")]
    [InlineData("The quest is done and its branch is committed.")]
    [InlineData("Internal error: API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment.")]
    public void A_limit_in_the_agent_s_own_other_words_or_any_other_reading_is_none(string words)
    {
        var (limit, read) = Read(words);

        Assert.False(limit);
        Assert.Null(read);
    }

    [Fact]
    public void A_door_whose_agent_records_no_limit_words_reads_none() => Assert.False(Read(Sentence, "dsh").Limit);
}
