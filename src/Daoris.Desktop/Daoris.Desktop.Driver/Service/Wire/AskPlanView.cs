using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>An ask's plan as the service answers it, for the page, and the person's two acts on its delivery.</summary>
public static class AskPlanView
{
    /// <summary>
    /// The plans the ask holds, none where it has none or the host is from before the door. <paramref name="seen"/> is the blob each
    /// evidence place (<c>repository:file</c>) holds on this machine, so the service names an order whose evidence moved stale.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the plans.</exception>
    public static async Task<AskPlanResponse> AskPlanAsync(
        this ServiceClient service, string ask, CancellationToken ct = default, IReadOnlyDictionary<string, string>? seen = null)
    {
        var id = ask.TrimStart('#');
        var sent = await service.SendAsync(new ReadAskPlan(id, seen is { Count: > 0 } ? seen : null), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return new(id, [], [], []);

        return sent.Answer ?? throw new DriverException($"the service would not read ask `#{id}`'s plan: {sent.Reply.Payload}");
    }

    /// <summary>The person's answer to one open question of the ask's delivery plan; the service's sentence where it refuses.</summary>
    /// <exception cref="DriverException">The service refused, in its own words.</exception>
    public static Task<AskPlanResponse> AnswerPlanQuestionAsync(this ServiceClient service, string ask, string key, string words, CancellationToken ct = default) =>
        SettleAsync(service, new SettleAskPlan(ask.TrimStart('#'), Key: key, Words: words), ct);

    /// <summary>The person's waiver of one release order of the ask's plan (each deployable as <c>repository/id</c>), with their reason.</summary>
    /// <exception cref="DriverException">The service refused, in its own words.</exception>
    public static Task<AskPlanResponse> WaiveReleaseAsync(
        this ServiceClient service, string ask, string deployable, string on, string reason, CancellationToken ct = default) =>
        SettleAsync(service, new SettleAskPlan(ask.TrimStart('#'), Deployable: deployable, On: on, Reason: reason), ct);

    private static async Task<AskPlanResponse> SettleAsync(ServiceClient service, SettleAskPlan route, CancellationToken ct)
    {
        var sent = await service.SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is { } settled) return settled;
        throw new DriverException(QuestViews.Error(sent)
            ?? $"the service at {service.BaseUrl} has no door for the plan's answers ({sent.Reply.Status}) — is it older than this driver?");
    }
}
