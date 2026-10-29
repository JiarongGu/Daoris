using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e1 over real git (D215's note; the integration design §4.6, §17.4): the landing and its advance write their
/// acceptance's provenance, and the one door's recorded-landing path admits a landing from its record alone, bound to the tip the record
/// holds, its gate proof read as the record keeps it, never twice and never one the line holds. Called from no production path yet.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationRequestsGitTests : LandedFixture
{
    private const string Pattern = "feature/{quest}-{slug}";

    /// <summary><c>engine</c>'s own branch rule, with <paramref name="integrate"/> or none.</summary>
    private void Save(LandingRule rule, LandingIntegrate? integrate) =>
        DriverConfig.Empty.WithLanding("engine", rule).WithLandingIntegrate("engine", integrate)
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    /// <summary>
    /// The landing writes, beside who accepted it and the rule, that <c>integrate</c> stood with its <c>request</c> and the rule as its
    /// source; an advance made under the rule as it then stands writes its own, as it writes its own <c>acceptedBy</c>, and the landing's
    /// stays as it was accepted.
    /// </summary>
    [Fact]
    public async Task A_landing_and_its_advance_each_write_the_integrate_that_stood_at_its_acceptance()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the work\n", "the work");
        var landed = await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"));
        Assert.True(landed.Landed, landed.Message);

        var entry = trees.Recorded.Of("engine", landed.Branch!)!;
        Assert.Equal((AcceptedBy.Person, new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule)), (entry.AcceptedBy, entry.Integrate));

        Save(new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true),
            new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared));
        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "more.txt", "more work\n", "more work");
        var advanced = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.True(advanced.Landed, advanced.Message);
        Assert.Equal(landed.Branch, advanced.Branch);

        var moved = trees.Recorded.Of("engine", landed.Branch!)!;
        var advance = Assert.Single(moved.Advances);
        Assert.Equal((AcceptedBy.Person, new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule)), (advance.AcceptedBy, advance.Integrate));
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule), moved.Integrate);
    }
}
