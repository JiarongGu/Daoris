using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.SupervisorDoorActs;

namespace Daoris.Driver;

/// <summary>
/// A supervisor's answer at a door that waits on the person until a place appoints a role to it: a session's staged memory kept or
/// dropped (<c>memory.keep</c>), a memory end approved or declined (<c>memory.decide</c>), a quest's departure accepted
/// (<c>departure.accept</c>); or the subject left to the person (<see cref="SupervisorDoorActs.Hold"/>). Its subject, the repository
/// whose supervisor answers it, the hold's generation, the act and its yes or no, the reasons and the holder; and the pin of what the
/// holder judged (<see cref="SupervisorDoorSubjects.PinAsync"/>), which the release reads again before it posts.
/// </summary>
public sealed record SupervisorDoorAnswer(
    SupervisorSubject Subject, string Repository, string Generation, string Do, string? Choice, IReadOnlyList<SupervisorReason> Reasons,
    SupervisorHolder Holder)
{
    public string? Pin { get; init; }
}

/// <summary>
/// A held answer whose moment came, as the release hands it to the service's door, with the role and the agent it is the answer of, the
/// pin of what it judged and the record of the agent's turn that held the role (none for a terminal's).
/// </summary>
public sealed record SupervisorDoorDue(string Id, SupervisorSubject Subject, string Repository, string Do, string? Choice, string Role, string Agent)
{
    public string? Pin { get; init; }

    public string? Turn { get; init; }

    /// <summary>Whether the answer is a yes: a keep, an approve, an accept.</summary>
    public bool Yes => Choice is null or Keep or Approve or Approved;
}

/// <summary>
/// The release's three reads of the service, as landing's <see cref="LandingRelease"/> is: why the answer may not go on now (what it judged
/// changed since: null where it stands), its post to the door (null where the door kept it, else why not), and, for an answer a restart
/// cut off after its release, whether the service shows it applied (null where it cannot be read now).
/// </summary>
public sealed record DoorRelease(
    Func<SupervisorDoorDue, CancellationToken, Task<string?>> Why,
    Func<SupervisorDoorDue, CancellationToken, Task<string?>> Post,
    Func<SupervisorDoorDue, CancellationToken, Task<bool?>> Made);

/// <summary>The door answers' acts and choices, as the kinds' rows, the release and the agent's block name them.</summary>
public static class SupervisorDoorActs
{
    public const string Keep = "keep", Decide = "decide", Drop = "drop", Approve = "approve", Decline = "decline", Accept = "accept", Hold = "hold";

    /// <summary>A go-ahead's yes and no, as the service's door takes them.</summary>
    public const string Approved = "approved", Refused = "refused";

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

/// <summary>
/// A session's answer at a door as the page reads it, to offer <i>Overrule</i> while it is held: its kind, its subject's id (<c>staged</c>, an
/// end's operation, a quest), the act and its choice, the role and the agent it is the answer of, where it stands and when it goes on.
/// </summary>
public sealed record SupervisorDoorView(
    string Id, string Kind, string Subject, string Do, string? Choice, string Role, string Agent, SupervisorState State,
    DateTimeOffset KeptAt, DateTimeOffset GoesOnAt);

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
    public string? Pin { get; init; }
    public string? Turn { get; init; }
    public required DateTimeOffset KeptAt { get; init; }
    public required DateTimeOffset GoesOnAt { get; init; }
    public SupervisorState State { get; set; } = SupervisorState.Held;

    public SupervisorDoorDue Due() => new(Id, Subject, Repository, Do, Choice, Role, Agent) { Pin = Pin, Turn = Turn };
}

