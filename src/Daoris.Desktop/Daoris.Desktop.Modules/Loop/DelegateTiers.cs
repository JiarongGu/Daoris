using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A session's tier as a proved press reads it at its admission (<see cref="PipeDelegateSession"/>): the keys held for it, Ask Daoris's
/// mode and the families of acts the person granted it, or a working session's repository's permission prompts, each read from its
/// file now so a change between two presses takes the next.
/// </summary>
internal static class DelegateTiers
{
    public static PipeDelegateSession Of(DriverLoop loop, string session)
    {
        var mode = loop.Chat?.HelpModeOf(session);
        return new PipeDelegateSession(loop.Processes.Delegates.Of(session), mode, route => loop.Chat?.HelpPressed(session, route))
        {
            Families = mode == HelpModes.Restricted ? ConfiguredAccess.Read(loop.Home).Acts : [],
            Permissions = mode is null ? Permissions(loop, loop.Processes.Delegates.PlaceOf(session)) : null,
        };
    }

    /// <summary>The place's permission prompts as its access setup resolves now; null where it names none or the file does not read.</summary>
    private static string? Permissions(DriverLoop loop, SessionPlace? place)
    {
        if (place is null) return null;
        try
        {
            return AccessRules.Resolve(DriverConfig.Load(loop.ConfigPath), place.Repository, place.Workspace).Value(AccessRules.Permissions);
        }
        catch (DriverException)
        {
            return null;
        }
    }
}
