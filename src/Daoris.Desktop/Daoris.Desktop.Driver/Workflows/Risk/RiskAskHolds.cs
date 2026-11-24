namespace Daoris.Driver;

/// <summary>
/// The holds that stand outside a run's step, as the judge's shadow reads them: an open go-ahead on an ask, a parked question
/// its agent marked as the owner's, and a permission request held under asking prompts. Each is an act of the census at the asking
/// session's place: a go-ahead by its kind and target (a push <c>remote.push</c>; a release, a write or a run <c>release.start</c>, qualified
/// by the destination its target names), a marked question by its mark, a held permission <c>reach.widen</c>. A sign-in is a person in a
/// browser, a dependency and no act of the policy, so neither a sign-in go-ahead nor a sign-in mark is a hold here.
/// </summary>
public static class RiskAskHolds
{
    public const string GoAheadDomain = "goahead";
    public const string QuestionDomain = "question";
    public const string PermissionDomain = "permission";

    public const string GoAheadReleased = "released: answered, or no session that asked it is live";
    public const string QuestionReleased = "released: answered, or its park ended";
    public const string PermissionReleased = "released: answered, refused at its bound, or its turn ended";

    /// <summary>
    /// The act a go-ahead of <paramref name="kind"/> on <paramref name="on"/> is: a push <c>remote.push</c>; a release, a write or a run
    /// <c>release.start</c>, qualified by its target where that is a destination's name (one word, as the service reads an environment), else
    /// unqualified; none for a sign-in or a kind this build does not know.
    /// </summary>
    public static RiskAct? GoAheadAct(string kind, string on) => kind switch
    {
        GoAheadAuthority.Push => new RiskAct(RiskActs.RemotePush, null),
        GoAheadAuthority.Release or "write" or "run" => new RiskAct(RiskActs.ReleaseStart, Destination(on)),
        _ => null,
    };

    /// <summary>The judge's outcome on the go-ahead of <paramref name="kind"/> on <paramref name="on"/> at <paramref name="place"/>; null where its kind names no act.</summary>
    public static string? GoAheadOutcome(RiskPolicy policy, RiskPlace place, string kind, string on) =>
        GoAheadAct(kind, on) is { } act ? RiskGate.Judge(policy, act, "", place, "").Outcome : null;

    /// <summary>Why the go-ahead is the owner role's at <paramref name="place"/> by the policy <paramref name="config"/> holds (<see cref="GoAheadAuthority.OwnersOwn"/>).</summary>
    public static string? GoAheadOwners(DriverConfig config, RolePlace place, string kind, string on) =>
        GoAheadAuthority.OwnersOwn(kind, GoAheadOutcome(RiskPolicy.Of(config), RiskPlace.Of(config, place.Workspace, place.Repository), kind, on));

    /// <summary>The act a question marked <paramref name="mark"/> asks the owner for; none for a sign-in, no mark or one this build does not know.</summary>
    public static RiskAct? MarkAct(string? mark) => mark switch
    {
        OwnerClass.Direction => new RiskAct(RiskActs.DirectionSet, null),
        OwnerClass.Money => new RiskAct(RiskActs.SpendMoney, null),
        OwnerClass.Release => new RiskAct(RiskActs.ReleaseStart, null),
        OwnerClass.History => new RiskAct(RiskActs.HistoryRewrite, null),
        OwnerClass.Data => new RiskAct(RiskActs.DataDelete, null),
        OwnerClass.Publish => new RiskAct(RiskActs.PublishOutside, null),
        _ => null,
    };

