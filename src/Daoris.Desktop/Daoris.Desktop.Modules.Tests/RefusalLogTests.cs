using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Every refusal the bridge answers, in the machine log by its code and the request it answered (LOG1b,
/// D94) — never by its sentence, whose parameters can name a folder, a quest or a machine path.
/// </summary>
/// <remarks>
/// Composed the way the shell composes its dispatcher: the kit's own <c>UseMessageDispatcher</c>, the
/// middleware in its application slot, and the modules registered as the shell registers them. So what
/// is asserted is the pipeline every module's answer actually travels, not a module called by hand.
/// </remarks>
public sealed class RefusalLogTests : Bridge
{
    private sealed class Windows : ISecondaryWindows
    {
        public bool Open(string name, string address) => true;

        public IReadOnlyList<string> Opened => [];

        public bool SetTheme(string name, bool dark) => true;
    }

    /// <summary>A module whose request the page gave up on: a cancelled request is no refusal.</summary>
    private sealed class Cancelled : ModuleBase
    {
        public override string ModuleName => "TEST.CANCELLED";

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }

    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<ISecondaryWindows>(new Windows());
        services.AddSingleton(new PlatformAddress("http://localhost:5177"));
        services.AddIpcModule<WindowsModule>();
        services.AddIpcModule<Cancelled>();
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    private static Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string module, string type, object? payload = null) =>
        dispatcher.DispatchAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = module,
                Type = type,
                Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload),
            },
            CancellationToken.None);

    private List<JsonElement> Lines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }).Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    [Fact]
    public async Task A_module_s_refusal_is_written_by_its_code_and_request_and_never_its_words()
    {
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, services) = Compose(log);
        using var _ = services;

        var answer = await Dispatch(dispatcher, "DAORIS.WINDOWS", "OPEN", new { name = "session:../../private-folder" });

        Assert.False(answer.Success);
        Assert.Equal(Refusals.WindowUnknown, answer.Error?.Code);
        var line = Assert.Single(Lines());
        Assert.Equal("refused", line.GetProperty("event").GetString());
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal(
            """{"code":"WINDOW_UNKNOWN","request":"DAORIS.WINDOWS.OPEN"}""",
            line.GetProperty("data").GetRawText());
    }

    /// <summary>
    /// The kit's own codes are the page asking for what this host cannot do (a type no module has, a
    /// module the shell does not carry), which is a defect rather than a decision, so they are warnings.
    /// </summary>
    [Fact]
    public async Task A_code_the_kit_answered_is_written_as_a_warning()
    {
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, services) = Compose(log);
        using var _ = services;

        await Dispatch(dispatcher, "DAORIS.WINDOWS", "CLOSE");
        await Dispatch(dispatcher, "DAORIS.NOWHERE", "STATE");

        var lines = Lines();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal("warn", line.GetProperty("level").GetString()));
        Assert.Equal(
            """{"code":"NO_ROUTE","request":"DAORIS.WINDOWS.CLOSE"}""",
            lines[0].GetProperty("data").GetRawText());
        Assert.Equal(
            """{"code":"NO_HANDLER","request":"DAORIS.NOWHERE.STATE"}""",
            lines[1].GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task An_answer_or_a_cancelled_request_writes_nothing()
    {
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, services) = Compose(log);
        using var _ = services;

        var opened = await Dispatch(dispatcher, "DAORIS.WINDOWS", "OPEN", new { name = "monitor" });
        var cancelled = await Dispatch(dispatcher, "TEST.CANCELLED", "ANY");

        Assert.True(opened.Success);
        Assert.Equal(IpcErrorCodes.OperationCancelled, cancelled.Error?.Code);
        Assert.Empty(Lines());
    }
}
