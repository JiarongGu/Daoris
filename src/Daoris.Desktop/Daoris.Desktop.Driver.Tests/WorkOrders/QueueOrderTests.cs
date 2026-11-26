using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one comparator: effective class, then stage (a landing, a run back at its checks, a resume, first checks, a new start),
/// then first arrival; and aging by promotion, one class up past the class's bound, never into urgent.
/// </summary>
public sealed class QueueOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static QueuePlace Place(string @class, QueueStage stage, int minutesAgo) => new(@class, stage, Now.AddMinutes(-minutesAgo));

    [Fact]
    public void Class_comes_before_stage_and_stage_before_arrival()
    {
        var places = new Dictionary<string, QueuePlace>
        {
            ["normal new start, oldest"] = Place(PriorityClasses.Normal, QueueStage.NewStart, 500),
            ["normal resume"] = Place(PriorityClasses.Normal, QueueStage.Resume, 10),
            ["normal back at checks"] = Place(PriorityClasses.Normal, QueueStage.BackAtChecks, 5),
            ["normal first checks"] = Place(PriorityClasses.Normal, QueueStage.FirstChecks, 400),
            ["normal landing"] = Place(PriorityClasses.Normal, QueueStage.Landing, 1),
            ["high new start"] = Place(PriorityClasses.High, QueueStage.NewStart, 1),
            ["low landing"] = Place(PriorityClasses.Low, QueueStage.Landing, 900),
            ["normal resume, older"] = Place(PriorityClasses.Normal, QueueStage.Resume, 30),
        };

        var ordered = QueueOrder.Of(places.Keys.Reverse(), key => places[key]).ToList();

        Assert.Equal(
            ["high new start", "normal landing", "normal back at checks", "normal resume, older", "normal resume", "normal first checks", "normal new start, oldest", "low landing"],
            ordered);
    }

    [Fact]
    public void Stages_rank_a_landing_first_and_a_new_start_last()
    {
        Assert.Equal(
            [QueueStage.Landing, QueueStage.BackAtChecks, QueueStage.Resume, QueueStage.FirstChecks, QueueStage.NewStart],
            Enum.GetValues<QueueStage>().OrderBy(stage => (int)stage));
    }

    [Theory]
    [InlineData(PriorityClasses.Normal, 119, PriorityClasses.Normal, null)]
    [InlineData(PriorityClasses.Normal, 121, PriorityClasses.High, PriorityClasses.Normal)]
    [InlineData(PriorityClasses.Low, 479, PriorityClasses.Low, null)]
    [InlineData(PriorityClasses.Low, 481, PriorityClasses.Normal, PriorityClasses.Low)]
    [InlineData(PriorityClasses.High, 5000, PriorityClasses.High, null)]
    [InlineData(PriorityClasses.Urgent, 5000, PriorityClasses.Urgent, null)]
    public void An_item_past_its_class_s_bound_is_ordered_one_class_up_and_says_from_which(string @class, int minutes, string expected, string? from)
    {
        var aged = QueueOrder.Age(@class, Now.AddMinutes(-minutes), Now, PriorityRules.Default);

        Assert.Equal((expected, from), (aged.Class, aged.From));
        Assert.Equal(from is null ? null : $"aged from {from}", aged.Said);
    }

    [Fact]
    public void A_workspace_s_bounds_replace_the_defaults()
    {
        var rules = PriorityRules.Default with { AgeNormal = TimeSpan.FromMinutes(30) };

        Assert.Equal(PriorityClasses.High, QueueOrder.Age(PriorityClasses.Normal, Now.AddMinutes(-31), Now, rules).Class);
    }

    [Fact]
    public void An_arrival_not_known_never_ages_and_a_word_that_is_no_class_reads_as_normal()
    {
        Assert.Equal((PriorityClasses.Normal, null), (QueueOrder.Age(null, null, Now, PriorityRules.Default).Class, QueueOrder.Age(null, null, Now, PriorityRules.Default).From));
        Assert.Equal(PriorityClasses.High, QueueOrder.Age("whenever", Now.AddDays(-1), Now, PriorityRules.Default).Class);
    }
}
