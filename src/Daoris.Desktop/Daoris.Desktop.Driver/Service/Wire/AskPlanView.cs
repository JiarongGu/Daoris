using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>An ask's plan as the service answers it, for the page.</summary>
public static class AskPlanView
{
    /// <summary>The plans the ask holds, none where it has none or the host is from before the door.</summary>
    /// <exception cref="DriverException">The host answered something other than the plans.</exception>
    public static async Task<AskPlanResponse> AskPlanAsync(this ServiceClient service, string ask, CancellationToken ct = default)
    {
        var id = ask.TrimStart('#');
        var sent = await service.SendAsync(new ReadAskPlan(id), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return new(id, []);

        return sent.Answer ?? throw new DriverException($"the service would not read ask `#{id}`'s plan: {sent.Reply.Payload}");
    }
}
