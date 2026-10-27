using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Where a circle's intake room is on the disk, and the one folder name a circle goes by: the lower home of what history clearing and the
/// tool home's left-over files read of the room, which <see cref="IntakeRoom.PathOf"/> and <see cref="IntakeRoom.SafeName"/> keep under
/// their own names for the readers above the workplace.
/// </summary>
internal static class IntakeRoomPaths
{
    /// <summary>Where a circle's room is — the home's <see cref="HomeEntry.Intake"/>, one per circle, whatever it is called.</summary>
    internal static string PathOf(string home, string workspace) =>
        DaorisHome.Entry(home, HomeEntry.Intake, SafeName(workspace));

    /// <summary>
    /// A circle's name as one folder: ASCII letters, digits, `-` and `_`, lower case — and, when that
    /// lost anything, a short hash of the exact name beside it, so two circles are never one room.
    /// A workspace plan's file is named by the same rule (WSSETUP6), so two circles never share one.
    /// </summary>
    /// <remarks>
    /// 🔴 Every name with no ASCII letter in it used to fold to <c>default</c>, and `my circle` and
    /// `my-circle` to one folder (REV3). The room is the intake's lock and holds its circle's
    /// declarations, so two circles in one room blocked each other and read each other's family.
    /// A name that is already a folder name stays exactly that, so no existing room moves.
    /// </remarks>
    internal static string SafeName(string workspace)
    {
        var name = workspace.Trim().ToLowerInvariant();
        if (name.Length == 0) name = "default";   // silence is the default circle, as everywhere (D48)
        var safe = new string(name
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        if (safe == name && safe.Length > 0) return safe;

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8].ToLowerInvariant();
        return safe.Length == 0 ? $"workspace-{hash}" : $"{safe}-{hash}";
    }
}
