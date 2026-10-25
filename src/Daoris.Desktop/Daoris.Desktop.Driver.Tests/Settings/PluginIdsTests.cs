using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's id as the settings and the catalogue read it (FOUNDATION1 mod-iii): the settings' own shape, which the catalogue's
/// <c>IsId</c> answers through.
/// </summary>
public sealed class PluginIdsTests
{
    [Theory]
    [InlineData("example.github-pull-request", true)]
    [InlineData("a", true)]
    [InlineData("0plugin", true)]
    [InlineData("", false)]
    [InlineData(".hidden", false)]
    [InlineData("-dash", false)]
    [InlineData("Upper", false)]
    [InlineData("two words", false)]
    [InlineData("trailing\n", false)]
    public void An_id_is_lowercase_letters_digits_dots_and_dashes_never_leading_with_a_dot_or_a_dash(string id, bool shaped)
    {
        Assert.Equal(shaped, PluginIds.IsId(id));
        Assert.Equal(shaped, PluginCatalog.IsId(id));
    }
}
