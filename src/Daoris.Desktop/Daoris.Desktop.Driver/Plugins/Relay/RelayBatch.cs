namespace Daoris.Driver;

/// <summary>
/// One relay's watermark and batch: the plugin's first line settling a
/// loss at once, each frame after it moving the watermark, at most one gap line however many gaps, at most <see cref="Events"/> event lines
/// and a count past them, the diagnostics tallied, and every batch closing on its watermark. So stdout holds at most 53 lines and stderr one
/// line for each diagnostic kind, whatever reaches the pipe.
/// </summary>
/// <param name="since">The watermark <c>--since</c> gave, already checked; null where none is given.</param>
/// <param name="kinds">The kinds printed; a kind left out still moves the watermark, so filtering is never taken for loss.</param>
/// <param name="build">This copy's build: a plugin of another ends the relay with its update line.</param>
/// <param name="notBy">A poster whose <c>channel.posted</c> frames move the watermark and print nothing: a member's own posts never wake it.</param>
public sealed class RelayBatch((string Run, long Seq)? since, IReadOnlySet<string> kinds, string build, string? notBy = null)
{
    /// <summary>The kinds printed where <c>--kinds</c> names none: the first build's four, since a quest kind floods at a sync.</summary>
    public static readonly IReadOnlySet<string> Default = new HashSet<string>(
        [WorkEventKinds.SessionParked, WorkEventKinds.QuestParked, WorkEventKinds.SessionEnded, WorkEventKinds.WorkLanded], StringComparer.Ordinal);

    /// <summary>The most event lines a batch prints; the rest are counted.</summary>
    public const int Events = 50;

    private readonly List<string> _events = [];
    private readonly Dictionary<string, int> _tallies = new(StringComparer.Ordinal);
    private string _run = since?.Run ?? RelayChecks.NoRun;
    private long _seq = since?.Seq ?? 0;
    private (int At, string Line)? _gap;
    private int _more;
    private string? _ending;

    /// <summary>Whether the batch is open: a kept event or a gap began its gathering.</summary>
    public bool Opened => _events.Count > 0 || _gap is not null || _more > 0;

    /// <summary>Whether the relay ends now, its batch printed: a gap at the first line, another build, or the end it is told.</summary>
    public bool Ends { get; private set; }

    /// <summary>The plugin's first line, checked: another build, another run or seq than the watermark, or no watermark, ends at once.</summary>
    public void First(RelayFirst first)
    {
        var lost = since is not { } watermark || watermark.Run != first.Run || watermark.Seq != first.Seq;
        (_run, _seq) = (first.Run, first.Seq);
        if (first.Build != build) End((run, seq) => RelaySaid.Updated(run, seq));
        else if (lost) End();
    }

    /// <summary>A checked frame: a repeat dropped and counted, a gap said once at its place, a kept kind said while the bound holds.</summary>
    public void Frame(RelayFrame frame)
    {
        if (frame.Run != _run || frame.Seq <= _seq)
        {
            Tally(frame.Run != _run ? "dropped" : "repeated");
            return;
        }

        var gapped = frame.Seq > _seq + 1 || frame.Missed > 0 || frame.Kind == WorkEventKinds.Missed || !RelaySaid.Said.Contains(frame.Kind);
        _seq = frame.Seq;
        if (gapped) Gap();
        if (!RelaySaid.Said.Contains(frame.Kind) || !kinds.Contains(frame.Kind)) return;
        if (notBy is not null && frame.Kind == WorkEventKinds.ChannelPosted && frame.Fields.GetValueOrDefault("poster") == notBy) return;
        if (_events.Count < Events) _events.Add(RelaySaid.Event(frame));
        else _more++;
    }

    /// <summary>One diagnostic met, said only as the relay ends.</summary>
    public void Tally(string kind) => _tallies[kind] = _tallies.GetValueOrDefault(kind) + 1;

    /// <summary>The relay ends for a reason of its own (a connection ended, the bound, a second client): what it gathered, then that.</summary>
    /// <param name="line">The end's own sentence, or null for a gap line.</param>
    public void End(Func<string, long, string>? line = null)
    {
        if (Ends) return;
        Ends = true;
        if (line is null) Gap();
        else _ending = line(_run, _seq);
    }

    /// <summary>The batch: stdout's lines, the gap at the place of the first, the count, the end's line and the closing watermark; stderr's tallies.</summary>
    public (IReadOnlyList<string> Out, IReadOnlyList<string> Err) Printed()
    {
        var lines = new List<string>(_events);
        if (_gap is { } gap) lines.Insert(gap.At, gap.Line);
        if (_more > 0) lines.Add(RelaySaid.More(_more, _run, _seq));
        if (_ending is { } ending) lines.Add(ending);
        lines.Add(RelaySaid.ReadUpTo(_run, _seq));
        var tallies = RelaySaid.Tallies.Keys.Where(_tallies.ContainsKey).Select(kind => RelaySaid.Tally(kind, _tallies[kind])).ToList();
        return (lines, tallies);
    }

    // One gap line a batch, at the place of the first, naming the watermark where it is met.
    private void Gap() => _gap ??= (_events.Count, RelaySaid.Gap(_run, _seq));
}
