using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[CollectionDefinition("knowledge review port", DisableParallelization = true)]
public sealed class KnowledgeReviewPortCollection;

[Collection("knowledge review port")]
public sealed class KnowledgeReviewPortsTests
{
    [Fact]
    public void Review_installs_the_rooms_answer_before_Loop_exists()
    {
        var reader = KnowledgeReviews.Current;
        Assert.IsType<KnowledgeReviewReads>(reader);
        Assert.Null(reader.Review(DriverConfig.Empty, "app", "default"));
        Assert.Null(reader.Opinion(DriverConfig.Empty, "app", "default"));
        Assert.Equal("no review environment", reader.ReviewCell(null));
        Assert.Equal("no second opinion", reader.OpinionCell(null));
        Assert.Equal($"{OpinionRules.Waiting} {OpinionRules.StepWaiting} {OpinionRules.SafeNotHanded}", reader.GateWords);
    }

    [Fact]
    public void An_uninstalled_answer_throws_and_the_test_restores_it()
    {
        var reader = KnowledgeReviews.Current;
        try
        {
            KnowledgeReviews.Current = null!;
            Assert.Throws<InvalidOperationException>(() => KnowledgeReviews.Current);
        }
        finally
        {
            KnowledgeReviews.Current = reader;
        }
        Assert.Same(reader, KnowledgeReviews.Current);
    }
}
