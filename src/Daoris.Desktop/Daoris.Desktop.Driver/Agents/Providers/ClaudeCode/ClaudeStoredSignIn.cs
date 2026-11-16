using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's stored sign-in: <c>.credentials.json</c> in the account's home, its <c>claudeAiOauth</c> object's
/// <c>accessToken</c>, <c>refreshToken</c> and <c>expiresAt</c> (milliseconds since the epoch). Read for its shape alone: whether
/// each token is there, never its value, which is neither kept nor logged nor shown.
/// </summary>
/// <remarks>
/// 2026-11-16: an account's file was found with both tokens empty and <c>expiresAt</c> 0 at the minute the install restarted,
/// its refresh token valid for weeks: what the agent writes when a refresh of a rotating token is refused.
/// </remarks>
internal sealed class ClaudeStoredSignIn : IStoredSignIn
{
    /// <summary>The store's name in an account's home.</summary>
    public const string FileName = ".credentials.json";

    public StoredSignInShape? Read(string profileHome)
    {
        var path = Path.Combine(profileHome, FileName);
        try
        {
            if (!File.Exists(path)) return null;
            var written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
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

        // Presence alone: a non-empty string. The value is never read past its length.
        static bool There(JsonElement oauth, string name) =>
            oauth.TryGetProperty(name, out var token) && token.ValueKind == JsonValueKind.String && token.GetString() is { Length: > 0 };
    }
}
