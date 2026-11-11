using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What the person's growth switch says of one press that opens a department or a room (<see cref="GrowthKey"/>): its <paramref name="Value"/>, and
/// <paramref name="Refusal"/>, the route's own words, where the switch is off.
/// </summary>
public sealed record PipeGrowthStanding(string Value, string? Refusal = null);

/// <summary>A press's tier judged, as the growth switch left it: the covered judgement unchanged, or the asked one it turned it to; null judges the press as one with no delegate.</summary>
internal sealed record PipeGrowthJudged(PipeDelegateJudgement? Delegated);

/// <summary>
/// The one place the growth switch bears on a press's admission (<c>docs/2026-11-11-grow-department-design.md</c> §4a): a department's press
/// (<c>REGISTRY_CREATE</c>) and a room's (<c>PLACE_APPLY</c> with a <c>room</c>) that would run at once as a session's, Ask Daoris's in full
/// access or a terminal's under <c>act</c>, are read against the switch first. <c>off</c> refuses them in the route's words, recorded as any
/// refusal of Ask Daoris's is; <c>asked</c> puts them on the person's card as restricted mode does; <c>mode</c> leaves them as they were. A press
/// already asked, or asked by no one with a tier, meets the switch at its view (the plan the route and the card both judge by).
/// </summary>
internal static class PipeGrowth
{
    /// <summary>Whether <paramref name="request"/> opens a department or asks a room: the two presses the switch governs.</summary>
    public static bool Governs(IpcRequest request)
    {
        if (!string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)) return false;
        if (request.Type == "REGISTRY_CREATE") return true;
        return request.Type == "PLACE_APPLY" && request.Payload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("room", out var room) && room.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(room.GetString());
    }

    /// <summary>
    /// The press read against the switch, then admitted again with the judgement the switch leaves it (<paramref name="admit"/>): an unread switch
    /// asks, which is the safe side, and an <c>off</c> one is refused with its words and nothing run.
    /// </summary>
    public static async Task<string> ThenAsync(
        InstancePipe pipe, Func<IpcRequest, CancellationToken, Task<PipeGrowthStanding>> growth, IpcRequest request, PipeDelegateJudgement covered,
        PipeDelegateClaim? claim, PipeConnection connection, Func<PipeGrowthJudged, (string? Now, Task<string>? Later)> admit)
    {
        PipeGrowthStanding standing;
        try
        {
            standing = await growth(request, pipe.Lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            standing = new PipeGrowthStanding(GrowthKey.Asked);
        }

        if (standing.Refusal is { } words)
        {
            pipe.Delegate.Refused(covered, request, words);
            return IpcJson.Serialize(pipe.Refuse(request, InstancePipe.NotAllowed, words));
        }

        var judged = new PipeGrowthJudged(standing.Value == GrowthKey.Asked ? pipe.Delegate.Asking(covered, claim, connection) : covered);
        var (now, later) = admit(judged);
        return now ?? await later!.ConfigureAwait(false);
    }
}
