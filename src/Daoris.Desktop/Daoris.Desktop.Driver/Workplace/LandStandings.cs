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

    /// <summary>
    /// Whether a planned start is held back because its taken quest's landing waits: a first start or a carry-on of the quest, never one the
    /// person's or Daoris's words wait on (<see cref="Consideration.GoesOn"/>), which goes on in its session whatever its landing waits on.
    /// </summary>
    public static bool Holds(string home, Consideration start, DateTimeOffset now) =>
        start is { GoesOn: false, Quest: { Status: "Taken" } quest }
        && Owing.TryGetValue(Key(home, quest.Id), out var seen) && now - seen < LandRetries.Fresh;

    private static string Key(string home, string quest) => $"{Homes.Full(home)}\n{quest}";
}
