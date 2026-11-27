using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the work is told when a step it fed fails and a back wire takes the failure to it: the code, and a check's own finding.</summary>
public sealed class NodeRunKeepingWordsTests
{
    [Fact]
    public void A_failed_step_with_no_finding_says_its_code_and_that_the_work_goes_back()
    {
        var words = NodeRunKeeping.FailedWords("check", "gate-failed", null);

        Assert.Equal(
            "Daoris: `check` failed (`gate-failed`), so your work goes back to you: mend what it found and commit, and what you commit goes through the workflow again. "
            + FailedWordsRule, words);
    }

    private const string FailedWordsRule =
        "It is checked again on what you changed, scoped to its reach: ask for no full or end-to-end run unless the repository's own verification rule gives this change a reason for one.";

    [Fact]
    public void A_red_check_tells_the_work_it_is_checked_again_on_what_changed_and_that_a_full_run_needs_the_rules_reason()
    {
        Assert.EndsWith(FailedWordsRule, NodeRunKeeping.FailedWords("check", null, "the gate 'reached' failed with exit 1"));
        Assert.Contains(FailedWordsRule, SpentChecks.Words("check", HeavyExecutor.GateFailed, "the gate 'reached' failed with exit 1", 3, NodeTold.OwnerHeld, null));
    }

    [Fact]
    public void A_failed_check_says_what_its_newest_result_found_so_the_session_can_put_it_right()
    {
        var words = NodeRunKeeping.FailedWords("check", null, "Daoris read this hand-back from git: 1 commit behind `main`.");

        Assert.Contains("`check` failed, so your work goes back to you:", words);
        Assert.Contains("\n\nDaoris read this hand-back from git: 1 commit behind `main`.\n\nMend what it found and commit", words);
    }
}
