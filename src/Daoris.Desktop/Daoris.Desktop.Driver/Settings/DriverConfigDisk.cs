using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>driver.json</c> read from disk and an edit written onto it as it stands, under its lock. The edit (what changed between
/// <see cref="DriverConfig.Source"/> as read and the choices) is made onto the file read again here, under the same keep rules: written
/// over <see cref="DriverConfig.Source"/>, a change another writer made in between was lost, and a file that had since stopped reading
/// was overwritten.
/// </summary>
internal static class DriverConfigDisk
{
    /// <summary>
    /// A missing file is a machine that has opted nothing in — the empty config's choices, not an error. They carry the path and <c>{}</c>
    /// as their text, so their save is an edit like any other: read without a text, they were written whole over a file another writer made
    /// before the save, a file that did not read or held a key twice included.
    /// </summary>
    /// <exception cref="DriverConfigUnreadableException">
    /// The file is there and does not read (CONFIGREAD1): it could not be opened, is not JSON, or is not the choices' shape.
    /// The sentence names the file, and the line and byte where the parser stopped; every door says it as it says a refusal.
    /// </exception>
    public static DriverConfig Load(string path)
    {
        // A fresh home's staffing is in the store before it has a file (ONEWAY1b), so a missing file is filled too.
        if (!File.Exists(path)) return RoleStaffingPort.Fill(DriverConfig.Empty with { Source = "{}", SourceFile = Path.GetFullPath(path) }, path);

        string text;
        try
        {
            text = AtomicFile.ReadText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverConfigUnreadableException(path, $"{path} could not be read ({error.Message.TrimEnd('.')}).");
        }

        try
        {
            return RoleStaffingPort.Fill(DriverConfigReader.Parse(text) with { SourceFile = Path.GetFullPath(path) }, path);
        }
        catch (JsonException error) when (error.LineNumber is { } line && error.BytePositionInLine is { } position)
        {
            // The parser counts both from zero and repeats them at the end of its message; a person counts from one.
            var reason = error.Message;
            var counts = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (counts >= 0) reason = reason[..counts];
            throw new DriverConfigUnreadableException(
                path,
                $"{path} is not readable JSON at line {line + 1}, byte {position + 1} ({reason.TrimEnd('.', ' ')}). "
                + DriverConfigUnreadableException.Remedy,
                line + 1,
                position + 1);
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        {
            // JSON that parsed and is not the choices' shape: Parse's own words, or the runtime's where a reader below it
            // met a value of a kind it does not take.
            throw new DriverConfigUnreadableException(
                path, $"{path} does not read: {error.Message.TrimEnd('.', ' ')}. {DriverConfigUnreadableException.Remedy}");
        }
    }

    /// <summary>
    /// Write the choices back, atomically, beside-then-rename: the watch loop re-reads the file every tick, and a torn read must never
    /// be what it finds. A file that does not read now, or holds a key twice in one object, is refused and left as it is; one that is
    /// gone is written from nothing. The edit touches only the key paths it changed (<see cref="JsonKeeping"/>): an object taken out
    /// since is made again with only the edit's keys, and one that is no longer an object refuses it. The re-read and the rename are
    /// made holding the file's lock (CONFIGLOCK1, <see cref="KeepLock"/>), which the CLI's writer takes too; the lock is read again just
    /// before the rename, and a save whose lock was taken is refused with nothing written.
    /// </summary>
    public static void Save(DriverConfig config, string path, KeepSeams? seams)
    {
        var file = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var held = KeepLock.Take(file, within: null, seams?.Clock);
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string text;
        try
        {
            text = config.Source is not null && string.Equals(config.SourceFile, file, same)
                ? JsonKeeping.Kept(Current(path), Understood(config), DriverConfigWriter.Written(config), DriverConfig.Sets)
                : config.ToJson();
        }
        catch (JsonEditRefusedException refused)
        {
            // Another writer made a value this edit changes something inside no longer an object.
            throw new DriverConfigUnreadableException(path, $"{path} has changed under this edit: {refused.Message}, so this edit was not written. "
                + DriverConfigUnreadableException.Remedy);
        }

        held.Write(path, text, seams);
    }

    /// <summary>The choices as their reader understood <see cref="DriverConfig.Source"/>, written: what an edit is measured from.</summary>
    public static string Understood(DriverConfig config) =>
        DriverConfigWriter.Written(DriverConfigReader.Parse(config.Source!) with { Source = null });

    /// <summary>The file at <paramref name="path"/> as it stands, for an edit to be written over; <c>{}</c> where it is gone.</summary>
    private static string Current(string path)
    {
        DriverConfig now;
        try
        {
            now = Load(path);
        }
        catch (DriverConfigUnreadableException error)
        {
            throw new DriverConfigUnreadableException(
                error.Path, $"{error.Message} This edit was not written: a file that does not read is never overwritten.", error.Line, error.Byte);
        }

        if (now.Source is not { } text) return "{}\n";
        if (JsonKeeping.Twice(text) is { } key)
        {
            throw new DriverConfigUnreadableException(path, $"{path} holds the key `{key}` twice in one object, so an edit cannot tell which it "
                + $"changes; this edit was not written. {DriverConfigUnreadableException.Remedy}");
        }

        return text;
    }
}
