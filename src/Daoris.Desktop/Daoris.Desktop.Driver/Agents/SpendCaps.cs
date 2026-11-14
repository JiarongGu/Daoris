using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An account's spend cap, a window Daoris learns: the workspace's owner keeps it low on purpose and no one raises it, so a refusal says no
/// time and Daoris reads none from it. Each refusal is kept; when the account last worked again after one is the evidence, from the usage
/// history's turns, and the account is offered again when it reopened before, a refusal met again at once waiting twice as long.
/// </summary>
/// <remarks>
/// <para><b>Kept in <c>caps.json</c></b> beside the usage history: each account's refusal moments, newest 40, in one object keyed by its
/// owner and profile. A file that cannot be read or written leaves the account to the unlearned wait, never to a strike.</para>
/// <para><b>Pure where it decides</b> (<see cref="WaitAfter"/>): moments in, a wait out, so every shape is a test with no clock.</para>
/// </remarks>
public static class SpendCaps
{
    /// <summary>The window a cap's cool-off carries, in Daoris's own name.</summary>
    public const string Window = "cap";

    /// <summary>What a refusal's <c>hit</c> says where it is a cap.</summary>
    public const string Hit = "spend cap";

    /// <summary>The wait for an account whose cap has not reopened in any history Daoris holds: tried again after this, not hourly.</summary>
    public static readonly TimeSpan Unlearned = TimeSpan.FromHours(3);

    /// <summary>The shortest and longest wait: a cap met again at once doubles up to the longest.</summary>
    public static readonly TimeSpan Soonest = TimeSpan.FromMinutes(10), Latest = TimeSpan.FromHours(24);

    private const int Kept = 40;

    private static readonly object Gate = new();

    /// <summary>Whether a limit the reader read is a spend cap.</summary>
    public static bool Is(LimitSeen seen) => string.Equals(seen.Hit, Hit, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How long a cap refusal at <paramref name="seen"/> keeps its account from new work. <paramref name="caps"/> are the earlier refusals and
    /// <paramref name="worked"/> the moments the account ran a turn, each oldest first. A refusal followed by work before the next refusal
    /// gave a gap; the median gap is when it reopened before. The refusals since the last work, this one included, double the wait.
    /// </summary>
    public static (TimeSpan Wait, bool Learned) WaitAfter(IReadOnlyList<DateTimeOffset> caps, IReadOnlyList<DateTimeOffset> worked, DateTimeOffset seen)
    {
        List<TimeSpan> gaps = [];
        for (var i = 0; i < caps.Count; i++)
        {
            var next = i + 1 < caps.Count ? caps[i + 1] : seen;
            if (worked.Where(moment => moment > caps[i]).Select(moment => (DateTimeOffset?)moment).FirstOrDefault() is { } again && again < next && again - caps[i] <= TimeSpan.FromDays(3))
            {
                gaps.Add(again - caps[i]);
            }
        }

        var lastWorked = worked.Where(moment => moment < seen).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        var failed = caps.Count(moment => moment > lastWorked) + 1;
        var learned = gaps.Count > 0;
        var basis = learned ? gaps.Order().ElementAt(gaps.Count / 2) : Unlearned;
        var wait = TimeSpan.FromTicks(basis.Ticks * (1L << Math.Min(failed - 1, 8)));
        return (wait < Soonest ? Soonest : wait > Latest ? Latest : wait, learned);
    }

    /// <summary>
    /// <paramref name="seen"/>, a cap refusal, made the account's cool-off by what the history says: the refusal kept, the wait learned,
    /// <c>Stated</c> false since no one named the time, and the window <see cref="Window"/>.
    /// </summary>
    public static LimitSeen Learn(string home, string agent, string? account, LimitSeen seen, DateTimeOffset now)
    {
        var caps = Moments(home, agent, account);
        IReadOnlyList<DateTimeOffset> worked;
        try
        {
            worked = new UsageLedger(home).Of(agent, account ?? "", now).Turns.Select(turn => turn.Ended).Order().ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            worked = [];
        }

        var (wait, _) = WaitAfter(caps, worked, now);
        Keep(home, agent, account, now);
        return seen with { Window = Window, Until = now + wait, Stated = false, AssumedZone = false, NotBelieved = false };
    }

    /// <summary>The account's earlier refusals, oldest first; none where the file does not read.</summary>
    internal static IReadOnlyList<DateTimeOffset> Moments(string home, string agent, string? account)
    {
        try
        {
            return Load(home)[KeyOf(agent, account)] is JsonArray held
                ? [.. held.Select(node => DateTimeOffset.TryParse(node?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTimeOffset?)null)
                    .OfType<DateTimeOffset>().Order()]
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    private static void Keep(string home, string agent, string? account, DateTimeOffset at)
    {
        try
        {
            lock (Gate)
            {
                var root = Load(home);
                if (root[KeyOf(agent, account)] is not JsonArray held) root[KeyOf(agent, account)] = held = new JsonArray();
                held.Add(at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                while (held.Count > Kept) held.RemoveAt(0);
                Directory.CreateDirectory(UsageHistory.FolderOf(home));
                AtomicFile.WriteText(PathOf(home), root.ToJsonString());
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // A refusal not kept costs the account's learning, never its cool-off.
        }
    }

    private static JsonObject Load(string home) =>
        File.Exists(PathOf(home)) && JsonNode.Parse(AtomicFile.ReadText(PathOf(home))) is JsonObject root ? root : new JsonObject();

    private static string PathOf(string home) => Path.Combine(UsageHistory.FolderOf(home), "caps.json");

    private static string KeyOf(string agent, string? account) => $"{agent.ToLowerInvariant()}/{(account ?? "").ToLowerInvariant()}";
}
