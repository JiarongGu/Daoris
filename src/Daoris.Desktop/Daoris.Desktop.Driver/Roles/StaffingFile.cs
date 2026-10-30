using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How the file step came out (<see cref="StaffingFile.Project"/>).</summary>
public enum StaffingFiled
{
    /// <summary>The file showed the baseline and now shows the projection: renamed.</summary>
    Written,

    /// <summary>The file showed the baseline and the projection is the baseline: nothing to write.</summary>
    Unchanged,

    /// <summary>The file's paths are not the baseline (another writer changed them, or the file is gone while the baseline holds some): nothing written.</summary>
    Moved,

    /// <summary>The file does not read, or holds a key twice: a file that does not read is never written.</summary>
    Unread,

    /// <summary>The keeping lock stayed held for the whole of the wait, or was taken from this step before its rename: nothing written.</summary>
    Refused,
}

/// <summary>
/// What the file step did and, where it wrote nothing, why: <paramref name="Found"/> is the paths the file showed when they were not the
/// baseline (the check's repair reads them), <paramref name="Said"/> the reason a person reads.
/// </summary>
public sealed record StaffingFileResult(StaffingFiled How, string? Said = null, StaffingPaths? Found = null);

/// <summary>
/// The file step (FOUNDATION1w-s part 2, the design §19.15 step 3, C8-1.1): the staffing projection written onto <c>driver.json</c>'s four
/// staffing paths (<c>actors</c>, <c>adapter</c>, <c>intakeAdapter</c>, <c>helperAdapter</c>) alone, under the file's <see cref="KeepLock"/>,
/// only where the file shows the <b>expected baseline</b> (<c>written</c> for a press; the four paths the check observed for the check's
/// own repair), then renamed with the lock read again just before and the rename made only while it still holds this writer's text
/// (<see cref="DriverConfig.Save"/>'s own fence). Nothing calls it yet (parts 3 and 4 wire the writers and the check).
/// </summary>
/// <remarks>
/// <para><b>Read as the home's file is read:</b> through <see cref="DriverConfig.Load"/>, so a file that does not read, or holds a key
/// twice in one object, is never written; one that is gone reads as <c>{}</c> and is made from nothing where the baseline is nothing.
/// Each path is compared as <see cref="RoleStaffing.Compact"/> text, as the import records it.</para>
/// <para><b>Each path is written whole</b> (<see cref="JsonKeeping.Replaced"/>), never merged key by key into what the file holds, so the file
/// afterwards shows each path exactly as the projection's text, and the check, which compares text, finds it equal. Every other key stays as
/// <see cref="JsonKeeping"/> keeps it.</para>
/// <para><b>The two windows are the downgrade's bound, not closed:</b> a writer that takes this step's aged lock after its look and finishes
/// before its rename is renamed over, and an older build's save renames with no look at its lock, so one paused past its lock's minute may
/// rename late over this step's write. The next check finds either (§19.15).</para>
/// </remarks>
public static class StaffingFile
{
    /// <summary>
    /// Write <paramref name="projection"/> onto <paramref name="file"/>'s four staffing paths where they show <paramref name="expected"/>,
    /// taking the file's keeping lock, waiting <paramref name="wait"/> (the keeping wait where null) for another writer of it.
    /// </summary>
    public static StaffingFileResult Project(string file, StaffingPaths expected, StaffingPaths projection, TimeSpan? wait = null) =>
        Project(file, expected, projection, wait, seams: null);

    internal static StaffingFileResult Project(string file, StaffingPaths expected, StaffingPaths projection, TimeSpan? wait, KeepSeams? seams)
    {
        KeepLock held;
        try
        {
            held = KeepLock.Take(file, wait, seams?.Clock);
        }
        catch (IOException error)
        {
            return new StaffingFileResult(StaffingFiled.Refused, error.Message);
        }

        using (held) return ProjectHolding(file, held, expected, projection, seams);
    }

    /// <summary>
    /// <see cref="Project(string, StaffingPaths, StaffingPaths, TimeSpan?)"/> for a caller that already holds the file's lock over its own
    /// read (the check holds the staffing lock and this one while it repairs); the lock stays held.
    /// </summary>
    internal static StaffingFileResult ProjectHolding(string file, KeepLock held, StaffingPaths expected, StaffingPaths projection, KeepSeams? seams = null)
    {
        string text;
        try
        {
            if (DriverConfig.Load(file).Source is not { } source) return new StaffingFileResult(StaffingFiled.Unread, $"{file} could not be read.");
            if (JsonKeeping.Twice(source) is { } key)
            {
                return new StaffingFileResult(StaffingFiled.Unread, $"{file} holds the key `{key}` twice in one object, so it is not written.");
            }

            text = source;
        }
        catch (DriverConfigUnreadableException error)
        {
            return new StaffingFileResult(StaffingFiled.Unread, error.Message);
        }

        var shown = PathsOf(text);
        if (shown != expected)
        {
            return new StaffingFileResult(
                StaffingFiled.Moved, $"{file}'s staffing is not what this write was made from, so it was not written; the next check finds it.", shown);
        }

        if (projection == expected) return new StaffingFileResult(StaffingFiled.Unchanged);

        var edited = JsonKeeping.Replaced(text, [
            ("adapter", projection.Adapter), ("intakeAdapter", projection.IntakeAdapter),
            ("helperAdapter", projection.HelperAdapter), ("actors", projection.Actors)]);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            held.Write(file, edited, seams);
        }
        catch (KeepLockTakenException error)
        {
            return new StaffingFileResult(StaffingFiled.Refused, error.Message);
        }

        return new StaffingFileResult(StaffingFiled.Written);
    }

    /// <summary>The four staffing paths of a file's text, each as <see cref="RoleStaffing.Compact"/> text, null where the file holds none.</summary>
    internal static StaffingPaths PathsOf(string text)
    {
        using var document = JsonDocument.Parse(JsonKeeping.Once(text));
        string? Of(string name) => document.RootElement.TryGetProperty(name, out var value) ? RoleStaffing.Compact(value) : null;
        return new StaffingPaths(Of("actors"), Of("adapter"), Of("intakeAdapter"), Of("helperAdapter"));
    }
}
