namespace Daoris.Driver;

/// <summary>
/// A conversation's permission requests on the protocol door, held for the person below full access as a driven session's are (D253):
/// its posture marked so its session holds them (<see cref="PostureAsked.PersonAnswers"/>), and the registry's door it holds them
/// through. Ask Daoris in full access runs its harness's mode that asks nothing, and is handed neither.
/// </summary>
internal static class ChatHolds
{
    /// <summary>The posture the conversation's open asks: marked below full access.</summary>
    public static string? Posture(string? posture, string? helpMode) => helpMode == HelpModes.Full ? posture : PostureAsked.Held(posture);

    /// <summary>The door its requests are held through, in the registry the page and the supervisor read; null in full access.</summary>
    public static PermissionHolds? Door(ISessionEvents events, string session, string? helpMode) =>
        helpMode == HelpModes.Full ? null : HeldPermissions.Of(events).For(session);
}
