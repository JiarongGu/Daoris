using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1e (the context counters design §4.6 point 2): what keeps a supervisor's answer from going on, read at its keeping and again
/// at its release: <c>supervisor-bound</c> at the answers-alone rule, <c>rule-holds</c> where another hold rule stands at its number.
/// </summary>
public sealed class SupervisorRuleHoldTests
{
    private static readonly CountFact Failures =
        new(SupervisorRules.Failures, 3, "known", null, "records") { Watch = new CountWatch(3, SupervisorRules.Hold, "machine") };

    private static readonly CountFact Alone =
        new(SupervisorRules.AnswersAlone, 5, "known", null, "admissions") { Watch = new CountWatch(2, SupervisorRules.Hold, "machine") };

    [Fact]
    public void CONTEXTDRIVE1e_a_hold_rule_at_its_number_is_rule_holds_and_answers_alone_keeps_its_own_code()
    {
        Assert.Equal(SupervisorWhy.RuleHolds, SupervisorRuleHold.Why(atBound: false, [Failures]));
        Assert.Null(SupervisorRuleHold.Why(atBound: false, [Alone, Failures with { N = 2 }]));
        Assert.Null(SupervisorRuleHold.Why(atBound: false, [Failures with { N = null }]));
        Assert.Equal(SupervisorWhy.SupervisorBound, SupervisorRuleHold.Why(atBound: true, [Failures]));
        Assert.True(new SupervisedPark("daoris", true, true, null, false, false, false) { Counts = [Failures] }.RuleHolds);
    }
}
