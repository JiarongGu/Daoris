using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A rule's act said on the line by its code, kept in the decision record with
/// <c>holder.kind</c> <c>rule</c> and no question, read back by its holder kind, and passed by the questions' reader.
/// </summary>
public sealed class SupervisorRuleActsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-rule-acts", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorRuleActsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static readonly SupervisorRule AloneRule = new("answers-alone", SupervisorRules.AnswersAlone, "in-a-row", 2, SupervisorRules.Hold, null, "repository:daoris", null);

    private static RuleActed Act(DateTimeOffset at) => new("412", "d-1", AloneRule, 3, 2, Known: true, "go-on", at);

    [Fact]
    public void CONTEXTDRIVE1e_a_rule_s_act_is_kept_final_with_its_holder_and_read_back_by_its_kind()
    {
        var acts = new SupervisorRuleActs(_home);
        var first = Act(DateTimeOffset.Parse("2026-11-05T10:00:00Z"));
        acts.Record(first);
        acts.Record(Act(DateTimeOffset.Parse("2026-11-05T10:05:00Z")));

        var read = acts.Of("d-1");
        Assert.Equal(2, read.Count);
        Assert.Equal((first.Line, first.Rule, first.Revision, first.N, first.Point, first.At), (read[0].Line, read[0].Rule, read[0].Revision, read[0].N, read[0].Point, read[0].At));

        var text = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "d-1.decisions"), first.Id + ".json"));
        Assert.Contains("\"kind\": \"rule\"", text);
        Assert.DoesNotContain("\"question\"", text);
        Assert.False(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "d-1.decisions"), first.Id + ".states")));
        Assert.Null(SupervisorRuleActs.Parse("""{ "id": "x", "holder": { "kind": "terminal" }, "question": { "id": "q" } }"""));
        Assert.Empty(acts.Of("d-unknown"));
    }

    [Fact]
    public void CONTEXTDRIVE1e_the_note_names_the_count_its_value_the_rule_its_place_and_the_act()
    {
        var note = SupervisorRuleActs.Note(Act(DateTimeOffset.Parse("2026-11-05T10:00:00Z")));

        Assert.Equal("Your rule for repository:daoris acted on this line: answers-alone stood at 2, and the rule says hold at 2.", note.Text);
        Assert.Contains(note.Parts!, part => part.Code == HeldNoteCodes.SupervisorRule.Code);
        Assert.Contains(NoteCodes.All, code => code.Code == "supervisor.rule");
    }
}
