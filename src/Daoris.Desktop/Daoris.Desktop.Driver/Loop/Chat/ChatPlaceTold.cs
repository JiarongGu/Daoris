namespace Daoris.Driver;

/// <summary>
/// What a conversation in a git place is told of it with its first message: the place's manifest read at its start under the writer's
/// lease, as a driven turn's is (<see cref="PlaceTurns"/>), its end read where the record leaves a running state.
/// </summary>
internal static class ChatPlaceTold
{
    /// <summary>The words of what changed in the place outside Daoris and which uncommitted paths are not the conversation's own; null where there are none or the place reads no manifest.</summary>
    /// <param name="continues">The record a hand-over or carry-on continues, whose own paths the conversation carries in.</param>
    public static async Task<string?> ToldAsync(
        string home, string workspace, string department, string root, string record, string? continues, CancellationToken ct)
    {
        var place = await PlaceTurns.StartAsync(home, RemoteTarget.Workspace(workspace), department, root, record, continues, ct).ConfigureAwait(false);
        return PlacePrompt.Told(place).Trim() is { Length: > 0 } words ? words : null;
    }
}
