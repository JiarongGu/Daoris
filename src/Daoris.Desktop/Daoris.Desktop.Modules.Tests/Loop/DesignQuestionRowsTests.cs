using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A design pass's open closed questions as rows of What needs you: one per key, with its yes and no options; none once answered or for a brainstorm.</summary>
public sealed class DesignQuestionRowsTests
{
    private const string Q1 =
        """{"key":"Q1","question":"Evolve the engine in place?","yes":"One engine.","no":"A second engine.","where":"docs/areas/workflows.md"}""";

    private static JsonObject Brainstorm(string pass, string questions, string answered = "[]") => JsonNode.Parse(
        $$"""{"id":"b7","pass":"{{pass}}","state":"done","updated":"2026-11-16T09:00:00Z","comparison":{"route":"Evolve.","questions":{{questions}}},"answered":{{answered}}}""")!.AsObject();

    [Fact]
    public void Each_open_question_of_a_design_pass_is_one_row_with_its_two_options()
    {
        var row = Assert.Single(DesignQuestionRows.Of([Brainstorm("design", $"[{Q1}]")]));

        Assert.Equal("design-question", row["kind"]!.GetValue<string>());
        Assert.Equal("b7:Q1", row["id"]!.GetValue<string>());
        Assert.Equal("b7", row["brainstorm"]!.GetValue<string>());
        Assert.Equal("One engine.", row["design"]!["yes"]!.GetValue<string>());
        Assert.Equal(["yes", "no"], row["options"]!.AsArray().Select(option => option!.GetValue<string>()));
    }

    [Fact]
    public void An_answered_question_and_a_plain_brainstorm_give_none()
    {
        Assert.Empty(DesignQuestionRows.Of([Brainstorm("design", $"[{Q1}]", """["Q1"]"""), Brainstorm("", $"[{Q1}]")]));
    }

    [Fact]
    public void The_row_goes_through_the_needs_a_person_judgement_once()
    {
        var rows = NeedsAPerson.Rows(new() { Brainstorms = [Brainstorm("design", $"[{Q1}]")] });

        Assert.Single(rows, row => row["kind"]!.GetValue<string>() == "design-question");
    }
}
