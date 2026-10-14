namespace Daoris.Desktop;

/// <summary>
/// What an install's windows tell the Windows taskbar about themselves (TASKBAR1, D108): one application
/// id, and the command, name and icon a pin made from a running window is built from.
/// </summary>
/// <remarks>
/// <para><b>Why a window has to say it.</b> The window belongs to <c>app/Daoris.Desktop.exe</c>, while the
/// one thing a person runs is <c>Daoris.exe</c> at the install's root (D93), which starts it and exits.
/// Windows groups a taskbar button by its process's executable unless the window names an id, so a
/// pinned launcher sat beside a second button for the running window, and pinning that window pinned
/// the application in <c>app/</c>, which a republish replaces. A window that names an id is grouped by
/// it, and its relaunch properties are what a pin made from it starts: here, the launcher at the root.</para>
///
/// <para><b>Only an install wears it.</b> A workspace build has no launcher to relaunch, and the dev
/// loop's window must never join the person's pinned Daoris, so it keeps Windows' own grouping by its
/// executable. What tells the two apart is <see cref="InstallHome.Marker"/>, as it is for the home.</para>
///
/// <para><b>A twin.</b> <see cref="Launcher"/> is the launcher's own file name, its project's
/// <c>AssemblyName</c> (and <c>tools/desktop-publish.mjs</c>'s <c>LAUNCHER</c>, which lays it out);
/// <see cref="Id"/> is the launcher's <c>Launcher.AppId</c>, which its process carries because a
/// chained start is one application to Windows. <c>TaskbarIdentityTests</c> reads both from the
/// launcher's project and source.</para>
///
/// <para>Written by the application's <c>TaskbarWindow</c> into each window's property store. Whether
/// the taskbar then shows one button is seen by looking at one, not by any test here.</para>
/// </remarks>
/// <param name="AppId">The window's <c>System.AppUserModel.ID</c>: <see cref="Id"/>.</param>
/// <param name="RelaunchCommand">Its <c>System.AppUserModel.RelaunchCommand</c>: the root launcher, quoted.</param>
/// <param name="RelaunchDisplayName">Its <c>System.AppUserModel.RelaunchDisplayNameResource</c>: <see cref="DisplayName"/>.</param>
/// <param name="RelaunchIcon">Its <c>System.AppUserModel.RelaunchIconResource</c>: the launcher's own icon, which is Daoris's.</param>
public sealed record TaskbarIdentity(string AppId, string RelaunchCommand, string RelaunchDisplayName, string RelaunchIcon)
{
    /// <summary>
    /// The application id, in Windows' <c>Company.Product</c> form (no more than 128 characters, no
    /// spaces). No version in it, so a pin made before an upgrade is still the button after it, and it
    /// never changes, because every pin a person has made carries it. The same for every install (D108).
    /// </summary>
    public const string Id = "Daoris.Desktop";

    /// <summary>The launcher at an install's root (D93), which a pin starts.</summary>
    public const string Launcher = "Daoris.exe";

    /// <summary>The name a pin made from the window wears: the product's, in every language.</summary>
    public const string DisplayName = "Daoris";

    /// <summary>
    /// The identity for a process running from <paramref name="baseDirectory"/>, or null when that is
    /// not an install (no marker at the root <see cref="InstallHome.RootOf"/> finds) or the install has
    /// no launcher at its root: nothing a pin could start would outlive the next publish.
    /// </summary>
    public static TaskbarIdentity? For(string baseDirectory)
    {
        var root = InstallHome.RootOf(baseDirectory);
        if (!File.Exists(Path.Combine(root, InstallHome.Marker))) return null;

        var launcher = Path.Combine(root, Launcher);
        if (!File.Exists(launcher)) return null;

        // Quoted, because an install's folder may have a space in its name and the command is read as a
        // command line. The icon is a resource path with its index, which is never quoted.
        return new TaskbarIdentity(Id, $"\"{launcher}\"", DisplayName, $"{launcher},0");
    }
}
