using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A record that went on keeps each earlier turn's end on that turn, since the go-on forgives the record's own: the strikes and the failures
/// in a row read those ends beside the record's own, so a go-on never erases a failed end, and a turn of another part of the line, which every
/// record of a line answers, is counted on its own record only.
/// </summary>
public sealed class TurnEndStrikesTests
{
    private static string Turn(int number, string part, string? end = null) =>
        $$"""{"number":{{number}},"cause":"{{(number == 1 ? "start" : "daoris")}}","at":"2026-11-15T10:0{{number}}:00Z","part":"{{part}}"{{(end is null ? "" : $",\"end\":{end}")}}}""";

    private const string Cut = """{"state":"stopped","limit":false,"interrupted":true,"codes":["ended.driver-closed"]}""";

    [Fact]
    public void A_cut_turn_of_a_record_that_went_on_and_completed_is_still_a_strike()
    {
        var records = $$"""
            [{"id":"s1","quest":"q1","state":"completed","door":"acp","created":"2026-11-15T10:00:00Z",
              "turns":[{{Turn(1, "s1", Cut)}},{{Turn(2, "s1")}}]}]
            """;

        Assert.Equal(1, ServiceClient.ReadStrikes(records)["q1"]);
    }

    [Fact]
    public void A_go_on_that_failed_again_counts_both_ends_and_both_are_in_a_row_on_its_choice()
    {
        var records = $$"""
            [{"id":"s1","quest":"q1","state":"stopped","interrupted":true,"door":"acp","created":"2026-11-15T10:00:00Z",
              "turns":[{{Turn(1, "s1", Cut)}},{{Turn(2, "s1")}}]}]
            """;

        Assert.Equal(2, ServiceClient.ReadStrikes(records)["q1"]);
        Assert.Equal(2, ServiceClient.ReadFailedRuns(records)["q1"]);
    }

    [Fact]
    public void A_good_end_after_the_go_on_starts_the_run_again_but_keeps_the_strike()
    {
        var records = $$"""
            [{"id":"s1","quest":"q1","state":"completed","door":"acp","created":"2026-11-15T10:00:00Z",
              "turns":[{{Turn(1, "s1", Cut)}},{{Turn(2, "s1")}}]}]
            """;

        Assert.False(ServiceClient.ReadFailedRuns(records).ContainsKey("q1"));
        Assert.Equal(1, ServiceClient.ReadStrikes(records)["q1"]);
    }

    [Fact]
    public void A_kept_limited_or_update_closed_end_is_no_strike()
    {
        const string limited = """{"state":"failed","limit":true,"interrupted":false,"codes":[]}""";
        const string update = """{"state":"stopped","limit":false,"interrupted":true,"codes":["update.closed"]}""";
        var records = $$"""
            [{"id":"s1","quest":"q1","state":"completed","door":"acp","created":"2026-11-15T10:00:00Z",
              "turns":[{{Turn(1, "s1", limited)}},{{Turn(2, "s1", update)}},{{Turn(3, "s1")}}]}]
            """;

        Assert.False(ServiceClient.ReadStrikes(records).ContainsKey("q1"));
    }

    [Fact]
    public void The_line_s_turns_every_record_answers_are_counted_on_their_own_part_only()
    {
        var line = $$"""[{{Turn(1, "s1", Cut)}},{{Turn(2, "s1")}},{{Turn(3, "s2")}}]""";
        var records = $$"""
            [{"id":"s1","quest":"q1","state":"failed","door":"acp","created":"2026-11-15T10:00:00Z","turns":{{line}}},
             {"id":"s2","quest":"q1","state":"completed","door":"acp","created":"2026-11-15T11:00:00Z","from":"s1","turns":{{line}}}]
            """;

        Assert.Equal(2, ServiceClient.ReadStrikes(records)["q1"]);
    }
}
