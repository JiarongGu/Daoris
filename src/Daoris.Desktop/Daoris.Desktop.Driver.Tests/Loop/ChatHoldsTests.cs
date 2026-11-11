using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation's permission requests on the protocol door below full access are held for the person: a chat's and Ask Daoris's in
/// restricted mode are handed the marked posture and the registry's door, so a request its agent sends is listed for the person; Ask
/// Daoris in full access runs its harness's unrestricted mode and is handed neither.
/// </summary>
public sealed class ChatHoldsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(HelpModes.Restricted)]
    public async Task A_chat_and_a_restricted_Ask_Daoris_hold_their_requests_for_the_person(string? helpMode)
    {
        var events = new SessionEvents(Path.Combine("X:/daoris", "sessions"));

        Assert.Equal("?auto|acceptEdits", ChatHolds.Posture("auto|acceptEdits", helpMode));
        var door = ChatHolds.Door(events, "c1", helpMode);
        Assert.NotNull(door);

        var held = door.Hold("r1", "call-1", "Run a command", [new PermissionChoice("yes", "Yes", PermissionChoice.AllowOnce)]);
        Assert.NotNull(held);
        Assert.Equal("r1", Assert.Single(HeldPermissions.Of(events).Held("c1")).Request);
        Assert.True(HeldPermissions.Of(events).Answer("c1", "r1", "yes").Answered);
        Assert.Equal(PermissionWhy.Answered, (await held).Why);
    }

    [Fact]
    public void Full_access_is_handed_neither_the_mark_nor_the_door()
    {
        var events = new SessionEvents(Path.Combine("X:/daoris", "sessions"));

        Assert.Equal("!bypassPermissions", ChatHolds.Posture("!bypassPermissions", HelpModes.Full));
        Assert.Null(ChatHolds.Door(events, "h1", HelpModes.Full));
    }

    /// <summary>An agent whose wire names no mode is asked for none, so nothing is marked for it.</summary>
    [Fact]
    public void A_posture_naming_no_mode_is_left_as_it_is()
    {
        Assert.Null(ChatHolds.Posture(null, null));
        Assert.Equal("", ChatHolds.Posture("", HelpModes.Restricted));
    }
}
