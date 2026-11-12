namespace Daoris.Driver;

/// <summary>
/// What a door holds that was taken at a version other than the one pinned: each measured ability, by the adapter's version for a protocol
/// door and the program's for a native one, and each offers reading its opens kept. Said as measured at another version, so a pin that
/// moved shows what no longer applies. Nothing is listed where nothing is pinned, since there is then no version to differ from.
/// </summary>
public static class VersionCurrency
{
    public static IReadOnlyList<string> Of(string home, AgentDoor door, string? pinned)
    {
        if (pinned is not { Length: > 0 }) return [];

        var lines = new List<string>();
        foreach (var name in AbilityName.All)
        {
            var stated = AbilityReading.Stated(door.Abilities, name);
            if (stated is not { IsKnown: true, By: KnownBy.Measured, At: { } at }) continue;
            var (said, version) = door.Transport == Transport.Protocol ? ("adapter", at.Adapter) : ("agent", at.Agent);
            if (version is not null && !string.Equals(version, pinned, StringComparison.Ordinal))
            {
                lines.Add($"{name} measured at {said} {version}, not the pinned {pinned} ({stated.Evidence})");
            }
        }

        lines.AddRange(AgentOffers.Read(home)
            .Where(reading => string.Equals(reading.Agent, door.Id.Value, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(reading.Version, pinned, StringComparison.Ordinal))
            .Select(reading => $"offers of `{(reading.Account.Length == 0 ? "its own sign-in" : reading.Account)}` read at {reading.Version ?? "an unrecorded version"}, not the pinned {pinned}"));
        return lines;
    }
}
