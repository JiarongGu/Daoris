using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The conversation's membership, read from each record's <c>line</c> over <c>fixtures/line-counts.json</c> among the driver's tests,
/// whose keys the service's <c>SessionLinesTests</c> sets. A driven line keys by its quest across a hand-over, a carry-on and a resume with
/// no <c>from</c>; a conversation continued from a driven part is on its quest's line; a conversation alone keys by its first part; a
/// teammate's part is on none; a part whose walk the ledger's step found cut is its own conversation and reads at least.
/// </summary>
public sealed class LineKeysTests
{
    public static TheoryData<string> Rows => [.. LineCountsFixture.Read().GetProperty("keys").EnumerateArray().Select(row => row.GetProperty("name").GetString()!)];

    private static JsonElement Row(string name) =>
        LineCountsFixture.Read().GetProperty("keys").EnumerateArray().Single(row => row.GetProperty("name").GetString() == name);

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_part_is_keyed_as_the_table_says(string name)
    {
        var row = Row(name);
        var records = LineCountsFixture.Records(row);

        foreach (var named in row.GetProperty("named").EnumerateArray())
        {
            var key = LineKeys.Of(named.GetProperty("session").GetString()!, records);

            Assert.Equal(named.GetProperty("key").GetString(), key.Key);
            Assert.Equal(named.GetProperty("parts").EnumerateArray().Select(part => part.GetString()), key.Parts);
            Assert.Equal(named.GetProperty("cut").GetString(), key.Cut);
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Every_record_keyed_alike_is_the_line_the_table_names(string name)
    {
        var row = Row(name);
        var records = LineCountsFixture.Records(row);
        var key = LineKeys.Of(row.GetProperty("named")[0].GetProperty("session").GetString()!, records);

        Assert.Equal(
            row.GetProperty("line").EnumerateArray().Select(part => part.GetString()).Order(StringComparer.Ordinal),
            LineKeys.Members(key, records).Select(member => member.Session).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_conversation_longer_than_any_walk_keeps_its_id_and_every_part()
    {
        var records = Chain(400);

        var key = LineKeys.Of("r400", records);

        Assert.Equal(("r1", 400, (string?)null), (key.Key, key.Parts.Count, key.Cut));
        Assert.Equal(["r1", "r2", "r3"], LineKeys.Upto("r3", records).Select(part => part.Session));
        Assert.Equal([null, "press", "press"], LineKeys.Upto("r3", records).Select(part => part.Why));
    }

    [Fact]
    public void A_record_kept_without_a_line_is_a_conversation_of_its_own()
    {
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal)
        {
            ["a1"] = new("a1", null) { Kind = "chat" },
            ["a2"] = new("a2", null) { Kind = "chat", From = "a1", FromWhy = "press" },
        };

        Assert.Equal("a2", LineKeys.Of("a2", records).Key);
        Assert.Equal(["a2"], LineKeys.Of("a2", records).Parts);
    }

    private static IReadOnlyDictionary<string, PriorSession> Chain(int parts) =>
        Enumerable.Range(1, parts).ToDictionary(
            n => $"r{n}", n => new PriorSession($"r{n}", null)
            {
                Kind = "chat", Line = "r1", From = n == 1 ? null : $"r{n - 1}", FromWhy = n == 1 ? null : "press",
                Created = DateTimeOffset.Parse("2026-11-12T09:00:00Z").AddSeconds(n),
            }, StringComparer.Ordinal);
}
