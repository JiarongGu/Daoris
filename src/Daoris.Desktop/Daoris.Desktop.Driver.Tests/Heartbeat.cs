namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A heartbeat file a real child rewrites every 100 ms, read beside its writer: whether it moves, and what it holds. One read for
/// every class that watches one (PROCPAR1): <see cref="ProcessJobTests"/> had kept a copy that shared only reading, which met the
/// writer's open handle and threw at the Process half's first run eight classes at once, as <see cref="PseudoConsoleTests"/>'s had
/// twice in a row under load (TEST4).
/// </summary>
internal static class Heartbeat
{
    /// <summary>Whether the heartbeat moves over most of a second.</summary>
    public static async Task<bool> Beating(string path)
    {
        var before = await ReadAsync(path);
        await Task.Delay(800);
        var after = await ReadAsync(path);
        return before != after;
    }

    /// <summary>The heartbeat as it stands, read sharing writing and deletion with its writer; null where there is none yet.</summary>
    public static async Task<string?> ReadAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(15);
            }
        }
    }
}
