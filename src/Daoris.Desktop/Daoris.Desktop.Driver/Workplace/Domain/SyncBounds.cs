namespace Daoris.Driver;

/// <summary>
/// How long bringing up to date may take over each step (WSR6, WSR7), and how many fetches run at once. Public because
/// the page spells them again, to wait as long as the host may work: `bridge/call.ts`'s `hostBounds`, held to these by
/// `SyncBoundsTests`. A page that waited its bridge's default 30 seconds gave up on a look that ran for minutes.
/// </summary>
public static class SyncBounds
{
    /// <summary>A fetch is a round trip a person is waiting on, as a plugin's push is (D100).</summary>
    public static readonly TimeSpan Fetch = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How many repositories are fetched at once (WSR7): a look waits about one fetch's time rather than one per
    /// repository, and a small number asks little of the machine's connection and of a remote that limits its callers.
    /// </summary>
    public const int FetchesAtOnce = 4;

    /// <summary>A replay is local, but a signing prompt nobody answers must not hold the press for ever.</summary>
    public static readonly TimeSpan Replay = TimeSpan.FromMinutes(5);
}
