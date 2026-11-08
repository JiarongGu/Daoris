using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A lone surrogate escape is no text: <c>JsonElement.GetString()</c> throws on it, on .NET 10 too. A line holding one in its id or
/// key is no request and closes its connection; one in a string or name of its payload is refused under its id, the connection kept.
/// </summary>
public sealed class PipeLoneSurrogateTests : Bridge
{
    private string HomeId => HomeIdentity.Read(Home)!;

    private static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    private async Task<(InstancePipe Pipe, PipeClient Client, HeldModule Driver, MachineLog Log, ServiceProvider Services)> ServeAsync()
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(driver);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test") { Grounds = new StubGrounds() };
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);
        return (pipe, client, driver, log, provider);
    }

    [Theory]
    [InlineData("""{"id":"lone-1\ud800","module":"DAORIS.DRIVER","type":"TOOLS_GIT","payload":{"way":"system"},"key":"0123456789abcdef0123"}""")]
    [InlineData("""{"id":"lone-2","module":"DAORIS.DRIVER","type":"TOOLS_GIT","payload":{"way":"system"},"key":"0123456789abcdef\ud800"}""")]
    public async Task A_line_holding_a_lone_surrogate_escape_in_its_id_or_key_is_no_request_and_closes_its_connection(string line)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, log, services) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        await client.SendLineAsync(line);

        Assert.Null(await client.ReadAsync());
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    [Theory]
    [InlineData("""{"id":"lone-3","module":"DAORIS.DRIVER","type":"TOOLS_GIT","payload":{"way":"file","file":"a\udc00b"},"key":"0123456789abcdef0123"}""")]
    [InlineData("""{"id":"lone-3","module":"DAORIS.DRIVER","type":"TOOLS_GIT","payload":{"way":"system","note":["x",{"y\ud800":1}]},"key":"0123456789abcdef0123"}""")]
    public async Task A_lone_surrogate_escape_in_a_payload_is_refused_under_the_request_s_id_and_the_connection_stays(string line)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, log, services) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        await client.SendLineAsync(line);

        Assert.Equal(InstancePipe.Shape, Code(await client.AnswerAsync("lone-3")));
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);

        var system = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "TOOLS_GIT", new { way = "system" }));
        Assert.True(system.GetProperty("success").GetBoolean(), system.GetRawText());
    }
}
