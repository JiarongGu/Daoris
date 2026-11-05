namespace Daoris.Driver;

/// <summary>What a session on a door may carry for a model and an effort, in the tool's own words.</summary>
public sealed record ChoicesOffered(IReadOnlyList<string> Models, IReadOnlyList<string> Efforts);

/// <summary>
/// The session scope of a door's offers as the page's actors read it (the agent-provider design §7 row 5, row n's two scopes): its provider's
/// settings file where it states one, whose efforts a session on the native door also extends with the ones only a session carries
/// (<c>max</c>), else what the door's open last listed on any account, newest first. Never the account defaults' list alone, which holds no
/// <c>max</c>, and never one door's words for another agent.
/// </summary>
public static class SessionChoices
{
    /// <summary>The choices a session on <paramref name="door"/> offers, or null where nothing states or has listed any.</summary>
    public static ChoicesOffered? Of(string home, ProviderRegistry registry, string door)
    {
        if (registry.SettingsOf(door) is { } file)
        {
            var native = registry.Door(door).Transport == Transport.Native;
            return new ChoicesOffered(file.Models, native ? [.. file.Efforts, .. file.SessionOnly] : file.Efforts);
        }

        var listed = AgentOffers.Read(home)
            .Where(reading => string.Equals(reading.Agent, door, StringComparison.OrdinalIgnoreCase) && (reading.Models is not null || reading.Efforts is not null))
            .OrderByDescending(reading => reading.At)
            .FirstOrDefault();
        return listed is null ? null : new ChoicesOffered(listed.Models ?? [], listed.Efforts ?? []);
    }
}
