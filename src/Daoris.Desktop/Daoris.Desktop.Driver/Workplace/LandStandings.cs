using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The quests whose run's landing is still owed: a landing refused, held or waiting keeps its quest here, in memory, read at every look it
/// is judged, and a landing that ended removes it. The start stage reads it so a quest waiting at its landing is not started again at each
/// look (<see cref="Holds"/>): its session ended, and the landing is the run's own act, not a session's.
/// </summary>
public static class LandStandings
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Owing = new(StringComparer.Ordinal);

    /// <summary>Whether a try that came to <paramref name="code"/> leaves its landing owed.</summary>
    public static bool Owed(string code) => !LandingCode.Closes(code);

    /// <summary>The quest's landing waits, judged at <paramref name="now"/>; a wait no look reads again lapses after <see cref="LandRetries.Fresh"/>.</summary>
    public static void Wait(string home, string quest, DateTimeOffset now) => Owing[Key(home, quest)] = now;

    /// <summary>The quest's landing ended: nothing is owed.</summary>
    public static void Done(string home, string quest) => Owing.TryRemove(Key(home, quest), out _);

    /// <summary>Whether the quest's landing is owed and a look read it within <see cref="LandRetries.Fresh"/> of <paramref name="now"/>.</summary>
    public static bool Holds(string home, string quest, DateTimeOffset now) =>
        Owing.TryGetValue(Key(home, quest), out var seen) && now - seen < LandRetries.Fresh;

    private static string Key(string home, string quest) => $"{Homes.Full(home)}\n{quest}";
}
