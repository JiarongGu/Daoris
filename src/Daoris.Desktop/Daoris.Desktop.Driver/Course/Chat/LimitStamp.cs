using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// What the limit wait decides from, as one version: the choices' <see cref="Effective"/>, the revisions of every family of the store
/// the move reads (<see cref="LimitMove.Reads"/>) in one query, the ledger's holds' count, and beside them the accounts' parts that are
/// no declaration. Unversioned where the choices are, or the store did not read: no stamp equals it, so the look after it runs.
/// </summary>
/// <remarks>No record and no equality of its own: two are one by <see cref="Same"/> alone.</remarks>
internal sealed class LimitStamp
{
    private readonly string? _text;

    private LimitStamp(string? text) => _text = text;

    /// <summary>The stamp of <paramref name="config"/> as <paramref name="home"/>'s store and <paramref name="staff"/>'s holds stand now.</summary>
    internal static LimitStamp Of(string home, DriverConfig config, StaffLedger? staff)
    {
        // Choices built in memory carry no load: what they decide from is the object the look is handed.
        var choices = config.Effective is not { } effective ? "built" : effective.Versioned ? effective.Text : null;
        if (choices is null) return new(null);
        try
        {
            return new($"{choices}|{Revisions(home)}|holds:{staff?.Holds.Revision ?? 0}");
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return new(null);
        }
    }

    /// <summary>This stamp with <paramref name="accounts"/>, the parts that are no declaration, beside it.</summary>
    internal LimitStamp Beside(string accounts) => _text is { } text ? new($"{text}|{accounts}") : this;

    /// <summary>Whether two are one: false where either is missing or unversioned.</summary>
    internal static bool Same(LimitStamp? one, LimitStamp? other) => one?._text is { } a && other?._text is { } b && a == b;

    private static string Revisions(string home)
    {
        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
        if (store is null) return "none";
        var names = LimitMove.Reads.Select((_, at) => "$f" + at).ToArray();
        var rows = store.Query(
            $"SELECT family, revision FROM store_revisions WHERE family IN ({string.Join(", ", names)}) ORDER BY family",
            row => $"{row.GetString(0)}:{row.GetInt64(1)}",
            [.. LimitMove.Reads.Select((family, at) => (names[at], (object?)family))]);
        return string.Join(",", rows);
    }
}
