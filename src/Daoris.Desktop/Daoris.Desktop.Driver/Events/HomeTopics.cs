using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Where the files of the driver's home topics are, for a topic whose files belong to a subdomain above the events (FOUNDATION1 mod-vii,
/// the design §14.3): the watch names the topic, and the subdomain that owns the files answers their places, once, when the driver's
/// assembly loads (<see cref="Answer"/>), before any watch is built. The watch is built by the look, by the shell's loop and by every test,
/// so the answer is installed where the topic's owner sits and no construction site hands the watch anything.
/// </summary>
/// <remarks>
/// <b>A topic nobody answers throws</b> where a watch asks for it, rather than being watched with no file, which would leave a wake that
/// never comes; <c>HomeTopicsTests</c> holds the answered set, so a missing answer fails there.
/// </remarks>
public static class HomeTopics
{
    private static readonly ConcurrentDictionary<string, Func<string, HomePart[]>> Answers = new(StringComparer.Ordinal);

    /// <summary>Answers the places of a topic's files under a home, for the topic's owner (FOUNDATION1 mod-vii).</summary>
    internal static void Answer(string topic, Func<string, HomePart[]> places) => Answers[topic] = places;

    /// <summary>The topics answered, each with the places its owner gave.</summary>
    internal static IReadOnlyDictionary<string, Func<string, HomePart[]>> Answered => Answers;

    /// <summary>The places of a topic's files under <paramref name="home"/>, as its owner answered them.</summary>
    /// <exception cref="InvalidOperationException">No subdomain answers the topic: its files would be watched nowhere.</exception>
    internal static HomePart[] Of(string topic, string home) =>
        Answers.TryGetValue(topic, out var places)
            ? places(home)
            : throw new InvalidOperationException($"no subdomain answers where the `{topic}` topic's files are: its watch would hear nothing");
}
