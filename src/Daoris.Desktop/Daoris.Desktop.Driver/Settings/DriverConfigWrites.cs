using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The one writer of <c>driver.json</c>. Under the file's lock (<see cref="KeepLock"/>, which the CLI's writer takes too) it reads the
/// file's text as it stands, so a write never starts from a kept parse or from a read made before another writer's; hands it to the
/// change; writes what the change answers atomically; and drops the path's kept stamp before it returns, so the next ask reads the
/// new content.
/// </summary>
internal static class DriverConfigWrites
{
    /// <summary>Write what <paramref name="write"/> makes of the file's text as it stands, null where no file stands.</summary>
    /// <exception cref="IOException">Another writer held the file's lock for the whole of the wait, or took it before the rename; nothing is written.</exception>
    internal static void Change(string path, Func<string?, string> write, KeepSeams? seams = null)
    {
        var file = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            using var held = KeepLock.Take(file, within: null, seams?.Clock);
            held.Write(path, write(Current(path, file)), seams);
        }
        finally
        {
            FileStamp.Forget(file);
        }
    }

    private static string? Current(string path, string file)
    {
        try
        {
            return File.Exists(file) ? AtomicFile.ReadText(file) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverConfigUnreadableException(path, $"{path} could not be read ({error.Message.TrimEnd('.')}), so nothing was written.");
        }
    }

    /// <summary>Change the home's <c>driver.json</c> as a JSON object, every key the change has no part in kept as written.</summary>
    /// <exception cref="DriverException">It does not read as an object, so nothing is written over it.</exception>
    internal static void Change(string home, Action<JsonObject> change)
    {
        var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
        Change(path, text =>
        {
            JsonObject root;
            try
            {
                root = text is null
                    ? new JsonObject()
                    : JsonNode.Parse(text) as JsonObject ?? throw new DriverException($"git: {path} is not an object, so nothing was written (config-unread)");
            }
            catch (JsonException error)
            {
                throw new DriverException($"git: {path} does not read ({error.Message}), so nothing was written (config-unread)");
            }

            change(root);
            return JsonFile.Serialize(root);
        });
    }
}
