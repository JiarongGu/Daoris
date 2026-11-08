using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A landing decision as the page and the start's sweep read it: the step and attempt it was about, the act, who held it, when, and where
/// it stands. Codes and moments alone, never a sentence.
/// </summary>
/// <param name="Earlier">Whether it is read from its file as history: a restart empties memory, which alone decides.</param>
public sealed record SupervisorLandingView(
    string Id, string Session, string Run, string Node, int Attempt, string Do, SupervisorHolder Holder, DateTimeOffset KeptAt, DateTimeOffset GoesOnAt,
    SupervisorState State, DateTimeOffset StateAt, string? Why, bool Earlier)
{
    public SupervisorSubject Subject => SupervisorSubject.Landing(Session, Run, Node, Attempt);

    /// <summary>A landing decision read from its file and state lines (<see cref="SupervisorLandings"/> writes both), or null where the file is not one.</summary>
    internal static SupervisorLandingView? Read(SupervisorDecisionFiles files, string session, string id)
    {
        try
        {
            if (SupervisorDecisionFiles.Read(files.FileOf(session, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("question", out _)
                || !root.TryGetProperty("landing", out var landing) || landing.ValueKind != JsonValueKind.Object) return null;
            if (Json.Text(landing, "run") is not { } run || Json.Text(landing, "node") is not { } node || Json.Text(landing, "do") is not { } act
                || !landing.TryGetProperty("attempt", out var number) || !number.TryGetInt32(out var attempt))
            {
                return null;
            }

            var kept = Moments.Read(root, "keptAt") ?? DateTimeOffset.MinValue;
            var (state, at, why) = (SupervisorState.Held, kept, (string?)null);
            foreach (var line in (SupervisorDecisionFiles.Read(files.StatesOf(session, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var each = JsonDocument.Parse(line);
                if (SupervisorStates.Parse(Json.Text(each.RootElement, "state")) is not { } said) continue;
                (state, at, why) = (said, Moments.Read(each.RootElement, "at") ?? at, Json.Text(each.RootElement, "why"));
            }

            return new SupervisorLandingView(
                id, session, run, node, attempt, act, SupervisorHolders.Read(root), kept, Moments.Read(root, "goesOnAt") ?? kept, state, at, why, Earlier: true);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
