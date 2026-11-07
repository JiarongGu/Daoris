namespace Daoris.Driver;

/// <summary>
/// Claude Code's words for a start it refused for its sign-in, seen on its protocol door: the adapter's own
/// <i>authentication required</i>, which its ACP adapter answers when nobody is signed in to the account's home.
/// </summary>
public static class ClaudeSignIn
{
    public static SignInWords Words { get; } = new(
        // At the failure's start or after a door's `: `, and ending its clause, so a sentence that quotes the words or runs on
        // past them is not read as one.
        Markers: [@"(?:^|: )authentication required(?=$|[.:])"],
        Recorded:
        [
            new(
                "the ACP agent refused the call: Authentication required",
                "2026-10-04",
                "the protocol door: the JSON-RPC error a driven start was refused with, Claude Code over its ACP adapter, on an "
                + "account nobody had signed in to, which the rotation chose because every account read never read after an "
                + "update; four starts in two minutes, on the install"),
        ]);
}
