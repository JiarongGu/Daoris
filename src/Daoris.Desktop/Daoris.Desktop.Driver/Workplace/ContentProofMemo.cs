using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The content proofs that found nothing holding a tree's work, kept in the home across restarts, so a round or a start over work
/// no ref moved under asks git nothing. A proof rests on its branch tip, the line, the branches that may not hold it and the
/// repository's counted refs (<see cref="CountedRefs.Version"/>, read from git's files): any of them moving is another key.
/// Observations only, and only the answer "not held" read through; a hold, a failure and a lacking list are asked again.
/// Bounded by <see cref="Most"/> entries and <see cref="Age"/>.
/// </summary>
internal static class ContentProofMemo
{
    internal const int Most = 400;

    internal static readonly TimeSpan Age = TimeSpan.FromDays(14);

    private static readonly ConcurrentDictionary<string, Kept> Homes = new(StringComparer.OrdinalIgnoreCase);

    internal static string? KeyOf(string commonDir, string tip, string? line, IEnumerable<string> notHolders)
    {
        if (CountedRefs.Read(commonDir) is not { } refs) return null;
        var text = string.Join('\n', commonDir, tip, line ?? "", refs.Version, string.Join(',', notHolders.Order(StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    internal static bool Knows(string home, string key) => Of(home).Entries.ContainsKey(key);

    internal static void Keep(string home, string key, DateTimeOffset now)
    {
        var kept = Of(home);
        lock (kept.Gate)
        {
            // Milliseconds, each after the last kept, so the oldest is always one entry.
            var at = Math.Max(now.ToUnixTimeMilliseconds(), (kept.Entries.IsEmpty ? 0 : kept.Entries.Values.Max()) + 1);
            kept.Entries[key] = at;
            var old = now.Subtract(Age).ToUnixTimeMilliseconds();
            foreach (var stale in kept.Entries.Where(entry => entry.Value < old).Select(entry => entry.Key).ToList()) kept.Entries.TryRemove(stale, out _);
            foreach (var surplus in kept.Entries.OrderBy(entry => entry.Value).Take(Math.Max(0, kept.Entries.Count - Most)).Select(entry => entry.Key).ToList())
            {
                kept.Entries.TryRemove(surplus, out _);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(kept.Path)!);
                AtomicFile.WriteText(kept.Path, System.Text.Json.JsonSerializer.Serialize(kept.Entries));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Kept in memory alone this time.
            }
        }
    }

    /// <summary>Forgets what a process kept of <paramref name="home"/>, so the next ask reads the file: the tests' restart.</summary>
    internal static void Restart(string home) => Homes.TryRemove(System.IO.Path.GetFullPath(home), out _);

    private static Kept Of(string home) => Homes.GetOrAdd(System.IO.Path.GetFullPath(home), full => new Kept(System.IO.Path.Combine(full, "memo", "content-proofs.json")));

    private sealed class Kept
    {
        public Kept(string path)
        {
            Path = path;
            try
            {
                if (File.Exists(path) && System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(AtomicFile.ReadText(path)) is { } read)
                {
                    Entries = new ConcurrentDictionary<string, long>(read, StringComparer.Ordinal);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // A file that cannot be read keeps nothing.
            }
        }

        public string Path { get; }

        public object Gate { get; } = new();

        public ConcurrentDictionary<string, long> Entries { get; } = new(StringComparer.Ordinal);
    }
}
