namespace Daoris.Driver;

/// <summary>
/// Where a start works, as a provider's <see cref="Reach"/> reads it: a repository; a workspace with no
/// repository, a brainstorm's voice; or the machine's room across repositories, Ask Daoris's.
/// </summary>
/// <param name="Workspace">The start's workspace, where it names one.</param>
/// <param name="Repository">The repository it works in; null for a start on no repository.</param>
/// <param name="Across">A start across repositories, admitted only where the machine's room is opened to the provider.</param>
public readonly record struct ReachPlace(string? Workspace, string? Repository, bool Across = false)
{
    /// <summary>The machine's room across repositories.</summary>
    public static ReachPlace Machine { get; } = new(null, null, Across: true);

    /// <summary>The place in a sentence: the room, the repository, the workspace, or the start's own where it names none.</summary>
    public string Said => Across ? "the machine's room"
        : Repository is { Length: > 0 } repository ? $"the repository `{repository}`"
        : Workspace is { Length: > 0 } workspace ? $"the workspace `{workspace}`"
        : "this start's place";
}

/// <summary>
/// The places a person opened to a provider whose reach is <see cref="Reach.Opened"/>. Until a way for a person to open one exists nothing is opened, and nothing opened reads closed.
/// </summary>
public interface IReachOpenings
{
    /// <summary>Whether <paramref name="place"/> is opened to <paramref name="provider"/>.</summary>
    bool Opens(ProviderId provider, ReachPlace place);
}

/// <summary>
/// Whether a door's provider may work at a start's place: read before any
/// context or credential is sent, a joined door bound by its provider's reach. <see cref="Reach.Everywhere"/> admits every place;
/// <see cref="Reach.Opened"/> only a place the person opened to it.
/// </summary>
/// <remarks>
/// A name the registry does not know is not this reader's to refuse: the walk passes it <c>unavailable</c> before it asks here, and a start
/// outside the walk refuses it by its own words.
/// </remarks>
public sealed class ProviderReach(ProviderRegistry registry, IReachOpenings? openings = null)
{
    private readonly IReachOpenings _openings = openings ?? Closed;

    /// <summary>Nothing opened: every place is closed to an <see cref="Reach.Opened"/> provider.</summary>
    public static IReachOpenings Closed { get; } = new NothingOpened();

    /// <summary>The provider a door or an agent's id names, or null for a name the registry does not know.</summary>
    public Provider? ProviderOf(string name) =>
        registry.Providers.FirstOrDefault(provider => provider.Doors.Any(door => Ids.Same(door.Id.Value, name.Trim())))
        ?? registry.Providers.FirstOrDefault(provider => Ids.Same(provider.Id.Value, name.Trim()));

    /// <summary>
    /// Null where <paramref name="name"/>'s provider may work at <paramref name="place"/>; else the sentence saying it is not opened there.
    /// </summary>
    public string? Closes(string name, ReachPlace place)
    {
        if (ProviderOf(name) is not { } provider || provider.Reach == Reach.Everywhere || _openings.Opens(provider.Id, place)) return null;
        return $"`{name}` works only where it is opened, and {place.Said} is not opened to it";
    }

    private sealed class NothingOpened : IReachOpenings
    {
        public bool Opens(ProviderId provider, ReachPlace place) => false;
    }
}
