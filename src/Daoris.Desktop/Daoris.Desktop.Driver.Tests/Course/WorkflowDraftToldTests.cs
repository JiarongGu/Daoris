using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FLOWCHART2d (D223): the workflow draft open in the editor, told to Ask Daoris beside the person's words, bounded, and kept on the
/// conversation's record as told, where the draft door finds it again by its fingerprint.
/// </summary>
public sealed class WorkflowDraftToldTests
{
    private const string Graph =
        """{"schemaVersion":2,"entries":["work"],"end":"line","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"line","kind":"line","version":1,"reach":"observed"}],"wires":[{"id":"a","kind":"after","from":"work","to":"line"}]}""";

    private static JsonElement Payload(string graph = Graph, string fingerprint = "g1-0123456789abcdef", string extra = "") =>
        JsonDocument.Parse($$"""
            {"fingerprint":"{{fingerprint}}","place":"engine","workflow":"release-flow","base":2,"source":"release-flow@v2","pipeline":"development",
             "selected":"work","problems":[{"code":"input-unfed","node":"work","words":"`work` takes no input."}]{{extra}},"graph":{{graph}}}
            """).RootElement.Clone();

    [Fact]
    public void A_draft_is_read_with_its_graph_its_fingerprint_its_place_and_what_the_editor_holds_of_it()
    {
        var draft = WorkflowDraftTold.Read(Payload());

        Assert.Equal(("g1-0123456789abcdef", "engine", "release-flow", 2), (draft.Fingerprint, draft.Place, draft.Workflow, draft.Base));
        Assert.Equal(("release-flow@v2", "development", "work"), (draft.Source, draft.Pipeline, draft.Selected));
        Assert.Equal("input-unfed", Assert.Single(draft.Problems).Code);
        Assert.Equal(JsonDocument.Parse(Graph).RootElement.GetRawText(), draft.Graph.GetRawText());
    }

    /// <summary>A draft larger than a message carries is refused whole, in a sentence saying so, never cut to fit.</summary>
    [Fact]
    public void An_oversize_draft_is_refused_with_its_sentence_and_never_cut()
    {
        var nodes = string.Join(",", Enumerable.Range(0, 900).Select(n => $$$"""{"id":"step-{{{n}}}","kind":"claimed","version":1,"reach":"observed","options":{"brief":"{{{new string('x', 40)}}}"}}"""));
        var large = $$"""{"schemaVersion":2,"entries":["step-0"],"end":"step-0","nodes":[{{nodes}}],"wires":[]}""";
        var payload = Payload(large);
        Assert.True(payload.GetRawText().Length > WorkflowDraftTold.Limit);

        var refused = Assert.Throws<DriverException>(() => WorkflowDraftTold.Read(payload));
        Assert.Equal(
            $"the open workflow draft is {payload.GetRawText().Length} characters, more than the {WorkflowDraftTold.Limit} a message carries to Ask Daoris, "
            + "so neither the draft nor your words were sent: the draft is never cut to fit.",
            refused.Message);
    }

