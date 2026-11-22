using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The ask a terminal or a session leaves in the home before it closes the desktop's window from outside (<c>node tools/desktop.mjs
/// kill</c> and <c>restart</c>): the windows system reports that close as the person's, so the ask is how the stop says who closed it.
/// </summary>
/// <remarks>
/// <c>run/stop-ask.json</c> names the process ids it concerns, a coded <c>by</c>, the asking session where there is one and the
/// time. An ask for another process, or older than <see cref="Fresh"/>, is no ask of this stop.
/// </remarks>
public static class StopAsk
{
    /// <summary>How long an ask stands: the tool closes a window for up to fifteen seconds, and an old file is a stop's leftover.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(2);

    private static readonly Regex Code = new("^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant);

    private static readonly Regex Session = new("^[0-9a-f]{4,64}$", RegexOptions.CultureInvariant);

    /// <summary>The coded origin and asking session of the ask left for <paramref name="pid"/>; null where there is none.</summary>
    public static (string By, string? Session)? Read(string? home, int pid, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(home)) return null;
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.StopAsk);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pids", out var pids) || pids.ValueKind != JsonValueKind.Array
                || !pids.EnumerateArray().Any(each => each.ValueKind == JsonValueKind.Number && each.TryGetInt32(out var asked) && asked == pid)
                || !root.TryGetProperty("at", out var at) || !DateTimeOffset.TryParse(at.GetString(), out var when)
                || now - when > Fresh || when - now > Fresh)
            {
                return null;
            }

            var by = root.TryGetProperty("by", out var named) && named.GetString() is { } code && Code.IsMatch(code) ? code : StopReasons.Unknown;
            var session = root.TryGetProperty("session", out var held) && held.GetString() is { } id && Session.IsMatch(id) ? id : null;
            return (by, session);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
