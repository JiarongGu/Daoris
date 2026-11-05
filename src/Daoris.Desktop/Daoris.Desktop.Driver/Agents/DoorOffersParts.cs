namespace Daoris.Driver;

/// <summary>
/// A door whose provider's settings file states what it takes: the file's model aliases and efforts, and, where the door says its native
/// session carries more, the efforts only a session carries (<c>max</c>). Where the provider states no file, what the door's open last listed.
/// </summary>
/// <param name="SessionOnlyEfforts">Whether a session on this door also carries the efforts a default in the file may not hold.</param>
public sealed class SettingsOffers(bool SessionOnlyEfforts) : IDoorOffers
{
    public ChoicesOffered? Of(string home, string door, ISettingsFile? provider) =>
        provider is { } file
            ? new ChoicesOffered(file.Models, SessionOnlyEfforts ? [.. file.Efforts, .. file.SessionOnly] : file.Efforts)
            : ListedOffers.Instance.Of(home, door, provider);
}

/// <summary>
/// A door that states nothing: what its open last listed on any account of its own id, newest first, never one door's words for another
/// agent and never the account defaults' list alone.
/// </summary>
public sealed class ListedOffers : IDoorOffers
{
    public static ListedOffers Instance { get; } = new();

    public ChoicesOffered? Of(string home, string door, ISettingsFile? provider)
    {
        var listed = AgentOffers.Read(home)
            .Where(reading => string.Equals(reading.Agent, door, StringComparison.OrdinalIgnoreCase) && (reading.Models is not null || reading.Efforts is not null))
            .OrderByDescending(reading => reading.At)
            .FirstOrDefault();
        return listed is null ? null : new ChoicesOffered(listed.Models ?? [], listed.Efforts ?? []);
    }
}
