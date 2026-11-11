using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A done whose own record says work is left is a <c>left</c> row of <i>What needs you</i>, by the session Sessions' list places in Needs
/// you for it: its wait is since the session ended, it is never a review, and the person's archive of the session takes it off.
/// </summary>
public sealed class AttentionLeftRowTests
{
    private static JsonObject Placed(string over = "{}") => With(
        """{"session":"r3v13w00","group":"needs-you","state":"needs-you","reason":"left","shown":"completed","archived":false,"teammate":false}""", over);

    private static JsonObject Ended() =>
        Session("""{"id":"r3v13w00","quest":"7a82cc","state":"completed","updated":"2026-09-21T11:00:00Z"}""");

    [Fact]
    public void A_left_done_is_a_row_by_its_session_since_it_ended_with_its_place()
    {
        var placed = Placed();

        var item = Assert.Single(Rows(new() { Sessions = [Ended()], Registry = [Registration("engine")], Groups = [placed] }));

        Is($$"""{"kind":"left","id":"r3v13w00","since":"2026-09-21T11:00:00Z","workspace":"default","where":"engine","placed":{{placed.ToJsonString()}}}""", item);
    }

    [Fact]
    public void A_left_row_goes_with_the_persons_archive_and_with_a_session_no_longer_in_needs_you()
    {
        Assert.Equal(["left:r3v13w00"], Keys(Rows(new() { Sessions = [Ended()], Groups = [Placed()] })));
        Assert.Empty(Rows(new() { Sessions = [Ended()], Groups = [Placed("""{"group":"archived"}""")] }));
        Assert.Empty(Rows(new() { Sessions = [Ended()], Groups = [Placed("""{"group":"done"}""")] }));
    }
}
