using System.Text.Json;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hand-back reading's commit rule is the merge check's (<c>tools/merge/attribution.ts</c>): both are held to one fixture, which the
/// tool's own test reads too, so the two cannot drift apart.
/// </summary>
public sealed class CommitAttributionContractTests
{
    private static readonly JsonElement Contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "merge", "attribution-contract.json"))).RootElement;

    private static IEnumerable<string> Trailers(string list) => Contract.GetProperty(list).EnumerateArray().Select(each => each.GetString()!);

    private static bool Signed(string trailer) =>
        CommitAttribution.Unsigned($"a1\u001fp0\u001fwork\u001f{trailer}\u001e").Count == 0;

    [Fact]
    public void The_log_format_the_maker_pattern_and_the_trailer_shape_are_the_merge_checks()
    {
        Assert.Equal(Contract.GetProperty("format").GetString(), CommitAttribution.Format);
        Assert.Equal(Contract.GetProperty("maker").GetString(), CommitAttribution.MakerPattern);
        Assert.Equal(Contract.GetProperty("named").GetString(), CommitAttribution.NamedPattern);
    }

    [Fact]
    public void Every_trailer_the_merge_check_accepts_is_signed_and_every_one_it_refuses_is_not()
    {
        foreach (var trailer in Trailers("signed")) Assert.True(Signed(trailer), trailer);
        foreach (var trailer in Trailers("unsigned")) Assert.False(Signed(trailer), trailer);
    }
}
