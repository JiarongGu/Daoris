using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>An id a call names is the letters, digits and marks of a harness's ids alone, a line ending included.</summary>
public sealed class CallLineageIdTests
{
    [Theory]
    [InlineData("toolu_01AbC", "toolu_01AbC")]
    [InlineData("toolu_01AbC\n", null)]
    [InlineData("a\r\n", null)]
    [InlineData("a b", null)]
    public void An_id_is_kept_only_whole(string value, string? kept) => Assert.Equal(kept, CallLineage.Id(value));
}
