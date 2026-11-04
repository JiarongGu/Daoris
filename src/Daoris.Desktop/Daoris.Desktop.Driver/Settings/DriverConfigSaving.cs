namespace Daoris.Driver;

/// <summary>
/// An edit of <c>driver.json</c> made onto the file as it stands, under its lock. The edit (what changed between
/// <see cref="DriverConfig.Source"/> as read and the choices) is made onto the file read again here, under the same keep rules
/// (FABRIC-GRAPH1's re-check, finding 1): written over <see cref="DriverConfig.Source"/>, a change another writer made in between was
/// lost, and a file that had since stopped reading was overwritten.
/// </summary>
internal static class DriverConfigSaving
{
    /// <summary>
    /// Write the choices back, atomically, beside-then-rename: the watch loop re-reads the file every tick, and a torn read must never
    /// be what it finds. A file that does not read now, or holds a key twice in one object, is refused and left as it is; one that is
    /// gone is written from nothing. The edit touches only the key paths it changed (the second re-check, finding 1,
    /// <see cref="JsonKeeping"/>): an object taken out since is made again with only the edit's keys, and one that is no longer an object
    /// refuses it. The re-read and the rename are made holding the file's lock (CONFIGLOCK1, <see cref="KeepLock"/>), which the CLI's
    /// writer takes too; the lock is read again just before the rename, and a save whose lock was taken is refused with nothing written.
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
            // The second re-check's finding 1: another writer made a value this edit changes something inside no longer an object.
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
            now = DriverConfig.Load(path);
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
