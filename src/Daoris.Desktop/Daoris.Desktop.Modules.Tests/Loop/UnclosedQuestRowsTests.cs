using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A quest its finished session left taken is a row of <i>What needs you</i>, read from the planner's typed <c>Unclosed</c> verdict
/// and counted with the quests the person answers, so nobody's taken quest sits without the person told.
/// </summary>
public sealed class UnclosedQuestRowsTests
{
    private static readonly JsonObject Taken = JsonNode.Parse(
        """{"id":"7a82cc","from":"platform","to":"engine","title":"Review the plugins","body":"A review.","status":"Taken","workspace":"games","updated":"2026-11-08T04:55:09Z"}""")!.AsObject();

    private static readonly JsonObject Unclosed = JsonNode.Parse(
        """
        {"quest":"7a82cc","repository":"engine","verdict":"Unclosed","said":{"code":"unclosed","session":"031217fe"},
         "reason":"session `031217fe` ended and left `#7a82cc` taken, and nothing here carries it on."}
        """)!.AsObject();

    [Fact]
    public void A_quest_its_finished_session_left_taken_is_a_row_by_the_quest_and_is_counted()
    {
        var facts = new AttentionFacts { Quests = [Taken], Registry = [Registration("engine")], Considered = [Unclosed] };

        var item = Assert.Single(Rows(facts));

        Is($$"""{"kind":"unclosed","id":"7a82cc","since":"2026-11-08T04:55:09Z","workspace":"games","where":"engine","session":"031217fe","sitting":{{Unclosed.ToJsonString()}}}""", item);
        var counted = Assert.Single(NeedsAPerson.WaitingInSessions(facts));
        Assert.Equal(("unclosed:7a82cc", "unclosed", "games"), (counted.Key, counted.Value["kind"]!.GetValue<string>(), counted.Value["workspace"]!.GetValue<string>()));
    }
}
