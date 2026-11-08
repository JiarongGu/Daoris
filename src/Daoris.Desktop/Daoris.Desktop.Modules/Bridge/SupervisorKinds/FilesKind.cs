using System.Collections.Concurrent;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The files kind: an act of Daoris's in a tree that an <c>ask</c> in the person's permissions held at a start, its subject
/// <c>{ repository, start, what, text }</c>. It is decided where its act reaches, under <c>files.allow-run</c>, <c>files.allow-copy</c> or
/// <c>files.allow-share</c>: a run or a copy at its repository, a share at the narrowest place covering both repositories. An allow takes
/// effect at its keeping: the held act's own text is written into the person's own layer of each repository the act reaches, never the
/// machine's and never the <c>ask</c>'s pattern, with the role that gave it and the decision it was kept under. An ask naming the person
/// is listed for no supervisor; a leave keeps it for the person.
/// </summary>
internal sealed class FilesKind : ISupervisorKind
{
    private static readonly ConcurrentDictionary<string, byte> Left = new(StringComparer.Ordinal);

    public string Kind => SupervisorKinds.Files;

    public Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        foreach (var ask in FileAsks.Read(loop.Home).Where(each => each.Decision is null && !each.Person))
        {
            if (Left.ContainsKey(Key(loop, ask)) || SupervisorDecisionDoor.Covered(admission, Kind, PlaceOf(loop, ask)) is not { } covered) continue;
            var act = ActOf(ask);
            if (!covered.Acts.Contains(act)) continue;
            waiting.Add(new
            {
                Kind, Subject = SupervisorSubject.Ask(ask.Repository, ask.Start, ask.What, ask.Text).Wire(), Repository = ask.Repository,
                What = ask.What, Text = ask.Text, covered.Generation, Acts = (IReadOnlyList<string>)[act],
            });
        }

        return Task.FromResult<IReadOnlyList<object>>(waiting);
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct)
    {
        if (Waiting(loop, form.Subject) is not { } ask) return Done(SupervisorDecided.Refused(SupervisorWhy.NotHeld));
        if (ask.Decision is not null) return Done(SupervisorDecided.Refused(SupervisorWhy.Answered));
        if (ask.Person) return Done(SupervisorDecided.Refused(SupervisorWhy.Reserved));
        if (form.Do != ActOf(ask)) return Done(SupervisorDecided.Refused(SupervisorWhy.NotOffered));
        if (loop.Pipe?.StandingConfirmation is not { } judge) return Done(SupervisorDecided.Refused(SupervisorWhy.NotStaffed));

        var admitted = judge.Admit(Kind, PlaceOf(loop, ask), form.Do);
        if (admitted.Why is { } why) return Done(SupervisorDecided.Refused(why));
        if (admitted.Generation != form.Generation) return Done(SupervisorDecided.Refused(SupervisorWhy.RoleChanged));
        if (!FileAllows.StillHeld(loop.Home, ask)) return Done(SupervisorDecided.Refused(SupervisorWhy.Answered));

        var decision = Guid.NewGuid().ToString("N")[..12];
        var role = admitted.Role ?? "supervisor";
        if (FileAsks.Decide(loop.Home, ask.Repository, ask.Start, ask.What, ask.Text, role, decision) is not { } decided)
        {
            return Done(SupervisorDecided.Refused(SupervisorWhy.Answered));
        }

        int given;
        try
        {
            given = FileAllows.Give(loop.Home, decided, role, decision);
        }
        catch (DriverException)
        {
            // The person's file does not read, or no longer takes a rule here: nothing was written, and the ask waits for the person.
            FileAsks.Settle(loop.Home, decision, made: false);
            return Done(SupervisorDecided.Refused(SupervisorWhy.Reserved));
        }

        FileAsks.Settle(loop.Home, decision, made: given > 0);
        loop.Log?.Info("supervisor.files-allowed", ("repository", ask.Repository), ("what", ask.What), ("role", role), ("decision", decision));
        return Done(given > 0 ? new SupervisorDecided(true, decision, null, null) : SupervisorDecided.Refused(SupervisorWhy.Answered));
    }

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct)
    {
        if (Waiting(loop, form.Subject) is not { Decision: null } ask) return Done(SupervisorDecided.Refused(SupervisorWhy.NotHeld));
        Left[Key(loop, ask)] = 0;
        return Done(new SupervisorDecided(true, null, null, null));
    }

    public Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        Task.FromResult(Waiting(loop, subject) is { } ask ? PlaceOf(loop, ask) : (RolePlace?)null);

    // The grant a kind's act is made under: allow-run, allow-copy or allow-share.
    private static string ActOf(FileAsk ask) => $"allow-{ask.What}";

    // The place whose supervisor answers: the repository's, and for a share the narrowest scope covering both repositories.
    private static RolePlace PlaceOf(DriverLoop loop, FileAsk ask)
    {
        var judge = loop.Pipe?.StandingConfirmation;
        var places = ask.Repositories.Select(repository => judge?.Placed(repository) ?? new RolePlace(Repository: repository)).ToList();
        if (places.Count == 1) return places[0];
        return places[0].Workspace is { } workspace && places.All(place => place.Workspace == workspace) ? new RolePlace(Workspace: workspace) : new RolePlace();
    }

    // The ask the subject names, as it stands: same act, same start.
    private static FileAsk? Waiting(DriverLoop loop, SupervisorSubject subject) =>
        subject.Act is { } met
            ? FileAsks.Read(loop.Home).FirstOrDefault(each => each.Is(subject.Session, met.What, subject.Id) && each.Start == met.Start)
            : null;

    private static string Key(DriverLoop loop, FileAsk ask) => $"{loop.Home}\n{ask.Repository}\n{ask.Start}\n{ask.What}\n{ask.Text}";

    private static Task<SupervisorDecided> Done(SupervisorDecided decided) => Task.FromResult(decided);
}
