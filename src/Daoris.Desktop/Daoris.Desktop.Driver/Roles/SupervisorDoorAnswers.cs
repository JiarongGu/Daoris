using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.SupervisorDoorActs;

namespace Daoris.Driver;

/// <summary>
/// A supervisor's answer at a door that waits on the person until a place appoints a role to it: a session's staged memory kept or
/// dropped (<c>memory.keep</c>), a memory end approved or declined (<c>memory.decide</c>), a quest's departure accepted
/// (<c>departure.accept</c>); or the subject left to the person (<see cref="SupervisorDoorActs.Hold"/>). Its subject, the repository
/// whose supervisor answers it, the hold's generation, the act and its yes or no, the reasons and the holder.
/// </summary>
public sealed record SupervisorDoorAnswer(
    SupervisorSubject Subject, string Repository, string Generation, string Do, string? Choice, IReadOnlyList<SupervisorReason> Reasons,
    SupervisorHolder Holder);

/// <summary>A held answer whose moment came, as the release hands it to the service's door, with the role and the agent it is the answer of.</summary>
public sealed record SupervisorDoorDue(string Id, SupervisorSubject Subject, string Repository, string Do, string? Choice, string Role, string Agent)
{
    /// <summary>Whether the answer is a yes: a keep, an approve, an accept.</summary>
    public bool Yes => Choice is null or Keep or Approve;
}

/// <summary>The door answers' acts and choices, as the kinds' rows, the release and the agent's block name them.</summary>
public static class SupervisorDoorActs
{
    public const string Keep = "keep", Decide = "decide", Drop = "drop", Approve = "approve", Decline = "decline", Accept = "accept", Hold = "hold";

    /// <summary>The memory subject's id naming a session's staged changes, beside an end's operation id.</summary>
    public const string Staged = "staged";

    /// <summary>The yes and the no a memory act offers: a keep or a drop of staged changes, an approve or a decline of an end.</summary>
    public static IReadOnlyList<string> Choices(string act) => act switch
    {
        Keep => [Keep, Drop],
        Decide => [Approve, Decline],
        _ => [],
    };
}

/// <summary>The service's door for a released answer: null where it kept it, else why it refused, keeping nothing.</summary>
public delegate Task<string?> SupervisorDoorPost(SupervisorDoorDue due, CancellationToken ct);

/// <summary>An answer as memory keeps it: one per subject, held for the role's window, then posted to its door, or overruled.</summary>
internal sealed class SupervisorDoorDecision
{
    public required string Id { get; init; }
    public required SupervisorSubject Subject { get; init; }
    public required string Repository { get; init; }
    public required string Do { get; init; }
    public string? Choice { get; init; }
    public required string Generation { get; init; }
    public required string Role { get; init; }
    public required string Agent { get; init; }
    public required DateTimeOffset GoesOnAt { get; init; }
    public SupervisorState State { get; set; } = SupervisorState.Held;
}

