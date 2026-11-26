using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The quests whose run's landing is still owed: a landing refused, held or waiting keeps its quest here, in memory, set at every look the
/// landing is judged and removed by a try that ended it. The look's plan reads it once, so a quest waiting at its landing is not started
/// again at each look (<c>LandingSits</c>); a mark no look sets again lapses after <see cref="LandRetries.Fresh"/>.
/// </summary>
public static class LandStandings
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Owing = new(StringComparer.Ordinal);

    /// <summary>Whether a try that came to <paramref name="code"/> leaves its landing owed.</summary>
    public static bool IsOwed(string code) => !LandingCode.Closes(code);

    public static void Wait(string home, string quest, DateTimeOffset now) => Owing[Key(home, quest)] = now;

    public static void Done(string home, string quest) => Owing.TryRemove(Key(home, quest), out _);

    /// <summary>The quests of <paramref name="home"/> whose landing is owed as of <paramref name="now"/>.</summary>
    public static IReadOnlySet<string> Owed(string home, DateTimeOffset now)
    {
        var prefix = $"{Homes.Full(home)}\n";
        return Owing.Where(each => each.Key.StartsWith(prefix, StringComparison.Ordinal) && now - each.Value < LandRetries.Fresh)
            .Select(each => each.Key[prefix.Length..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Key(string home, string quest) => $"{Homes.Full(home)}\n{quest}";
}
