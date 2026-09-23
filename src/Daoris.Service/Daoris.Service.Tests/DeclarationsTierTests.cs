using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The intake's floor (D65 §1b, <c>model-decoupling</c>): with no harness at all, an ask is still
/// read against what each repository DECLARED it owns and accepts, and the best overlaps are
/// PROPOSED — ranked, with the words that matched as the evidence, never published unasked.
/// </summary>
public sealed class DeclarationsTierTests
{
    private static Registration Repo(
        string name, string summary, string[] owns, string[] accepts, string workspace = "work", bool adopted = true) =>
        new(name, adopted, summary, owns, accepts, [], 0, Workspace: workspace);

    private static readonly Registration[] Circle =
    [
        Repo("media-api", "The media service: uploads, transcoding and the media config.",
            ["media config", "video and image field names", "transcoding"], ["a media field bug"]),
        Repo("storefront", "The Angular storefront.", ["product pages", "checkout"], ["a UI bug on a product page"]),
        Repo("importer", "Nightly catalogue importer.", ["catalogue import"], ["an import failure"]),
    ];

    [Fact]
    public void The_repository_whose_declarations_share_the_most_words_ranks_first()
    {
        var ranked = DeclarationsTier.Rank(
            "check the ticket and use the media config instead of hard coding the video/image field name",
            Circle, "work");

        Assert.Equal("media-api", ranked[0].Repository);
        Assert.Contains("media", ranked[0].Matched);
        Assert.Contains("config", ranked[0].Matched);
    }

    /// <summary>The evidence is the point: a proposal that cannot say why is a guess wearing a score.</summary>
    [Fact]
    public void Every_candidate_says_which_words_matched_and_nothing_else_is_proposed()
    {
        var ranked = DeclarationsTier.Rank("the checkout page shows the wrong product price", Circle, "work");

        Assert.Equal("storefront", Assert.Single(ranked).Repository);
        Assert.Equal(["checkout", "page", "product"], ranked[0].Matched.Order().ToArray());
    }

    [Fact]
    public void A_sentence_nothing_declared_shares_proposes_nobody()
    {
        Assert.Empty(DeclarationsTier.Rank("rotate the office wifi password", Circle, "work"));
    }

    /// <summary>
    /// Only what can be ASKED is proposed: a repository in another circle, or one that has not adopted,
    /// would be a proposal the exchange then refuses — the workspace is the unit of sharing (D48 §4).
    /// </summary>
    [Fact]
    public void Only_adopted_repositories_in_the_asks_own_circle_are_candidates()
    {
        Registration[] mixed =
        [
            ..Circle,
            Repo("media-elsewhere", "Media config, in another circle.", ["media config"], [], workspace: "home"),
            Repo("media-unadopted", "Media config, never adopted.", ["media config"], [], adopted: false),
        ];

        var ranked = DeclarationsTier.Rank("the media config", mixed, "work");

        Assert.Equal("media-api", Assert.Single(ranked).Repository);
    }

    /// <summary>Words that match everything match nothing — a sentence's glue is not evidence.</summary>
    [Fact]
    public void Common_words_are_not_evidence()
    {
        Assert.Empty(DeclarationsTier.Rank("this should be the one that they have with some other things", Circle, "work"));
    }

    /// <summary>A plural and its singular are the same word to a person reading a declaration.</summary>
    [Fact]
    public void A_plural_matches_its_singular()
    {
        var ranked = DeclarationsTier.Rank("the imports failed again", Circle, "work");

        Assert.Equal("importer", ranked[0].Repository);
    }

    /// <summary>中文 is matched in bigrams, the way the index is (`Text.Segment`).</summary>
    [Fact]
    public void A_chinese_sentence_matches_a_chinese_declaration()
    {
        Registration[] circle = [Repo("媒体服务", "媒体配置与转码", ["媒体配置"], ["字段问题"])];

        var ranked = DeclarationsTier.Rank("请把视频字段改为读取媒体配置", circle, "work");

        Assert.Equal("媒体服务", Assert.Single(ranked).Repository);
        Assert.Contains("媒体", ranked[0].Matched);
    }
}
