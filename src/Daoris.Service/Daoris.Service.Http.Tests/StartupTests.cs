using Daoris.Knowledge;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The startup judgement, reached through the host's real entry point (D47 §3): local trust bound beyond
/// loopback does not warn, it does not start — judged on the addresses the host would actually bind,
/// Kestrel's own endpoint configuration included (REV3). The in-memory server replaces Kestrel, so the
/// one host here that does start binds nothing.
/// </summary>
public sealed class StartupTests
{
    /// <summary>Runs <paramref name="start"/> with the process's standard error captured, which is where a refusal is said.</summary>
    private static (Exception? Failed, string Said) Capturing(Action start)
    {
        var said = new StringWriter();
        var before = Console.Error;
        Console.SetError(said);
        try
        {
            return (Record.Exception(start), said.ToString());
        }
        finally
        {
            Console.SetError(before);
        }
    }

    [Theory]
    [InlineData("urls", "http://0.0.0.0:5199")]
    [InlineData("urls", "http://localhost:5199;http://192.168.1.20:5199")]
    [InlineData("Kestrel:Endpoints:Lan:Url", "http://192.168.1.20:5177")]
    public void A_local_host_asked_to_bind_beyond_loopback_refuses_to_start(string setting, string value)
    {
        var (failed, said) = Capturing(() =>
            new DaorisHost(ServiceMode.Local, new Dictionary<string, string?> { [setting] = value }).Dispose());

        // The entry point returned before it built a host: that is the refusal, as the factory sees it.
        Assert.NotNull(failed);
        Assert.Contains("Refusing to bind beyond loopback in local mode: http://", said);
        Assert.Contains("set DAORIS_MODE=shared and mint keys to serve a network", said);
    }

    /// <summary>The refusal is local mode's: the same addresses start a shared host, whose every route is gated.</summary>
    [Fact]
    public async Task A_shared_host_may_be_asked_to_bind_beyond_loopback()
    {
        using var host = new DaorisHost(ServiceMode.Shared, new Dictionary<string, string?> { ["urls"] = "http://0.0.0.0:5199" });
        var key = (await host.Composed.Keys.MintAsync("anyone@a-machine", TimeSpan.FromDays(1), DateTimeOffset.UtcNow)).Key;

        // Asked for every interface, and bound none: the server answering is the in-memory one.
        Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());
        Assert.Equal(401, (await host.GetAsync("/api/status")).Status);
        Assert.Equal(200, (await host.GetAsync("/api/status", key: key)).Status);
    }

    /// <summary>And a local host on the loopback starts, which is what makes the refusals above a judgement.</summary>
    [Fact]
    public async Task A_local_host_on_the_loopback_starts()
    {
        using var host = new DaorisHost(ServiceMode.Local, new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:5199;http://localhost:5198" });

        Assert.Equal(200, (await host.GetAsync("/api/status")).Status);
    }
}
