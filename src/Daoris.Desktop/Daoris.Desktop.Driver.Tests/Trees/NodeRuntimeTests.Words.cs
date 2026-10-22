using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1f (D190 points 3 and 6; the cutover design §3, §5.2): the person's skip and stop of a graph run's node, kept on the run's
/// record by their door under its lock and the lease, in a field of its own, and applied by the runtime's next pass through the one table.
/// Each seen failing first.
/// </summary>
public sealed partial class NodeRuntimeTests
{
    /// <summary>The go-ahead waits on an ask that raised none: nobody will answer it.</summary>
    private WorkUnit NobodyRaised() =>
        Assert.Single(AskUnits.Read([new AskView("a1", "default", "Ship it.", "Published", "repository") { Quests = [RunId] }]));

    private NodeWorded Word(string node, string act, string? words, string run = RunId) => RunNodeWords.Word(Files, run, node, act, words, () => _now);

    /// <summary>A run whose go-ahead was begun on an ask that raised none, its work done and its branch read: the go-ahead waits, the landing behind it.</summary>
    private async Task<NodeRuntime> GoAheadWaitingAsync()
    {
        BoundTo(GoAheadBeforeHanded, ask: "a1");
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]), NobodyRaised()];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (RunStatus("go").State, RunStatus("handed").State));
        return runtime;
    }

    /// <summary>
    /// §5.2: a go-ahead nobody raised never strands a run. The person's skip, with their words, is kept on the run's record by the door and moves
    /// nothing there; the next pass, a wake, applies it through the one table, scoped to the run, and <c>handed</c> begins; the pass says so.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_nobody_raised_skipped_with_words_lets_handed_begin()
    {
        var runtime = await GoAheadWaitingAsync();

        var worded = Word("go", NodeWordActs.Skip, "nobody will raise it; I checked the release myself");
        Assert.True(worded.Done, worded.Refused);
        var kept = Files.Read(RunUnit)!;
        Assert.Equal(("go", NodeWordActs.Skip, "nobody will raise it; I checked the release myself", _now),
            (kept.Pending.Single().Node, kept.Pending.Single().Act, kept.Pending.Single().Words, kept.Pending.Single().At));
        Assert.Equal(NodeStates.Active, kept.Statuses["go"].State);

        var pass = await RunPassAsync(runtime, whole: false);

        var go = RunStatus("go");
        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "nobody will raise it; I checked the release myself"), (go.State, go.By, go.Words));
        // Scoped to the run and its version's graph, as the runtime scopes its starts, so the skip holds for this run alone.
        Assert.Equal((RunId, true), (go.Scope!.Run, go.Scope.GraphDigest.StartsWith("gph-", StringComparison.Ordinal)));
        Assert.Equal((NodeStates.Active, "unit-b1"), (RunStatus("handed").State, RunStatus("handed").Binding!.Subject));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Single(pass.Said, line => line.StartsWith($"work  {RunUnit}:", StringComparison.Ordinal) && line.Contains("your skip of `go`", StringComparison.Ordinal));
        Assert.Contains(RunUnit, pass.Evaluated);

        // The skip holds: a whole look judges the run again and leaves it passed over.
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Skipped, RunStatus("go").State);
    }

    /// <summary>A skip passes a node over with the person's words, and none were given: refused at the door, nothing kept.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_skip_with_no_words_is_refused_and_nothing_is_kept(string? words)
    {
        await GoAheadWaitingAsync();

        var worded = Word("go", NodeWordActs.Skip, words);

        Assert.Equal(NodeRefusal.NoWords, worded.Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Contains("say why", RunNodeWords.Said(worded), StringComparison.Ordinal);
    }

    /// <summary>
    /// The person's stop of the go-ahead, words or none: the next pass stops it, nothing of the graph moves again, and the run ends on its
    /// record, said (§3's end).
    /// </summary>
    [Fact]
    public async Task A_stop_ends_the_run_said()
    {
        var runtime = await GoAheadWaitingAsync();

        Assert.True(Word("go", NodeWordActs.Stop, null).Done);
        var pass = await RunPassAsync(runtime, whole: false);

        Assert.Equal((NodeStates.Stopped, NodeSource.Person), (RunStatus("go").State, RunStatus("go").By));
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);
        Assert.Equal(RunId, Files.Read(RunUnit)!.Ended?.Quest);
        Assert.Single(pass.Said, line => line.Contains("your stop of `go`", StringComparison.Ordinal));
        Assert.Single(pass.Said, line => line.Contains("its run ended at #q7", StringComparison.Ordinal));
    }

    /// <summary>
    /// What the door refuses before anything is kept, each in its code: a run bound to no graph version here, a node its graph does not hold, a
    /// word that is neither, a node the person may not pass over, a node the table moves nowhere from where it stands, a second word on a node
    /// before the first is applied, and a run that ended.
    /// </summary>
    [Fact]
    public async Task The_door_refuses_what_the_table_or_the_run_does_not_take()
    {
        var runtime = await GoAheadWaitingAsync();

        Assert.Equal(RunNodeWords.NotRun, Word("go", NodeWordActs.Skip, "why", run: "q9").Refused);
        Assert.Equal(RunNodeWords.NotNode, Word("deploy", NodeWordActs.Stop, null).Refused);
        Assert.Equal(RunNodeWords.NotAct, Word("go", "drop", "why").Refused);
        Assert.Equal(NodeRefusal.NotSkippable, Word("handed", NodeWordActs.Skip, "why").Refused);
        Assert.Equal(NodeRefusal.NotInTable, Word("claimed", NodeWordActs.Stop, null).Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);

        Assert.True(Word("go", NodeWordActs.Stop, "not this one").Done);
        Assert.Equal(RunNodeWords.Worded, Word("go", NodeWordActs.Skip, "after all").Refused);
        Assert.Equal("not this one", Assert.Single(Files.Read(RunUnit)!.Pending).Words);

        await RunPassAsync(runtime, whole: false);
        Assert.Equal(RunNodeWords.Ended, Word("handed", NodeWordActs.Stop, null).Refused);
    }

    /// <summary>The lease's holder alone keeps a word: a process that does not hold it keeps nothing, and says where to say it.</summary>
    [Fact]
    public async Task A_process_without_the_lease_keeps_no_word()
    {
        await GoAheadWaitingAsync();

        var worded = RunNodeWords.Word(new NodeRecordFiles(_home, () => false), RunId, "go", NodeWordActs.Stop, null, () => _now);

        Assert.Equal(NodeRecordFiles.NotHeld, worded.Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    /// <summary>
    /// A word whose node moved before the pass came (here, reached) is not applied: the table refuses it at the pass, the word is taken off the
    /// record, and the pass says why, so nothing waits on a word that can never apply.
    /// </summary>
    [Fact]
    public async Task A_word_the_table_refuses_at_the_pass_is_taken_off_and_said()
    {
        var runtime = await GoAheadWaitingAsync();
        Files.Write(Files.Read(RunUnit)! with { Pending = [new NodeWord("claimed", NodeWordActs.Stop, "too late", _now)] });

        var pass = await RunPassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Reached, RunStatus("claimed").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Single(pass.Said, line => line.Contains("your stop of `claimed` was not applied", StringComparison.Ordinal)
            && line.Contains(NodeRefusal.NotInTable, StringComparison.Ordinal));
    }
}
