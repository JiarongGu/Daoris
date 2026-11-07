using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A plugin's pull-request answer is read whole or not at all: a commit id is a full hex id and nothing else, a trailing newline included.</summary>
public sealed class PullRequestAnswersTests
{
    private const string Id = "0123456789abcdef0123456789abcdef01234567";

    private static PullRequestState? Read(string merge, string source = Id)
    {
        using var answer = JsonDocument.Parse(JsonSerializer.Serialize(new { state = "completed", mergeCommit = merge, sourceCommit = source }));
        return PullRequestAnswers.Read(answer.RootElement, "acme-host");
    }

    [Fact]
    public void A_full_commit_id_reads()
    {
        Assert.NotNull(Read(Id));
    }

    [Theory]
    [InlineData(Id + "\n")]
    [InlineData("\n" + Id)]
    [InlineData(Id + " ")]
    public void A_commit_id_with_anything_around_it_is_not_an_answer(string merge)
    {
        Assert.Null(Read(merge));
        Assert.Null(Read(Id, merge));
    }
}
