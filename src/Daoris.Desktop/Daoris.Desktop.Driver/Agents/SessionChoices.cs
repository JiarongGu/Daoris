namespace Daoris.Driver;

/// <summary>What a session on a door may carry for a model and an effort, in the tool's own words.</summary>
/// <param name="Models">The model values.</param>
/// <param name="Efforts">The effort values.</param>
public sealed record ChoicesOffered(IReadOnlyList<string> Models, IReadOnlyList<string> Efforts)
{
    /// <summary>Where the models are a stamped list and not an open's, the program version of that list; null where an open listed them.</summary>
    public string? Stamped { get; init; }
}

/// <summary>
/// The session scope of a door's offers as the page's actors read it (the agent-provider design §7 row 5, row n's two scopes): the models its
/// agent's newest open listed, on either door, else its provider's stamped alias list; the efforts its provider's settings file states, which a
/// session on the native door also extends with the ones only a session carries (<c>max</c>); a door whose provider states no file offers what
/// its open last listed on any account, newest first. Never the account defaults' list alone, which holds no <c>max</c>, and never one
/// agent's words for another.
/// </summary>
public static class SessionChoices
{
    /// <summary>The choices a session on <paramref name="door"/> offers, or null where nothing states or has listed any.</summary>
    public static ChoicesOffered? Of(string home, ProviderRegistry registry, string door)
    {
        // A door the registry does not hold states nothing, so what its open listed is all there is.
        var held = registry.Doors.Any(each => string.Equals(each.Value, door, StringComparison.Ordinal));
        var part = held ? registry.Door(door).Offers : default;
        IReadOnlyList<string> sameAgent = held ? SameAgent(registry, door) : [door];
        return (part.IsKnown ? part.Required : ListedOffers.Instance).Of(home, door, registry.SettingsOf(door), sameAgent);
    }

    private static List<string> SameAgent(ProviderRegistry registry, string door)
    {
        var owner = registry.OwnerOfAgent(door).Value;
        return [.. registry.Names.Where(name => string.Equals(registry.OwnerOfAgent(name).Value, owner, StringComparison.OrdinalIgnoreCase)).Append(door)];
    }
}
