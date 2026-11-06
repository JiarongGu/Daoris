namespace Daoris.Driver;

/// <summary>A stored fact read for a retry's check: a read that cannot be done is no fact found.</summary>
internal static class StoredReads
{
    /// <summary>What <paramref name="read"/> says, or false where the file could not be read.</summary>
    internal static bool Holds(Func<bool> read)
    {
        try
        {
            return read();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
