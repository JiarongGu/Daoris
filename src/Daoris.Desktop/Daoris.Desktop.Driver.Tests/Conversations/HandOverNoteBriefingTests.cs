using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (the mixed agents design §2: "where the old agent wrote one through its tool, its own hand-over note, kept and shown as
/// that agent's claim"): the outgoing agent's note reaches the next agent beside Daoris's own record, marked as that agent's claim,
/// never in its place; a conversation's briefing and a quest's carry-on alike. Nothing changes where no note was written.
/// </summary>
public sealed class HandOverNoteBriefingTests
{
    [Fact]
    public void A_conversations_briefing_carries_the_outgoing_agents_note_as_its_claim_after_daoris_record()
    {
        var briefed = HandOverBriefing.Compose(
            "claude-code", "s1", HandOverWhy.Agent, "Fix the parser.", [], "Parser done.", ["src/Parser.cs"],
            note: "ParserTests fail on empty input; start there.");
        var without = HandOverBriefing.Compose("claude-code", "s1", HandOverWhy.Agent, "Fix the parser.", [], "Parser done.", ["src/Parser.cs"]);

        Assert.Contains("ParserTests fail on empty input; start there.", briefed);
        Assert.Contains("claim", briefed);
        Assert.True(briefed.IndexOf("Uncommitted in this tree", StringComparison.Ordinal) < briefed.IndexOf("ParserTests fail", StringComparison.Ordinal));
        Assert.Equal(without, HandOverBriefing.Compose("claude-code", "s1", HandOverWhy.Agent, "Fix the parser.", [], "Parser done.", ["src/Parser.cs"], note: "  "));
        Assert.DoesNotContain("claim", without);
    }

    [Fact]
    public void A_quests_carry_on_is_told_the_note_beside_its_records_reading()
    {
        var target = new SessionTarget("q1", "Fix the parser", "Fix it.", "Owner", "engine", "trees/engine/q1", "http://stand-in")
        {
            CutOff = "Handed over to codex-acp.",
            HandedOver = "claude-code",
            HandedNote = "ParserTests fail on empty input; start there.",
        };
        var plain = target with { HandedNote = null };

        var told = TargetPrompt.Compose(target);

        Assert.Contains("ParserTests fail on empty input; start there.", told);
        Assert.Contains("that agent's own claim", told);
        Assert.DoesNotContain("ParserTests fail", TargetPrompt.Compose(plain));
    }
}
