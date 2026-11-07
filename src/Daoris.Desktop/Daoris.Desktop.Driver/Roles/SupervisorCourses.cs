using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The supervisor's course acts on a referred point (the context counters design §5's <c>course</c>): go on, or leave it to the person.</summary>
public static class SupervisorCourseActs
{
    public const string GoOn = "go-on";
    public const string Hold = "hold";

    /// <summary>Every course act; none ends, stops or moves a session.</summary>
    public static IReadOnlyList<string> All { get; } = [GoOn, Hold];

    /// <summary>The bound a <c>hold</c> leaves the point to the person by, as a defer names one.</summary>
    public const string Bound = "course";
}

/// <summary>What a holder decides on a referred point (<c>SUPERVISOR_COURSE</c> less its proof, which the door checks).</summary>
public sealed record SupervisorCourse(
    string Session, string Point, string Generation, string Do, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder);

/// <summary>A course decision as memory keeps it: one per referred point, held for the window, then gone on or closed.</summary>
internal sealed class SupervisorCourseDecision
{
    public required string Id { get; init; }
    public required string Session { get; init; }
    public required string Repository { get; init; }
    public required string Point { get; init; }
    public required string Count { get; init; }
    public required string Do { get; init; }
    public required string Generation { get; init; }
    public required DateTimeOffset GoesOnAt { get; init; }
    public SupervisorState State { get; set; } = SupervisorState.Held;
}

