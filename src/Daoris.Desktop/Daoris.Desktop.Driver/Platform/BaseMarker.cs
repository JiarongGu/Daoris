using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A base's marker found above a path: the folder it stands in, the line whose build it is, and the guard's sentence.</summary>
public sealed record BaseStanding(string Folder, string From, string Says);

/// <summary>
/// The marker a kept base holds, <c>.daoris-base</c> in its configuration folder, naming the previous line and the guard's sentence: a
/// runner that reads a build output without building reads it in the folders from the one it runs from up to the checkout's top, which
/// needs no Daoris installed and finds nothing in a tree Daoris did not take. Written before the record names the base and removed after
/// the record lets it go, so a step cut off leaves a refusal, never an admission.
/// </summary>
public static class BaseMarker
{
    public const string FileName = ".daoris-base";

    /// <summary>The guard's sentence for the base folder <paramref name="relative"/> (repository-relative).</summary>
    public static string Says(string relative) =>
        $"this output is the previous line's build; run `daoris driver trees build {relative.TrimEnd('/')}` first";

    internal static void Write(string slot, string relative, string from)
    {
        var folder = FolderOf(slot, relative);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), new JsonObject { ["from"] = from, ["says"] = Says(relative) }.ToJsonString());
    }

    internal static void Remove(string slot, string relative)
    {
        var marker = Path.Combine(FolderOf(slot, relative), FileName);
        if (File.Exists(marker)) File.Delete(marker);
    }

    /// <summary>The first marker in <paramref name="path"/>'s folder or one above it, up to and including the checkout's top; null where none stands.</summary>
    public static BaseStanding? Above(string path)
    {
        var folder = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path));
        for (; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (Read(folder) is { } standing) return standing;
            if (Directory.Exists(Path.Combine(folder, ".git")) || File.Exists(Path.Combine(folder, ".git"))) return null;
        }

        return null;
    }

    private static BaseStanding? Read(string folder)
    {
        var marker = Path.Combine(folder, FileName);
        if (!File.Exists(marker)) return null;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(marker));
            var root = json.RootElement;
            return new BaseStanding(folder, root.GetProperty("from").GetString() ?? "", root.GetProperty("says").GetString() ?? "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // A marker that does not read still stands: it refuses with the sentence every marker says.
            return new BaseStanding(folder, "", "this output is the previous line's build; run `daoris driver trees build` first");
        }
    }

    internal static string FolderOf(string slot, string relative) => Path.Combine(slot, relative.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
}
