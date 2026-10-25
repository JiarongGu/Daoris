using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a declared write-across may hold (FOUNDATION1 mod-iii): the settings' reading, which the permissions' <c>AcrossRules.Problem</c>
/// answers through, so the file, the verb and the reach refuse one thing in one sentence.
/// </summary>
public sealed class AcrossDeclarationsTests
{
    [Theory]
    [InlineData("engine", "plugins", null)]
    [InlineData("engine", "", "a relationship names the repository it may write into.")]
    [InlineData("engine", "   ", "a relationship names the repository it may write into.")]
    [InlineData("engine", "engine", "`engine` writes in its own tree already — a relationship names another repository.")]
    [InlineData(" Engine ", "engine ", "`Engine` writes in its own tree already — a relationship names another repository.")]
    public void A_relationship_names_another_repository(string repository, string to, string? problem)
    {
        Assert.Equal(problem, AcrossDeclarations.Problem(repository, to));
        Assert.Equal(problem, AcrossRules.Problem(repository, to));
    }
}
