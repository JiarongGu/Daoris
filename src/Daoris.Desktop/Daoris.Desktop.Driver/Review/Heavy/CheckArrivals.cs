using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// When a run first asked the check slots: kept in its folder (<c>lines/&lt;run&gt;/arrival.json</c>) at its first
/// send and read at every later one, so a run back at its checks keeps its first arrival in the queue, until a check of it reaches.
/// </summary>
public static class CheckArrivals
{
    private const string Name = "arrival.json";

    /// <summary>The run's first arrival: the one kept, else <paramref name="now"/>, kept. A run whose folder cannot be written arrives now.</summary>
    public static DateTimeOffset Keep(string home, string run, DateTimeOffset now)
    {
        try
        {
            var path = Path.Combine(VerifyFiles.RunFolder(home, run), Name);
            if (Read(path) is { } kept) return kept;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, $"{{ \"arrived\": \"{now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}\" }}\n");
            return now;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return now;
        }
    }

    /// <summary>A run whose check reached: its next check is a new arrival. One that cannot be removed stays until the run's folder goes.</summary>
    public static void Forget(string home, string run)
    {
        try
        {
            File.Delete(Path.Combine(VerifyFiles.RunFolder(home, run), Name));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
        }
    }

    private static DateTimeOffset? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("arrived", out var arrived)
                   && arrived.ValueKind == JsonValueKind.String && arrived.TryGetDateTimeOffset(out var at)
                ? at
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
