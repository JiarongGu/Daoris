using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>sessions handback &lt;session|quest&gt;</c>: one quest's handback whole, by the quest's id or the id of the session that served
/// it, in any state, as the service's handback door answers it, in the words the connector's <c>handback_read</c> says.
/// </summary>
internal static class SessionsHandback
{
    /// <summary>
    /// <c>--json</c> prints the answer as the service gave it. Exit 0 where the id named a quest or a session, 1 where it named
    /// nothing here, 2 where the service could not answer or has no door.
    /// </summary>
    internal static async Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        Daoris.Contracts.Quests.HandbackResponse? answer;
        try
        {
            answer = await world.Service.HandbackAsync(ask.Ids[0], ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 2;
        }

        if (answer is null)
        {
            output.WriteLine("sessions: the service has no handback door; is it older than this driver?");
            return 2;
        }

        output.WriteLine(ask.Json
            ? JsonSerializer.Serialize(answer, Daoris.Contracts.ContractJson.Options.GetTypeInfo(typeof(Daoris.Contracts.Quests.HandbackResponse)))
            : answer.Said);
        return answer.Found ? 0 : 1;
    }
}
