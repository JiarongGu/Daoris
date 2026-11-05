using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CONTEXTDRIVE1h: a held answer carries the rule that held it, read back from the line's decision record: the place whose rule it is, what
/// the count stood at and the threshold, so the head, the foot and <i>What needs you</i> say one hold in one sentence; an act of another
/// count or another act is not its rule.
/// </summary>
public sealed class AnswerRuleWordsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-answer-rule-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    private static SupervisorRule Rule(string id, string count, string window, int at, string place) => new(id, count, window, at, SupervisorRules.Hold, null, place, null);

    private JsonObject? RuleOfFirstWait()
    {
        var waits = JsonSerializer.SerializeToNode(AttentionStateSlice.AnswerWaits(_home), IpcJson.Options)!.AsArray();
        return waits[0]!["rule"]?.AsObject();
    }

    [Fact]
    public void CONTEXTDRIVE1h_a_held_answer_with_no_act_on_record_says_no_rule()
    {
        new GoOnMarks(_home).Mark("s1", ["w3"], GoOnMarks.AnswersAlone, At, agent: "codex-acp");

        Assert.Null(RuleOfFirstWait());
    }

    [Fact]
    public void CONTEXTDRIVE1h_a_held_answer_carries_the_answers_alone_rule_that_held_it_not_another_count_s()
    {
        new GoOnMarks(_home).Mark("s1", ["w3"], GoOnMarks.AnswersAlone, At, agent: "codex-acp");
        var acts = new SupervisorRuleActs(_home);
        acts.Record(new RuleActed("412", "s1", Rule("r-1", SupervisorRules.Failures, "since-try-again", 3, "machine"), 1, 3, true, "start", At));
        Assert.Null(RuleOfFirstWait());

        acts.Record(new RuleActed("412", "s1", Rule("r-2", SupervisorRules.AnswersAlone, "in-a-row", 2, "repository:daoris"), 4, 2, true, "go-on", At.AddMinutes(1)));

        var rule = RuleOfFirstWait()!;
        Assert.Equal(("repository:daoris", 2, 2, "answers-alone", "hold"),
            (rule["place"]!.GetValue<string>(), rule["at"]!.GetValue<int>(), rule["n"]!.GetValue<int>(), rule["count"]!.GetValue<string>(), rule["do"]!.GetValue<string>()));
    }
}
