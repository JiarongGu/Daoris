using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A helper's answer kept as a word on the part that asked, which that part never took, goes with the line when it moves to a new
/// part (a resume or a carry-on in a new part), said there as the helper's answer and never the person's words, so the part that
/// goes on is handed the read answered rather than reporting it still out.
/// </summary>
public sealed class HelperAnswerCarryTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 12, 22, 16, 45, TimeSpan.Zero);

    private static readonly SaidWordView Answer = new("daaeec10", "Helper 65689c27 answered your read: the design holds.", At, [], Reopens: false)
    {
        Helper = "65689c27",
    };

    private static readonly SaidWordView Findings = new("f1", "A reviewer's findings.", At, [], Reopens: false) { By = "o1" };

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_new_part_of_the_line_takes_the_helpers_answer_the_part_before_never_took(bool resumed, bool switched)
    {
        var hands = DriverContinuations.Hands(resumed, switched);

        Assert.True(hands(Answer));
        Assert.False(hands(Findings));
    }

    [Fact]
    public void The_carry_on_is_told_the_answer_as_the_helpers_and_not_the_persons()
    {
        var target = new SessionTarget("q1", "Title", "Body", "person", "engine", "X:/engine", "http://localhost") { Handed = [Answer] };

        var told = string.Concat(ResumedPrompt.CarryingOnPieces(target, "it failed").Select(piece => piece.Text));

        Assert.Contains("the design holds.", told);
        Assert.Contains("a helper's answer", told);
        Assert.DoesNotContain("the person wrote to that session", told);
    }
}
