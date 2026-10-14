using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>SESSONE1h: the one reader of an entry's key splits at the first slash and tolerates a key without one.</summary>
public sealed class EntryKeyTests
{
    [Theory]
    [InlineData("ask:c98a7e/app", "ask:c98a7e", "app", true)]
    [InlineData("ask:c98a7e/intake", "ask:c98a7e", "intake", true)]
    [InlineData("session:s1/a/b", "session:s1", "a/b", true)]
    [InlineData("loose", "loose", "", false)]
    [InlineData("/app", "", "app", false)]
    [InlineData("", "", "", false)]
    public void Parse_splits_at_the_first_slash(string key, string origin, string repository, bool hasSlash)
    {
        var read = EntryKey.Parse(key);
        Assert.Equal(origin, read.Origin);
        Assert.Equal(repository, read.Repository);
        Assert.Equal(hasSlash, read.HasSlash);
    }

    [Fact]
    public void Parse_of_null_is_empty() => Assert.Equal(new EntryKey("", "", false), EntryKey.Parse(null));
}
