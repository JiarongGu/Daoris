using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one line key, over <c>fixtures/line-counts.json</c> among the driver's tests, which
/// the service's walk is held to as well (<c>LineKeysFixtureTests</c>). A driven line keys by its quest across a hand-over, a carry-on and
/// a resume with no <c>from</c>; a conversation continued from a driven part is on its quest's line; a conversation alone keys by its first
/// part; a teammate's part ends the walk before it; a cycle and the bound key the line by the part itself and read at least.
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
    public void A_line_of_the_bound_is_whole_and_one_hop_past_it_is_cut_and_keyed_by_the_part_itself()
    {
        var whole = Chain(LineKeys.Bound + 1);
        var past = Chain(LineKeys.Bound + 2);

        var inside = LineKeys.Of($"r{LineKeys.Bound + 1}", whole);
        var outside = LineKeys.Of($"r{LineKeys.Bound + 2}", past);

        Assert.Equal("r1", inside.Key);
        Assert.Null(inside.Cut);
        Assert.Equal($"r{LineKeys.Bound + 2}", outside.Key);
        Assert.Equal(LineKey.BoundCut, outside.Cut);
    }

    [Fact]
    public void The_bound_is_the_service_walk_s_own()
    {
        Assert.Equal(256, LineKeys.Bound);
    }

    private static IReadOnlyDictionary<string, PriorSession> Chain(int parts) =>
        Enumerable.Range(1, parts).ToDictionary(
            n => $"r{n}", n => new PriorSession($"r{n}", null) { Kind = "chat", From = n == 1 ? null : $"r{n - 1}" }, StringComparer.Ordinal);
}
