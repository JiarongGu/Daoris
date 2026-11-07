namespace Daoris.Driver;

/// <summary>The host's token for a place's work process: the place's origin read from its checkout, judged by <see cref="PluginHostToken"/>.</summary>
public static class PlaceHostTokens
{
    /// <summary>What <see cref="HookSet"/> asks at each place's start: the identity's token, or the refusal that leaves the process unstarted.</summary>
    public static Func<PluginEntry, WorkPlace, CancellationToken, Task<HostTokenHand>> For(string home) =>
        async (_, place, ct) => PluginHostToken.Hand(
            home, place.Repository, place.Workspace, await GitRemotes.AddressOf(place.Root, "origin", push: false, ct).ConfigureAwait(false));
}
