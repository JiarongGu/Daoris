using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (the co-agent design §2.3 steps 1 and 3, §3.3): what the ask and its lead's record say of a piece, the production choice's
/// admission (nothing measured, nothing admitted) and the partner's instruction, whole, in <c>golden/partner-prompt/</c>.
/// </summary>
public sealed class PartnerJudgeTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static PartnerAsk Ask(string piece = "Make the window seven days.", IReadOnlyList<string>? paths = null, string @base = "abc123", string? agent = "codex-acp") =>
        new("p1", "lead", piece, paths ?? ["src"], agent, @base, [], PartnerAsk.ByPerson, At);

    private static PriorSession Lead(string? tree = "trees/default/reports/s-1") =>
        new("lead", tree, "working", Repository: "reports");

    [Fact]
    public void A_working_lead_with_a_tree_and_a_sound_ask_may_go_on()
    {
        Assert.Null(PartnerJudge.Refusal(Ask(), Lead()));
    }

    /// <summary>Each refused asker, a blank or overlong piece, a path outside, a base that names no commit, each by its code.</summary>
    [Fact]
    public void Each_refusal_is_its_own_code()
    {
        Assert.Equal(PartnerWhy.AskerGone, PartnerJudge.Refusal(Ask(), null));
        Assert.Equal(PartnerWhy.AskerUnfit, PartnerJudge.Refusal(Ask(), Lead() with { Serves = "other" }));
        Assert.Equal(PartnerWhy.AskerUnfit, PartnerJudge.Refusal(Ask(), Lead() with { Opinion = "o1" }));
        Assert.Equal(PartnerWhy.AskerUnfit, PartnerJudge.Refusal(Ask(), Lead() with { Ask = "a1" }));
        Assert.Equal(PartnerWhy.AskerUnfit, PartnerJudge.Refusal(Ask(), Lead() with { HelpMode = "restricted" }));
        Assert.Equal(PartnerWhy.AskerUnfit, PartnerJudge.Refusal(Ask(), new PriorSession("lead", null, "working", Repository: "reports")));
        Assert.Equal(PartnerWhy.Blank, PartnerJudge.Refusal(Ask(piece: "  "), Lead()));
        Assert.Equal(PartnerWhy.Blank, PartnerJudge.Refusal(Ask(piece: new string('x', PartnerAsk.PieceLimit + 1)), Lead()));
        Assert.Equal(PartnerWhy.PathOutside, PartnerJudge.Refusal(Ask(paths: ["../other"]), Lead()));
        Assert.Equal(PartnerWhy.PathOutside, PartnerJudge.Refusal(Ask(paths: ["C:/elsewhere"]), Lead()));
        Assert.Equal(PartnerWhy.PathOutside, PartnerJudge.Refusal(Ask(paths: ["/abs"]), Lead()));
        Assert.Equal(PartnerWhy.NoCommit, PartnerJudge.Refusal(Ask(@base: "main"), Lead()));
    }

    /// <summary>
    /// 🔴 D199 point 4: with no measured row, the production choice admits nobody; an ask that names no agent is refused before any walk,
    /// never substituted.
    /// </summary>
    [Fact]
    public async Task Nothing_is_admitted_without_a_measured_row_and_an_unnamed_agent_is_never_substituted()
    {
        var admitted = PartnerChoice.Admit(new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp reads it."));
        Assert.Equal((false, PartnerWhy.NotMeasured), (admitted.Chosen, admitted.Code));

        var unnamed = await PartnerChoice.ChooseAsync(null!, Ask(agent: null), "claude-code-acp", DriverConfig.Empty, null);
        Assert.Equal((false, PartnerWhy.NotListed), (unnamed.Chosen, unnamed.Code));
    }

    [Theory]
    [InlineData(true, 0, false, PartnerEnds.Stopped)]
    [InlineData(false, null, false, PartnerEnds.OutOfTime)]
    [InlineData(false, 1, true, PartnerEnds.Limited)]
    [InlineData(false, 0, false, PartnerEnds.Answered)]
    public void A_turns_end_is_what_was_observed(bool byPerson, int? exit, bool limited, string end)
    {
        Assert.Equal(end, PartnerTurns.End(byPerson, exit, limited));
    }

    internal static readonly PartnerPacket Packet = new(
        "lead0001", "reports", "2222222222222222222222222222222222222222",
        "Make the report's window seven days, not six.\n\nKeep the tests passing.", ["src/report.ts", "tests"], ["src/window.ts"], 45);

    private static string GoldenPath(string file) =>
        Path.Combine(Path.GetDirectoryName(HelperPromptGoldenTests.GoldenPath("asked.md"))!, "..", "partner-prompt", file);

    /// <summary>The partner's instruction, byte for byte: the piece quoted, its paths, the lead's uncommitted paths, how it ends.</summary>
    [Fact]
    public void The_partners_instruction_is_its_golden_text()
    {
        var text = PartnerInstruction.Compose(Packet);

        Daoris.Driver.Tests.HelpRoomGoldenTests.Settle(Path.GetFullPath(GoldenPath("asked.md")), text, Daoris.Driver.Tests.HelpRoomGoldenTests.WriteRequested);
        Assert.Contains("> Make the report's window seven days, not six.\n>\n> Keep the tests passing.\n", text);
        Assert.Contains("`partner_answer`", text);
        Assert.Contains("- `src/window.ts`", text);
    }
}
