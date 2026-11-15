using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The end of a workflow's run at the words ticks' stand-in service. An agent's done in a workflow's run ends its turn and not its session,
/// which waits on the run's next step, its tree still locked; the run's end moves it to <c>completed</c> with the run's note, as the service's
/// run-end door does through the ordinary move, its step's wait gone. A case whose session the person writes to once its quest closed ends
/// the run first.
/// </summary>
internal static class StandInRunEnds
{
    /// <summary>
    /// A first look whose agent's done ends its turn waiting on its run's next step, that run's end, and then the watch's tidy letting go of
    /// the ended record's tree lock, as it does for a record no session running or waiting holds; a look alone lets go of none ended so.
    /// </summary>
    public static async Task DoneRunAsync(this SessionMessagesTickTests.SaidStandIn service, Daoris.Driver.Driver driver)
    {
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        await service.EndRunAsync();
        var tree = service.TreeOf("s1");
        Assert.True(await SessionLocks.LetGoAsync(tree, "s1", CancellationToken.None), $"s1 held no lock on `{tree}`");
    }

    /// <summary>The tree the record says its session ran in.</summary>
    public static string TreeOf(this SessionMessagesTickTests.SaidStandIn service, string session) =>
        JsonNode.Parse(service.Record(session))!["tree"]!.GetValue<string>();

    /// <summary>The run's end on <paramref name="session"/>, which must be waiting on its run's next step.</summary>
    public static async Task EndRunAsync(this SessionMessagesTickTests.SaidStandIn service, string session = "s1")
    {
        Assert.True(service.State(session) == "awaiting-step", service.Record(session));
        using var http = new HttpClient();
        var end = new JsonObject { ["state"] = "completed", ["note"] = "Its workflow's run `q1` came to its end, and the session with it." };
        using var response = await http.PostAsync(
            $"{service.Url}/api/sessions/{session}/state", new StringContent(end.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
