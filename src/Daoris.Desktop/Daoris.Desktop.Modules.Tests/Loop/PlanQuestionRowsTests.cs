using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The delivery plan's open questions as rows of What needs you: one per key, carrying what it is, what yes does and what it blocks, and none for a closed ask.</summary>
public sealed class PlanQuestionRowsTests
{
    private static JsonObject Ask(string state, string questions) => JsonNode.Parse(
        $$"""{"id":"7c1e9a04b2d5","workspace":"aurora","state":"{{state}}","updated":"2026-11-14T09:00:00Z","planQuestions":{{questions}}}""")!.AsObject();

    private const string Viewers =
        """{"key":"contract:viewers","kind":"open","text":"Do admins or viewers see candidates?","yes":"Both see them.","blocks":"The web app's release.","default":"Admins only."}""";

    [Fact]
    public void Each_open_question_is_one_row_under_its_key_with_what_it_is_what_yes_does_and_what_it_blocks()
    {
        var rows = PlanQuestionRows.Of([Ask("Published", $"[{Viewers}]")]).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("plan-question", row["kind"]!.GetValue<string>());
        Assert.Equal("7c1e9a04b2d5:contract:viewers", row["id"]!.GetValue<string>());
        Assert.Equal("aurora", row["workspace"]!.GetValue<string>());
        Assert.Equal("7c1e9a04b2d5", row["ask"]!.GetValue<string>());
        Assert.Equal("Both see them.", row["question"]!["yes"]!.GetValue<string>());
        Assert.Equal("The web app's release.", row["question"]!["blocks"]!.GetValue<string>());
    }

    [Fact]
    public void A_closed_ask_and_an_ask_with_no_questions_give_none()
    {
        Assert.Empty(PlanQuestionRows.Of([Ask("Closed", $"[{Viewers}]"), Ask("Published", "[]")]));
    }

    [Fact]
    public void The_row_goes_through_the_needs_a_person_judgement_once()
    {
        var rows = NeedsAPerson.Rows(new() { Asks = [Ask("Published", $"[{Viewers}]")] });

        Assert.Single(rows, row => row["kind"]!.GetValue<string>() == "plan-question");
    }
}
