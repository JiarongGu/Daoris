using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A workspace's new conversation is told the requests the last conversation of that workspace left open, so it files none again.
/// </summary>
public sealed class ChatOpenToldTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static PriorSession Chat(string id, string workspace, int minutes) =>
        new(id, null) { Kind = SessionKinds.Chat, Workspace = workspace, Updated = Noon.AddMinutes(minutes) };

    [Fact]
    public void The_last_conversation_is_the_workspaces_newest_chat_of_the_persons_own_beside_the_new_one()
    {
        PriorSession[] records =
        [
            Chat("c1", "home", 1), Chat("c2", "home", 5), Chat("c3", "other", 9), Chat("new", "home", 20),
            Chat("helper", "home", 8) with { Serves = "c1" },
            new("quest-part", null) { Kind = SessionKinds.Driven, Workspace = "home", Updated = Noon.AddMinutes(30) },
        ];

        Assert.Equal("c2", ChatOpenTold.Last(records, "home", "new")!.Session);
        Assert.Null(ChatOpenTold.Last(records, "empty", "new"));
    }

    [Fact]
    public void Each_request_left_open_is_named_with_where_it_stands_and_none_left_open_says_nothing()
    {
        Assert.Null(ChatOpenTold.Of("c2", []));

        var told = ChatOpenTold.Of("c2", [new BriefWait("q7", "c2", "engine", "kept", "pending", "add the cap")])!;

        Assert.Contains("`c2`, left a request open", told, StringComparison.Ordinal);
        Assert.Contains("`q7` to engine; kept by Daoris, not yet taken up: add the cap", told, StringComparison.Ordinal);
    }
}
