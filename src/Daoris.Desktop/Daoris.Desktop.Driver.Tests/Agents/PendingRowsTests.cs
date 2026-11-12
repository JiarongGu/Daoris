using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pending lines' rows after the agent-lyntai design's re-cut (§6.2, §6.4): a line may name the run kind it holds its case on, the
/// closed list of rows holds the design's own and no longer the rows it re-cut, and each frames line waits on the row that now makes it
/// pass. Its twin is the tools' reader (<c>tools/stand-ins.mjs</c>, <c>stand-ins.test.ts</c>'s pending table).
/// </summary>
public sealed class PendingRowsTests
{
    private const string Lines = """
        [
          { "case": "frames.unknown-unread", "door": "claude-code", "run": "Driven", "row": "AGENTLYNTAI1d", "why": "Driven." },
          { "case": "frames.unknown-unread", "door": "claude-code", "run": "Conversation", "row": "AGENTLYNTAI1f-i", "why": "A conversation." },
          { "case": "dialect.recorded-frames", "door": "*", "row": "j", "why": "Every kind." }
        ]
        """;

    [Fact]
    public void A_line_naming_its_run_holds_its_case_on_that_run_kind_alone_and_absent_on_both()
    {
        var read = ConformancePending.Parse(Lines, "runs.json");

        Assert.Empty(read.Problems);
        Assert.Equal("AGENTLYNTAI1d", ConformancePending.Holding(read.Lines, "frames.unknown-unread", "claude-code", StartKind.Driven)!.Row);
        Assert.Equal("AGENTLYNTAI1f-i", ConformancePending.Holding(read.Lines, "frames.unknown-unread", "claude-code", StartKind.Conversation)!.Row);
        Assert.Equal("j", ConformancePending.Holding(read.Lines, "dialect.recorded-frames", "claude-code", StartKind.Conversation)!.Row);
        // A view that runs every kind as today is held by a line of either kind.
        Assert.NotNull(ConformancePending.Holding(read.Lines, "frames.unknown-unread", "claude-code", run: null));
        Assert.Null(ConformancePending.Holding(read.Lines, "frames.unknown-unread", "claude-code-acp", StartKind.Driven));
    }

    [Fact]
    public void A_run_is_one_of_the_two_kinds_and_a_line_held_twice_names_the_same_case_door_and_run()
    {
        var read = ConformancePending.Parse(
            """
            [
              { "case": "frames.unknown-unread", "door": "claude-code", "run": "Sideways", "row": "AGENTLYNTAI1d", "why": "No such kind." },
              { "case": "dialect.recorded-frames", "door": "claude-code", "run": "Driven", "row": "AGENTLYNTAI1d", "why": "Driven." },
              { "case": "dialect.recorded-frames", "door": "claude-code", "row": "AGENTLYNTAI1d", "why": "Both, so the driven one twice." },
              { "case": "ids.names-resolve", "door": "*", "run": "Driven", "row": "h", "why": "Driven." },
              { "case": "ids.names-resolve", "door": "*", "run": "Conversation", "row": "h", "why": "A conversation, not twice." }
            ]
            """,
            "runs.json");

        Assert.Equal(2, read.Problems.Count);
        Assert.Contains(read.Problems, problem => problem.Contains("'run' is Driven or Conversation", StringComparison.Ordinal));
        Assert.Contains(read.Problems, problem => problem.Contains("'dialect.recorded-frames' on 'claude-code' is held twice", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_naming_a_run_its_view_does_not_run_names_no_case_the_suite_runs()
    {
        var read = ConformancePending.Parse(Lines, "runs.json");
        IReadOnlyCollection<(string, string, StartKind?)> driven =
            [("frames.unknown-unread", "claude-code", StartKind.Driven), ("dialect.recorded-frames", "claude-code", StartKind.Driven)];
        IReadOnlyCollection<(string, string, StartKind?)> every =
            [("frames.unknown-unread", "claude-code", null), ("dialect.recorded-frames", "claude-code", null)];

        var unmatched = ConformancePending.Unmatched(read.Lines, driven);

        Assert.Contains("'frames.unknown-unread' on 'claude-code' (Conversation)", Assert.Single(unmatched), StringComparison.Ordinal);
        Assert.Empty(ConformancePending.Unmatched(read.Lines, every));
    }

    [Fact]
    public void The_rows_a_line_may_name_are_the_backlog_s_open_ones_and_no_built_row()
    {
        var rows = OpenBacklogRows.Read(
        [
            "- [ ] **AGENTPROVIDER1 — one provider contract** (driver): left **h** containment; **r** the partner; **t** last.",
            "- [ ] **AGENTPROVIDER1j ⚖ — each protocol door** (driver)",
            "- [ ] **AGENTLYNTAI1e-ii ⚖ — codex** (driver)",
            "- [x] **DONE1 — finished**",
            "  - a continued line naming **g** is no row",
        ]);

        Assert.Equal(["AGENTPROVIDER1", "h", "r", "t", "AGENTPROVIDER1j", "j", "AGENTLYNTAI1e-ii"], rows);
        Assert.DoesNotContain("g", ConformancePending.Rows);
        Assert.DoesNotContain("u", ConformancePending.Rows);
        Assert.Contains("AGENTLYNTAI1e-ii", ConformancePending.Rows);
    }

    public static TheoryData<string, string, string?, string> FramesLines => new()
    {
        { "dsh", "dsh", null, "AGENTLYNTAI1e-i" },
        { "codex", "codex-acp", null, "AGENTLYNTAI1e-ii" },
        { "stub", "acp-stub", null, "AGENTLYNTAI1e-ii" },
        { "claude-code", "claude-code-acp", null, "AGENTLYNTAI1e-iii" },
        { ConformanceSubjects.PluginDoor, ConformanceSubjects.PluginDoor, null, "AGENTLYNTAI1e-iv" },
        { "claude-code", "claude-code", "Driven", "AGENTLYNTAI1d" },
        { "claude-code", "claude-code", "Conversation", "AGENTLYNTAI1f-i" },
        // Its own `_meta` keys, which no layer reader reads before a registration brings its own dialect set.
        { "alt-stub", "alt-acp-stub", null, "j" },
    };

    /// <summary>Each frames line waits on the row that now makes it pass (§6.4): a protocol door's move, Claude Code's native door by run.</summary>
    [Theory]
    [MemberData(nameof(FramesLines))]
    public void Each_frames_line_waits_on_the_row_that_now_makes_it_pass(string provider, string door, string? run, string row)
    {
        var kind = run is null ? (StartKind?)null : Enum.Parse<StartKind>(run);
        var lines = ConformancePending.Of(provider);

        // A door with a recorded corpus passes the dialect case over it, so only its unknown-frame line is held.
        string[] cases = ProviderCorpus.Of(provider, door).Count > 0 ? ["frames.unknown-unread"] : ["frames.unknown-unread", "dialect.recorded-frames"];
        foreach (var @case in cases)
        {
            var held = ConformancePending.Holding(lines, @case, door, kind);
            Assert.True(held is not null, $"{@case} on {door} is held");
            Assert.Equal((row, run), (held.Row, held.Run?.ToString()));
        }
    }
}
