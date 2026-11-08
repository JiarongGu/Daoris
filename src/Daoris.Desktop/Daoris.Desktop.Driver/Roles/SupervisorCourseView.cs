using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A course decision as the page and the print read it: the point a person's <c>refer</c> rule held a line at, what the rule counted and
/// by which rule and place, the supervisor's act, who held it, when, and where it stands. Codes and moments alone, never a sentence.
/// </summary>
/// <param name="Rule">The id of the rule that referred the point.</param>
/// <param name="Earlier">Whether it is read from its file as history: a restart empties memory, which alone decides.</param>
public sealed record SupervisorCourseView(
    string Id, string Session, string Point, string Count, int N, string Rule, string Place, string Do, SupervisorHolder Holder,
    DateTimeOffset KeptAt, DateTimeOffset GoesOnAt, SupervisorState State, DateTimeOffset StateAt, string? Why, bool Earlier)
{
    /// <summary>
    /// A course decision read from its file and state lines (<see cref="SupervisorCourses"/> writes both), or null where the file is not one.
    /// </summary>
    internal static SupervisorCourseView? Read(SupervisorDecisionFiles files, string session, string id)
    {
        try
        {
            if (SupervisorDecisionFiles.Read(files.FileOf(session, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("question", out _)
                || !root.TryGetProperty("course", out var course) || course.ValueKind != JsonValueKind.Object) return null;
            if (Json.Text(course, "point") is not { } point || Json.Text(course, "do") is not { } act) return null;
            var holder = SupervisorHolders.Read(root);
            var kept = Moments.Read(root, "keptAt") ?? DateTimeOffset.MinValue;
            var (state, at, why) = (SupervisorState.Held, kept, (string?)null);
            foreach (var line in (SupervisorDecisionFiles.Read(files.StatesOf(session, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var each = JsonDocument.Parse(line);
                if (SupervisorStates.Parse(Json.Text(each.RootElement, "state")) is not { } said) continue;
                (state, at, why) = (said, Moments.Read(each.RootElement, "at") ?? at, Json.Text(each.RootElement, "why"));
            }

            return new SupervisorCourseView(
                id, session, point, Json.Text(course, "count") ?? "", course.TryGetProperty("n", out var n) && n.TryGetInt32(out var value) ? value : 0,
                Json.Text(course, "rule") ?? "", Json.Text(course, "place") ?? "", act, holder, kept, Moments.Read(root, "goesOnAt") ?? kept, state, at, why,
                Earlier: true);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
