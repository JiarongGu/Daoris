using Lyntai.Agents;
using LyntaiDoor = Lyntai.Agents.AgentDoor;

namespace Daoris.Driver;

/// <summary>
/// The registry's layer view (the agent-lyntai design §4.1, §4.2, §5.5): the doors Daoris's registry holds that are bound to the layer, over
/// the registry's container, built once with the view and never spawning. Records hold Daoris's ids alone: an id is resolved without case to
/// the spelling Daoris's registry gives it, and only that spelling reaches the layer's registry, whose ids are ordinal. A bound door's layer
/// door and its claims are read here, its claims into <see cref="Known{T}"/> (<see cref="LayerClaims"/>); a start of a kind the binding
/// covers builds its spawn's container from the registration the view answers.
/// </summary>
internal sealed class LayerView : IAsyncDisposable
{
    private readonly ProviderRegistry _registry;

    private readonly Dictionary<string, LayerBinding> _bound;

    private readonly LayerContainer _container;

    private LayerView(ProviderRegistry registry, Dictionary<string, LayerBinding> bound, LayerContainer container)
    {
        _registry = registry;
        _bound = bound;
        _container = container;
    }

    /// <summary>The layer's registry over the bound doors, each under the spelling Daoris's registry gives it.</summary>
    public AgentProviderRegistry Registry => _container.Registry;

    /// <summary>
    /// The view of <paramref name="bindings"/> over <paramref name="registry"/>.
    /// </summary>
    /// <exception cref="DriverException">
    /// A door Daoris's registry does not hold, one spelled otherwise than it spells it, a binding covering no run kind, or a door bound twice.
    /// </exception>
    public static LayerView Over(ProviderRegistry registry, IEnumerable<LayerBinding> bindings)
    {
        var bound = new Dictionary<string, LayerBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            var door = binding.Registration.Door;
            var spelled = registry.Doors.Select(each => each.Value).FirstOrDefault(each => string.Equals(each, door, StringComparison.OrdinalIgnoreCase))
                ?? throw new DriverException($"the layer's door `{door}` is no door Daoris's registry holds.");
            if (!string.Equals(spelled, door, StringComparison.Ordinal))
            {
                throw new DriverException($"the layer's door `{door}` is registered as `{spelled}`: only that spelling reaches the layer.");
            }

            if (binding.Runs.Count == 0) throw new DriverException($"the layer's door `{door}` is bound for no run kind.");
            if (!bound.TryAdd(door, binding)) throw new DriverException($"the layer's door `{door}` is bound twice.");
        }

        return new LayerView(registry, bound, LayerContainer.ForRegistry(bound.Values.Select(each => each.Registration)));
    }

    /// <summary>Whether <paramref name="door"/>'s <paramref name="kind"/> runs through the layer.</summary>
    public bool Binds(string door, StartKind kind) => _bound.TryGetValue(door, out var binding) && binding.Runs.Contains(kind);

    /// <summary>The registration a start of <paramref name="kind"/> on <paramref name="door"/> builds its spawn's container from; null where unbound.</summary>
    public LayerRegistration? RegistrationOf(string door, StartKind kind) => Binds(door, kind) ? _bound[door].Registration : null;

    /// <summary>The layer door <paramref name="door"/> is bound to, as its provider describes it.</summary>
    /// <exception cref="DriverException">The door is not bound.</exception>
    public LyntaiDoor Describe(string door)
    {
        var binding = _bound.TryGetValue(door, out var found) ? found : throw new DriverException($"`{door}` is not bound to the layer.");
        return Registry.Get(binding.Registration.Door).Describe().Doors.Single(each => each.Id == binding.Kind);
    }

    /// <summary><paramref name="ability"/> as the bound door claims it, applying to the Daoris door; absent where it states none.</summary>
    public Known<bool> Claim(string door, string ability) =>
        LayerClaims.Of(Describe(door), _registry.Door(door).Id.Value, ability);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
