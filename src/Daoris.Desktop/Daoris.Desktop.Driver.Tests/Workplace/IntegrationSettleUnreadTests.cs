using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A git that did not answer is no answer: the settle writes nothing and asks again, and never reads it as a branch that is gone.</summary>
public sealed class IntegrationSettleUnreadTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly IntegrationCandidate Candidate = new("c1", "engine", "aurora", "main", "base", "job", "run-a", 1, At);

    private static readonly IntegrationMember Member = new("c1", "r1", 1, IntegrationMemberStates.Promoted, At) { Replayed = "replayed" };

    private static readonly IntegrationRequest Request = new("r1", "engine", "aurora", "feature/x", "tip", IntegrationRoutes.Recorded, "main",
        IntegrationRequester.Card, new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.NoGates).Resolved(), At);

    private static async Task<(IntegrationSettles.Read? Read, string? Unread)> ReadAsync(string failing, int code)
    {
        GitFlow.Answered.Value = args => args[0] == failing ? (code, "", "git did not answer within 30 seconds, and was stopped.") : args[0] switch
        {
            "rev-parse" => (0, "branchtip\n", ""),
            "merge-base" => (0, "fork\n", ""),
            "rev-list" => (0, "replayed\n", ""),
            _ => (1, "", ""),
        };
        try
        {
            return await IntegrationSettles.ReadAsync("repo", Candidate, Member, Request, CancellationToken.None);
        }
        finally
        {
            GitFlow.Answered.Value = null;
        }
    }

    [Theory]
    [InlineData("rev-parse")]
    [InlineData("merge-base")]
    public async Task A_git_that_timed_out_is_asked_again(string failing)
    {
        var (read, unread) = await ReadAsync(failing, -1);

        Assert.Null(read);
        Assert.Contains("asked again", unread, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rev-parse")]
    [InlineData("merge-base")]
    public async Task A_git_that_finds_nothing_is_read_as_nothing(string failing)
    {
        var (read, unread) = await ReadAsync(failing, 1);

        Assert.Null(unread);
        Assert.NotNull(read);
    }
}
