using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An allow decision as the page reads it: which shape it answered (<see cref="SupervisorAllowShapes"/>), the act it took, who held it, when,
/// where it stands, its reasons and a leave's bound. Codes and moments alone, never a sentence.
/// </summary>
public sealed record SupervisorAllowView(
    string Id, string Shape, string Subject, string? Session, string Repository, string Do, string? Role, string? Act, SupervisorHolder Holder,
    DateTimeOffset KeptAt, DateTimeOffset GoesOnAt, SupervisorState State, DateTimeOffset StateAt, string? Why)
{
    public string? Bound { get; init; }

    public IReadOnlyList<SupervisorReason> Reasons { get; init; } = [];

    /// <summary>An allow decision read from its file and state lines (<see cref="SupervisorAllows"/> writes both), or null where the file is not one.</summary>
    internal static SupervisorAllowView? Read(SupervisorDecisionFiles files, string folder, string id)
    {
        try
        {
            if (SupervisorDecisionFiles.Read(files.FileOf(folder, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("allow", out var allow) || allow.ValueKind != JsonValueKind.Object) return null;
            if (Json.Text(allow, "shape") is not { } shape || Json.Text(allow, "subject") is not { } subject || Json.Text(allow, "do") is not { } act) return null;
            var kept = Moments.Read(root, "keptAt") ?? DateTimeOffset.MinValue;
            var (state, at, why) = (SupervisorState.Held, kept, (string?)null);
            foreach (var line in (SupervisorDecisionFiles.Read(files.StatesOf(folder, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var each = JsonDocument.Parse(line);
                if (SupervisorStates.Parse(Json.Text(each.RootElement, "state")) is not { } said) continue;
                (state, at, why) = (said, Moments.Read(each.RootElement, "at") ?? at, Json.Text(each.RootElement, "why"));
            }

            return new SupervisorAllowView(
                id, shape, subject, Json.Text(root, "session"), Json.Text(allow, "repository") ?? "", act, Json.Text(allow, "role"), Json.Text(allow, "act"),
                SupervisorHolders.Read(root), kept, Moments.Read(root, "goesOnAt") ?? kept, state, at, why)
            {
                Bound = Json.Text(allow, "bound"),
                Reasons = [.. Json.Items(root, "reasons").Select(reason => new SupervisorReason(Json.Text(reason, "says") ?? "", Json.Text(reason, "source") ?? ""))],
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
