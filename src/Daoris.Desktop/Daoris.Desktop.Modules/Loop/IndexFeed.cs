using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What the knowledge index holds of each repository, as the service lists it (<c>/api/repositories</c>), read at each look (FOUNDATION1s
/// part B): its signature moves while the index feeds, so the look tells the <c>repositories</c> scope and the status bar's size is read
/// again, and it is quiet once the index has fed. The tick's refetch of the repositories used to cover it (D182's part A note: a summary
/// read mid-feed said 555 entries of 1,050).
/// </summary>
public sealed class IndexFeed
{
    /// <summary>The service's door that lists the repositories with each one's index.</summary>
    public const string Door = "/api/repositories";

    private string? _last;

    /// <summary>Whether <paramref name="signature"/> differs from the last one read; the first is no move, and a failed read (null) none.</summary>
    public bool Moved(string? signature)
    {
        if (signature is null) return false;
        var last = Interlocked.Exchange(ref _last, signature);
        return last is not null && !string.Equals(last, signature, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list's signature: each repository's name, workspace, size and when and at what it was indexed, by name. Null where the answer
    /// is no list.
    /// </summary>
    public static string? Signature(string listed)
    {
        try
        {
            using var document = JsonDocument.Parse(listed);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            var rows = document.RootElement.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => string.Join('\u001f', Field(row, "repository"), Field(row, "workspace"), Field(row, "total"),
                    Field(row, "indexedAt"), Field(row, "indexedCommit")))
                .Order(StringComparer.Ordinal);
            return string.Join('\u001e', rows);
        }
        catch (JsonException)
        {
            return null;
        }

        static string Field(JsonElement row, string name) => row.TryGetProperty(name, out var value) ? value.GetRawText() : "";
    }

    /// <summary>The list's signature as the service answers it now through <paramref name="doors"/>, or null where it does not answer.</summary>
    public static async Task<string?> ReadAsync(IServiceDoors doors, CancellationToken ct)
    {
        try
        {
            return Signature(await doors.GetAsync(Door, ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
            || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }
}