/// <summary>
/// The supervisor's answers at the person's doors a role may answer at low and medium risk: kept for the role's window with
/// <i>Overrule</i>, written to the session's decisions with the role and the agent that gave it, then released in one step that asks the
/// admission again and posts the answer to the service's own door as the role's, never the person's. A refusal there goes stale; the
/// person's overrule inside the window closes the subject to them. A restart empties memory, and the start's sweep closes the files.
/// </summary>
internal sealed class SupervisorDoorAnswers(SupervisorBook book, IDecisionAdmission admission, MachineLog? log)
{
    /// <summary>Keep <paramref name="answer"/>, or refuse it keeping nothing; <paramref name="refused"/> is what the subject itself refuses.</summary>
    public Task<SupervisorKept> KeepAsync(SupervisorDoorAnswer answer, string? refused, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(answer.Subject.Session, () =>
        {
            var subject = answer.Subject;
            if (book.Doors.Values.FirstOrDefault(each => each.Subject == subject) is { } decided)
            {
                if (refused is null && decided.Do == answer.Do && decided.Choice == answer.Choice && decided.Generation == answer.Generation
                    && decided.State is SupervisorState.Held or SupervisorState.Left)
                {
                    return Task.FromResult(new SupervisorKept(true, decided.Id, decided.State == SupervisorState.Held ? decided.GoesOnAt : null, null));
                }

                refused ??= SupervisorStates.OwnersAgain(decided.State) ? SupervisorWhy.Overruled : SupervisorWhy.Answered;
            }

            refused ??= book.ClosedWhy(subject);
            var left = answer.Do == Hold;
            var admitted = admission.Admit(subject.Kind, new RolePlace(Repository: answer.Repository), left ? null : answer.Do);
            refused ??= admitted.Why ?? (admitted.Generation != answer.Generation ? SupervisorWhy.RoleChanged : null);
            refused ??= left || Offered(subject.Kind, answer.Do, answer.Choice) ? null : SupervisorWhy.NotOffered;
            refused ??= SupervisorKeeping.Refused(answer.Reasons, true, null);
            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            var id = Guid.NewGuid().ToString("N")[..12];
            var goesOnAt = left ? now : now + QuestionSupervisorDecisions.WindowOf(admitted);
            var decision = new SupervisorDoorDecision
            {
                Id = id, Subject = subject, Repository = answer.Repository, Do = answer.Do, Choice = answer.Choice, Generation = answer.Generation,
                Role = admitted.Role ?? AppointedRoles.Supervisor, Agent = AgentOf(answer.Holder), GoesOnAt = goesOnAt,
                State = left ? SupervisorState.Left : SupervisorState.Held,
            };
            Directory.CreateDirectory(book.Files.FolderOf(subject.Session));
            AtomicFile.WriteText(book.Files.FileOf(subject.Session, id), FileText(decision, answer, now));
            Append(decision, decision.State, now, why: null);
            book.Doors[id] = decision;
            if (left) book.Closed[subject] = SupervisorState.Left;
            log?.Info(left ? "supervisor.left" : "supervisor.door", ("session", subject.Session), ("kind", subject.Kind), ("do", answer.Do));
            return Task.FromResult(new SupervisorKept(true, id, left ? null : goesOnAt, null));
        }, ct);

    /// <summary>
    /// Each held answer whose moment came, released under its session's lock: the admission asked again, then the answer posted to its door
    /// as the role's. Kept there it is <c>went-on</c>; refused, <c>stale</c> with the door's why. Returns the states the moved answers reached.
    /// </summary>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(SupervisorDoorPost post, DateTimeOffset now, CancellationToken ct = default)
    {
        var moved = new List<SupervisorState>();
        foreach (var due in book.Doors.Values.Where(each => each.State == SupervisorState.Held && each.GoesOnAt <= now).ToList())
        {
            var reached = await book.UnderAsync(due.Subject.Session, async () =>
            {
                if (due.State != SupervisorState.Held) return (SupervisorState?)null;
                var admitted = admission.Admit(due.Subject.Kind, new RolePlace(Repository: due.Repository), due.Do);
                var why = admitted.Why is null && admitted.Generation == due.Generation
                    ? await post(new SupervisorDoorDue(due.Id, due.Subject, due.Repository, due.Do, due.Choice, due.Role, due.Agent), ct).ConfigureAwait(false)
                    : SupervisorWhy.RoleChanged;
                var to = why is null ? SupervisorState.WentOn : SupervisorState.Stale;
                Append(due, to, now, why);
                if (to == SupervisorState.Stale) book.Closed[due.Subject] = to;
                log?.Info($"supervisor.{SupervisorStates.Spell(to)}", ("session", due.Subject.Session), ("kind", due.Subject.Kind));
                return to;
            }, ct).ConfigureAwait(false);
            if (reached is { } state) moved.Add(state);
        }

        return moved;
    }

    /// <summary>The owner's <i>Overrule</i> of a held answer: nothing is posted, and the subject is theirs. Null where none of the session has the id.</summary>
    public Task<SupervisorState?> OverruleAsync(string session, string decision, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, () =>
        {
            if (!book.Doors.TryGetValue(decision, out var held) || held.Subject.Session != session) return Task.FromResult<SupervisorState?>(null);
            if (held.State == SupervisorState.Held)
            {
                Append(held, SupervisorState.Overruled, now, why: null);
                book.Closed[held.Subject] = SupervisorState.Overruled;
            }

            return Task.FromResult<SupervisorState?>(held.State);
        }, ct);

    /// <summary>Whether <paramref name="subject"/> waits for the holder: no answer on it, and it is not closed to the person.</summary>
    public bool Waits(SupervisorSubject subject) => !book.Doors.Values.Any(each => each.Subject == subject) && !book.Closed.ContainsKey(subject);

    /// <summary>The soonest moment a held answer is due.</summary>
    public DateTimeOffset? NextGoesOn() =>
        book.Doors.Values.Where(each => each.State == SupervisorState.Held).Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    /// <summary>A decision file of these kinds: its subject and the state its lines reached, for the start's sweep; null for another kind's.</summary>
    public static (SupervisorSubject Subject, SupervisorState State)? Read(SupervisorDecisionFiles files, string session, string id)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(files.FileOf(session, id)));
            var root = document.RootElement;
            var kind = Json.Text(root, "kind");
            if (kind is not (SupervisorKinds.Memory or SupervisorKinds.Departure)
                || !root.TryGetProperty("subject", out var wire) || SupervisorSubject.Read(kind, wire) is not { } subject)
            {
                return null;
            }

