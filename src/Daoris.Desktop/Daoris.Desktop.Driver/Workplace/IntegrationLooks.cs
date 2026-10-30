namespace Daoris.Driver;

/// <summary>A request as the Integration view and a session's head say it: its unit, its branch, its frozen tip and where it stands.</summary>
public sealed record IntegrationLookRequest(string Request, string Line, string Branch, string Tip, DateTimeOffset Since)
{
    public string? Unit { get; init; }

    /// <summary>The words a member was handed back or held with; null where none were said.</summary>
    public string? Said { get; init; }
}

/// <summary>A member of the standing candidate and what the steps have made of it.</summary>
public sealed record IntegrationLookMember(string Request, string Branch, string State)
{
    public string? Said { get; init; }
}

/// <summary>One step of the standing candidate, as its last attempt ended.</summary>
public sealed record IntegrationLookStep(string Step, string State);

/// <summary>
/// A line's standing candidate: its members, a fixed strip of steps (§5.8), and what holds it with the act that releases it, who may press it.
/// </summary>
public sealed record IntegrationLookCandidate(string Candidate, string Line, string Base, DateTimeOffset Since)
{
    public IReadOnlyList<IntegrationLookMember> Members { get; init; } = [];

    public IReadOnlyList<IntegrationLookStep> Steps { get; init; } = [];

    /// <summary>The step it is at; null once nothing is open or ended yet.</summary>
    public string? At { get; init; }

    /// <summary>What holds it: <c>conflict</c> (a member held for its conflict) or <c>promotion</c> (the line could not take it now); null where nothing does.</summary>
    public string? Holds { get; init; }

    /// <summary>The act that releases what holds it (<c>decide</c>); null where nothing does.</summary>
    public string? Release { get; init; }

    /// <summary>Who may press it: <c>owner</c>, <c>steward</c> (§8); null where nothing does.</summary>
    public string? By { get; init; }
}

/// <summary>A superseded commit awaiting its disposition (§5.7).</summary>
public sealed record IntegrationLookSuperseded(string Request, string Commit, string Subject, string By, int Files);

/// <summary>A repository's integration as its view draws it: the queue, the standing candidates, what went back, what awaits a disposition, and the promoted history.</summary>
public sealed record IntegrationLook(string Repository)
{
    public IReadOnlyList<IntegrationLookRequest> Queue { get; init; } = [];

    public IReadOnlyList<IntegrationLookCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<IntegrationLookRequest> HandedBack { get; init; } = [];

    public IReadOnlyList<IntegrationLookSuperseded> Superseded { get; init; } = [];

    public IReadOnlyList<IntegrationLookRequest> Promoted { get; init; } = [];
}

/// <summary>The one line a session's head says of its unit's integration, from the same record (§5.8).</summary>
/// <param name="State">One of <see cref="IntegrationHeads"/>.</param>
public sealed record IntegrationHead(string State, string Line)
{
    public string? Step { get; init; }
}

/// <summary>The states a unit's integration head says.</summary>
public static class IntegrationHeads
{
    public const string Queued = "queued";
    public const string InCandidate = "in-candidate";
    public const string Held = "held";
    public const string HandedBack = "handed-back";
    public const string Settled = "settled";
    public const string Dropped = "dropped";
}

/// <summary>Reads a repository's integration for its view and a unit's head from the integration record, never copying it (§5.6, §5.8).</summary>
public static class IntegrationLooks
{
    private const int History = 20;

