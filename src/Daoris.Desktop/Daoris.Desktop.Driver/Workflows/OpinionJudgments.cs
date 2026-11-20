using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// What an owed opinion's judgment under the watch reads, as one stamp: a later look judges it again only where that stamp moved
/// (<see cref="LookFacts.Stands"/>), so an opinion sitting unchanged for hours costs no git, no review read and no service read of its
/// own at each look.
/// </summary>
/// <remarks>
/// <para><b>The stamp.</b> Read without git and without a read of its own of the service: the due itself (its quest or run, its tree,
/// when it fell owed, a press's tip and turns), which a press replaces in place; the chain's quests and the working line's, its
/// reviewers' and its rechecks' records from the look's one read of each (<see cref="OpinionLookWorld"/>); the tree's link, its
/// <c>HEAD</c> and the commit its branch names, from git's files; the chain's gate, the deliveries of the opinions it asked, the run's
/// binding and node record by what they hold, and the landings, the rule and the cool-offs by their write time and length; and
/// whether an update drains.</para>
///
/// <para><b>What no stamp holds</b> is read again by time: every owed opinion is judged again <see cref="LookFacts.Refresh"/> after
/// its last judgment whatever moved (a branch of the person's taking the work, an ask's words, a workflow saved under the same
/// name), and one whose gate cools or whose pass still reads once the reset or the pass's minutes pass. A judgment that failed, or
/// read its gate or review <c>unread</c>, keeps no stamp, and a pass begun for it, or a pass's or a delivery's end, lets the stamp go
/// (<see cref="LookFacts.Unstamped"/>).</para>
///
/// <para>The judgments of a look share its one read of the quests, and only a few run at once (<c>OpinionGateLook.OpinionsAtOnce</c>):
/// one queued behind the others reads a quest list as old as its wait.</para>
/// </remarks>
internal static class OpinionJudgments
{
    /// <summary>The stamp of what <paramref name="due"/>'s judgment reads now (<see cref="OpinionJudgments"/>).</summary>
    internal static async Task<string> StampAsync(
        string home, OpinionDue due, OpinionLookWorld world, SessionTrees trees, DriverConfig config, CancellationToken ct)
    {
        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var records = await world.RecordsAsync(ct).ConfigureAwait(false);
        var run = due.OnRun ? due.Run : null;
        var chain = run is null ? ReviewGate.ChainOf(quests, due.Quest) : [];
        var bound = run ?? WorkflowRunBindings.RunOf(chain, due.Repository) ?? due.Quest;
        var key = run ?? TreeGates.OpinionChain(chain, due.Quest);
        var gates = new OpinionGates(home);
        var deliveries = new OpinionDeliveries(home);
        var asked = gates.Read(key, due.Repository).Asks.Select(ask => ask.Opinion).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        // A delivery's recheck is a pass of its own, whose reviewer's record moves as the first pass's does.
        var passes = asked.Concat(asked.Select(opinion => deliveries.Read(opinion).Recheck).OfType<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stamp = new StringBuilder();
        stamp.Append(CultureInfo.InvariantCulture,
            $"d {due.Quest}|{due.Run}|{due.Repository}|{due.Workspace}|{due.Tree}|{due.At.UtcTicks}|{due.Tip}|{due.Turns}\n");
        foreach (var quest in chain) Quest(stamp, quest);
        var working = records.FirstOrDefault(record => Same(record.Id, due.Session));
        var line = working?.Line ?? due.Session;
        foreach (var record in records.Where(record => Same(record.Id, due.Session) || Same(record.Line, line) || Same(record.From, due.Session)
                                                       || (record.Opinion is { } opinion && passes.Contains(opinion))))
        {
            Record(stamp, record);
        }

        foreach (var opinion in asked.Order(StringComparer.OrdinalIgnoreCase)) Held(stamp, deliveries.PathOf(opinion));
        Held(stamp, gates.PathOf(key));
        Held(stamp, WorkflowRunBindings.PathOf(home, bound));
        Held(stamp, NodeRecordFiles.PathOf(home, RunUnits.IdOf(bound)));
        Written(stamp, trees.LandingsPath);
        Written(stamp, trees.ConfigPath);
        Written(stamp, AccountCooling.PathOf(home));
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

    // One of the chain's own small records by what it holds, so a rewrite of the same length within the clock's grain still moves it.
    private static void Held(StringBuilder stamp, string? path) => Read(stamp, path, info =>
        info.Exists ? Convert.ToHexString(SHA256.HashData(AtomicFile.ReadBytes(info.FullName))) : "-");

    // A record every judgment shares, which may be long, by its write and its length: each of the home's records is written whole, by a rename.
    private static void Written(StringBuilder stamp, string? path) => Read(stamp, path, info =>
        info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : "-");

    // A file that cannot be read stamps afresh each time, so it is judged at every look as before.
    private static void Read(StringBuilder stamp, string? path, Func<FileInfo, string> of)
    {
        if (path is null) return;
        try
        {
            stamp.Append(CultureInfo.InvariantCulture, $"f {path}|{of(new FileInfo(path))}\n");
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
            if (head is { Name: not null, Loose: null }) Written(stamp, Path.Combine(head.Common, "packed-refs"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stamp.Append(CultureInfo.InvariantCulture, $"t {Guid.NewGuid():N}\n");
        }
    }

    private static bool Same(string? left, string? right) => left is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
