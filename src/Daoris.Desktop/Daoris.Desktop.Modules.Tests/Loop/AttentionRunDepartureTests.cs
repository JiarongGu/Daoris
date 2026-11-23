using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A done its workflow's run holds for the person's yes stays Taken, its done submitted: it is a <c>departure</c> row of
/// <i>What needs you</i> with its departure's words, as a closed done's is, and the person's accept takes it off.
/// </summary>
public sealed class AttentionRunDepartureTests
{
    private static JsonObject Submitted(string over = "{}") => With(
        """
        {"id":"52b3f6","from":"platform","to":"engine","title":"Cap hydration per frame","status":"Taken","held":true,"hold":"departed",
         "closure":"run:52b3f6","submission":{"machine":"m1","sequence":4,"at":"2026-11-16T01:52:00Z"},
         "filed":"2026-11-16T00:57:00Z","updated":"2026-11-16T01:52:00Z",
         "requirements":[{"quote":"cap it per frame","check":"a test"},{"quote":"on the chunk API","check":"the API"}],
         "answers":[{"requirement":1,"met":"tested"},{"requirement":2,"departed":"the scheduler holds the budget instead","quote":"on the chunk API"}]}
        """,
        over);

    [Fact]
    public void A_submitted_done_its_run_holds_for_a_departure_is_a_departure_row_with_its_words()
    {
        var item = Assert.Single(Rows(new() { Quests = [Submitted()], Registry = [Registration("engine")] }));

        Is("""
           {"kind":"departure","id":"52b3f6","since":"2026-11-16T01:52:00Z","workspace":"default","where":"engine",
            "departed":{"count":1,"number":2,"reason":"the scheduler holds the budget instead"}}
           """, item);
    }

    [Fact]
    public void A_departure_the_policy_classes_role_is_its_holders_and_shows_the_person_no_row_while_another_stays() =>
        Assert.Equal(["a1b2c3"], Rows(new()
        {
            Quests = [Submitted(), Submitted("""{"id":"a1b2c3"}""")], Registry = [Registration("engine")], RoleHeld = new HashSet<string> { "52b3f6" },
        }).Select(row => row["id"]!.GetValue<string>()).ToArray());

    [Fact]
    public void The_persons_accept_takes_the_row_off_while_the_quest_stays_taken() =>
        Assert.Empty(Rows(new()
        {
            Quests = [Submitted("""{"held":false,"hold":null,"accepted":"2026-11-16T02:14:35Z"}""")], Registry = [Registration("engine")],
        }));
}