    [Theory]
    [InlineData("""{"place":"engine","graph":{}}""", "the open workflow draft names its fingerprint")]
    [InlineData("""{"fingerprint":"g1 bad","place":"engine","graph":{}}""", "the open workflow draft names its fingerprint")]
    [InlineData("""{"fingerprint":"g1-1","graph":{}}""", "the open workflow draft names its place")]
    [InlineData("""{"fingerprint":"g1-1","place":"engine","graph":[]}""", "the open workflow draft carries its graph")]
    public void A_draft_missing_what_it_names_is_refused_by_its_sentence(string json, string said)
    {
        var refused = Assert.Throws<DriverException>(() => WorkflowDraftTold.Read(JsonDocument.Parse(json).RootElement));
        Assert.StartsWith(said, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>The agent is handed the draft after where the person is, and the palette only where the message carries it.</summary>
    [Fact]
    public void The_agent_is_handed_the_draft_after_where_the_person_is_and_the_palette_once_told()
    {
        var draft = WorkflowDraftTold.Read(Payload()) with { Palette = new WorkflowDraftPalette("p1-aaaa", "- `claimed`: options `brief`") };
        var prompt = new ChatMessage("add a release", []) { Preface = "Where the person is now: the Repositories view.", Draft = draft }.Prompt;

        Assert.True(prompt.IndexOf("Where the person is now", StringComparison.Ordinal) < prompt.IndexOf(draft.Json, StringComparison.Ordinal));
        Assert.EndsWith("\n\nadd a release", prompt, StringComparison.Ordinal);
        Assert.Contains("- `claimed`: options `brief`", prompt);
        Assert.Contains("`workflow_propose`", prompt);
        Assert.DoesNotContain("options `brief`", new ChatMessage("again", []) { Draft = draft with { Palette = draft.Palette! with { Text = null } } }.Prompt);
    }

    /// <summary>The flowchart design §8.3: the palette the room is handed names each kind with its options and a plugin step's outcomes and outputs.</summary>
    [Fact]
    public void The_palette_told_names_the_kinds_their_options_and_a_plugin_step_s_outcomes_and_outputs()
    {
        var find = new NodeDeclaration("find", "local-release/find", 1, NodeReach.Performed)
        {
            Outcomes = ["release", "none"],
            Outputs = new Dictionary<string, string> { ["release"] = "local-release/release@1" },
            OutputsOn = new Dictionary<string, IReadOnlyList<string>> { ["release"] = ["release"] },
        };
        var compact = WorkflowPalette.Build([("local-release", [find])]).Compact();

        Assert.All(NodeKit.Kinds,kind => Assert.Contains($"- `{kind}`:", compact));
        Assert.Contains("options `method` (choice: ", compact);
        Assert.Contains("- `local-release/find` v1: performed; outcomes `release`, `none`; outputs `release` (local-release/release@1, on `release`)", compact);
        Assert.Equal(WorkflowPalette.DigestOf(compact), WorkflowPalette.DigestOf(WorkflowPalette.Build([("local-release", [find])]).Compact()));
        Assert.NotEqual(WorkflowPalette.DigestOf(compact), WorkflowPalette.DigestOf(WorkflowPalette.Build([]).Compact()));
    }

    /// <summary>The record keeps the draft as told: its own note, its lead by code, and the draft whole as the program's words.</summary>
    [Fact]
    public void The_record_keeps_the_draft_as_told_and_finds_it_again_by_its_fingerprint()
    {
        var draft = WorkflowDraftTold.Read(Payload());
        var message = new ChatMessage("add a release", []) { Preface = "Where the person is now: the Repositories view.", Draft = draft };

        var recorded = ChatRunner.Recorded(message).ToList();
        Assert.Equal(["chat.told-where", "chat.told-draft", "person"], recorded.Select(each => each.Parts?[0].Code ?? each.Origin).ToArray());
        var told = recorded[1];
        Assert.Equal(SessionEventKind.Note, told.Kind);
        Assert.Equal((draft.Json, NoteBy.Program), (told.Parts![^1].Words, told.Parts![^1].By));
        NoteAssert.Holds(told.Text, told.Parts);

        var later = WorkflowDraftTold.Read(Payload(fingerprint: "g1-fedcba9876543210"));
        var found = WorkflowDraftTold.Told([told, .. ChatRunner.Recorded(new ChatMessage("and now", []) { Draft = later })]);
        Assert.Equal(["g1-0123456789abcdef", "g1-fedcba9876543210"], found.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(draft.Json, found["g1-0123456789abcdef"].Json);
        Assert.Equal(Graph, found["g1-0123456789abcdef"].Graph.GetRawText());
        Assert.Null(ChatRecorded.ToldDraft(new ChatMessage("hello", [])));
    }

    /// <summary>REVIEW2 S7: a limit move opens a new part, and a draft told to an earlier part is still the conversation's.</summary>
    [Fact]
    public void A_draft_told_to_an_earlier_part_is_found_from_the_part_that_goes_on_from_it()
    {
        var first = ChatRunner.Recorded(new ChatMessage("hello", []) { Draft = WorkflowDraftTold.Read(Payload()) }).ToList();
        var second = new[] { new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "brief", Continues = "part-1" } };
        var parts = new Dictionary<string, IReadOnlyList<SessionEvent>> { ["part-1"] = first, ["part-2"] = second };

        Assert.Empty(WorkflowDraftTold.Told(second));
        var found = WorkflowDraftTold.ToldAcross("part-2", id => parts.GetValueOrDefault(id) ?? []);

        Assert.Equal(["g1-0123456789abcdef"], found.Keys);
        // A part that names itself or loops back ends the walk.
        parts["part-1"] = [.. first, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "b", Continues = "part-2" }];
        Assert.Single(WorkflowDraftTold.ToldAcross("part-2", id => parts.GetValueOrDefault(id) ?? []));
    }
}
