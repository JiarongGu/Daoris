using Lyntai.Agents;
using Lyntai.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Driver;

/// <summary>
/// One of the two containers Daoris composes of the layer, and nothing else of the layer composes (the agent-lyntai design §5.5). The
/// registry's, one per driver process, holds every layer-run door's registration for its descriptor and claims alone and never spawns. A
/// spawn's, one per start or usage read, holds that one registration over the start's own <see cref="LauncherRunner"/>, so concurrent
/// starts share no keyed singleton; its owner is the run that built it, which disposes it at its close. Each registration runs inside
/// <c>AddLyntai</c> whole, as the layer's builder allows no other way.
/// </summary>
internal sealed class LayerContainer : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    private LayerContainer(ServiceProvider services, AgentProviderRegistry registry)
    {
        _services = services;
        Registry = registry;
    }

    /// <summary>The layer's registry over this container's registrations.</summary>
    public AgentProviderRegistry Registry { get; }

    /// <summary>The runner this container's doors spawn through.</summary>
    public IProcessRunner Runner
    {
        get
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return _services.GetRequiredService<IProcessRunner>();
        }
    }

    public bool Disposed { get; private set; }

    /// <summary>
    /// The registry's container: each door registered once, refused by Daoris before the layer's registry would let a later registration
    /// replace an earlier one, its doors compared without case as Daoris's registry compares them.
    /// </summary>
    /// <exception cref="DriverException">A door registered twice, naming both, or a registration that registers another id than its door.</exception>
    public static LayerContainer ForRegistry(IEnumerable<LayerRegistration> registrations)
    {
        var all = registrations.ToList();
        if (all.GroupBy(each => each.Door, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            throw new DriverException($"the layer's door `{string.Join("` and `", twice.Select(each => each.Door))}` is registered twice.");
        }

        return Build(all, NoSpawn.Runner);
    }

    /// <summary>A spawn's container: <paramref name="registration"/> alone, over <paramref name="runner"/>.</summary>
    public static LayerContainer ForSpawn(LayerRegistration registration, LauncherRunner runner) => Build([registration], runner);

    /// <summary>The session of <paramref name="door"/>'s layer door <paramref name="kind"/>.</summary>
    public IAgentSession Open(string door, string kind)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        return Registry.Get(door).Open(kind);
    }

    public async ValueTask DisposeAsync()
    {
        if (Disposed) return;
        Disposed = true;
        await _services.DisposeAsync().ConfigureAwait(false);
    }

    private static LayerContainer Build(IReadOnlyList<LayerRegistration> registrations, IProcessRunner runner)
    {
        var services = new ServiceCollection();
        // Registered before the layer's, which stands its own runner down for one already there.
        services.AddSingleton(runner);
        services.AddLyntai(builder =>
        {
            foreach (var each in registrations) each.Register(builder);
        });
        var built = services.BuildServiceProvider();
        var registry = built.GetRequiredService<AgentProviderRegistry>();
        if (registrations.FirstOrDefault(each => !registry.Ids.Contains(each.Door, StringComparer.Ordinal)) is { } other)
        {
            built.Dispose();
            throw new DriverException(
                $"the layer's door `{other.Door}` registers `{string.Join("`, `", registry.Ids)}`, not its own id as Daoris spells it.");
        }

        return new LayerContainer(built, registry);
    }

    /// <summary>The registry's runner: its container states doors and never spawns.</summary>
    private sealed class NoSpawn : IProcessRunner
    {
        public static readonly NoSpawn Runner = new();

        private static InvalidOperationException Refused() => new("the layer's registry container never spawns: a start builds a spawn's.");

        public Task<ProcessResult> RunAsync(
            string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
            string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default) =>
            throw Refused();

        public IAsyncEnumerable<string> StreamLinesAsync(
            string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
            string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default) =>
            throw Refused();

        public IAsyncEnumerable<string> ExchangeLinesAsync(
            string command, IReadOnlyList<string> args, IAsyncEnumerable<string> stdinLines, TimeSpan? inactivityTimeout = null,
            TimeSpan? maxDuration = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null,
            CancellationToken ct = default) =>
            throw Refused();

        public bool CommandExists(string command) => false;
    }
}
