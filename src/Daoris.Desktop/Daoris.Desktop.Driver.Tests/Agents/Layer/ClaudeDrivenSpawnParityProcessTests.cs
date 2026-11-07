using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a driven start hands Claude Code on each path: today's spawn of the shipped door
/// with the rules, servers and plugins its start is handed, and the layer's Claude door over the same start through
/// <see cref="ClaudeDrivenLayer.Start"/> and the carriers, its arguments said back by the program it spawns. Compared flag by flag, every
/// difference named. Real node processes.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ClaudeDrivenSpawnParityProcessTests : IDisposable
{
    /// <summary>
    /// The three tools a headless turn cannot answer, which the layer's door always denies and today's driven start does not: whether a
    /// driven start keeps them is a measurement not yet made.
    /// </summary>
    private const string FlowTools = "--disallowed-tools AskUserQuestion,ExitPlanMode,EnterPlanMode";

    private readonly string _scratch = ConformanceScratch.New("claude-driven-spawn");

    private readonly CancellationTokenSource _bound = new(Poll.Patience);

    public void Dispose()
    {
        _bound.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private SpawnRequest Request(string? mode) => new("engine", _scratch, "http://127.0.0.1:9")
    {
        Mode = mode,
        Asked = new ActorAsked(ClaudeDrivenLayer.Door, new ActorValue("entry", "opus", true), new ActorValue("entry", "high", true)),
    };

    [Theory]
    [InlineData("acceptEdits")]
    [InlineData("bypassPermissions")]
    [InlineData("default")]
    [InlineData(null)]
    public async Task A_driven_start_hands_Claude_Code_today_s_arguments_on_the_layer_but_for_the_flow_tools(string? mode)
    {
        var (settings, servers, plugin) = (Path.Combine(_scratch, "rules.json"), Path.Combine(_scratch, "servers.json"), Path.Combine(_scratch, "plugin"));
        var request = Request(mode);
        var spawn = ProviderRegistry.Built().Door(ClaudeDrivenLayer.Door).Spawned();
        var info = Shell.Start(spawn.Start(request), request);
        spawn.HandSettings(info, settings);
        spawn.HandServers(info, servers);
        spawn.HandPlugins(info, [plugin]);

        var layer = await LayerArgumentsAsync(request, new RunCarriers(settings, servers), [plugin]);

        Assert.Contains(FlowTools, layer);
        Assert.Equal(Pairs(info.ArgumentList.SkipWhile(each => each != "-p")), [.. layer.Where(each => each != FlowTools)]);
    }

    private async Task<IReadOnlyList<string>> LayerArgumentsAsync(SpawnRequest request, RunCarriers? carriers, IReadOnlyList<string> plugins)
    {
        var program = LayerTestProvider.Shim(_scratch, LayerTestProvider.Scenario(_scratch, new { argv = true, @out = Array.Empty<object>() }), "claude");
        var seen = new ParityObservation("layer", DateTimeOffset.UnixEpoch);
        await using (var run = new LayerRun(
            ClaudeDrivenLayer.Registration(program),
            ClaudeDrivenLayer.Start(new LayerStart(ClaudeDrivenLayer.Door, "the prompt", Path.Combine(_scratch, "layer.log"), TimeSpan.FromHours(1)), request, plugins),
            new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null),
            exists: _ => true))
        {
            if (carriers is not null) run.HandCarriers(carriers);
            run.HandSink(ParityObserver.Sink(seen));
            await run.OpenAsync(_bound.Token);
            await run.EndAsync(_bound.Token);
        }

        // The scenario says its arguments after its spec file; the shim hands the door's after the scenario's own two.
        var said = seen.Seen.Where(each => each.Channel == ParityChannel.Console && each["text"]!.StartsWith("argv ", StringComparison.Ordinal))
            .Select(each => JsonSerializer.Deserialize<string[]>(each["text"]!["argv ".Length..])!)
            .SingleOrDefault();
        return said is null ? [] : Pairs(said);
    }

    /// <summary>The arguments as flags each with its values, in order of the flags: what a flag says, never where it sits.</summary>
    private static IReadOnlyList<string> Pairs(IEnumerable<string> args)
    {
        var pairs = new List<string>();
        foreach (var arg in args)
        {
            if (arg.StartsWith('-') || pairs.Count == 0) pairs.Add(arg);
            else pairs[^1] += " " + arg;
        }

        return [.. pairs.Order(StringComparer.Ordinal)];
    }
}
