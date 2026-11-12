namespace Daoris.Driver;

/// <summary>
/// A door whose provider's settings file states what it takes: the models its agent's newest open listed, on either of its doors, else
/// the file's alias list as a stamped fallback; the file's efforts and, where the door says its native session carries more, the efforts
/// only a session carries (<c>max</c>). Where the provider states no file, what the door's open last listed.
/// </summary>
/// <param name="SessionOnlyEfforts">Whether a session on this door also carries the efforts a default in the file may not hold.</param>
public sealed class SettingsOffers(bool SessionOnlyEfforts) : IDoorOffers
{
    public ChoicesOffered? Of(string home, string door, ISettingsFile? provider, IReadOnlyList<string>? sameAgent = null)
    {
        if (provider is not { } file) return ListedOffers.Instance.Of(home, door, provider, sameAgent);

        var efforts = SessionOnlyEfforts ? [.. file.Efforts, .. file.SessionOnly] : file.Efforts;
        return ListedOffers.Newest(home, sameAgent ?? [door]) is { Models: { Count: > 0 } listed }
            ? new ChoicesOffered(listed, efforts)
            : new ChoicesOffered(file.Models, efforts) { Stamped = AgentSettings.ModelsReadAt };
    }
}

/// <summary>
/// A door that states nothing: what its open last listed on any account of its own id, newest first, never one door's words for another
/// agent and never the account defaults' list alone.
/// </summary>
public sealed class ListedOffers : IDoorOffers
{
    public static ListedOffers Instance { get; } = new();

    public ChoicesOffered? Of(string home, string door, ISettingsFile? provider, IReadOnlyList<string>? sameAgent = null) =>
        Newest(home, sameAgent ?? [door]) is { } listed ? new ChoicesOffered(listed.Models ?? [], listed.Efforts ?? []) : null;

    /// <summary>The newest reading any of <paramref name="doors"/> kept, by the time of its open, that listed a model or an effort.</summary>
    public static OfferReading? Newest(string home, IReadOnlyList<string> doors) =>
        AgentOffers.Read(home)
            .Where(reading => doors.Contains(reading.Agent, StringComparer.OrdinalIgnoreCase) && (reading.Models is not null || reading.Efforts is not null))
            .OrderByDescending(reading => reading.At)
            .FirstOrDefault();
}
