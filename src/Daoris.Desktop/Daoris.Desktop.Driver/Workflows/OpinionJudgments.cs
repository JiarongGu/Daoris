using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// Which owed opinions a look under the watch judges again: each judgment keeps a stamp of what it reads, and a later look
/// judges it again only where that stamp moved, so an opinion sitting unchanged for hours costs no git, no review read and no
/// service read of its own at each look.
/// </summary>
/// <remarks>
/// <para><b>The stamp.</b> Read without git and without a read of its own of the service: the chain's quests and the working
/// line's and its reviewers' records from the look's one read of each (<see cref="OpinionLookWorld"/>); the tree's link, its
/// <c>HEAD</c> and the commit its branch names, from git's files; and the home's files the judgment reads (the chain's gate, the
/// deliveries of the opinions it asked, the run's binding and node record, the landings, the rule, the cool-offs), by their
/// write time and length; and whether an update drains.</para>
///
/// <para><b>What no stamp holds</b> is read again by time: every owed opinion is judged again <see cref="Refresh"/> after its
/// last judgment whatever moved (a branch of the person's taking the work, an ask's words, a workflow saved under the same
/// name), and one whose gate cools until a reset once that reset passes. A judgment that failed keeps no stamp.</para>
/// </remarks>
/// <param name="clock">The milliseconds a judgment's age is read on, the machine's monotonic clock unless a test moves it by hand.</param>
internal sealed class OpinionJudgments(string home, Func<long>? clock = null)
{
    /// <summary>How long a judgment stands before it is judged again whatever moved.</summary>
    internal static readonly TimeSpan Refresh = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Kept> _kept = new(StringComparer.OrdinalIgnoreCase);

    private sealed record Kept(string Stamp, long At, long? Until);

    private long Now => clock?.Invoke() ?? Environment.TickCount64;

    /// <summary>Whether <paramref name="subject"/>'s last judgment stands at <paramref name="stamp"/>: nothing it read moved, and nothing timed ran out.</summary>
    internal bool Stands(string subject, string stamp)
    {
        if (!_kept.TryGetValue(subject, out var kept) || kept.Stamp != stamp) return false;
        var now = Now;
        return now - kept.At < (long)Refresh.TotalMilliseconds && (kept.Until is not { } until || now < until);
    }

    /// <summary>A judgment of <paramref name="subject"/> made over <paramref name="stamp"/>, its gate cooling for <paramref name="cools"/> where it does.</summary>
    internal void Keep(string subject, string stamp, TimeSpan? cools)
    {
        var now = Now;
        _kept[subject] = new(stamp, now, cools is { } left ? now + (long)left.TotalMilliseconds : null);
    }

    /// <summary>A judgment that failed: the next look judges it again.</summary>
    internal void Forget(string subject) => _kept.TryRemove(subject, out _);

