using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>The person's two acts on an ask's delivery plan, sent to the service: an answer to a question, and a waiver of a release order.</summary>
public static class AskPlanSettleView
{
    /// <summary>The person's answer to one open question of the ask's delivery plan.</summary>
    /// <exception cref="DriverException">The service refused, in its own words.</exception>
    public static Task<AskPlanResponse> AnswerPlanQuestionAsync(this ServiceClient service, string ask, string key, string words, CancellationToken ct = default) =>
        SettleAsync(service, new SettleAskPlan(ask.TrimStart('#'), Key: key, Words: words), ct);

    /// <summary>The person's waiver of one release order of the ask's plan, each deployable as <c>repository/id</c>.</summary>
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
