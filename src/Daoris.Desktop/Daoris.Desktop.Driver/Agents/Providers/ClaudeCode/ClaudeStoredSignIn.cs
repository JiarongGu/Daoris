using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's stored sign-in: <c>.credentials.json</c> in the account's home, its <c>claudeAiOauth</c> object's
/// <c>accessToken</c>, <c>refreshToken</c> and <c>expiresAt</c> (milliseconds since the epoch). Read for its shape alone: whether
/// each token is there, never its value, which is neither kept nor logged nor shown.
/// </summary>
/// <remarks>
/// Both tokens empty and <c>expiresAt</c> 0 is what the agent writes when a refresh of a rotating token is refused, though
/// the refresh token stays valid for weeks.
/// </remarks>
internal sealed class ClaudeStoredSignIn : IStoredSignIn
{
    /// <summary>The store's name in an account's home.</summary>
    public const string FileName = ".credentials.json";

    /// <summary>The agent writes the store through a temp file beside it, <c>.credentials.json.tmp.*</c>, and renames it over the store.</summary>
    public const string TempPattern = FileName + ".tmp.*";

    string? IStoredSignIn.FileName => FileName;

    string? IStoredSignIn.LeftoverPattern => TempPattern;

    public StoredSignInShape? Read(string profileHome) => ReadFile(Path.Combine(profileHome, FileName));

    public StoredSignInShape? ReadFile(string path)
    {
        byte[]? bytes = null;
        try
        {
            if (!File.Exists(path)) return null;
            var written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            bytes = ReadShared(path);
            // Parsed over the bytes read, never copied out of them, and those bytes cleared once the shape is read.
            using var document = JsonDocument.Parse(bytes.AsMemory());
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object)
            {
                return new StoredSignInShape(false, false, null, written);
            }

            DateTimeOffset? expires = oauth.TryGetProperty("expiresAt", out var at) && at.ValueKind == JsonValueKind.Number
                && at.TryGetInt64(out var ms) && ms > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : null;
            return new StoredSignInShape(There(oauth, "accessToken"), There(oauth, "refreshToken"), expires, written);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            // A store that does not read says nothing: neither signed in nor cleared.
            return null;
        }
        finally
        {
            if (bytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public StoredFingerprint? Fingerprint(string profileHome)
    {
        byte[]? bytes = null;
        try
        {
            var path = Path.Combine(profileHome, FileName);
            if (!File.Exists(path)) return null;
            bytes = ReadShared(path);
            using var document = JsonDocument.Parse(bytes.AsMemory());
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object
                || !There(oauth, "refreshToken")) return null;

            // The token's own bytes in the file, quotes included: hashed where they lie, so no string of it is ever made.
            return StoredFingerprint.Of(System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(oauth.GetProperty("refreshToken")));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
        finally
        {
            if (bytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>The bytes, opened sharing delete too, so a read never fails the agent's rename over the store.</summary>
    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>
    /// Presence alone: a string that is not empty, judged against the JSON's own bytes. No string of the value is made: it is never decoded,
    /// so it can be neither kept nor logged.
    /// </summary>
    internal static bool There(JsonElement oauth, string name) =>
        oauth.TryGetProperty(name, out var token) && token.ValueKind == JsonValueKind.String && !token.ValueEquals(""u8);
}
