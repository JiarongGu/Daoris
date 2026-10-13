namespace Daoris.Driver;

/// <summary>
/// The plugin kit's files on this machine (PLUG8, D101; STRUCT3a): the folder a new plugin is written into, the templates
/// the build carries under their pinned names, and a trial's scratch under the home.
/// </summary>
internal static class PluginKitFiles
{
    /// <summary>A new plugin's folder: named by its id, inside <paramref name="parent"/> as this machine resolves it.</summary>
    public static string FolderIn(string parent, string id) => Path.Combine(Path.GetFullPath(parent), id);

    /// <summary>A new plugin goes into a folder that is not there yet, or is empty, inside one that is.</summary>
    /// <exception cref="DriverException">The folder is no place to write a new plugin.</exception>
    public static void Writable(string folder)
    {
        var parent = Path.GetDirectoryName(folder)!;
        if (!Directory.Exists(parent))
        {
            throw new DriverException(
                $"`{parent}` does not exist — name a folder that does; the plugin's own folder is made inside it, named by its id.");
        }

        if (File.Exists(folder))
        {
            throw new DriverException($"`{folder}` already holds a file of that name — choose another id, or another folder to make it in.");
        }

        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Count() is > 0 and var held)
        {
            throw new DriverException(
                $"`{folder}` already holds {held} item(s) — the kit writes a new plugin into a new or empty folder, "
                + "never over one. Choose another id, or another folder to make it in.");
        }
    }

    /// <summary>Write a plan's files, each whole or not at all. The folder is judged again first: a plan is what the disk said when it was made.</summary>
    /// <returns>The names written, in the plan's order.</returns>
    public static IReadOnlyList<string> Write(ScaffoldPlan plan)
    {
        Writable(plan.Folder);
        Directory.CreateDirectory(plan.Folder);
        foreach (var file in plan.Files) AtomicFile.WriteText(Path.Combine(plan.Folder, file.Name), file.Content);
        return plan.Files.Select(file => file.Name).ToList();
    }

    /// <summary>A template the build carries, with its line endings made LF whatever the checkout did to it.</summary>
    public static string Template(string name)
    {
        using var stream = typeof(PluginKitFiles).Assembly.GetManifestResourceStream($"Daoris.Driver.plugin-kit.{name}.template")
            ?? throw new InvalidOperationException($"the plugin kit's `{name}` template is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }

    /// <summary>The folder this process stands in: where `plugins new` writes when no folder is named.</summary>
    public static string Here() => Directory.GetCurrentDirectory();

    /// <summary>A folder's whole path on this machine.</summary>
    public static string WholePath(string folder) => Path.GetFullPath(folder);

    /// <summary>Whether a file is there.</summary>
    public static bool HasFile(string path) => File.Exists(path);

    /// <summary>A folder made, with every folder above it.</summary>
    public static void Make(string folder) => Directory.CreateDirectory(folder);

    /// <summary>One trial's scratch folder, made fresh under <paramref name="parent"/>; removed when the trial ends.</summary>
    public static string ScratchIn(string parent) =>
        Directory.CreateDirectory(Path.Combine(parent, "try-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    /// <summary>A scratch folder goes when the trial ends; a process a moment slow to let go of it is waited for, briefly.</summary>
    public static void Remove(string folder)
    {
        for (var attempt = 0; Directory.Exists(folder); attempt++)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 10) return;
                Thread.Sleep(200);
            }
        }
    }
}
