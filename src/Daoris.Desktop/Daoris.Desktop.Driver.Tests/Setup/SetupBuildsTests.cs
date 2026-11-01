using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTSPLACE1a2 (the Agents-place design §4): <i>What it builds</i> shapes only the set-up quest's request, the checks its own session is
/// asked to declare in its safe work. It sets no permission, appointment, account or model, and a set-up that asks no safe work reads the same.
/// </summary>
public sealed class SetupBuildsTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);

    private static SetupBriefInput Input(SetupCase kind, string? builds) => new(
        kind, Day, "reports", new LayoutReaderTests.Scratch().File("README.md", "# Reports\n").Read(), "0.0.1",
        Description: null, Entries: [], Neighbours: [], Builds: builds);

    [Theory]
    [InlineData(SetupBuilds.Library, "a library or package")]
    [InlineData(SetupBuilds.Service, "a web app or a service")]
    [InlineData(SetupBuilds.Desktop, "a desktop app or a game")]
    [InlineData(SetupBuilds.Tooling, "documentation or tooling")]
    public void Each_kind_adds_its_checks_to_the_safe_work_step_and_nothing_else(string builds, string said)
    {
        var (title, plain) = SetupBrief.Compose(Input(SetupCase.Whole, null));
        var (shapedTitle, shaped) = SetupBrief.Compose(Input(SetupCase.Whole, builds));

        Assert.Equal(title, shapedTitle);
        var sentence = SetupBuilds.Checks(builds);
        Assert.Contains($"This repository builds {said}", sentence);
        Assert.Equal(plain, shaped.Replace(" " + sentence, "", StringComparison.Ordinal));
        var step = shaped.Split('\n').Single(line => line.Contains("**Declare the safe work**", StringComparison.Ordinal));
        Assert.EndsWith(sentence, step);
    }

    [Fact]
    public void A_set_up_that_asks_no_safe_work_reads_the_same_whatever_it_builds()
    {
        Assert.Equal(SetupBrief.Compose(Input(SetupCase.Declare, null)), SetupBrief.Compose(Input(SetupCase.Declare, SetupBuilds.Library)));
    }

    /// <summary>The body is read in a repository that may know nothing of Daoris: no decision, no path of Daoris's own, no setting it grants.</summary>
    [Fact]
    public void Each_sentence_is_in_the_canons_words_and_grants_nothing()
    {
        foreach (var builds in SetupBuilds.All)
        {
            var sentence = SetupBuilds.Checks(builds);
            Assert.DoesNotMatch(@"\bD\d{1,3}\b", sentence);
            foreach (var word in new[] { "docs/", "src/Daoris", "daoris-driver", "permission", "account", "model", "appoint" })
            {
                Assert.DoesNotContain(word, sentence, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void An_unknown_kind_is_refused_naming_the_four()
    {
        Assert.True(SetupBuilds.Is("library"));
        Assert.False(SetupBuilds.Is("game"));
        Assert.Equal("`game` is not what a repository builds here — `library`, `service`, `desktop` or `tooling`.", SetupBuilds.Problem("game"));
    }
}
