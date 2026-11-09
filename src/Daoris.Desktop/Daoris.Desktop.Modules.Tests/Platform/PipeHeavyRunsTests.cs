using System.IO.Pipes;
using System.Text;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A terminal's heavy run over the pipe (<c>RUN_HEAVY</c>): run in this process, its gate writing to the handle the terminal named
/// and the run answered with its exit code; and held to the connection that asked it, so <c>RUN_STOP</c> or the connection's end
/// stops it, everything it started ended.
/// </summary>
/// <remarks>
/// The terminal is this test's own process, so the handle it names is a pipe it reads; each test serves its own pipe in this
/// process, named by its own scratch home's id. The pipe is Windows': elsewhere a test returns at once.
/// </remarks>
public sealed class PipeHeavyRunsTests : Bridge
{
    [Fact]
    public async Task A_run_writes_to_the_terminals_own_output_and_answers_its_exit_code()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        using var output = new AnonymousPipeServerStream(PipeDirection.In);
        var said = Read(output);

        var answer = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, HeavyTerminalRun.Type, Payload(output, "cmd", "/c", "echo", "ran-for-the-terminal")));
        output.DisposeLocalCopyOfClientHandle();

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(0, answer.GetProperty("data").GetProperty("exit").GetInt32());
        var text = await said;
        Assert.Contains("ran-for-the-terminal", text);
        Assert.Contains("run-heavy: 1 heavy gate passed", text);
    }

    [Fact]
    public async Task Its_connection_ending_stops_the_run_and_ends_what_it_started()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        using var output = new AnonymousPipeServerStream(PipeDirection.In);
        var said = new StringBuilder();
        var reading = Read(output, said);

        await client.SendAsync(InstancePipe.Module, HeavyTerminalRun.Type, Payload(output, "ping", "-n", "60", "127.0.0.1"));
        await UntilAsync(() => Text(said).Contains("run-heavy: `command`: ping"));
        await client.DisposeAsync();

        await UntilAsync(() => Text(said).Contains("run-heavy: stopped while `command` ran; everything it started was ended before the slot was let go."));
        output.DisposeLocalCopyOfClientHandle();
        await reading;
    }

    [Fact]
    public async Task Its_stop_from_the_same_connection_ends_the_run_which_answers_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        using var output = new AnonymousPipeServerStream(PipeDirection.In);
        var said = new StringBuilder();
        var reading = Read(output, said);

        var run = await client.SendAsync(InstancePipe.Module, HeavyTerminalRun.Type, Payload(output, "ping", "-n", "60", "127.0.0.1"));
        await UntilAsync(() => Text(said).Contains("run-heavy: `command`: ping"));
        var stop = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, HeavyTerminalRun.StopType, new { id = run }));
        var answer = await client.AnswerAsync(run);
        output.DisposeLocalCopyOfClientHandle();
        await reading;

        Assert.True(stop.GetProperty("success").GetBoolean(), stop.GetRawText());
        Assert.Equal(1, answer.GetProperty("data").GetProperty("exit").GetInt32());
        Assert.Contains("run-heavy: stopped while `command` ran", Text(said));
    }

    /// <summary>A run of <paramref name="command"/> as a heavy run's own command, from the scratch home, writing to <paramref name="output"/>.</summary>
    private object Payload(AnonymousPipeServerStream output, params string[] command)
    {
        var handle = (long)output.ClientSafePipeHandle.DangerousGetHandle();
        return new
        {
            words = new[] { "--" }.Concat(command).ToArray(),
            directory = Home,
            environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .Where(entry => !((string)entry.Key).StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? ""),
            handles = new { input = 0L, output = handle, error = handle },
        };
    }

    private static Task<string> Read(AnonymousPipeServerStream output) =>
        Task.Run(async () =>
        {
            using var reader = new StreamReader(output, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        });

    private static Task Read(AnonymousPipeServerStream output, StringBuilder said) =>
        Task.Run(async () =>
        {
            using var reader = new StreamReader(output, Encoding.UTF8, leaveOpen: true);
            while (await reader.ReadLineAsync() is { } line)
            {
                lock (said) said.AppendLine(line);
            }
        });

    private static string Text(StringBuilder said)
    {
        lock (said) return said.ToString();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }

    private async Task<(InstancePipe Pipe, PipeClient Client, ServiceProvider Services, MachineLog Log)> ServeAsync()
    {
        var log = new MachineLog(Home, "desktop");
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test");
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeIdentity.Ensure(Home)!);
        return (pipe, client, provider, log);
    }
}
