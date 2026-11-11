using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A second Apply on a card the first press already settled is answered <i>already applied</i> with what the first came to, and logged with
/// its reason, never refused bare.
/// </summary>
public sealed class HelpAppliedAgainTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-again-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpAppliedAgainTests() => Directory.CreateDirectory(HelpProposals.FolderOf(_home));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HelpProposal Settled(string state)
    {
        File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p1.json"), $$"""{"id":"p1","kind":"workflow","state":"{{state}}"}""");
        return new HelpProposal("p1", "workflow", "new", "work", null, null, null, "the person asked", "s1", state);
    }

    [Fact]
    public void A_second_apply_on_an_applied_card_is_answered_with_the_first_presses_outcome()
    {
        var proposal = Settled("applied");
        HelpAppliedAgain.Keep(_home, "p1", "Saved workflow `ship` version 2 for `work`.");

        var again = HelpAppliedAgain.Answer(_home, proposal);

        Assert.True(again.Applied);
        Assert.StartsWith("Already applied:", again.Told);
        Assert.Contains("Saved workflow `ship` version 2 for `work`.", again.Told);
    }

    [Fact]
    public void A_second_apply_on_a_refused_card_says_it_was_refused_and_its_log_line_names_the_reason()
    {
        var proposal = Settled("refused");

        var again = HelpAppliedAgain.Answer(_home, proposal);

        Assert.False(again.Applied);
        Assert.StartsWith("Already refused:", again.Told);
        Assert.Contains(("reason", (object?)HelpAppliedAgain.Reason), HelpAppliedAgain.Line(proposal));
        Assert.Contains(("state", (object?)"refused"), HelpAppliedAgain.Line(proposal));
    }
}
