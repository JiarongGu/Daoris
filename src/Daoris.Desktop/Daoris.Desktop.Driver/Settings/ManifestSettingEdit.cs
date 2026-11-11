namespace Daoris.Driver;

/// <summary>The manifest setting's edit of a config.</summary>
public static class ManifestSettingEdit
{
    /// <summary>Set the files bound, the minutes bound, the copy bound in megabytes, or any of them; a null keeps what the config has.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithManifest(this DriverConfig config, int? files, int? minutes, int? copy = null)
    {
        if (files is { } count && !ManifestSetting.TakesFiles(count)) throw new DriverException(ManifestSetting.FilesRefusal);
        if (minutes is { } time && !ManifestSetting.TakesMinutes(time)) throw new DriverException(ManifestSetting.MinutesRefusal);
        if (copy is { } megabytes && !ManifestSetting.TakesCopy(megabytes)) throw new DriverException(ManifestSetting.CopyRefusal);
        return config with
        {
            Manifest = new ManifestSetting(files ?? config.Manifest.Files, minutes ?? config.Manifest.Minutes) { Copy = copy ?? config.Manifest.Copy },
        };
    }
}