/// <summary>
/// The supervisor's answers at the person's doors a role may answer at low and medium risk: kept for the role's window with
/// <i>Overrule</i>, written to the session's decisions with the role and the agent that gave it, then released in one step that asks the
/// admission again, reads what it judged again, and posts the answer to the service's own door as the role's, never the person's. A
/// refusal there, or a subject changed since, goes stale; the person's overrule inside the window closes the subject to them. A
/// <c>released</c> line comes before the post, so an answer a restart cut off after it is settled by what the service shows, never
/// guessed; the start's sweep closes every other file.
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
            var admitted = admission.Admit(subject, new RolePlace(Repository: answer.Repository), left ? null : answer.Do);
            refused ??= admitted.Why ?? (admitted.Generation != answer.Generation ? SupervisorWhy.RoleChanged : null);
            refused ??= left || Offered(subject.Kind, answer.Do, answer.Choice) ? null : SupervisorWhy.NotOffered;
            refused ??= SupervisorKeeping.Refused(answer.Reasons, true, null);
            refused ??= left || answer.Pin is not null ? null : SupervisorWhy.Answered;
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
                Role = admitted.Role ?? AppointedRoles.Supervisor, Agent = AgentOf(answer.Holder), Pin = answer.Pin, Turn = answer.Holder.Agent?.Session,
                KeptAt = now, GoesOnAt = goesOnAt, State = left ? SupervisorState.Left : SupervisorState.Held,
            };
            Directory.CreateDirectory(book.Files.FolderOf(subject.Session));
            var text = FileText(decision, answer, now);
            AtomicFile.WriteText(book.Files.FileOf(subject.Session, id), text);
            Append(decision, decision.State, now, why: null);
            book.Window(decision.State, decision.State, () => new HeldDecisionRow(
                id, subject.Session, subject.Kind, answer.Repository, answer.Generation, goesOnAt, text), id);
            book.Doors[id] = decision;
            if (left) book.Closed[subject] = SupervisorState.Left;
            log?.Info(left ? "supervisor.left" : "supervisor.door", ("session", subject.Session), ("kind", subject.Kind), ("do", answer.Do));
            return Task.FromResult(new SupervisorKept(true, id, left ? null : goesOnAt, null));
        }, ct);

    /// <summary>
    /// Each held answer whose moment came, released under its session's lock: the admission asked again and what it judged read again,
    /// then <c>released</c> written and the answer posted to its door as the role's. Kept there it is <c>went-on</c>; refused or changed,
    /// <c>stale</c> with the why. A <c>released</c> answer with no end, which a restart cut off, is <c>went-on</c> where the service shows
    /// it applied and <c>stale</c> (<see cref="SupervisorWhy.Restarted"/>) where it does not. Returns the states the moved answers reached.
    /// </summary>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(DoorRelease release, DateTimeOffset now, CancellationToken ct = default)
    {
        var moved = new List<SupervisorState>();
        foreach (var due in book.Doors.Values.Where(each => each.State == SupervisorState.Released || (each.State == SupervisorState.Held && each.GoesOnAt <= now)).ToList())
        {
            var reached = await book.UnderAsync(due.Subject.Session, async () =>
            {
                if (due.State == SupervisorState.Released)
                {
                    if (await release.Made(due.Due(), ct).ConfigureAwait(false) is not { } made) return (SupervisorState?)null;
                    return Settle(due, made ? SupervisorState.WentOn : SupervisorState.Stale, now, made ? null : SupervisorWhy.Restarted);
                }

                if (due.State != SupervisorState.Held) return null;
                var admitted = admission.Admit(due.Subject, new RolePlace(Repository: due.Repository), due.Do);
                var why = admitted.Why is null && admitted.Generation == due.Generation
                    ? OwnersAgain(due) ?? await release.Why(due.Due(), ct).ConfigureAwait(false)
                    : SupervisorWhy.RoleChanged;
                if (why is null)
                {
                    Append(due, SupervisorState.Released, now, why: null);
                    why = await release.Post(due.Due(), ct).ConfigureAwait(false);
                }

                return Settle(due, why is null ? SupervisorState.WentOn : SupervisorState.Stale, now, why);
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

    /// <summary>A session's answers this run, oldest first; a restart's sweep has closed every earlier one but a released one, so none of them is held.</summary>
    public IReadOnlyList<SupervisorDoorView> DoorsOf(string session) =>
    [
        .. book.Doors.Values.Where(each => each.Subject.Session == session)
            .OrderBy(each => each.KeptAt).ThenBy(each => each.Id, StringComparer.Ordinal)
            .Select(each => new SupervisorDoorView(
                each.Id, each.Subject.Kind, each.Subject.Id, each.Do, each.Choice, each.Role, each.Agent, each.State, each.KeptAt, each.GoesOnAt)),
    ];

    /// <summary>Whether <paramref name="subject"/> waits for the holder: no answer on it, and it is not closed to the person.</summary>
    public bool Waits(SupervisorSubject subject) => !book.Doors.Values.Any(each => each.Subject == subject) && !book.Closed.ContainsKey(subject);

    /// <summary>The soonest moment a held answer is due; now where a released one waits to be settled.</summary>
    public DateTimeOffset? NextGoesOn() =>
        book.Doors.Values.Where(each => each.State is SupervisorState.Held or SupervisorState.Released)
            .Select(each => (DateTimeOffset?)(each.State == SupervisorState.Released ? each.KeptAt : each.GoesOnAt)).Min();

    /// <summary>
    /// A decision file of these kinds read whole, its state the one its lines reached, for the start's sweep: a released one is kept again
    /// for the release to settle. Null for another kind's or a file that does not read.
    /// </summary>
    public static SupervisorDoorDecision? Recover(SupervisorDecisionFiles files, string session, string id)
    {
        try
        {
            if (SupervisorDecisionFiles.Read(files.FileOf(session, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var kind = Json.Text(root, "kind");
            if (kind is not (SupervisorKinds.Memory or SupervisorKinds.Departure or SupervisorKinds.GoAhead)
                || !root.TryGetProperty("subject", out var wire) || SupervisorSubject.Read(kind, wire) is not { } subject)
            {
                return null;
            }

            var last = (SupervisorDecisionFiles.Read(files.StatesOf(session, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var state = last is null ? null : SupervisorStates.Parse(Json.Text(JsonDocument.Parse(last).RootElement, "state"));
            return new SupervisorDoorDecision
            {
                Id = id, Subject = subject, Repository = Json.Text(root, "repository") ?? "", Do = Json.Text(root, "do") ?? "", Choice = Json.Text(root, "choice"),
                Generation = Json.Text(root, "generation") ?? "", Role = Json.Text(root, "role") ?? "", Agent = Json.Text(root, "agent") ?? "",
                Pin = Json.Text(root, "pin"), Turn = Json.Text(root, "turn"), KeptAt = Moment(root, "keptAt"), GoesOnAt = Moment(root, "goesOnAt"),
                State = state ?? SupervisorState.Held,
            };
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A memory act offers its yes and no; a go-ahead's answer its approved and refused; a departure's accept offers none.
    private static bool Offered(string kind, string act, string? choice) => kind switch
    {
        SupervisorKinds.Memory => choice is not null && Choices(act).Contains(choice, StringComparer.Ordinal),
        SupervisorKinds.GoAhead => act == SupervisorKinds.Answer && choice is Approved or Refused,
        _ => act == Accept && choice is null,
    };

    /// <summary>The agent an answer is the holder's: an agent's harness, model and effort, or the holder's kind (a started terminal).</summary>
    public static string AgentOf(SupervisorHolder holder) =>
        holder.Agent is { } agent ? string.Join(" · ", new[] { agent.Agent, agent.Model, agent.Effort }.Where(each => each is { Length: > 0 })) : holder.Kind;

    // The risk policy may change inside the window, so a go-ahead is judged again at the release, by the kind and target its pin names; one
    // whose pin does not read is no longer what the role judged.
    private string? OwnersAgain(SupervisorDoorDecision due) =>
        due.Subject.Kind != SupervisorKinds.GoAhead ? null
        : GoAheadPin.Read(due.Pin) is var (kind, on) ? admission.GoAheadOwners(new RolePlace(Repository: due.Repository), kind, on, due.Role)
        : SupervisorWhy.SubjectChanged;

    private SupervisorState Settle(SupervisorDoorDecision due, SupervisorState to, DateTimeOffset now, string? why)
    {
        Append(due, to, now, to == SupervisorState.Stale ? why : null);
        if (to == SupervisorState.Stale) book.Closed[due.Subject] = to;
        log?.Info($"supervisor.{SupervisorStates.Spell(to)}", ("session", due.Subject.Session), ("kind", due.Subject.Kind), ("why", why ?? ""));
        return to;
    }

    private void Append(SupervisorDoorDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        if (decision.State == SupervisorState.Held && to != SupervisorState.Held) book.Held.Drop(decision.Id);
        decision.State = to;
        SupervisorDecisionFiles.TryAppend(book.Files.StatesOf(decision.Subject.Session, decision.Id), SupervisorDecisionFiles.StateLine(to, at, why, null));
    }

    private static DateTimeOffset Moment(JsonElement root, string name) =>
        Json.Text(root, name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : DateTimeOffset.MinValue;

    // The decision's file: the subject, the role and the agent that answered, the holder, the act and its choice, the pin of what it
    // judged and the agent's turn, the reasons, and when it goes on.
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
        ["pin"] = decision.Pin,
        ["turn"] = decision.Turn,
        ["reasons"] = new JsonArray([.. answer.Reasons.Select(reason => (JsonNode)new JsonObject { ["says"] = reason.Says, ["source"] = reason.Source })]),
        ["keptAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["goesOnAt"] = decision.GoesOnAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