    /// <summary>What <paramref name="repository"/>'s view draws now.</summary>
    public static IntegrationLook Of(IntegrationStore store, string repository)
    {
        var open = store.Open().Where(request => Is(request, repository)).ToList();
        var lines = open.Select(request => request.Line).Distinct(StringComparer.Ordinal).ToList();
        var candidates = store.Standing().Where(candidate => string.Equals(candidate.Repository, repository, StringComparison.OrdinalIgnoreCase)).ToList();
        lines.AddRange(candidates.Select(candidate => candidate.Line).Where(line => !lines.Contains(line)));

        var queue = lines.SelectMany(line => store.Queued(repository, line)).Select(request => Row(request)).ToList();
        var closed = store.Closed(repository, History * 3);
        return new IntegrationLook(repository)
        {
            Queue = queue,
            Candidates = [.. candidates.Select(candidate => Candidate(store, candidate))],
            HandedBack = [.. closed.Where(request => request.ClosedWhy == IntegrationRequestCloses.HandedBack).Take(History).Select(request => Row(request, store.MemberOf(request.Id)?.Said))],
            Promoted = [.. closed.Where(request => request.ClosedWhy == IntegrationRequestCloses.Settled).Take(History).Select(request => Row(request))],
            Superseded =
            [
                .. store.Pending().Where(each => store.Request(each.Request) is { } request && Is(request, repository))
                    .Select(each => new IntegrationLookSuperseded(each.Request, each.Commit, each.Subject, each.By, each.Files.Count)),
            ],
        };
    }

    /// <summary>The head of <paramref name="unit"/>'s newest request; null where the unit never requested an integration.</summary>
    public static IntegrationHead? HeadOf(IntegrationStore store, string unit)
    {
        if (store.OfUnit(unit) is not { } request) return null;
        if (request.ClosedAt is not null)
        {
            return new IntegrationHead(request.ClosedWhy switch
            {
                IntegrationRequestCloses.Settled => IntegrationHeads.Settled,
                IntegrationRequestCloses.HandedBack => IntegrationHeads.HandedBack,
                _ => IntegrationHeads.Dropped,
            }, request.Line);
        }

        if (store.MemberOf(request.Id) is not { Stands: true } member) return new IntegrationHead(IntegrationHeads.Queued, request.Line);
        if (member.State == IntegrationMemberStates.Held) return new IntegrationHead(IntegrationHeads.Held, request.Line);
        return new IntegrationHead(IntegrationHeads.InCandidate, request.Line) { Step = AtOf(store, member.Candidate) };
    }

    private static IntegrationLookCandidate Candidate(IntegrationStore store, IntegrationCandidate candidate)
    {
        var members = store.Members(candidate.Id);
        var steps = store.Steps(candidate.Id);
        var held = members.Any(member => member.Stands && member.State == IntegrationMemberStates.Held);
        var stuck = !held && steps.LastOrDefault() is { Step: IntegrationStepNames.Promote, Outcome: IntegrationStepOutcomes.Held };
        return new IntegrationLookCandidate(candidate.Id, candidate.Line, candidate.Base, candidate.FormedAt)
        {
            Members =
            [
                .. members.Select(member => new IntegrationLookMember(member.Request, store.Request(member.Request)?.Branch ?? "", member.State) { Said = member.Said }),
            ],
            Steps =
            [
                .. IntegrationStepNames.All.Select(name => new IntegrationLookStep(name, steps.LastOrDefault(step => step.Step == name) is { } last ? last.Outcome ?? "open" : "waiting")),
            ],
            At = AtOf(store, candidate.Id),
            Holds = held ? "conflict" : stuck ? "promotion" : null,
            Release = held ? "decide" : null,
            By = held ? "owner" : null,
        };
    }

    private static string? AtOf(IntegrationStore store, string candidate) =>
        store.OpenStep(candidate)?.Step ?? store.Steps(candidate).LastOrDefault()?.Step;

    private static IntegrationLookRequest Row(IntegrationRequest request, string? said = null) =>
        new(request.Id, request.Line, request.Branch, request.Tip, request.ClosedAt ?? request.At) { Unit = request.Unit, Said = said };

    private static bool Is(IntegrationRequest request, string repository) =>
        string.Equals(request.Repository, repository, StringComparison.OrdinalIgnoreCase);
}
