using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>One part of the standing revision as the train reads it: the parts it builds on and those that only land before it.</summary>
/// <param name="After">Parts that must have landed first (a build dependency).</param>
/// <param name="Orders">Parts that land before it when both are ready (an order, never a wait).</param>
public sealed record TrainPlanPart(string Id, IReadOnlySet<string> After, IReadOnlySet<string> Orders)
{
    /// <summary>The quest the plan published for the part; null where it has none yet.</summary>
    public string? Quest { get; init; }
    public string? Repository { get; init; }
}

/// <summary>
/// The ask's own plan at its newest revision, as the train reads it: every part it names is required, so a part a revision dropped is no
/// longer named and is not waited for.
/// </summary>
public sealed record TrainPlan(string Ask, int Revision, IReadOnlyList<TrainPlanPart> Parts)
{
    /// <summary>The ask's own plan out of the service's answer; null where the ask has none yet.</summary>
    public static TrainPlan? Of(AskPlanResponse answer)
    {
        if (answer.Plans.FirstOrDefault(plan => plan.Scope is null) is not { } own) return null;
        var parts = own.Parts.Select(part => new TrainPlanPart(
            part.Id,
            Named(own.Edges, part.Id, "after"),
            Named(own.Edges, part.Id, "orders")) { Quest = part.Quest, Repository = part.Repository }).ToList();
        return new TrainPlan(answer.Ask, own.Revision, parts);
    }

    private static HashSet<string> Named(IReadOnlyList<AskPlanEdgeResponse> edges, string part, string kind) =>
        [.. edges.Where(edge => edge.Part == part && edge.Kind == kind).Select(edge => edge.On)];

    /// <summary>Whether every part of the revision has landed: none is left to wait for.</summary>
    public bool Delivered(IReadOnlySet<string> landed) => Parts.All(part => landed.Contains(part.Id));
}

/// <summary>No plan read: every part waits.</summary>
internal sealed class NoPlans : ITrainPlans
{
    public static NoPlans Read { get; } = new();

    public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => Task.FromResult<TrainPlan?>(null);
}

/// <summary>Where the train reads an ask's plan: the service's door, or a test's.</summary>
public interface ITrainPlans
{
    /// <summary>The ask's own plan, or null where it is not read (no plan, or the service did not answer): its parts then wait, never guessed.</summary>
    Task<TrainPlan?> OfAsync(string ask, CancellationToken ct);
}