    /// <summary>
    /// Each go-ahead of <paramref name="asks"/> that waits for an answer and has a live asker: at the newest live asker's repository, the
    /// ask's workspace, and today's holder as <see cref="GoAheadAuthority"/> reads it.
    /// </summary>
    public static IReadOnlyList<RiskHold> GoAheads(
        IReadOnlyList<AskView> asks, IReadOnlyCollection<PriorSession> records, DriverConfig config, Func<string, string?> workspaceOf)
    {
        var live = Live(records);
        var holds = new List<RiskHold>();
        foreach (var ask in asks)
        {
            foreach (var goAhead in (ask.GoAheads ?? []).Where(each => each.Answer is null))
            {
                if (GoAheadAct(goAhead.Kind, goAhead.On) is not { } act) continue;
                var asker = goAhead.Asked.Reverse().Select(request => live.GetValueOrDefault(request.Session)).FirstOrDefault(each => each?.Repository is not null);
                if (asker is null) continue;
                var workspace = ask.Workspace is { Length: > 0 } named ? named : workspaceOf(asker.Repository!);
                var owners = GoAheadOwners(config, new RolePlace(workspace, asker.Repository), goAhead.Kind, goAhead.On);
                var today = owners is { } code
                    ? $"Waiting on the owner role (`{code}`)."
                    : $"Waiting on you, or a role granted `{GoAheadAuthority.Act}` at its place.";
                holds.Add(new RiskHold(
                    $"{GoAheadDomain}:{ask.Id.TrimStart('#')}/{goAhead.Number}", "go-ahead", act, Actor(asker.Session),
                    RiskPlace.Of(config, workspace, asker.Repository), $"ask {ask.Id}, go-ahead {goAhead.Number}: {goAhead.Named}", today, TodayHeld: true));
            }
        }

        return holds;
    }

    /// <summary>Each question of <paramref name="questions"/> a live session's agent marked as the owner's, by an act of the census, at its session's place.</summary>
    public static IReadOnlyList<RiskHold> Questions(
        IReadOnlyList<ParkQuestion> questions, IReadOnlyCollection<PriorSession> records, DriverConfig config, Func<string, string?> workspaceOf)
    {
        var live = Live(records);
        return [.. questions
            .Where(question => !question.Conversation)
            .Select(question => (question, act: MarkAct(question.Owner), record: live.GetValueOrDefault(question.Session)))
            .Where(each => each.act is not null && each.record?.Repository is not null)
            .Select(each => new RiskHold(
                $"{QuestionDomain}:{each.question.Session}/{each.question.Id}", "question", each.act!, Actor(each.question.Session),
                RiskPlace.Of(config, workspaceOf(each.record!.Repository!), each.record.Repository), $"session {each.question.Session}, question {each.question.Id}",
                $"Waiting on the owner role: its agent marked it `{each.question.Owner}` (`{LinePasses.OwnerClass}`).", TodayHeld: true))];
    }

    /// <summary>Each permission request <paramref name="held"/> for the person: <c>reach.widen</c> at its live session's place.</summary>
    public static IReadOnlyList<RiskHold> Permissions(
        IReadOnlyList<HeldPermission> held, IReadOnlyCollection<PriorSession> records, DriverConfig config, Func<string, string?> workspaceOf)
    {
        var live = Live(records);
        return [.. held
            .Select(request => (request, record: live.GetValueOrDefault(request.Session)))
            .Where(each => each.record?.Repository is not null)
            .Select(each => new RiskHold(
                $"{PermissionDomain}:{each.request.Session}/{each.request.Request}", "permission", new RiskAct(RiskActs.ReachWiden, null),
                Actor(each.request.Session), RiskPlace.Of(config, workspaceOf(each.record!.Repository!), each.record.Repository),
                $"session {each.request.Session}, request {each.request.Request}" + (each.request.Kind is { } kind ? $" ({kind})" : ""),
                "Held for you under asking permission prompts.", TodayHeld: true))];
    }

    /// <summary>
    /// A hold point of <paramref name="domain"/> journaled in shadow: the holds <paramref name="read"/> gives, under the policy as it stands.
    /// A read that fails journals nothing and fails nothing: the shadow decides nothing.
    /// </summary>
    public static void Shadow(RiskShadow shadow, string domain, DriverConfig config, Func<IReadOnlyList<RiskHold>> read, Func<string, string> released)
    {
        IReadOnlyList<RiskHold> holds;
        RiskPolicy policy;
        try
        {
            policy = RiskPolicy.Of(config);
            holds = read();
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            return;
        }

        shadow.Look(domain, policy, holds, released);
    }

    private static string Actor(string session) => $"session:{session}";

    private static Dictionary<string, PriorSession> Live(IReadOnlyCollection<PriorSession> records)
    {
        var live = new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        foreach (var record in records.Where(record => !SessionStates.IsEnded(record.State))) live[record.Session] = record;
        return live;
    }

    // A destination's name as a qualifier: one word of letters, digits, dots, hyphens and underscores, lower-cased; else none.
    private static string? Destination(string on)
    {
        var word = on.Trim().ToLowerInvariant();
        return word.Length is > 0 and <= 40 && word.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.' or '_') ? word : null;
    }
}