            var last = File.Exists(files.StatesOf(session, id)) ? File.ReadLines(files.StatesOf(session, id)).LastOrDefault(line => line.Length > 0) : null;
            var state = last is null ? null : SupervisorStates.Parse(Json.Text(JsonDocument.Parse(last).RootElement, "state"));
            return (subject, state ?? SupervisorState.Held);
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A memory act offers its yes and no; a departure's accept offers none.
    private static bool Offered(string kind, string act, string? choice) =>
        kind == SupervisorKinds.Memory ? choice is not null && Choices(act).Contains(choice, StringComparer.Ordinal) : act == Accept && choice is null;

    /// <summary>The agent an answer is the holder's: an agent's harness, model and effort, or the holder's kind (a started terminal).</summary>
    public static string AgentOf(SupervisorHolder holder) =>
        holder.Agent is { } agent ? string.Join(" · ", new[] { agent.Agent, agent.Model, agent.Effort }.Where(each => each is { Length: > 0 })) : holder.Kind;

    private void Append(SupervisorDoorDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        decision.State = to;
        SupervisorDecisionFiles.TryAppend(book.Files.StatesOf(decision.Subject.Session, decision.Id), SupervisorDecisionFiles.StateLine(to, at, why, null));
    }

    // The decision's file: the subject, the role and the agent that answered, the holder, the act and its choice, the reasons, and when it goes on.
    private static string FileText(SupervisorDoorDecision decision, SupervisorDoorAnswer answer, DateTimeOffset now) => new JsonObject
    {
        ["id"] = decision.Id,
        ["kind"] = decision.Subject.Kind,
        ["subject"] = new JsonObject([.. decision.Subject.Wire().Select(each => KeyValuePair.Create(each.Key, (JsonNode?)each.Value))]),
        ["session"] = decision.Subject.Session,
        ["role"] = decision.Role,
        ["agent"] = decision.Agent,
        ["holder"] = SupervisorHolders.Node(answer.Holder),
        ["generation"] = decision.Generation,
        ["repository"] = decision.Repository,
        ["do"] = decision.Do,
        ["choice"] = decision.Choice,
        ["reasons"] = new JsonArray([.. answer.Reasons.Select(reason => (JsonNode)new JsonObject { ["says"] = reason.Says, ["source"] = reason.Source })]),
        ["keptAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["goesOnAt"] = decision.GoesOnAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
