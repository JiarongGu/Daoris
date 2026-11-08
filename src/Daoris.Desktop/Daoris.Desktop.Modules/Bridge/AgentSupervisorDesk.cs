using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The decision door's half an agent supervisor's look reaches: the subjects every kind's row lists whose hold is an agent's, and an
/// agent's block kept through its kind's row, its grant and its hold read again first (<see cref="SupervisorDecisionDoor.AgentAsync"/>).
/// </summary>
internal sealed class AgentSupervisorDesk(DriverLoop loop) : ISupervisorDesk
{
    public async Task<IReadOnlyList<SupervisorWaiting>> WaitingAsync(CancellationToken ct) =>
        [.. (await SupervisorDecisionDoor.AllWaitingAsync(loop, ct).ConfigureAwait(false)).Select(Row).OfType<SupervisorWaiting>()
            .Where(each => AgentHold.Is(each.Generation))];

    public async Task<SupervisorBlockKept> KeepAsync(SupervisorWaiting waiting, SupervisorBlock block, SupervisorHolder holder, CancellationToken ct)
    {
        var decided = block.Leave is { } bound
            ? await SupervisorDecisionDoor.AgentAsync(loop, block.Subject, waiting.Generation, null, holder, row =>
                row.LeaveAsync(loop, new SupervisorLeaveForm(block.Subject, waiting.Generation, "", bound, block.Reasons), holder, ct)).ConfigureAwait(false)
            : await SupervisorDecisionDoor.AgentAsync(loop, block.Subject, waiting.Generation, block.Do, holder, row =>
                row.DecideAsync(loop, new SupervisorDecideForm(block.Subject, waiting.Generation, "", block.Do!, block.Choice, block.Reasons), holder, ct)).ConfigureAwait(false);
        return new SupervisorBlockKept(decided.Kept, decided.Why);
    }

    /// <summary>A listed subject as the look reads it, from the row each kind lists; null where it does not read.</summary>
    internal static SupervisorWaiting? Row(object listed)
    {
        var row = JsonSerializer.SerializeToElement(listed);
        if (Json.Text(row, "Kind") is not { } kind || !row.TryGetProperty("Subject", out var wire) || SupervisorSubject.Read(kind, wire) is not { } subject
            || Json.Text(row, "Generation") is not { } generation)
        {
            return null;
        }

        IReadOnlyList<string> acts = row.TryGetProperty("Acts", out var listedActs) && listedActs.ValueKind == JsonValueKind.Array
            ? [.. listedActs.EnumerateArray().Select(each => each.GetString()).OfType<string>()]
            : [];
        return new SupervisorWaiting(kind, subject, Json.Text(row, "Repository") ?? "", generation, acts)
        {
            Point = kind == SupervisorKinds.Course
                ? (Json.Text(row, "Count") ?? "", row.TryGetProperty("N", out var n) ? n.ToString() : "", Json.Text(row, "Rule") ?? "", Json.Text(row, "Place") ?? "")
                : null,
        };
    }
}
