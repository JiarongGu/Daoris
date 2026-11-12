namespace Daoris.Driver;

/// <summary>
/// The agents' facts from each provider's registration: who each agent is, its account acts,
/// and each door's sheet read by <see cref="Sheet.Of"/> over the door's stated abilities. Nothing is negotiated here: an opening's answers
/// describe that opening alone.
/// </summary>
internal sealed class ProviderFacts(ProviderRegistry registry) : IAgentFacts
{
    private IReadOnlyList<AgentFacts>? _agents;

    public IReadOnlyList<AgentFacts> Agents => _agents ??= [.. registry.Providers.Select(Facts)];

    public AgentFacts? Of(string name)
    {
        var agents = Agents;
        return agents.FirstOrDefault(agent => Ids.Same(agent.Id, name))
            ?? agents.FirstOrDefault(agent => agent.Door(name) is not null)
            ?? registry.Providers
                .Where(provider => ProviderRegistration.Names(provider).Any(each => Ids.Same(each, name)))
                .Select(provider => agents.First(agent => agent.Id == provider.Id.Value))
                .FirstOrDefault();
    }

    private AgentFacts Facts(Provider provider)
    {
        // The door named for the agent leads, as a person knows the agent by it; the rest keep the registration's ordinal order.
        var doors = provider.Doors.Where(door => Ids.Same(door.Id.Value, provider.Id.Value))
            .Concat(provider.Doors.Where(door => !Ids.Same(door.Id.Value, provider.Id.Value)))
            .ToList();
        var identity = provider.Identity;
        var lead = doors.FirstOrDefault();
        return new AgentFacts(
            provider.Id.Value,
            identity.Product ?? lead?.Identity.Product,
            identity.Maker ?? lead?.Identity.Maker,
            identity.Mark,
            identity.StandIn,
            provider.Unavailable ? AgentActs.None : Sheet.ActsOf(provider, lead),
            [.. doors.Select(door => Sheet.Of(provider, door, negotiated: null))]);
    }
}