/// <summary>
/// The supervisor's course decisions (CONTEXTDRIVE1j; the context counters design §4.6 point 3, §5): a point a person's <c>refer</c> rule holds
/// a line at, decided by the holder the admission admits for the line's repository. A <c>go-on</c> is held for the role's window, then
/// released between turns through the count's own door, the rules read again first; a <c>hold</c> leaves the point to the person at once.
/// Each is kept in the <see cref="SupervisorBook"/> under the session's lock, and written beside the questions' decisions with no
/// <c>question</c>, so the questions' reader and the start's sweep pass it by. A restart empties memory: a held go-on is never released,
/// and the line waits for the person.
/// </summary>
internal sealed class SupervisorCourses(SupervisorBook book, IDecisionAdmission admission, MachineLog? log)
{
    /// <summary>Keep <paramref name="course"/> on <paramref name="point"/>, or refuse it keeping nothing.</summary>
    public Task<SupervisorKept> KeepAsync(SupervisorCourse course, ReferredPoint point, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(course.Session, () =>
        {
            var refused = Judge(course, point);
            if (refused is null && book.Courses.Values.FirstOrDefault(each => each.Session == course.Session && each.Point == course.Point) is { } decided)
            {
                refused = SupervisorWhy.Answered;
                if (decided.Do == course.Do && decided.Generation == course.Generation && decided.State is SupervisorState.Held or SupervisorState.Left)
                {
                    return Task.FromResult(new SupervisorKept(true, decided.Id, decided.State == SupervisorState.Held ? decided.GoesOnAt : null, null));
                }
            }

            // One point is asked once: a question decision of the holder still moving on the line answers for it first.
            if (refused is null && book.Live.Values.Any(each => each.Session == course.Session && !SupervisorStates.Final(each.State))) refused = SupervisorWhy.Answered;
            var admitted = admission.Admit(point.Repository);
            refused ??= admitted.Why ?? (admitted.Generation != course.Generation ? SupervisorWhy.RoleChanged : null);
            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            var id = Guid.NewGuid().ToString("N")[..12];
            var left = course.Do == SupervisorCourseActs.Hold;
            var goesOnAt = left ? now : now + QuestionSupervisorDecisions.WindowOf(admitted);
            var decision = new SupervisorCourseDecision
            {
                Id = id, Session = course.Session, Repository = point.Repository, Point = course.Point, Count = point.Open!.Rule.Count, Do = course.Do, Generation = course.Generation,
                GoesOnAt = goesOnAt, State = left ? SupervisorState.Left : SupervisorState.Held,
            };
            Directory.CreateDirectory(book.Files.FolderOf(course.Session));
            AtomicFile.WriteText(book.Files.FileOf(course.Session, id), Json(id, course, point, now, goesOnAt));
            Append(decision, decision.State, now, why: null);
            book.Courses[id] = decision;
            log?.Info(left ? "supervisor.left" : "supervisor.course", ("session", course.Session), ("point", course.Point), ("do", course.Do));
            return Task.FromResult(new SupervisorKept(true, id, left ? null : goesOnAt, null));
        }, ct);

    /// <summary>
    /// Each held go-on whose moment came, released under its session's lock: the admission asked again, then the point and the rules read
    /// again (<paramref name="line"/>), a line running a turn left held until it ends, and only then the count's door. Returns the states
    /// the moved decisions reached.
    /// </summary>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(CourseLine line, DateTimeOffset now, IReadOnlyCollection<string>? counts = null, CancellationToken ct = default)
    {
        var moved = new List<SupervisorState>();
        // A point is released by the look that reads what holds it: answers alone's before the plan, a quest's with the plan's verdicts.
        foreach (var due in book.Courses.Values
                     .Where(each => each.State == SupervisorState.Held && each.GoesOnAt <= now && (counts is null || counts.Contains(each.Count))).ToList())
        {
            var reached = await book.UnderAsync(due.Session, () =>
            {
                if (due.State != SupervisorState.Held) return Task.FromResult<SupervisorState?>(null);
                var admitted = admission.Admit(due.Repository);
                var why = admitted.Why is null && admitted.Generation == due.Generation ? line.Why(due.Session, due.Point) : SupervisorWhy.RoleChanged;
                if (why is null && line.Running(due.Session)) return Task.FromResult<SupervisorState?>(null);
                if (why is null && !line.GoOn(due.Session)) why = SupervisorWhy.Answered;
                var to = why is null ? SupervisorState.WentOn : SupervisorState.Stale;
                Append(due, to, now, why);
                if (why is null) log?.Info("supervisor.went-on", ("session", due.Session), ("point", due.Point));
                else log?.Info("supervisor.stale", ("session", due.Session), ("why", why));
                return Task.FromResult<SupervisorState?>(to);
            }, ct).ConfigureAwait(false);
            if (reached is { } state) moved.Add(state);
        }

        return moved;
    }

    /// <summary>The owner's <i>Overrule</i> of a held go-on: the point stays theirs. Null where <paramref name="decision"/> is no course decision of the session.</summary>
    public Task<SupervisorState?> OverruleAsync(string session, string decision, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, () =>
        {
            if (!book.Courses.TryGetValue(decision, out var held) || held.Session != session) return Task.FromResult<SupervisorState?>(null);
            if (held.State == SupervisorState.Held) Append(held, SupervisorState.Overruled, now, why: null);
            return Task.FromResult<SupervisorState?>(held.State);
        }, ct);

    /// <summary>Whether <paramref name="point"/> waits for the holder: no course decision on it, and no question decision moving on the line.</summary>
    public bool Waits(string session, string point) =>
        !book.Courses.Values.Any(each => each.Session == session && each.Point == point)
        && !book.Live.Values.Any(each => each.Session == session && !SupervisorStates.Final(each.State));

    /// <summary>The soonest moment a held go-on is due, across every session.</summary>
    public DateTimeOffset? NextGoesOn() =>
        book.Courses.Values.Where(each => each.State == SupervisorState.Held).Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    // The act's form and its point: an act of the course's vocabulary, its reasons as an answer's are judged, and the point the one open.
    private static string? Judge(SupervisorCourse course, ReferredPoint point) =>
        SupervisorKeeping.Refused(course.Reasons, SupervisorCourseActs.All.Contains(course.Do, StringComparer.Ordinal), point.Counts)
        ?? (point.Open is not { } open || open.Id != course.Point ? SupervisorWhy.NotReferred : null);

    private void Append(SupervisorCourseDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        decision.State = to;
        SupervisorDecisionFiles.TryAppend(book.Files.StatesOf(decision.Session, decision.Id), SupervisorDecisionFiles.StateLine(to, at, why, null));
    }

    // The decision's file: its holder, the point and what the rule acted on, the act, the reasons and counts, and when it goes on.
    private static string Json(string id, SupervisorCourse course, ReferredPoint point, DateTimeOffset now, DateTimeOffset goesOnAt) => new JsonObject
    {
        ["id"] = id,
        ["session"] = course.Session,
        ["holder"] = new JsonObject { ["kind"] = course.Holder.Kind, ["parent"] = course.Holder.Parent },
        ["generation"] = course.Generation,
        ["course"] = new JsonObject
        {
            ["point"] = course.Point, ["count"] = point.Open!.Rule.Count, ["n"] = point.Open.N, ["rule"] = point.Open.Rule.Id,
            ["place"] = point.Open.Rule.Place, ["do"] = course.Do,
        },
        ["bound"] = course.Do == SupervisorCourseActs.Hold ? SupervisorCourseActs.Bound : null,
        ["reasons"] = new JsonArray([.. course.Reasons.Select(reason => (JsonNode)new JsonObject { ["says"] = reason.Says, ["source"] = reason.Source })]),
        ["counts"] = point.Counts is { } counts ? JsonSerializer.SerializeToNode(counts) : null,
        ["keptAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["goesOnAt"] = goesOnAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
