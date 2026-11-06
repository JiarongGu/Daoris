using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The publisher over the routes the driver has and the marks it really declares (D169 point 5): a declared press refused as an error and a
/// read publish nothing, and the next declared press that answers publishes once, so the silence was the publisher's and not a route that
/// never ran. <c>StatePublisherTests</c> holds the same rule over stand-in route names.
/// </summary>
public sealed class StatePublisherRealRoutesTests : DriverModuleBridge
{
    private const string Refused = "STOP_SESSION";

    private const string Read = "PLUGINS";

    private const string Pressed = "SESSION_ARCHIVE";

    /// <summary>The driver's module name: one declared press that refuses before it acts, a read, and a declared press that answers.</summary>
    private sealed class Driving : ModuleBase
    {
        public override string ModuleName => DriverModule.Name;

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken) =>
            request.Type == Refused
                ? throw new DriverException("refused before anything moved.")
                : Task.FromResult<object?>(new { Done = true });
    }

    private Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string type, object? payload = null) =>
        dispatcher.DispatchAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = DriverModule.Name,
                Type = type,
                Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload),
            },
            CancellationToken.None);

    [Fact]
    public async Task A_real_press_refused_and_a_real_read_publish_nothing_where_a_real_press_answered_does()
    {
        Assert.All([Refused, Read, Pressed], route => Assert.Contains(route, DriverModule.Routes));
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(new Driving());
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(StatePublisher.Middleware(Bus)));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();

        var refused = await Dispatch(dispatcher, Refused, new { id = "s1" });
        var read = await Dispatch(dispatcher, Read);
        await Task.Delay(50);
        Assert.False(refused.Success);
        Assert.True(read.Success);
        Assert.DoesNotContain(Raised, message => message.Type == StatePublisher.Type);

        var pressed = await Dispatch(dispatcher, Pressed);

        Assert.True(pressed.Success);
        await UntilAsync(() => Raised.Count(message => message.Type == StatePublisher.Type) == 1);
        var changed = Assert.Single(Raised, message => message.Type == StatePublisher.Type);
        Assert.Equal("""{"scopes":["sessions"]}""", JsonSerializer.Serialize(changed.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
