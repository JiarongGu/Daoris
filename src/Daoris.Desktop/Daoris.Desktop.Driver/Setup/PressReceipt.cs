namespace Daoris.Driver;

/// <summary>
/// What a press that opened a place says it made, for the line the window draws after it: a department's folder, line and set-up quest
/// (<see cref="KindOpened"/>), or a room's one quest (<see cref="KindRoom"/>). Read from the apply's own result, never from the verb's lines. The work a
/// department is opened for is not a quest yet: the service publishes it when the set-up closes done.
/// </summary>
/// <param name="Kind"><c>opened</c> for a department, <c>room</c> for a room.</param>
/// <param name="Repository">The department's name, or the repository the room is asked of.</param>
/// <param name="Workspace">The workspace the department joined; none for a room.</param>
/// <param name="Folder">The department's full folder; none for a room.</param>
/// <param name="Line">The line its history starts on; none for a folder department or a room.</param>
/// <param name="Room">The room's path in its repository; none for a department.</param>
/// <param name="Quests">The quests the press asked, by id; none where it asked none, or not yet.</param>
/// <param name="Done">Whether every operation is done.</param>
public sealed record PressReceipt(string Kind, string Repository, string? Workspace, string? Folder, string? Line, string? Room, IReadOnlyList<string> Quests, bool Done)
{
    public const string KindOpened = "opened";
    public const string KindRoom = "room";

    /// <summary>The receipt of a creation's result.</summary>
    public static PressReceipt Of(CreateResult result) => new(
        KindOpened, result.Name ?? "", result.Workspace, result.Folder, result.Line, null, QuestsOf(result.Operations), result.Done);

    /// <summary>The receipt of a room's result: its one quest.</summary>
    public static PressReceipt OfRoom(PlaceResult result, string repository, string room) => new(
        KindRoom, repository, null, null, null, room, QuestsOf(result.Operations), result.Done);

    private static IReadOnlyList<string> QuestsOf(IReadOnlyList<PlaceOperation> operations) =>
        [.. operations.Select(each => each.Quest).OfType<string>().Where(quest => quest.Length > 0)];
}
