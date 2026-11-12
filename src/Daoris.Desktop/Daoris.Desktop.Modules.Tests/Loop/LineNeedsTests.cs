using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <i>What needs you</i> reads a line's state, not one part's: a line whose newest part works is not listed for an earlier part that
/// ended waiting; a line whose co-worker waits on the person is listed once, by the co-worker; and the terminal's <c>notices</c>
/// lists what the window's rows are.
/// </summary>
public sealed class LineNeedsTests
{
    private static JsonObject Placed(string session, string reason, string line) => With(
        $$"""{"session":"{{session}}","group":"needs-you","reason":"{{reason}}","shown":"completed","archived":false,"teammate":false,"line":{{line}}}""", "{}");

    private static string Line(string state, string by, string role = "part") =>
        $$"""{"state":"{{state}}","group":"{{state}}","reason":"working","by":"{{by}}","role":"{{role}}"}""";

    private static AttentionFacts Facts(JsonObject[] sessions, JsonObject[] groups) =>
        new() { Sessions = sessions, Groups = groups, Registry = [Registration("engine")] };

    [Fact]
    public void A_review_row_of_an_earlier_part_is_no_row_while_the_line_s_newest_part_works()
    {
        var facts = Facts(
            [Session("""{"id":"e4r11e00","state":"completed","quest":"7a82cc"}"""), Session("""{"id":"n3w3r000","state":"working","line":"e4r11e00"}""")],
            [Placed("e4r11e00", "review", Line("working", "n3w3r000"))]);

        Assert.Empty(Rows(facts));
    }

    [Fact]
    public void A_parked_session_whose_line_works_through_a_later_part_is_no_row()
    {
        var facts = Facts(
            [Session("""{"id":"p4rk3d00","state":"awaiting-person"}"""), Session("""{"id":"n3w3r000","state":"working","line":"p4rk3d00"}""")],
            [Placed("p4rk3d00", "question", Line("working", "n3w3r000"))]);

        Assert.Empty(Rows(facts));
    }

    [Fact]
    public void A_line_whose_co_worker_waits_on_the_person_is_one_row_naming_the_co_worker()
    {
        var facts = Facts(
            [
                Session("""{"id":"e4r11e00","state":"completed"}"""),
                Session("""{"id":"h3lp3r00","state":"awaiting-person","serves":"e4r11e00","kind":"helper"}"""),
            ],
            [
                Placed("e4r11e00", "review", Line("needs-you", "h3lp3r00", "helper")),
                Placed("h3lp3r00", "question", Line("needs-you", "h3lp3r00", "helper")),
            ]);

        Assert.Equal(["parked:h3lp3r00"], Keys(Rows(facts)));
    }

    [Fact]
    public void A_line_that_decides_its_own_state_keeps_its_row_and_a_row_with_no_line_is_kept()
    {
        var facts = Facts(
            [Session("""{"id":"e4r11e00","state":"completed","quest":"7a82cc"}"""), Session("""{"id":"p4rk3d00","state":"awaiting-person"}""")],
            [Placed("e4r11e00", "review", Line("needs-you", "e4r11e00")), Placed("p4rk3d00", "question", "null")]);

        Assert.Equal(["parked:p4rk3d00", "review:e4r11e00"], Keys(Rows(facts)));
    }

    /// <summary>The terminal lists, subject for subject, what the window's rows are, for each of these cases.</summary>
    [Fact]
    public void The_terminal_s_list_is_the_window_s_for_a_line_a_later_part_works_and_one_a_co_worker_waits_on()
    {
        var facts = Facts(
            [
                Session("""{"id":"e4r11e00","state":"completed"}"""), Session("""{"id":"n3w3r000","state":"working","line":"e4r11e00"}"""),
                Session("""{"id":"0th3r000","state":"completed"}"""),
                Session("""{"id":"h3lp3r00","state":"awaiting-person","serves":"0th3r000","kind":"helper"}"""),
            ],
            [
                Placed("e4r11e00", "review", Line("working", "n3w3r000")),
                Placed("0th3r000", "review", Line("needs-you", "h3lp3r00", "helper")),
                Placed("h3lp3r00", "question", Line("needs-you", "h3lp3r00", "helper")),
            ]);
        var rows = new JsonObject();
        foreach (var row in Rows(facts)) rows[$"{row["kind"]!.GetValue<string>()}:{row["id"]!.GetValue<string>()}"] = row;

        var look = NoticesRoutes.Look(
            null, new ServiceConfirmationsNow(0, []),
            new SliceSnapshot(AttentionStateSlice.Name, "e1", StateSlice.Ready, [new SlicePartition(AttentionStateSlice.Rows, 1, StateSlice.Ready, rows)]), null, []);

        Assert.Equal(Keys(Rows(facts)).Select(key => key.Split(':')[1]), look.Needs!.Select(need => need.Subject).Order(StringComparer.Ordinal));
        Assert.Equal(["h3lp3r00"], look.Needs!.Select(need => need.Subject));
    }
}
