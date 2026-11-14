using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A seed's record is the session's context entries, written from the line a brief is composed from, in place of the brief's text: what was
/// asked, where it stands and why it moves, what the tree holds, what the line read and the memory it was handed, and the outgoing agent's
/// note as its claim. No machine path is named.
/// </summary>
public sealed class SeedContextTests
{
    private static readonly string Tree = Path.Combine(TestHomes.Root, "seed-context-tree");

    private static BriefLine Line(string? note = null) =>
        new(HandOverWhy.Press,
        [
            new BriefPart("c1", "claude-code-acp",
            [
                new SessionEvent { Kind = SessionEventKind.User, Text = "Make the port configurable.", Origin = "person" },
                new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", ToolKind = "read", Locations = [Path.Combine(Tree, "src", "Port.cs")] },
                new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", ToolKind = "read", Locations = [Path.Combine(Tree, "src", "Port.cs")], Status = "completed" },
                new SessionEvent { Kind = SessionEventKind.Tool, Id = "t2", ToolKind = "read", Locations = [Path.Combine(Tree, "src", "Port.cs")] },
                new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new PlanEntry("Read the port", "completed"), new PlanEntry("Add the setting")] },
                new SessionEvent { Kind = SessionEventKind.User, Text = "Also the docs.", Origin = "person" },
            ]),
        ])
        {
            Conversation = true,
            Tree = Tree,
            State = new BriefTree("abc1234 Start", [" M src/Port.cs"], ["abc1234 Start"]),
            Memory = ["The port is read once at start."],
            Note = note,
        };

    /// <summary>🔴 The line's request, its standing with the move's reason and last plan, its tree, its reads and its memory, each by kind.</summary>
    [Fact]
    public void A_line_s_context_is_its_request_standing_tree_reads_and_memory()
    {
        var entries = SeedContext.Of(Line());

        Assert.Equal("Make the port configurable.", Assert.Single(entries, e => e.Kind == ContextKind.Work).Text);
        var standing = Assert.Single(entries, e => e.Kind == ContextKind.Standing);
        Assert.Contains("because the person asked for it", standing.Text);
        Assert.Contains("[completed] Read the port", standing.Text);
        Assert.Contains("[pending] Add the setting", standing.Text);
        Assert.Contains("src/Port.cs", Assert.Single(entries, e => e.Kind == ContextKind.Tree).Text);
        var read = Assert.Single(entries, e => e.Kind == ContextKind.Found && e.Subject == "files read");
        Assert.Contains("src/Port.cs (2 times)", read.Text);
        Assert.DoesNotContain(Tree, read.Text);
        Assert.Contains(entries, e => e is { Kind: ContextKind.Found, Subject: "memory", Text: "The port is read once at start." });
        Assert.DoesNotContain(entries, e => e.Claim);
    }

    /// <summary>🔴 The outgoing agent's note is its claim, never the host's record.</summary>
    [Fact]
    public void The_outgoing_agent_s_note_is_its_claim()
    {
        var claim = Assert.Single(SeedContext.Of(Line("The setting goes in the options file.")), e => e.Claim);

        Assert.Equal((ContextKind.Standing, "The setting goes in the options file."), (claim.Kind, claim.Text));
    }

    /// <summary>🔴 A composed brief carries the same line as its context entries, which a seed tells in place of its text.</summary>
    [Fact]
    public void A_composed_brief_carries_its_line_s_context_entries()
    {
        var brief = ContextBrief.Compose(Line());

        Assert.Equal(SeedContext.Of(Line()), brief.Context);
    }
}
