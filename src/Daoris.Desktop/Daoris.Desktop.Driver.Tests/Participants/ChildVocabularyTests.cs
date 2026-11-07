using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A lineage line is read in its agent's own words before the fold, by its provider's <see cref="IChildVocabulary"/>,
/// so the fold names no agent: Claude Code joins a call to its agent by the hook's line naming the same tool use, Codex names a thread
/// with its parent and its root, and an agent that declares no vocabulary reads no call.
/// </summary>
public sealed class ChildVocabularyTests
{
    private static JsonObject Line(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Claude_Code_joins_a_call_to_its_agent_by_the_hook_s_line_beside_it()
    {
        var lines = ClaudeChildVocabulary.Vocabulary.Lines();
        lines.Learn(Line("""{"by":"hook","event":"call","session":"root-1","toolUse":"t1","agent":"a1"}"""));
        lines.Learn(Line("""{"by":"hook","event":"call","session":"root-1","toolUse":"t2"}"""));
        lines.Learn(Line("""{"by":"hook","event":"call","session":"root-1","toolUse":"t3","agentUnreadable":true}"""));

        Assert.Equal("root-1", lines.Root);
        Assert.Equal(["root-1"], lines.Roots);
        Assert.Equal(ChildCall.Of("a1"), lines.Call(Line("""{"by":"call","door":"claude-code","toolUse":"t1"}""")));
        Assert.Equal(ChildCall.Root, lines.Call(Line("""{"by":"call","door":"claude-code","toolUse":"t2"}""")));
        Assert.Equal(ChildCall.Unknown, lines.Call(Line("""{"by":"call","door":"claude-code","toolUse":"t3"}""")));
        Assert.Equal(ChildCall.Unjoined, lines.Call(Line("""{"by":"call","door":"claude-code","toolUse":"t9"}""")));
        Assert.Equal(ChildCall.Unknown, lines.Call(Line("""{"by":"call","door":"claude-code"}""")));

        Assert.Equal(ChildCall.Of("a1"), ClaudeChildVocabulary.Vocabulary.Wake(Line("""{"by":"hook","event":"call","agent":"a1"}""")));
        Assert.Equal(ChildCall.None, ClaudeChildVocabulary.Vocabulary.Wake(Line("""{"by":"call","door":"claude-code","toolUse":"t1"}""")));
        Assert.Null(ClaudeChildVocabulary.Vocabulary.Wake(Line("""{"by":"hook","event":"started","agent":"a1"}""")));
    }

    [Fact]
    public void Codex_names_a_thread_with_no_parent_its_root_and_a_malformed_call_unknown()
    {
        var lines = CodexChildVocabulary.Vocabulary.Lines();
        lines.Learn(Line("""{"by":"call","door":"codex","thread":"th-root"}"""));
        lines.Learn(Line("""{"by":"call","door":"codex","thread":"th-2","parent":"th-root","root":"th-root"}"""));

        Assert.Equal("th-root", lines.Root);
        Assert.Equal(ChildCall.Root, lines.Call(Line("""{"by":"call","door":"codex","thread":"th-root"}""")));
        Assert.Equal(ChildCall.Of("th-2"), lines.Call(Line("""{"by":"call","door":"codex","thread":"th-2","parent":"th-root"}""")));
        Assert.Equal(ChildCall.Unknown, lines.Call(Line("""{"by":"call","door":"codex","thread":"th-2","malformed":"parent"}""")));

        Assert.Equal(new ChildCall(ChildCallKind.Root, "th-9"), CodexChildVocabulary.Vocabulary.Wake(Line("""{"by":"call","door":"codex","thread":"th-9"}""")));
        Assert.Equal(ChildCall.None, CodexChildVocabulary.Vocabulary.Wake(Line("""{"by":"call","door":"codex"}""")));
    }

    [Fact]
    public void The_fold_reads_each_agent_s_child_in_its_own_words_and_a_call_of_an_agent_with_none_as_unknown()
    {
        var codex = ChildBindings.Fold(
        [
            """{"by":"call","door":"codex","thread":"th-root","at":"2026-11-06T10:00:00.000Z"}""",
            """{"by":"call","door":"codex","thread":"th-2","parent":"th-root","at":"2026-11-06T10:00:01.000Z"}""",
            """{"by":"call","door":"dsh","thread":"th-3","parent":"th-root","at":"2026-11-06T10:00:02.000Z"}""",
        ]);

        Assert.Equal(("codex", "th-root"), (codex.Harness, codex.Root));
        var child = Assert.Single(codex.Children);
        Assert.Equal(("codex", "th-2", "th-root"), (child.Harness, child.Native, child.Parent));
        Assert.Equal(1, codex.RootCalls);
        Assert.Equal(1, codex.UnknownCalls);
        Assert.False(codex.Wakes("""{"by":"call","door":"dsh","thread":"th-4","parent":"th-root"}"""));
        Assert.True(codex.Wakes("""{"by":"announced","event":"started","agent":"x"}"""));
    }
}
