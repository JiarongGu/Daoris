using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A second opinion's consultation record, in the brainstorm's <c>consultation.json</c> shape: its mode, maker and
/// product, what it read, whether it saw another answer, what of independence did not hold, and the decider's line once answered.
/// </summary>
public sealed class OpinionConsultationTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "opinion-consultation", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static OpinionView View(string label, string? rechecks = null) => new(
        "op1", OpinionRules.Landing, rechecks is null ? "first" : "recheck", "s1", "r1", "reports", "1111111", "2222222", "codex-acp", label, "given")
    {
        Maker = "OpenAI", Product = "Codex", Rechecks = rechecks, Read = "the diff and its two tests",
    };

    private static OpinionReading Answers(params string[] counts) =>
        new("s1", "3333333", DateTimeOffset.Parse("2026-11-16T10:00:00Z"), [.. counts.Select((each, at) => new OpinionAnswerRead(at + 1, OpinionViews.Must, each))]);

    /// <summary>🔴 A same-maker opinion says it is not independent; another maker's first pass holds every condition.</summary>
    [Theory]
    [InlineData(ReviewerLabels.SameAgent, new[] { Consultations.SameMaker })]
    [InlineData(ReviewerLabels.MakerNotDeclared, new[] { Consultations.MakerNotDeclared })]
    [InlineData(ReviewerLabels.AnotherMaker, new string[0])]
    public void A_same_maker_opinion_says_it_is_not_independent(string label, string[] notHeld)
    {
        var giver = Assert.Single(Consultations.OfOpinion(View(label), null).Givers);

        Assert.Equal((Consultations.ReviewMode, "OpenAI", "Codex", "the diff and its two tests", false), (giver.Mode, giver.Maker, giver.Adapter, giver.Read, giver.SawAnother));
        Assert.Equal(notHeld, giver.NotHeld);
    }

    /// <summary>🔴 A recheck read the first pass's findings: it saw another answer, and says so.</summary>
    [Fact]
    public void A_recheck_saw_another_answer()
    {
        var giver = Assert.Single(Consultations.OfOpinion(View(ReviewerLabels.AnotherMaker, rechecks: "op0"), null).Givers);

        Assert.True(giver.SawAnother);
        Assert.Equal([Consultations.SawAnother], giver.NotHeld);
    }

    /// <summary>🔴 The decider's line is the working session's answers: none before it answered, then how many findings changed the work.</summary>
    [Fact]
    public void The_deciders_line_is_how_many_findings_changed_the_work()
    {
        Assert.Null(Consultations.OfOpinion(View(ReviewerLabels.AnotherMaker), null).Changed);
        Assert.Equal("1 of 2 findings changed the work",
            Consultations.OfOpinion(View(ReviewerLabels.AnotherMaker), Answers(OpinionViews.Fixed, OpinionViews.Rejected)).Changed);
    }

    /// <summary>🔴 The record is kept beside the opinion's delivery in the brainstorm's shape.</summary>
    [Fact]
    public void The_record_is_kept_beside_the_delivery_in_the_brainstorms_shape()
    {
        var deliveries = new OpinionDeliveries(_home);
        var view = View(ReviewerLabels.SameAgent);
        deliveries.Answered(view.Id, Answers(OpinionViews.Fixed));
        deliveries.Consulted(view, Answers(OpinionViews.Fixed));

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(OpinionPackets.Folder(_home, "op1"), OpinionDeliveries.ConsultationFile)));
        var root = json.RootElement;
        Assert.Equal((1, "opinion", "op1", false), (root.GetProperty("consultation").GetInt32(), root.GetProperty("kind").GetString(),
            root.GetProperty("subject").GetString(), root.GetProperty("independent").GetBoolean()));
        var giver = root.GetProperty("givers")[0];
        Assert.Equal(("review", "OpenAI", Consultations.SameMaker), (giver.GetProperty("mode").GetString(), giver.GetProperty("maker").GetString(),
            giver.GetProperty("notHeld")[0].GetString()));
        Assert.Equal("1 of 1 findings changed the work", root.GetProperty("changed").GetString());
    }
}
