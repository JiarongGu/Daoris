using System.Net;
using System.Net.Sockets;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// An ask naming no session is the terminal's, so an agent that leaves its session off is one asker with both of the
/// terminal's places. The host reads the asking process from the connection (the loopback port's owner), and one process holds one
/// place of the terminal's; a connection whose process cannot be read keeps the terminal's two.
/// </summary>
public sealed class NamelessAskTests
{
    private const string Key = KeyedHost.Key;

    private static string AskOf(int n) => $$"""{"workspace":"default","sentence":"Ask number {{n}}."}""";

    private static Task<Answer> AskAsync(DaorisHost host, int n, int? port) =>
        host.SendAsync("POST", "/api/confirmations", DaorisHost.Loopback,
            json: System.Text.Json.JsonSerializer.Serialize(new { method = "POST", path = "/api/asks", body = AskOf(n) }), port: port);

    [Fact]
    public async Task A_nameless_ask_holds_one_place_for_its_process_and_a_process_that_cannot_be_read_keeps_the_terminals_two()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var host = new DaorisHost(ServiceMode.Local, seed: LocalHost.Seed, input: Key + "\n");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var port = ((IPEndPoint)client.Client.LocalEndPoint!).Port;

        Assert.Equal(200, (await AskAsync(host, 1, port)).Status);
        var second = await AskAsync(host, 2, port);
        Assert.Equal(403, second.Status);
        Assert.Equal(PersonDoors.AskerFull, second.Json.GetProperty("code").GetString());

        // Without a readable process the nameless asks are one asker, bounded as before.
        Assert.Equal(200, (await AskAsync(host, 3, null)).Status);
        Assert.Equal(200, (await AskAsync(host, 4, null)).Status);
        Assert.Equal(PersonDoors.AskerFull, (await AskAsync(host, 5, null)).Json.GetProperty("code").GetString());
    }
}
