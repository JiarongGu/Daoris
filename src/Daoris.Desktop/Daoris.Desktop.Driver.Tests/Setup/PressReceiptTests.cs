using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a press that opened a place says it made, as the window words it afterwards: a department's folder, line and quests, or a room's one
/// quest. It is read from the apply's own result, never from the verb's lines, and says nothing for a press that made nothing.
/// </summary>
public sealed class PressReceiptTests
{
    private static CreateResult Created(bool done, string? quest = "q7") => new(
        done, null, "message", new string('a', 64),
        [
            new PlaceOperation("folder", "folder", "ledger", "make", PlaceOutcomes.Done),
            new PlaceOperation("setup", "setup", "ledger", "ask", done ? PlaceOutcomes.Done : PlaceOutcomes.Failed) { Quest = quest },
        ])
    { Name = "ledger", Workspace = "aurora", Folder = "/work/aurora/ledger", Line = "main" };

    [Fact]
    public void A_department_is_opened_with_its_folder_line_and_set_up_quest()
    {
        var receipt = PressReceipt.Of(Created(done: true));

        Assert.Equal(("opened", "ledger", "aurora", "/work/aurora/ledger", "main", true), (receipt.Kind, receipt.Repository, receipt.Workspace, receipt.Folder, receipt.Line, receipt.Done));
        Assert.Equal(["q7"], receipt.Quests);
    }

    /// <summary>A creation that stopped says what it made so far and that it is not done, with no quest it never asked.</summary>
    [Fact]
    public void A_creation_that_stopped_is_not_done_and_names_no_quest_it_never_asked()
    {
        var receipt = PressReceipt.Of(Created(done: false, quest: null));

        Assert.False(receipt.Done);
        Assert.Empty(receipt.Quests);
    }

    [Fact]
    public void A_folder_department_names_no_quest_and_no_line()
    {
        var result = new CreateResult(true, null, "m", new string('b', 64), [new PlaceOperation("folder", "folder", "notes", "make", PlaceOutcomes.Done)])
        {
            Name = "notes", Workspace = "aurora", Folder = "/work/aurora/notes",
        };

        var receipt = PressReceipt.Of(result);

        Assert.Null(receipt.Line);
        Assert.Empty(receipt.Quests);
    }

    /// <summary>A room's receipt is its quest's line alone: the repository, the path and the quest.</summary>
    [Fact]
    public void A_room_is_the_one_quest_asked_of_its_repository()
    {
        var result = new PlaceResult(true, null, "m", new string('c', 64), [new PlaceOperation("room", "room", "engine", "ask", PlaceOutcomes.Done) { Quest = "q9" }]);

        var receipt = PressReceipt.OfRoom(result, "engine", "tools/web");

        Assert.Equal(("room", "engine", "tools/web", true), (receipt.Kind, receipt.Repository, receipt.Room, receipt.Done));
        Assert.Equal(["q9"], receipt.Quests);
        Assert.Null(receipt.Folder);
    }

    /// <summary>The answer carries it in the wire's camel case, with the quests as a list of ids.</summary>
    [Fact]
    public void It_serializes_as_the_answer_carries_it()
    {
        var wire = JsonSerializer.SerializeToElement(PressReceipt.Of(Created(done: true)), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("opened", wire.GetProperty("kind").GetString());
        Assert.Equal("q7", wire.GetProperty("quests")[0].GetString());
    }
}
