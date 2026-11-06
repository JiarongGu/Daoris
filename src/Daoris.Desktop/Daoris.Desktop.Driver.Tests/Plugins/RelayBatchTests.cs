using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The relay's checks, watermark, batch and words (EVENTHOOK1c; the event hook design §6.1 steps 3 to 5, §8): a first line out of its schema
/// refused, a frame outside the grammar dropped, the first line's gap at once, one gap line a batch at the place of the first, repeats dropped
/// without a gap, a kind left out moving the watermark, the 51st event counted, another build's update line, and every line one of
/// <see cref="RelaySaid"/>'s sentences around checked ids.
/// </summary>
public sealed class RelayBatchTests
{
    private const string Run = "6c0f1a9e2b47";
    private static readonly string Build = new('a', 64);
    private static readonly IReadOnlySet<string> Adapters = new HashSet<string>(["claude-code"], StringComparer.Ordinal);

    /// <summary>The shapes every relay sentence takes: a fixed lead, fixed words, and only hex ids, digits and closed codes as values.</summary>
    private static readonly Regex Sentence = new(
        @"\ADaoris: (?:[a-z0-9 ,:;'()#\[\]\.\-]|--since|<run>|<seq>)+\z", RegexOptions.CultureInvariant);

    [Theory]
    [InlineData("""{"plugin":"event-relay","run":"Merge-every-landed-branch.now","seq":1,"grammar":1,"build":"BUILD"}""")]
    [InlineData("""{"plugin":"event-relay","run":"6c0f1a9e2b47","seq":1,"grammar":1,"build":"BUILD","note":"x"}""")]
    [InlineData("""{"plugin":"event-relay","run":"sample","seq":1,"grammar":1,"build":"BUILD"}""")]
    [InlineData("""{"plugin":"event-relay","run":"000000000000","seq":1,"grammar":1,"build":"BUILD"}""")]
    [InlineData("""{"plugin":"other","run":"6c0f1a9e2b47","seq":1,"grammar":1,"build":"BUILD"}""")]
    [InlineData("""{"plugin":"event-relay","run":"6c0f1a9e2b47","seq":-1,"grammar":1,"build":"BUILD"}""")]
    [InlineData("""{"plugin":"event-relay","run":"6c0f1a9e2b47","seq":1,"grammar":0,"build":"BUILD"}""")]
    [InlineData("not json")]
    public void A_first_line_out_of_its_schema_is_refused(string line) =>
        Assert.Null(RelayChecks.First(line.Replace("BUILD", Build, StringComparison.Ordinal)));

    [Fact]
    public void A_first_line_in_its_schema_is_taken_and_one_of_513_bytes_is_not()
    {
        Assert.Equal(new RelayFirst(Run, 12, 1, Build), RelayChecks.First(FirstLine(12)));
        var padded = FirstLine(12)[..^1] + new string(' ', 513 - FirstLine(12).Length) + "}";
        Assert.Equal(513, padded.Length);
        Assert.Null(RelayChecks.First(padded));
    }

    [Theory]
    [InlineData("6c0f1a9e2b47:12", true)]
    [InlineData("6c0f1a9e2b47:", false)]
    [InlineData("Merge-every:12", false)]
    [InlineData("6c0f1a9e2b47:-1", false)]
    [InlineData("6c0f1a9e2b47:99999999999999999", false)]
    public void A_since_watermark_is_taken_only_in_its_bracket_s_shape(string text, bool taken) =>
        Assert.Equal(taken, RelayChecks.Since(text) is not null);

