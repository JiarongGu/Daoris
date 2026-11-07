using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The rules the helper's, the partner's and the opinion's looks hand a solicited answer
/// by, in one place. An attempt is asked again under its key only inside the settlement's horizon; the line is read forward to its head as the
/// door reads it; a head takes the answer between turns, an answered park included; and the door's answer is read as kept, already waiting
/// with the record that holds the word, settled with no word, refused with nothing kept, or uncertain.
/// </summary>
public sealed class SolicitedHandTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-03T10:00:00Z");

    private static PriorSession Record(string id, string state = "completed", string? from = null, int minute = 0, string kind = "driven", string? quest = null) =>
        new(id, "trees/s", state) { From = from, Created = At.AddMinutes(minute), Kind = kind, Quest = quest };

    [Fact]
    public void An_attempt_is_asked_again_under_its_key_only_inside_the_horizon()
    {
        Assert.Equal(SolicitedStep.Hand, SolicitedHand.Before(null, At));
        Assert.Equal(SolicitedStep.Retry, SolicitedHand.Before(At, At.AddMinutes(1)));
        Assert.Equal(SolicitedStep.Retry, SolicitedHand.Before(At, At + SolicitedHand.Horizon - TimeSpan.FromSeconds(1)));
        Assert.Equal(SolicitedStep.Uncertain, SolicitedHand.Before(At, At + SolicitedHand.Horizon));
        Assert.Equal(TimeSpan.FromDays(7), SolicitedHand.Horizon);
    }

    [Fact]
    public void The_line_is_walked_by_from_to_its_newest_part_and_a_driven_parts_later_run()
    {
        PriorSession[] records =
        [
            Record("s1", "stopped", quest: "q1"), Record("s2", "completed", from: "s1", minute: 1, kind: "chat"),
            Record("s3", "stopped", from: "s1", minute: 2, quest: "q1"), Record("s4", "working", minute: 5, quest: "q1"),
            Record("h1", "completed", minute: 9, kind: "chat", quest: "q1"),
        ];

        var line = SolicitedHand.Walk(records, "s1");

        Assert.Equal("s1", line.Named!.Session);
        Assert.Equal("s4", line.Head!.Session);
        Assert.Equal(["s1", "s3", "s4"], line.Records.Select(record => record.Session));
        Assert.Equal(SolicitedLineEnd.Head, line.End);
        Assert.Equal("s4", SolicitedHand.Walk(records, "s4").Head!.Session);
    }

    [Fact]
    public void A_line_that_loops_or_runs_past_the_bound_has_no_head_and_a_record_not_here_has_none()
    {
        PriorSession[] loop = [Record("s1", from: "s2"), Record("s2", from: "s1", minute: 1)];
        var looped = SolicitedHand.Walk(loop, "s1");
        Assert.Null(looped.Head);
        Assert.Equal(SolicitedLineEnd.Cycle, looped.End);

        var parts = Enumerable.Range(0, SolicitedHand.Bound + 2)
            .Select(at => Record($"p{at}", from: at == 0 ? null : $"p{at - 1}", minute: at)).ToList();
        var past = SolicitedHand.Walk(parts, "p0");
        Assert.Null(past.Head);
        Assert.Equal(SolicitedLineEnd.Bound, past.End);
        Assert.Equal($"p{SolicitedHand.Bound}", SolicitedHand.Walk(parts.Take(SolicitedHand.Bound + 1), "p0").Head!.Session);

        var gone = SolicitedHand.Walk(loop, "s9");
        Assert.Null(gone.Named);
        Assert.Null(gone.Head);
    }

    [Theory]
    [InlineData("completed", "driven", null, true)]
    [InlineData("stopped", "driven", null, true)]
    [InlineData("working", "driven", null, false)]
    [InlineData("starting", "chat", null, false)]
    [InlineData("working", "chat", null, true)]
    [InlineData("awaiting-person", "driven", null, false)]
    [InlineData("awaiting-person", "driven", "Yes, go on.", true)]
    public void A_head_takes_the_answer_between_turns_and_an_answered_park_takes_it_beside_the_persons(string state, string kind, string? answer, bool takes)
    {
        Assert.Equal(takes, SolicitedHand.TakesNow(new PriorSession("s1", "trees/s", state) { Kind = kind, Said = answer is null ? null : PersonWords.Of(answer) }));
    }

    private static SolicitedAnswer Read(int status, string? payload) =>
        SolicitedHand.Read(
            status is >= 200 and < 300, status, payload is null ? null : JsonDocument.Parse(payload).RootElement.Clone(), payload ?? "",
            "/api/helpers/h1/hand", "http://stand-in");

    [Fact]
    public void The_doors_answer_is_read_kept_waiting_settled_refused_or_uncertain()
    {
        var kept = Read(200, """{"message":"Handed.","session":{"id":"s2","state":"completed","kind":"chat"},"said":{"id":"w1","text":"the answer"}}""");
        Assert.Equal((SolicitedFate.Kept, "w1", "the answer", "s2", "chat"), (kept.Fate, kept.Word, kept.Text, kept.Holder!.Session, kept.Holder.Kind));

        var waiting = Read(200, """{"message":"Waits already.","waiting":true,"session":{"id":"s3","state":"working"},"said":{"id":"w0","text":"the answer"}}""");
        Assert.Equal((SolicitedFate.Waiting, "w0", "s3", "working"), (waiting.Fate, waiting.Word, waiting.Holder!.Session, waiting.Holder.State));

        var settled = Read(200, """{"message":"Settled.","settled":"s1"}""");
        Assert.Equal((SolicitedFate.Settled, "s1", null), (settled.Fate, settled.SettledOn, settled.Word));

        var refused = Read(409, """{"error":"Session `s1` is working. Nothing was kept."}""");
        Assert.Equal((SolicitedFate.Refused, "Session `s1` is working. Nothing was kept."), (refused.Fate, refused.Message));

        Assert.Equal(SolicitedFate.Uncertain, Read(500, """{"error":"An unexpected error."}""").Fate);
        Assert.Equal(SolicitedFate.Uncertain, Read(200, null).Fate);
        Assert.Equal(SolicitedFate.Refused, Read(404, null).Fate);
        Assert.Contains("older than this driver", Read(404, null).Message);
        Assert.Equal(SolicitedFate.Uncertain, SolicitedHand.Lost("/api/helpers/h1/hand", "http://stand-in", "timed out").Fate);
    }
}
