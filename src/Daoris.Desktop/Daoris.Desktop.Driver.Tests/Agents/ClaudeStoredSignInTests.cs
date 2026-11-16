using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Claude Code's stored sign-in is read for its shape and never decoded (CREDRACE1): whether a token is there is judged on the JSON's own
/// bytes, so no string of a credential's value is made, to be kept, logged or left for the collector.
/// </summary>
public sealed class ClaudeStoredSignInTests
{
    /// <summary>
    /// The proof is what the read allocates: reading three fields' shape over a 64 KiB token allocates far less than one copy of it, which
    /// a string of its value would be twice over.
    /// </summary>
    [Fact]
    public void A_tokens_presence_is_read_without_a_string_of_its_value()
    {
        var token = new string('t', 64 * 1024);
        using var document = JsonDocument.Parse($$"""{"accessToken":"{{token}}","refreshToken":"","expiresAt":1}""");
        var oauth = document.RootElement;
        // Once first, so what the first call loads is not counted.
        ClaudeStoredSignIn.There(oauth, "accessToken");

        var before = GC.GetAllocatedBytesForCurrentThread();
        var access = ClaudeStoredSignIn.There(oauth, "accessToken");
        var refresh = ClaudeStoredSignIn.There(oauth, "refreshToken");
        var number = ClaudeStoredSignIn.There(oauth, "expiresAt");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal((true, false, false), (access, refresh, number));
        Assert.True(allocated < token.Length / 4, $"reading the shape allocated {allocated} bytes, as a copy of the token would");
    }
}
