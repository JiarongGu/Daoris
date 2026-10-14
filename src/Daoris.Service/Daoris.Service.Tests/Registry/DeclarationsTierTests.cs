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
        string name, string summary, string[] owns, string[] accepts, string workspace = "work", bool adopted = true,
        string? root = null) =>
        new(name, adopted, summary, owns, accepts, [], 0, Root: root, Workspace: workspace);

    /// <summary>
    /// A repository registered here with a root and no manifest (D70): askable, and declaring nothing a
    /// sentence's words could overlap.
    /// </summary>
    private static Registration Unadopted(string name, string workspace = "work") =>
        new(name, Adopted: false, null, [], [], [], 0, Root: $"/trees/{name}", Workspace: workspace);

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
    /// Only a DECLARATION is matched on words: a repository in another circle would be a proposal the
    /// exchange then refuses — the workspace is the unit of sharing (D48 §4) — and one that has not
    /// adopted has declared nothing to match. Its summary here is the test: it is never read.
    /// </summary>
    [Fact]
    public void Only_adopted_repositories_in_the_asks_own_circle_are_matched_on_words()
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

    /// <summary>
    /// ASKNAME1: an ask opened by naming its repository proposes THAT repository first, adopted or not,
    /// with its name as the evidence. On the owner's install the named one was registered and askable
    /// (D70) but declared nothing, so a different, adopted repository was proposed alone on words — the
    /// one word that settles the receiver counted for nothing.
    /// </summary>
    [Fact]
    public void A_repository_the_sentence_names_is_proposed_first_over_a_richer_word_overlap()
    {
        Registration[] circle = [.. Circle, Unadopted("release-infra")];

        var ranked = DeclarationsTier.Rank(
            "In release-infra: the media config and the video and image field names break the transcoding",
            circle, "work");

        Assert.Equal(["release-infra", "media-api"], ranked.Select(match => match.Repository).ToArray());
        Assert.Equal(["release-infra"], ranked[0].Matched);
        Assert.True(ranked[0].Score > ranked[1].Score, $"{ranked[0].Score} should outrank {ranked[1].Score}");
    }

    /// <summary>
    /// A name is named only WHOLE: a longer name or word that contains it names something else, and a
    /// sentence in any case names it. A script written without spaces between words has no edge to find,
    /// so beside an ideograph a name is named as written — the last row is that gap, stated.
    /// </summary>
    [Theory]
    [InlineData("release-infra", "In release-infra: rotate the signing keys", true)]
    [InlineData("release-infra", "rotate the signing keys in RELEASE-INFRA.", true)]
    [InlineData("release-infra", "see release-infra/deploy.yml for the job", true)]
    [InlineData("release-infra", "the release-infra's keys expired", true)]
    [InlineData("release-infra", "the release-infra-v2 keys expired", false)]
    [InlineData("release-infra", "the release-infra.next keys expired", false)]
    [InlineData("release-infra", "the old-release-infra keys expired", false)]
    [InlineData("release-infra", "the prerelease-infra keys expired", false)]
    [InlineData("release-infra", "the release-infrastructure keys expired", false)]
    [InlineData("portal", "the portal-ui page is blank", false)]
    [InlineData("orders-db", "the orders-db-v2 migration stalls", false)]
    [InlineData("orders-db", "the orders-db migration stalls", true)]
    [InlineData("media", "the media-api config is wrong", false)]
    [InlineData("storefront", "the storefront-v2 checkout is wrong", false)]
    [InlineData("storefront", "请在storefront里修改价格", true)]
    [InlineData("媒体服务", "请在媒体服务里修改配置", true)]
    [InlineData("媒体服务", "媒体服务器的配置有误", true)]
    public void A_name_is_named_only_whole(string name, string sentence, bool named)
    {
        var ranked = DeclarationsTier.Rank(sentence, [Unadopted(name)], "work");

        if (named)
        {
            var match = Assert.Single(ranked);
            Assert.Equal(name, match.Repository);
            Assert.Equal([name], match.Matched);
        }
        else
        {
            Assert.Empty(ranked);
        }
    }

    /// <summary>
    /// Naming does not cross a circle (D48 §4): a repository of another workspace named in the sentence
    /// would be a proposal the exchange refuses. Nor does it reach one nobody here can ask — no manifest
    /// and no root (D70) — for the same reason.
    /// </summary>
    [Fact]
    public void A_repository_named_in_another_circle_or_unaskable_is_never_proposed()
    {
        Registration[] mixed =
        [
            .. Circle,
            Unadopted("release-infra", workspace: "home"),
            Repo("billing", "Invoices, in another circle.", ["invoices"], [], workspace: "home"),
            new("archive", Adopted: false, null, [], [], [], 0, Workspace: "work"),
        ];

        var ranked = DeclarationsTier.Rank(
            "release-infra, billing and archive all read the media config", mixed, "work");

        Assert.Equal("media-api", Assert.Single(ranked).Repository);
    }

    /// <summary>
    /// Several named repositories keep the order the sentence names them in, all before any overlap, and
    /// a named one is proposed once — its name the evidence, even where its declaration also overlaps.
    /// </summary>
    [Fact]
    public void Named_repositories_keep_the_sentences_order_ahead_of_every_overlap()
    {
        Registration[] circle = [.. Circle, Unadopted("release-infra")];

        var ranked = DeclarationsTier.Rank(
            "release-infra should publish what storefront shows on the checkout and product pages, from the media config",
            circle, "work");

        Assert.Equal(["release-infra", "storefront", "media-api"], ranked.Select(match => match.Repository).ToArray());
        Assert.Equal(["storefront"], ranked[1].Matched);
        Assert.True(ranked[0].Score > ranked[1].Score && ranked[1].Score > ranked[2].Score,
            string.Join(", ", ranked.Select(match => match.Score)));
    }

    /// <summary>The list still caps at <see cref="DeclarationsTier.Proposed"/>, named or not.</summary>
    [Fact]
    public void Named_repositories_still_cap_at_the_proposal_length()
    {
        Registration[] circle = [.. Circle, Unadopted("release-infra")];

        var ranked = DeclarationsTier.Rank(
            "importer feeds storefront, which media-api serves, and release-infra ships them all", circle, "work");

        Assert.Equal(["importer", "storefront", "media-api"], ranked.Select(match => match.Repository).ToArray());
    }
}
