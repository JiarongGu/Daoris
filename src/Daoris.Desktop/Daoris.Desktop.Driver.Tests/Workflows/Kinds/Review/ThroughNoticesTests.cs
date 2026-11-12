using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A step the runtime passed on the person's through words: its run's step names the quote and the ask (<see cref="ThroughAccepts.On"/>),
/// and <i>Done and failed</i> holds one notice by run, node and attempt (<see cref="ThroughNotices"/>), never for a word of their own press
/// or a supervisor's.
/// </summary>
public sealed class ThroughNoticesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "through-notices", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"landing",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"holds","kind":"quest.holds","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"daoris/{quest}"}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"holds"},{"id":"b","kind":"after","from":"holds","to":"review"},
                  {"id":"c","kind":"after","from":"review","to":"landing"}]}
        """;

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void A_step_passed_on_the_persons_words_names_the_quote_and_the_ask_and_no_other_word_does()
    {
        var unit = RunUnits.IdOf("q1");
        var words = ThroughAccepts.Words("a1", "please do \"it\" without my accept");
        var accepted = new NodeStatus(unit, "review", NodeStates.Reached) { By = NodeSource.Person, Words = words };

        Assert.Equal(new RunStepThrough("a1", "please do \"it\" without my accept"), ThroughAccepts.On(accepted));
        Assert.Equal(new RunStepThrough("a1", "ship"), ThroughAccepts.On(new NodeStatus(unit, "holds", NodeStates.Skipped) { By = NodeSource.Person, Words = ThroughAccepts.Words("a1", "ship") }));
        Assert.Null(ThroughAccepts.On(accepted with { By = NodeSource.Supervisor }));
        Assert.Null(ThroughAccepts.On(accepted with { Words = "looks right" }));
        Assert.Null(ThroughAccepts.On(accepted with { State = NodeStates.Active }));
    }

    [Fact]
    public void Each_step_passed_on_words_is_one_notice_by_run_node_and_attempt_and_a_press_of_the_persons_is_none()
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["holds"] = new(unit, "holds", NodeStates.Skipped) { Attempt = 1, Since = T, By = NodeSource.Person, Words = ThroughAccepts.Words("a1", "ship it") },
                ["review"] = new(unit, "review", NodeStates.Reached) { Attempt = 2, Since = T.AddMinutes(1), By = NodeSource.Person, Words = ThroughAccepts.Words("a1", "ship it") },
                ["landing"] = new(unit, "landing", NodeStates.Reached) { Attempt = 1, Since = T.AddMinutes(2), By = NodeSource.Git, Words = "landed" },
            },
        });

        var told = ThroughNotices.Occurrences(_home, T.AddMinutes(5));

        Assert.Equal(["through:q1/holds/1", "through:q1/review/2"], told.Select(each => each.Key));
        var review = told[1].Notice!;
        Assert.Equal((ThroughNotices.Kind, "q1", "review", NodeKinds.Review, NodeStates.Reached, "a1", 2),
            (review.Kind, review.Quest, review.Act, review.Route, review.State, review.Code, review.Count));
        Assert.Empty(ThroughNotices.Occurrences(_home, T.AddDays(8)));
    }
}
