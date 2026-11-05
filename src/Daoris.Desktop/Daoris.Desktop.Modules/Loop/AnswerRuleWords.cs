using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The rule behind a held answer (CONTEXTDRIVE1h; the context counters design §7.5): the person's rule that acted on the line as the last act of
/// its answers-alone count read back from the line's decision record, so the head, the foot and <i>What needs you</i> say one hold in one
/// sentence, with the place whose rule it is and the count it watched. None where no rule acted (a hold marked before the rules).
/// </summary>
public static class AnswerRuleWords
{
    /// <summary>The wire's rule of the answer held on <paramref name="session"/>, or null.</summary>
    public static object? Of(string home, string session)
    {
        var act = new SupervisorRuleActs(home).Of(session).LastOrDefault(each => each.Rule.Count == SupervisorRules.AnswersAlone && each.Rule.Do == SupervisorRules.Hold);
        return act is null ? null : new { act.Rule.Place, act.Rule.At, act.N, Count = act.Rule.Count, Do = act.Rule.Do };
    }
}
