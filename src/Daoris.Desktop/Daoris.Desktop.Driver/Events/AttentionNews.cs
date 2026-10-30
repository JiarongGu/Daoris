using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The look's own news told once across restarts (FOUNDATION1 m-iv, the foundation design §4.5): a wait for an account and a quest's park, which
/// the attention watch finds from a look's verdicts, each claimed in the said-set by its key before it is shown. The watch's first-look baseline
/// gives way to the said-set: only a home with no said-set yet takes what stands as said, and one claimed before is never told again.
/// </summary>
public static class AttentionNews
{
    /// <summary>The baseline's name in the said-set.</summary>
    public const string Branch = "attention";

    /// <summary>The key of a look's news: a wait by its sentence, which names its accounts and the cool-off's end, a quest's park by its quest and last session.</summary>
    public static string KeyOf(AttentionEvent item) => item.Kind switch
    {
        AttentionKind.Waiting => "wait:" + Digest(item.Repository + "\n" + item.Note),
        AttentionKind.QuestParked => $"questpark:{item.Quest}:{item.Session}",
        _ => "attention:" + Digest($"{item.Kind}\n{item.Session}\n{item.Repository}\n{item.Note}"),
    };

    /// <summary>
    /// <paramref name="events"/> the look found, those not claimed before, in order. The first run on a home takes them all as said and tells none;
    /// a store that refuses the claim tells none, which keeps each at most once.
    /// </summary>
    public static IReadOnlyList<AttentionEvent> Told(News news, IReadOnlyList<AttentionEvent> events, DateTimeOffset now)
    {
        var first = news.First(Branch, now);
        if (events.Count == 0) return [];
        var keyed = events.Select(item => (Key: KeyOf(item), Item: item)).ToList();
        var claim = news.Claim(Branch, [.. keyed.Select(each => new NewsOccurrence(each.Key, now))], null, now);
        if (first || claim.Refused is not null) return [];
        var told = claim.Told.Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
        return [.. keyed.Where(each => told.Contains(each.Key)).Select(each => each.Item)];
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
