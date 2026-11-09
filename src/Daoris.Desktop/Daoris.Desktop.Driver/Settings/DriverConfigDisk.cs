using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>driver.json</c> read from disk and an edit written onto it as it stands, through its one writer (<see cref="DriverConfigWrites"/>).
/// The edit (what changed between <see cref="DriverConfig.Source"/> as read and the choices) is made onto the file the writer reads
/// under its lock, under the same keep rules: written over <see cref="DriverConfig.Source"/>, a change another writer made in between
/// would be lost, and a file that had since stopped reading would be overwritten.
/// </summary>
internal static class DriverConfigDisk
{
    /// <summary>
    /// A missing file is a machine that has opted nothing in — the empty config's choices, not an error. They carry the path and <c>{}</c>
    /// as their text, so their save is an edit like any other: read without a text, they were written whole over a file another writer made
    /// before the save, a file that did not read or held a key twice included.
    /// </summary>
    /// <exception cref="DriverConfigUnreadableException">
    /// The file is there and does not read: it could not be opened, is not JSON, or is not the choices' shape.
    /// The sentence names the file, and the line and byte where the parser stopped; every door says it as it says a refusal.
    /// </exception>
    public static DriverConfig Load(string path)
    {
        if (!File.Exists(path)) return Of(path, null);

        byte[] bytes;
        try
        {
            bytes = AtomicFile.ReadBytes(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverConfigUnreadableException(path, $"{path} could not be read ({error.Message.TrimEnd('.')}).");
        }

        return Of(path, BesideReads.Text(bytes), FileStamp.Hash(bytes));
    }

    /// <summary>The choices of <paramref name="text"/> (none where no file stands), the store's staffing laid over them at this ask;
    /// <paramref name="key"/> is the bytes' content key, the text's own where the writer read the text.</summary>
    private static DriverConfig Of(string path, string? text, string? key = null)
    {
        var file = Path.GetFullPath(path);

        // A fresh home's staffing is in the store before it has a file, so a missing file is filled too.
        if (text is null) return RoleStaffingPort.Fill(DriverConfig.Empty with { Source = "{}", SourceFile = file, Effective = Effective.Of("absent") }, path);
        var effective = Effective.Of(key ?? FileStamp.Hash(System.Text.Encoding.UTF8.GetBytes(text)));
        try
        {
            return RoleStaffingPort.Fill(DriverConfigReader.Parse(text) with { SourceFile = file, Effective = effective }, path);
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
    /// since is made again with only the edit's keys, and one that is no longer an object refuses it. The writer reads the file and
    /// renames holding its lock, which the CLI's writer takes too; a save whose lock another writer takes is refused with nothing written.
    /// </summary>
    public static void Save(DriverConfig config, string path, KeepSeams? seams) =>
        DriverConfigWrites.Change(path, current => Written(config, path, current), seams);

    /// <summary>A standing choice written: <paramref name="change"/> made to the file as the writer reads it under its lock, so it starts
    /// from what a second writer wrote meanwhile, and the edit written over it. The choices it wrote.</summary>
    public static DriverConfig Edit(string path, Func<DriverConfig, DriverConfig> change)
    {
        DriverConfig? next = null;
        DriverConfigWrites.Change(path, current =>
        {
            next = change(Of(path, current));
            return Written(next, path, current);
        });
        return next!;
    }

    /// <summary>The choices as their reader understood <see cref="DriverConfig.Source"/>, written: what an edit is measured from.</summary>
    public static string Understood(DriverConfig config) =>
        DriverConfigWriter.Written(DriverConfigReader.Parse(config.Source!) with { Source = null });

    private static string Written(DriverConfig config, string path, string? current)
    {
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        try
        {
            return config.Source is not null && string.Equals(config.SourceFile, Path.GetFullPath(path), same)
                ? JsonKeeping.Kept(Current(path, current), Understood(config), DriverConfigWriter.Written(config), DriverConfig.Sets)
                : config.ToJson();
        }
        catch (JsonEditRefusedException refused)
        {
            // Another writer made a value this edit changes something inside no longer an object.
            throw new DriverConfigUnreadableException(path, $"{path} has changed under this edit: {refused.Message}, so this edit was not written. "
                + DriverConfigUnreadableException.Remedy);
        }
    }

    /// <summary>The file's text as it stands, for an edit to be written over; <c>{}</c> where it is gone.</summary>
    private static string Current(string path, string? text)
    {
        if (text is null) return "{}\n";
        try
        {
            Of(path, text);
        }
        catch (DriverConfigUnreadableException error)
        {
            throw new DriverConfigUnreadableException(
                error.Path, $"{error.Message} This edit was not written: a file that does not read is never overwritten.", error.Line, error.Byte);
        }

        if (JsonKeeping.Twice(text) is { } key)
        {
            throw new DriverConfigUnreadableException(path, $"{path} holds the key `{key}` twice in one object, so an edit cannot tell which it "
                + $"changes; this edit was not written. {DriverConfigUnreadableException.Remedy}");
        }

        return text;
    }
}