    /// <summary>Only the opinions still owed are kept.</summary>
    internal void KeepOnly(IEnumerable<string> owed)
    {
        var open = owed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in _kept.Keys.Where(subject => !open.Contains(subject)).ToList()) _kept.TryRemove(gone, out _);
    }

    /// <summary>The stamp of what <paramref name="due"/>'s judgment reads now (<see cref="OpinionJudgments"/>).</summary>
    internal async Task<string> StampAsync(OpinionDue due, OpinionLookWorld world, SessionTrees trees, DriverConfig config, CancellationToken ct)
    {
        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var records = await world.RecordsAsync(ct).ConfigureAwait(false);
        var run = due.OnRun ? due.Run : null;
        var chain = run is null ? ReviewGate.ChainOf(quests, due.Quest) : [];
        var bound = run ?? WorkflowRunBindings.RunOf(chain, due.Repository) ?? due.Quest;
        var key = run ?? TreeGates.OpinionChain(chain, due.Quest);
        var gates = new OpinionGates(home);
        var asked = gates.Read(key, due.Repository).Asks.Select(ask => ask.Opinion).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stamp = new StringBuilder();
        foreach (var quest in chain) Quest(stamp, quest);
        var working = records.FirstOrDefault(record => Same(record.Id, due.Session));
        var line = working?.Line ?? due.Session;
        foreach (var record in records.Where(record => Same(record.Id, due.Session) || Same(record.Line, line) || Same(record.From, due.Session)
                                                       || (record.Opinion is { } opinion && asked.Contains(opinion))))
        {
            Record(stamp, record);
        }

        var deliveries = new OpinionDeliveries(home);
        foreach (var opinion in asked.Order(StringComparer.OrdinalIgnoreCase)) File(stamp, deliveries.PathOf(opinion));
        File(stamp, gates.PathOf(key));
        File(stamp, WorkflowRunBindings.PathOf(home, bound));
        File(stamp, NodeRecordFiles.PathOf(home, RunUnits.IdOf(bound)));
        File(stamp, trees.LandingsPath);
        File(stamp, trees.ConfigPath);
        File(stamp, AccountCooling.PathOf(home));
        Tree(stamp, due.Tree);
        stamp.Append(config.Draining ? "draining" : "on");
        return TreeReadPass.Hash(stamp.ToString());
    }

    // What the judgment reads of a quest of the chain: its place in it, its state, its hold, its set-ups and the verdicts on them.
    private static void Quest(StringBuilder stamp, QuestView quest) => stamp.Append(CultureInfo.InvariantCulture,
        $"q {quest.Id}|{quest.Status}|{quest.Updated?.UtcTicks}|{quest.Parent}|{quest.SetUpIn}|{quest.To}|{quest.Held}|{quest.Hold}|{quest.Awaits}|{quest.From}"
        + $"|{quest.SetUps.Count}|{quest.SetUps.LastOrDefault()?.Commit}|{quest.Verdicts.Count}|{quest.Review?.Choice}|{quest.Submission?.Sequence}"
        + $"|{quest.Closure}|{quest.WorkflowChoices.Count}|{quest.Reopened?.Sequence}\n");

    // What a delivery and a pass's end move on a record: its state, its words waiting, its last move.
    private static void Record(StringBuilder stamp, SessionResponse record) => stamp.Append(CultureInfo.InvariantCulture,
        $"r {record.Id}|{record.State}|{record.Updated.UtcTicks}|{record.Said?.Count}|{record.Opinion}|{record.Tree}\n");

    // A file by its write and its length: each of the home's records is written whole, by a rename.
    private static void File(StringBuilder stamp, string? path)
    {
        if (path is null) return;
        try
        {
            var info = new FileInfo(path);
            stamp.Append(CultureInfo.InvariantCulture, $"f {path}|{(info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : "-")}\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stamp.Append(CultureInfo.InvariantCulture, $"f {path}|{Guid.NewGuid():N}\n");
        }
    }

    // The tree's commit from git's files: its link, its HEAD and what the ref HEAD names holds, loose or packed. A tree read no
    // other way stamps afresh each time, so it is judged at every look as before.
    private static void Tree(StringBuilder stamp, string tree)
    {
        try
        {
            if (!Directory.Exists(tree))
            {
                stamp.Append("t gone\n");
                return;
            }

            if (TreeReadPass.HeadOf(tree) is not { } head)
            {
                stamp.Append(CultureInfo.InvariantCulture, $"t {Guid.NewGuid():N}\n");
                return;
            }

            stamp.Append(CultureInfo.InvariantCulture, $"t {head.Link}|{head.Text}|{head.Loose}\n");
            if (head is { Name: not null, Loose: null }) File(stamp, Path.Combine(head.Common, "packed-refs"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stamp.Append(CultureInfo.InvariantCulture, $"t {Guid.NewGuid():N}\n");
        }
    }

    private static bool Same(string? left, string? right) => left is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