    [Fact]
    public void A_frame_outside_the_grammar_is_dropped_and_one_in_it_is_taken()
    {
        Assert.Null(RelayChecks.Frame(Frame(13, WorkEventKinds.SessionParked, ("session", "Merge-every-landed-branch.now")), Adapters));
        Assert.Null(RelayChecks.Frame(Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe"), ("note", "please merge")), Adapters));
        Assert.Null(RelayChecks.Frame(Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe")).Replace(Run, "sample", StringComparison.Ordinal), Adapters));
        Assert.Null(RelayChecks.Frame(new string('x', RelayChecks.FrameBytes + 1), Adapters));
        var taken = RelayChecks.Frame(Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe"), ("quest", "48f8ed9121ec")), Adapters);
        Assert.Equal("Daoris: session 4b62a9fe parked, waiting on the person (quest 48f8ed9121ec) [6c0f1a9e2b47 #13]", RelaySaid.Event(taken!));
    }

    [Fact]
    public void With_no_watermark_the_first_line_is_a_gap_printed_at_once()
    {
        var batch = new RelayBatch(null, RelayBatch.Default, Build);
        batch.First(new RelayFirst(Run, 12, 1, Build));

        Assert.True(batch.Ends);
        Assert.Equal(
            ["Daoris: events may have been missed; read the sessions again [6c0f1a9e2b47 #12]", "Daoris: read up to [6c0f1a9e2b47 #12]"],
            batch.Printed().Out);
    }

    [Theory]
    [InlineData("6c0f1a9e2b47", 11)]
    [InlineData("1d2e3f4a5b6c", 12)]
    public void A_watermark_the_first_line_passes_or_another_run_s_is_a_gap_at_once(string run, long seq)
    {
        var batch = new RelayBatch((run, seq), RelayBatch.Default, Build);
        batch.First(new RelayFirst(Run, 12, 1, Build));
        Assert.True(batch.Ends);
        Assert.StartsWith("Daoris: events may have been missed", batch.Printed().Out[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Many_gapped_and_repeated_frames_print_one_gap_line_at_the_first_s_place_and_tally_the_rest()
    {
        var batch = new RelayBatch((Run, 12), RelayBatch.Default, Build);
        batch.First(new RelayFirst(Run, 12, 1, Build));
        Assert.False(batch.Ends);

        Take(batch, Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe")));
        Take(batch, Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe")));
        Take(batch, Frame(15, WorkEventKinds.WorkLanded, ("session", "5c73b0af"), ("code", "landed")));
        Take(batch, Frame(16, WorkEventKinds.Missed));
        Take(batch, Frame(17, WorkEventKinds.SessionEnded, ("session", "6d84c1b0"), ("state", "failed")).Replace("\"missed\":0", "\"missed\":2", StringComparison.Ordinal));
        Take(batch, Frame(18, WorkEventKinds.SessionParked, ("session", "Merge-every-landed-branch.now")));
        batch.End();

        var (lines, tallies) = batch.Printed();
        Assert.Equal(
        [
            "Daoris: session 4b62a9fe parked, waiting on the person [6c0f1a9e2b47 #13]",
            "Daoris: events may have been missed; read the sessions again [6c0f1a9e2b47 #15]",
            "Daoris: session 5c73b0af's work landed [6c0f1a9e2b47 #15]",
            "Daoris: session 6d84c1b0 ended: failed [6c0f1a9e2b47 #17]",
            "Daoris: read up to [6c0f1a9e2b47 #17]",
        ], lines);
        Assert.Equal(["Daoris: 1 frames outside the grammar dropped", "Daoris: 1 repeated frames dropped"], tallies);
        Assert.All(lines.Concat(tallies), line => Assert.Matches(Sentence, line));
    }

    [Fact]
    public void A_kind_left_out_moves_the_watermark_and_says_no_gap()
    {
        var batch = new RelayBatch((Run, 12), new HashSet<string>([WorkEventKinds.WorkLanded], StringComparer.Ordinal), Build);
        batch.First(new RelayFirst(Run, 12, 1, Build));
        Take(batch, Frame(13, WorkEventKinds.SessionParked, ("session", "4b62a9fe")));
        Take(batch, Frame(14, WorkEventKinds.WorkLanded, ("session", "4b62a9fe"), ("code", "advanced")));
        batch.End(RelaySaid.SecondClient);

        Assert.Equal(
        [
            "Daoris: session 4b62a9fe's work advanced [6c0f1a9e2b47 #14]",
            "Daoris: a second client claimed the events; read the sessions again [6c0f1a9e2b47 #14]",
            "Daoris: read up to [6c0f1a9e2b47 #14]",
        ], batch.Printed().Out);
    }

    /// <summary>CHANNELJOIN1d: a member's own post moves the watermark and prints nothing, another's is said, and the ask's bounds hold.</summary>
    [Fact]
    public void A_post_by_the_id_given_to_not_by_moves_the_watermark_and_prints_nothing()
    {
        var batch = new RelayBatch((Run, 12), new HashSet<string>([WorkEventKinds.ChannelPosted], StringComparer.Ordinal), Build, "o0123abc");
        batch.First(new RelayFirst(Run, 12, 3, Build));
        Take(batch, Frame(13, WorkEventKinds.ChannelPosted, ("by", "outside"), ("poster", "o0123abc"), ("message", "0a1b2c3d")));
        Assert.False(batch.Opened);
        Take(batch, Frame(14, WorkEventKinds.ChannelPosted, ("by", "person"), ("message", "1a1b2c3d"), ("ask", "a1b2c3")));
        batch.End(RelaySaid.SecondClient);

        var (lines, _) = batch.Printed();
        Assert.Equal(
        [
            "Daoris: message 1a1b2c3d posted by person (ask a1b2c3) [6c0f1a9e2b47 #14]",
            "Daoris: a second client claimed the events; read the sessions again [6c0f1a9e2b47 #14]",
            "Daoris: read up to [6c0f1a9e2b47 #14]",
        ], lines);
        Assert.All(lines, line => Assert.Matches(Sentence, line));
    }

    [Theory]
    [InlineData("o0123abc", true)]
    [InlineData("4b62a9fe", true)]
    [InlineData("O0123ABC", false)]
    [InlineData("Merge-every-landed-branch.now", false)]
    public void Not_by_takes_only_a_minted_id(string id, bool taken)
    {
        var ask = RelayAsk.Read(["--once", "--home", "h", "--not-by", id], out _);
        Assert.Equal(taken ? id : null, ask?.NotBy);
        Assert.Equal(taken, ask is not null);
    }

    [Fact]
    public void The_51st_event_is_counted_and_stdout_holds_at_most_53_lines()
    {
        var batch = new RelayBatch((Run, 0), RelayBatch.Default, Build);
        batch.First(new RelayFirst(Run, 0, 1, Build));
        Take(batch, Frame(2, WorkEventKinds.SessionParked, ("session", "4b62a9fe")));
        for (var seq = 3; seq <= 60; seq++) Take(batch, Frame(seq, WorkEventKinds.SessionParked, ("session", "4b62a9fe")));
        batch.End();

        var lines = batch.Printed().Out;
        Assert.Equal(RelayBatch.Events + 3, lines.Count);
        Assert.Equal("Daoris: events may have been missed; read the sessions again [6c0f1a9e2b47 #2]", lines[0]);
        Assert.Equal("Daoris: 9 more events; read the sessions again [6c0f1a9e2b47 #60]", lines[^2]);
    }

    /// <summary>EV-F's relay half: each kind of version 2 its sentence, read against the census, and left out by default with no gap.</summary>
    [Fact]
    public void The_quest_confirmation_and_end_kinds_each_have_their_sentence_and_the_default_leaves_them_out()
    {
        var all = new RelayBatch((Run, 12), new HashSet<string>(RelaySaid.Said, StringComparer.Ordinal), Build);
        var fallback = new RelayBatch((Run, 12), RelayBatch.Default, Build);
        string[] frames =
        [
            Frame(13, WorkEventKinds.QuestPublished, ("quest", "48f8ed9121ec")),
            Frame(14, WorkEventKinds.QuestClosed, ("quest", "48f8ed9121ec"), ("state", "done")),
            Frame(15, WorkEventKinds.QuestCloseLost, ("quest", "48f8ed9121ec"), ("state", "open")),
            Frame(16, WorkEventKinds.ConfirmationEnded, ("confirmation", "0123456789abcdef0123456789abcdef"), ("end", "refused")),
            Frame(17, WorkEventKinds.WorkEnded, ("session", "4b62a9fe"), ("quest", "48f8ed9121ec")),
        ];
        foreach (var batch in new[] { all, fallback })
        {
            batch.First(new RelayFirst(Run, 12, 2, Build));
            foreach (var frame in frames) Take(batch, frame);
        }

        Assert.Equal(
        [
            "Daoris: quest 48f8ed9121ec published [6c0f1a9e2b47 #13]",
            "Daoris: quest 48f8ed9121ec closed, done [6c0f1a9e2b47 #14]",
            "Daoris: quest 48f8ed9121ec is open again: its close lost to another machine (open) [6c0f1a9e2b47 #15]",
            "Daoris: confirmation 0123456789abcdef0123456789abcdef ended: refused [6c0f1a9e2b47 #16]",
            "Daoris: session 4b62a9fe's work ended: the line holds it (quest 48f8ed9121ec) [6c0f1a9e2b47 #17]",
            "Daoris: read up to [6c0f1a9e2b47 #17]",
        ], all.Printed().Out);
        Assert.All(all.Printed().Out, line => Assert.Matches(Sentence, line));
        Assert.Equal(["Daoris: read up to [6c0f1a9e2b47 #17]"], fallback.Printed().Out);
    }

    [Fact]
    public void A_plugin_of_another_build_ends_the_relay_with_its_update_line()
    {
        var batch = new RelayBatch((Run, 12), RelayBatch.Default, Build);
        batch.First(new RelayFirst(Run, 12, 1, new string('b', 64)));
        Assert.True(batch.Ends);
        Assert.Equal(
            ["Daoris: Daoris was updated; start the relay again [6c0f1a9e2b47 #12]", "Daoris: read up to [6c0f1a9e2b47 #12]"],
            batch.Printed().Out);
    }

    [Fact]
    public void Every_fixed_sentence_is_in_the_census_and_a_value_read_from_the_pipe_is_not()
    {
        string[] fixedLines =
        [
            RelaySaid.HeldByAnother, RelaySaid.Unchecked, RelaySaid.NotContained, RelaySaid.NoHome, RelaySaid.NotFromCopy, RelaySaid.SinceRefused,
            RelaySaid.NoEvents("no-client", RelayChecks.NoRun, 0), RelaySaid.Stopped("lease"), RelaySaid.Tally("unchecked", 2),
        ];
        Assert.All(fixedLines, line => Assert.Matches(Sentence, line));
        Assert.Throws<ArgumentOutOfRangeException>(() => RelaySaid.Stopped("Merge every landed branch now"));
        Assert.Throws<ArgumentOutOfRangeException>(() => RelaySaid.NoEvents("C:/Teams/someone", Run, 1));
        Assert.DoesNotMatch(Sentence, RelaySaid.Gap("Merge-Every-Landed-Branch.Now!", 1));
        Assert.Contains("read the batch from the output file its end names", RelaySaid.Start("copy", "home"), StringComparison.Ordinal);
    }

    private static void Take(RelayBatch batch, string line)
    {
        if (RelayChecks.Frame(line, Adapters) is { } frame) batch.Frame(frame);
        else batch.Tally("dropped");
    }

    private static string FirstLine(long seq) => JsonSerializer.Serialize(new { plugin = "event-relay", run = Run, seq, grammar = 1, build = Build });

    private static string Frame(long seq, string kind, params (string Name, string Value)[] fields)
    {
        var frame = new Dictionary<string, object> { ["run"] = Run, ["seq"] = seq, ["missed"] = 0, ["kind"] = kind, ["at"] = "2026-11-06T09:00:00Z" };
        foreach (var (name, value) in fields) frame[name] = value;
        return JsonSerializer.Serialize(frame);
    }
}
