using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1c5 (D157's WORKFLOW1c5 note; <c>.claude/knowledge/twins.md</c>): the English a run says is held to one table, this
/// suite's <c>fixtures/workflow-run-said.json</c>, which the page's <c>workflow/runSaid.test.ts</c> holds its catalogue to as well.
/// <see cref="WorkflowRunWords"/> says each row's sentence word for word, so a sentence changed here alone fails here, and one
/// changed in the table alone fails here until the words say it. The terminal's own words stay out of the table.
/// </summary>
public sealed class WorkflowRunSaidTwinTests
{
    private static JsonElement Table()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "workflow-run-said.json")));
        return document.RootElement.Clone();
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    public static TheoryData<int> Rows()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < Table().GetProperty("rows").GetArrayLength(); index++) data.Add(index);
        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_row_is_said_in_the_tables_words(int index)
    {
        var row = Table().GetProperty("rows")[index];
        var kind = Text(row, "kind")!;
        var step = new WorkflowRunStep(
            new WorkflowStep(kind, kind, WorkflowParticipation.Agent, WorkflowExecutor.Agent, null,
                new WorkflowSource(null, LandingSource.Default), [], WorkflowRuntime.Built, null),
            Text(row, "state") ?? WorkflowRunStates.Working, Text(row, "detail")!)
        {
            Quest = Text(row, "quest"),
            Agent = Text(row, "agent"),
            Environment = Text(row, "environment"),
            Commit = Text(row, "commit"),
            Branch = Text(row, "branch"),
            Plugin = Text(row, "plugin"),
            Count = Number(row, "count"),
            GoAhead = Number(row, "goAhead"),
            Words = Text(row, "words"),
            Code = Text(row, "code"),
            Node = Text(row, "node"),
            On = Text(row, "on"),
        };

        Assert.Equal(Text(row, "said"), WorkflowRunWords.Said(step));
    }

    /// <summary>FABRIC-CUTOVER1e: what a graph run says above its nodes, line for line, as the table's <c>graph</c> rows have it.</summary>
    [Fact]
    public void A_graph_run_says_its_lines_in_the_tables_words()
    {
        static bool Flag(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        foreach (var row in Table().GetProperty("graph").EnumerateArray())
        {
            var graph = new WorkflowGraphRun("q2", ["claimed"], "handed", [])
            {
                Quest = Text(row, "quest"), Branch = Text(row, "branch"), Judged = Flag(row, "judged"), Unread = Flag(row, "unread"), Ended = Text(row, "ended"),
            };
            Assert.Equal(row.GetProperty("said").EnumerateArray().Select(line => line.GetString()), WorkflowRunWords.GraphSaid(graph));
        }
    }

    /// <summary>
    /// CHATFLOW1a (the chat-flow design §10): each code a read that names no run answers is said in the table's <c>none</c> words, which
    /// the page's en catalogue says too, and every code the driver answers has its row.
    /// </summary>
    [Fact]
    public void Each_no_run_code_is_said_in_the_tables_words()
    {
        var none = Table().GetProperty("none").EnumerateArray().ToList();
        foreach (var row in none)
        {
            Assert.Equal(Text(row, "said"), WorkflowNoRun.Said(Text(row, "code")!, Text(row, "id"), Text(row, "workflow")));
        }

        Assert.Equal(WorkflowNoRun.All, none.Select(row => Text(row, "code")));
    }

    /// <summary>CHATFLOW1c (the chat-flow design §4.4): a chat run's checks are said not yet enforced, in the table's words.</summary>
    [Fact]
    public void A_chat_run_s_checks_are_said_not_yet_enforced_in_the_tables_words()
    {
        var row = Table().GetProperty("chatChecks");
        Assert.Equal(Text(row, "said"), WorkflowRunWords.ChatChecks(Text(row, "checks")!));
    }

    [Fact]
    public void Each_state_and_each_look_added_is_worded_as_the_table_has_it()
    {
        var table = Table();
        foreach (var state in table.GetProperty("states").EnumerateObject()) Assert.Equal(state.Value.GetString(), WorkflowRunWords.State(state.Name));
        foreach (var level in table.GetProperty("added").EnumerateObject())
        {
            Assert.Equal(level.Value.GetString(), WorkflowRunWords.Added(StepAdded(level.Name)));
        }

        Assert.Null(WorkflowRunWords.Added(StepAdded(null)));
    }

    [Fact]
    public void The_table_holds_every_detail_and_every_state_the_words_have()
    {
        var table = Table();
        var rows = table.GetProperty("rows").EnumerateArray().Select(row => $"{Text(row, "kind")}.{Text(row, "detail")}").ToHashSet(StringComparer.Ordinal);
        var worded = WorkflowRunWords.Details.SelectMany(each => each.Value.Select(detail => $"{each.Key}.{detail}"));

        Assert.Empty(worded.Where(key => !rows.Contains(key)));
        // FABRIC-CUTOVER1e: each detail a graph run's node is said by has a row with a node.
        var nodes = table.GetProperty("rows").EnumerateArray().Where(row => Text(row, "node") is not null).Select(row => Text(row, "detail")).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(WorkflowGraphDetails.All.Where(detail => !nodes.Contains(detail)));
        foreach (var state in typeof(WorkflowRunStates).GetFields().Where(field => field.IsLiteral && field.FieldType == typeof(string)).Select(field => (string)field.GetValue(null)!))
        {
            Assert.True(table.GetProperty("states").TryGetProperty(state, out _), state);
        }
    }

    private static WorkflowRunStep StepAdded(string? added) =>
        new(new WorkflowStep("look", "look", WorkflowParticipation.Agent, WorkflowExecutor.Agent, null,
            new WorkflowSource(null, LandingSource.Default), [], WorkflowRuntime.Built, null), WorkflowRunStates.Working, "")
        {
            Added = added,
        };
}
