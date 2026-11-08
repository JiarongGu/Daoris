using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A chat run's candidate numbers: each names one candidate of the run for good, whatever branch the run is on when it is submitted.</summary>
public sealed class ChatCandidatesTests : IDisposable
{
    private const string Run = "chat-c1";

    private const string Tip1 = "1111111111111111111111111111111111111111";

    private const string Tip2 = "2222222222222222222222222222222222222222";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-candidates-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private (ChatCandidate Candidate, CandidateSubmitted What) Submit(string branch, string tip, Func<string, bool>? landed = null) =>
        ChatCandidates.Submit(_home, Run, "engine", "default", branch, tip, DateTimeOffset.UtcNow, landed ?? (_ => false));

    /// <summary>A press after the run's branch changed opens the next number: the earlier candidate's id is the one an ended run is judged unchanged by.</summary>
    [Fact]
    public void A_press_on_another_branch_numbers_past_the_candidates_of_the_run()
    {
        Assert.Equal(1, Submit("daoris/a", Tip1).Candidate.Number);

        var moved = Submit("daoris/b", Tip2);

        Assert.Equal((2, CandidateSubmitted.Opened), (moved.Candidate.Number, moved.What));
        Assert.Equal(["daoris/b"], [ChatCandidates.Read(_home, Run)!.Branch]);
        Assert.Equal(3, Submit("daoris/b", Tip1, landed: _ => true).Candidate.Number);
    }
}
