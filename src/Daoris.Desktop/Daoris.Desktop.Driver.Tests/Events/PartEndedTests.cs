using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 l-i (the foundation design §4.2): <c>part.ended</c> judged in .NET from the journal's raw transition alone, its reason the note's
/// <c>ended.*</c> code and its class one closed code from the state, the limit flag and that code; the class table held both ways against
/// <see cref="NoteCodes"/>.
/// </summary>
public sealed class PartEndedTests
{
    private static JournalEntry Row(string? old, string state, bool limited = false, string? codes = null, string marks = "driven", long position = 7) =>
        new(position, 1_761_800_000_000, "s1", "sessions", "state", old, state, false, limited, codes?.Split(','), marks.Split(','), "team", "q1");

    /// <summary>A move into a closed state is an end, with its reason and class; the store its position is of goes with it.</summary>
    [Theory]
    [InlineData("Working", "Completed", false, "ended.done", "completed")]
    [InlineData("Working", "Failed", false, "ended.exit", "failed")]
    [InlineData("Working", "Failed", true, "ended.exit,account.cooling", "limited")]
    [InlineData("Working", "Failed", false, "ended.transient", "transient-bound")]
    [InlineData("Working", "Failed", false, "ended.choice-not-offered", "refused-choice")]
    [InlineData("Working", "Failed", false, "ended.exit,account.refused", "access")]
    [InlineData("Working", "Stopped", false, "ended.stopped", "stopped-person")]
    [InlineData("Working", "Stopped", false, "ended.stopped-steward", "stopped-role")]
    [InlineData("Working", "Stopped", false, "ended.driver-closed", "stopped-daoris")]
    [InlineData("Working", "Completed", false, "ended.awaits-helper", "awaits-helper")]
    [InlineData("Working", "StoodDown", false, null, "stood-down")]
    [InlineData("Working", "Stopped", false, null, "unknown")]
    [InlineData("Working", "Completed", false, null, "completed")]
    public void A_move_into_a_closed_state_is_an_end_with_its_reason_and_class(string old, string state, bool limited, string? codes, string expected)
    {
        var ended = PartEnds.Judge(Row(old, state, limited, codes), "store-1");

        Assert.NotNull(ended);
        Assert.Equal((expected, "s1", 7L, "store-1"), (ended.Class, ended.Session, ended.Position, ended.Store));
        Assert.Equal(codes?.Split(',').FirstOrDefault(code => code.StartsWith("ended.", StringComparison.Ordinal)), ended.Reason);
    }

    /// <summary>A move that ends nothing, a reopen, a same-state write, a flag alone and another family's row, is no end.</summary>
    [Fact]
    public void A_row_that_ends_nothing_is_no_end()
    {
        Assert.Null(PartEnds.Judge(Row("Queued", "Working"), "s"));
        Assert.Null(PartEnds.Judge(Row("Completed", "Working"), "s"));
        Assert.Null(PartEnds.Judge(Row("Completed", "Failed"), "s"));
        Assert.Null(PartEnds.Judge(Row("Working", "Failed") with { Change = "limited" }, "s"));
        Assert.Null(PartEnds.Judge(Row("Working", "Failed") with { Family = "asks" }, "s"));
        Assert.Null(PartEnds.Judge(Row("Working", "Failed") with { Marks = ["driven"], StateNew = null }, "s"));
    }

    /// <summary>An end, a reopen and an end are two ends at two positions, each coded from its own row.</summary>
    [Fact]
    public void An_end_a_reopen_and_an_end_are_two_ends()
    {
        var first = PartEnds.Judge(Row("Working", "Failed", codes: "ended.exit", position: 5), "s")!;
        Assert.Null(PartEnds.Judge(Row("Failed", "Working", position: 6), "s"));
        var second = PartEnds.Judge(Row("Working", "Completed", codes: "ended.done", position: 7), "s")!;

        Assert.Equal([("failed", 5L), ("completed", 7L)], [(first.Class, first.Position), (second.Class, second.Position)]);
    }

    /// <summary>A conversation is a chat record; its workspace and moment ride with the end.</summary>
    [Fact]
    public void A_conversation_is_a_chat_record()
    {
        var chat = PartEnds.Judge(Row("Working", "Completed", marks: "chat,help-mode"), "s")!;
        var driven = PartEnds.Judge(Row("Working", "Completed"), "s")!;

        Assert.Equal([true, false], [chat.Conversation, driven.Conversation]);
        Assert.Equal("team", chat.Workspace);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_761_800_000_000), chat.At);
    }

    /// <summary>Every <c>ended.*</c> code of <see cref="NoteCodes"/> is in the class table or says it takes its class from the state, and the table names no code that is gone.</summary>
    [Fact]
    public void The_class_table_matches_the_note_codes_both_ways()
    {
        var written = NoteCodes.All.Select(code => code.Code).Where(code => code.StartsWith("ended.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var named = PartEnds.Decided.Keys.Concat(PartEnds.ByState).ToList();

        Assert.Equal(named.Count, named.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(written.Order(StringComparer.Ordinal), named.Where(code => code.StartsWith("ended.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.All(PartEnds.Decided.Values, cls => Assert.Contains(cls, PartEnds.Classes));
        Assert.All(PartEnds.Classes, cls => Assert.Contains(cls, PartEnds.Causes.Keys));
    }
}
