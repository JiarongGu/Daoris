using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (the mixed agents design §2: "where the old agent wrote one through its tool, its own hand-over note, kept and shown as
/// that agent's claim"): the outgoing agent's note reaches the next agent beside Daoris's own record, marked as that agent's claim,
/// never in its place; a conversation's briefing and a quest's carry-on alike. Nothing changes where no note was written.
/// </summary>
public sealed class HandOverNoteBriefingTests
{
    /// <summary>SESSCTX1b: the conversation's briefing is Daoris's brief of its line (<see cref="ContextBrief"/>), the note its own section.</summary>
    [Fact]
    public void A_conversations_briefing_carries_the_outgoing_agents_note_as_its_claim_after_daoris_record()
    {
        var line = new BriefLine(HandOverWhy.Agent,
        [
            new BriefPart("s1", "claude-code",
            [
                new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Fix the parser." },
                new SessionEvent { Kind = SessionEventKind.Message, Text = "Parser done." },
                new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" },
            ]),
        ])
        {
            Conversation = true,
            State = new BriefTree("4f2a9c1 Parse", [" M src/Parser.cs"], []),
        };
        var briefed = ContextBrief.Compose(line with { Note = "ParserTests fail on empty input; start there." }).Text;
        var without = ContextBrief.Compose(line).Text;

        Assert.Contains("ParserTests fail on empty input; start there.", briefed);
        Assert.Contains("claim", briefed);
        Assert.True(briefed.IndexOf("Uncommitted in the tree", StringComparison.Ordinal) < briefed.IndexOf("ParserTests fail", StringComparison.Ordinal));
        Assert.Equal(without, ContextBrief.Compose(line with { Note = "  " }).Text);
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
