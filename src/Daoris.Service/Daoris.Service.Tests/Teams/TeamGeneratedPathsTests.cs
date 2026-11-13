using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The outputs a repository declares generated, read from its own <c>.gitattributes</c> as the driver reads them (<c>linguist-generated</c>), so a
/// plan strips a claim on one without a part's pack having to say so. Pure text, so each runs in microseconds.
/// </summary>
public sealed class TeamGeneratedPathsTests
{
    [Fact]
    public void A_path_marked_generated_is_read_and_a_path_unmarked_or_unset_is_not()
    {
        const string attributes = """
            * text=auto eol=lf
            docs/code-map.json linguist-generated
            /src/web/data/api.ts linguist-generated=true
            docs/other.json -linguist-generated
            docs/off.json linguist-generated=false
            docs/index/** linguist-generated -diff
            CHANGELOG.md merge=union
            # docs/commented.json linguist-generated
            """;

        Assert.Equal(["docs/code-map.json", "src/web/data/api.ts", "docs/index/**"], TeamPathPolicies.GeneratedIn(attributes));
    }

    [Fact]
    public void A_pattern_that_matches_at_any_depth_is_not_read_as_a_root_path()
    {
        Assert.Equal(["package-lock.json"], TeamPathPolicies.GeneratedIn("package-lock.json linguist-generated\n*.g.cs linguist-generated\n"));
    }

    [Fact]
    public void A_declared_generated_output_strips_a_claim_on_it_with_no_pack_naming_it()
    {
        var policy = new TeamPathPolicy([], TeamPathPolicies.GeneratedIn("docs/code-map.json linguist-generated\n"));
        var part = new TeamPart("a", "api", "Add the route", "why", "the route answers") { Claims = ["docs/code-map.json", "src/route.cs"] };

        var judged = TeamPlanJudge.Judge([part], [], [], [], _ => policy);

        Assert.Equal(["src/route.cs"], judged.Parts[0].Claims);
        Assert.Contains(judged.Flags, flag => flag.Kind == TeamFlags.ClaimGenerated);
    }
}
