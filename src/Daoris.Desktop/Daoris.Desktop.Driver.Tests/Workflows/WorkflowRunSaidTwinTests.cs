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
        };

        Assert.Equal(Text(row, "said"), WorkflowRunWords.Said(step));
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
