using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-NODE1 (the fabric events design §E2): one lifecycle for every node kind. The table holds the only moves, every kind passes
/// the same transition tests (canary 43), and what differs by kind is only who may report each outcome. Silence and a deadline never
/// fail a node, a claim is no evidence, a retry is bounded, and an unknown outcome is reconciled before any new attempt.
/// FABRIC-NODE1c (a second maker's review): every outcome is bound to its attempt and a reach to its attempt's whole binding, a
/// dispatched attempt is reconciled before another starts, a move returns what it cleared, and a skip holds for its graph and run.
/// </summary>
public sealed class NodeLifecycleTests
{
    private const string Unit = "unit-a";

    private static readonly string[] Causes = [NodeCause.Daoris, NodeCause.Authority, NodeCause.Person];

    private static readonly NodeScope Run = new("graph-1", "run-1");

    /// <summary>Every built-in kind, and a plugin's own node: the same table for each.</summary>
    public static TheoryData<string> Kinds() => new([.. NodeKinds.BuiltIn, "acme.deploy/deploy"]);

    private static NodeDeclaration Declared(string kind, NodeRetry? retry = null, bool rejectable = false) =>
        new("n1", kind, 1, NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed)
        {
            Retry = retry ?? new NodeRetry(3, [5, 30]),
            Rejectable = rejectable,
            // FABRIC-UNIT1d: a plugin's performed node retries only a transient code, and only where it is idempotent or reconciled.
            Perform = builtIn is null ? Retrying : null,
        };

    private static readonly NodePerform Retrying = new(Idempotent: true, Reconcile: false, ["unavailable"]);

    /// <summary>The binding an attempt starts on: what Daoris's evaluation hands the start.</summary>
    private static NodeAcceptance Bind(int attempt, string node = "n1") => new(Unit, "tip-1", "graph-1", node, 1, attempt);

    /// <summary>That binding reached, with its evidence.</summary>
    private static NodeAcceptance Accept(int attempt, string node = "n1") =>
        Bind(attempt, node) with { Evidence = new NodePort("acme/receipt@1", $"{node}#{attempt}", "tip-1", null) };

    private static NodeMove Start(int attempt) => new(NodeStates.Active, NodeCause.Daoris) { Binding = Bind(attempt), Scope = Run };

    private static NodeStatus At(string state, int attempt = 1, int tries = 1, bool transient = true, int? unreconciled = null) =>
        new(Unit, "n1", state)
        {
            Attempt = attempt,
            Tries = tries,
            Transient = transient,
            Code = state == NodeStates.Failed ? "deploy-failed" : null,
            Unreconciled = unreconciled,
            Binding = NodeStates.Bound.Contains(state) ? Bind(attempt) : null,
            Acceptance = state == NodeStates.Reached ? Accept(attempt) : null,
        };

    /// <summary>A move that must be taken: the status after it.</summary>
    private static NodeStatus Moved(NodeDeclaration node, NodeStatus status, NodeMove move)
    {
        var moved = NodeLifecycle.Apply(node, status, move);
        Assert.Equal((status.State, move.To, (string?)null), (status.State, move.To, moved.Refused));
        return moved.Status;
    }

    /// <summary>The move a row asks for, as its cause would make it: the kind's own source, the attempt, a binding, a code, the person's words.</summary>
    private static NodeMove MoveFor(NodeTransition row, NodeAuthority authority, NodeStatus status) => (row.To, row.By) switch
    {
        (NodeStates.Reached, _) => new NodeMove(row.To, row.By) { Source = authority.ReachBy, Attempt = status.Attempt, Acceptance = Accept(status.Attempt) },
        (NodeStates.Failed, _) => new NodeMove(row.To, row.By)
        {
            Source = authority.FailureBy ?? NodeSource.Plugin, Attempt = status.Attempt, Code = "deploy-failed", Transient = true,
        },
        (NodeStates.Rejected, _) => new NodeMove(row.To, row.By) { Attempt = status.Attempt, Words = "not what was asked" },
        (NodeStates.Skipped, _) => new NodeMove(row.To, row.By) { Words = "not what was asked", Scope = Run },
        (NodeStates.Unknown, _) => new NodeMove(row.To, row.By) { Attempt = status.Attempt },
        (NodeStates.Active, _) => new NodeMove(row.To, row.By) { Binding = Bind(status.Attempt + 1), Scope = Run },
        _ => new NodeMove(row.To, row.By),
    };

    /// <summary>What a kind's authority forbids of a row, and nothing else: a person's judgement, a failure, a skip.</summary>
    private static string? Forbidden(NodeTransition row, NodeAuthority authority) => row.To switch
    {
        NodeStates.Rejected when !authority.PersonJudges => NodeRefusal.NotJudged,
        NodeStates.Failed when authority.FailureBy is null => NodeRefusal.NoFailureAuthority,
        NodeStates.Skipped when !authority.PersonSkips => NodeRefusal.NotSkippable,
        _ => null,
    };

    // ---- the one table (canary 43)

    /// <summary>The table is §E2's, row for row, expanded over the live and the bound states, with a skip's withdrawal and lapse.</summary>
    [Fact]
    public void The_table_is_the_designs_row_for_row()
    {
        string Row(NodeTransition row) => $"{row.From}>{row.To}:{row.By}";
        var rows = NodeLifecycle.Table.Select(Row).ToHashSet();

        string[] expected =
        [
            "blocked>ready:daoris", "ready>blocked:daoris", "ready>active:daoris",
            "active>reached:authority", "active>failed:authority", "active>rejected:person", "active>unknown:daoris",
            "unknown>active:daoris", "unknown>reached:authority", "unknown>failed:authority",
            "failed>ready:daoris", "failed>ready:person", "rejected>ready:person", "stopped>ready:person",
            "ready>skipped:person", "blocked>skipped:person", "active>skipped:person",
            "skipped>blocked:person", "skipped>invalidated:daoris",
            "blocked>stopped:person", "ready>stopped:person", "active>stopped:person", "unknown>stopped:person", "invalidated>stopped:person",
            "active>invalidated:daoris", "reached>invalidated:daoris", "failed>invalidated:daoris", "rejected>invalidated:daoris",
            "unknown>invalidated:daoris",
            "invalidated>blocked:daoris", "invalidated>ready:daoris",
        ];

        Assert.Equal(expected.Order(), rows.Order());
        Assert.Equal(expected.Length, NodeLifecycle.Table.Count);
        Assert.All(NodeLifecycle.Table, row => Assert.False(string.IsNullOrWhiteSpace(row.When)));
    }

    /// <summary>Canary 43: every kind passes every row of the same table, and only its authority refuses one.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_kind_passes_the_same_table_and_only_its_authority_differs(string kind)
    {
        var node = Declared(kind);
        var authority = NodeAuthorities.Of(node);
        Assert.NotNull(authority);

        foreach (var row in NodeLifecycle.Table)
        {
            var status = At(row.From);
            var moved = NodeLifecycle.Apply(node, status, MoveFor(row, authority, status));

            var forbidden = Forbidden(row, authority);
            Assert.Equal((row.From, row.To, row.By, forbidden), (row.From, row.To, row.By, moved.Refused));
            Assert.Equal(forbidden is null ? row.To : row.From, moved.Status.State);
        }
    }

    /// <summary>A move the table does not hold is refused whoever asks; one it holds for another cause is refused naming the cause.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_move_the_table_does_not_hold_is_refused_whoever_asks(string kind)
    {
        var node = Declared(kind);
        foreach (var from in NodeStates.All)
        {
            foreach (var to in NodeStates.All)
            {
                foreach (var by in Causes)
                {
                    var rows = NodeLifecycle.Table.Where(row => row.From == from && row.To == to).ToList();
                    if (rows.Any(row => row.By == by)) continue;

                    var moved = NodeLifecycle.Apply(node, At(from), new NodeMove(to, by) { Words = "words", Code = "code" });

                    Assert.Equal((from, to, by, rows.Count == 0 ? NodeRefusal.NotInTable : NodeRefusal.NotCause), (from, to, by, moved.Refused));
                    Assert.Equal(from, moved.Status.State);
                }
            }
        }
    }

    // ---- evidence authority

    /// <summary>Silence and a deadline never count: Daoris cannot fail a node, only its failure authority can.</summary>
    [Fact]
    public void Silence_and_a_deadline_never_fail_a_node()
    {
        var node = Declared("acme.deploy/deploy");

        var timedOut = NodeLifecycle.Apply(node, At(NodeStates.Active), new NodeMove(NodeStates.Failed, NodeCause.Daoris) { Code = "timeout", Attempt = 1 });

        Assert.Equal(NodeRefusal.NotCause, timedOut.Refused);
        Assert.Equal(NodeStates.Active, timedOut.Status.State);
    }

    /// <summary>Only the kind's reach authority reaches it: the agent's claim reaches <c>claimed</c> and nothing else.</summary>
    [Fact]
    public void Only_the_kinds_authority_reaches_it_and_a_claim_is_no_evidence()
    {
        var line = Declared(NodeKinds.Line);
        NodeMove Reach(string source) => new(NodeStates.Reached, NodeCause.Authority) { Source = source, Attempt = 1, Acceptance = Accept(1) };

        Assert.Equal(NodeRefusal.NotAuthority, NodeLifecycle.Apply(line, At(NodeStates.Active), Reach(NodeSource.Ledger)).Refused);
        Assert.Equal(NodeRefusal.NotAuthority, NodeLifecycle.Apply(line, At(NodeStates.Active), Reach(NodeSource.Plugin)).Refused);
        Assert.Equal(NodeStates.Reached, NodeLifecycle.Apply(line, At(NodeStates.Active), Reach(NodeSource.Git)).Status.State);

        var claimed = Declared(NodeKinds.Claimed);
        Assert.Equal(NodeStates.Reached, NodeLifecycle.Apply(claimed, At(NodeStates.Active), Reach(NodeSource.Ledger)).Status.State);
        Assert.False(NodeAuthorities.BuiltIn[NodeKinds.Claimed].MayEnd);
        Assert.All(NodeAuthorities.BuiltIn.Values.Where(each => each.Kind != NodeKinds.Claimed), each => Assert.NotEqual(NodeSource.Ledger, each.ReachBy));
    }

    /// <summary>Git's proofs never fail: not yet is pending. A plugin's failure needs its code.</summary>
    [Fact]
    public void Git_proofs_never_fail_and_a_failure_needs_its_code()
    {
        var failed = new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Git, Code = "not-yet", Attempt = 1 };
        Assert.Equal(NodeRefusal.NoFailureAuthority, NodeLifecycle.Apply(Declared(NodeKinds.Line), At(NodeStates.Active), failed).Refused);
        Assert.Equal(NodeRefusal.NoFailureAuthority, NodeLifecycle.Apply(Declared(NodeKinds.Handed), At(NodeStates.Active), failed).Refused);

        var plugin = Declared("acme.deploy/deploy");
        var uncoded = new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 1 };
        Assert.Equal(NodeRefusal.NoCode, NodeLifecycle.Apply(plugin, At(NodeStates.Active), uncoded).Refused);
        var coded = NodeLifecycle.Apply(plugin, At(NodeStates.Active), uncoded with { Code = "quota", Transient = true });
        Assert.Equal((NodeStates.Failed, "quota", true), (coded.Status.State, coded.Status.Code, coded.Status.Transient));
    }

    /// <summary>A reach binds evidence to this attempt: none, or another attempt's, is refused.</summary>
    [Fact]
    public void A_reach_needs_an_acceptance_bound_to_this_attempt_and_this_node()
    {
        var node = Declared("acme.deploy/deploy");
        var reach = new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 2 };

        Assert.Equal(NodeRefusal.NoAcceptance, NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), reach).Refused);
        Assert.Equal(NodeRefusal.StaleAttempt, NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), reach with { Attempt = 1, Acceptance = Accept(1) }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), reach with { Acceptance = Accept(1) }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), reach with { Acceptance = Accept(2, "n2") }).Refused);

        var reached = NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), reach with { Acceptance = Accept(2) });
        Assert.Equal(NodeStates.Reached, reached.Status.State);
        Assert.Equal(Accept(2).Id, reached.Status.Current?.Id);
    }

    /// <summary>A person rejects only a node whose kind lets a person judge it, a plugin's only where it declares so, and always with words.</summary>
    [Fact]
    public void A_rejection_is_a_persons_with_words_on_a_node_a_person_may_judge()
    {
        var reject = new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = 1 };

        Assert.Equal(NodeRefusal.NoWords, NodeLifecycle.Apply(Declared(NodeKinds.Human), At(NodeStates.Active), reject).Refused);
        var rejected = NodeLifecycle.Apply(Declared(NodeKinds.Human), At(NodeStates.Active), reject with { Words = "the copy is wrong" });
        Assert.Equal((NodeStates.Rejected, "the copy is wrong"), (rejected.Status.State, rejected.Status.Words));

        Assert.Equal(NodeRefusal.NotJudged, NodeLifecycle.Apply(Declared("acme.deploy/deploy"), At(NodeStates.Active), reject with { Words = "no" }).Refused);
        Assert.Equal(NodeStates.Rejected,
            NodeLifecycle.Apply(Declared("acme.deploy/deploy", rejectable: true), At(NodeStates.Active), reject with { Words = "no" }).Status.State);
    }

    /// <summary>A skip is the person's, with their words, where the kind allows one; git's proofs and the close are never skipped.</summary>
    [Fact]
    public void A_skip_is_the_persons_with_words_where_the_kind_allows_it()
    {
        var skip = new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "shipped by hand", Scope = Run };

        Assert.Equal(NodeRefusal.NoWords, NodeLifecycle.Apply(Declared(NodeKinds.Look), At(NodeStates.Ready), skip with { Words = " " }).Refused);
        Assert.Equal(NodeStates.Skipped, NodeLifecycle.Apply(Declared(NodeKinds.Look), At(NodeStates.Ready), skip).Status.State);
        foreach (var kind in new[] { NodeKinds.Line, NodeKinds.Handed, NodeKinds.Claimed, NodeKinds.QuestClose, NodeKinds.All })
        {
            Assert.Equal((kind, NodeRefusal.NotSkippable), (kind, NodeLifecycle.Apply(Declared(kind), At(NodeStates.Ready), skip).Refused));
        }
    }

    // ---- retries

    /// <summary>Daoris retries only a declared transient failure with attempts left; each start is a new attempt, counted.</summary>
    [Fact]
    public void A_declared_retry_takes_a_transient_code_with_attempts_left()
    {
        var node = Declared("acme.deploy/deploy", new NodeRetry(3, [5, 30]));
        var retry = new NodeMove(NodeStates.Ready, NodeCause.Daoris);

        Assert.Equal(NodeRefusal.NotTransient, NodeLifecycle.Apply(node, At(NodeStates.Failed, transient: false), retry).Refused);
        Assert.Equal(NodeRefusal.NoAttemptsLeft, NodeLifecycle.Apply(node, At(NodeStates.Failed, attempt: 3, tries: 3), retry).Refused);

        var again = NodeLifecycle.Apply(node, At(NodeStates.Failed, attempt: 1, tries: 1), retry);
        Assert.Equal(NodeStates.Ready, again.Status.State);
        Assert.Null(again.Status.Code);
        var started = NodeLifecycle.Apply(node, again.Status, Start(2));
        Assert.Equal((NodeStates.Active, 2, 2), (started.Status.State, started.Status.Attempt, started.Status.Tries));

        var undeclared = Declared("acme.deploy/deploy") with { Retry = null };
        Assert.Equal(NodeRefusal.NoAttemptsLeft, NodeLifecycle.Apply(undeclared, At(NodeStates.Failed), retry).Refused);
        Assert.Equal(TimeSpan.FromSeconds(30), NodeLifecycle.DelayBefore(node, 3));
        Assert.Equal(TimeSpan.FromSeconds(5), NodeLifecycle.DelayBefore(node, 2));
    }

    /// <summary>The person's new attempt resets the bound, as a fresh binding does; the attempt's number only grows.</summary>
    [Fact]
    public void The_persons_new_attempt_and_a_fresh_binding_reset_the_bound()
    {
        var node = Declared("acme.deploy/deploy", new NodeRetry(3, [5]));

        var asked = NodeLifecycle.Apply(node, At(NodeStates.Failed, attempt: 3, tries: 3), new NodeMove(NodeStates.Ready, NodeCause.Person));
        Assert.Equal((NodeStates.Ready, 3, 0), (asked.Status.State, asked.Status.Attempt, asked.Status.Tries));
        var started = NodeLifecycle.Apply(node, asked.Status, Start(4));
        Assert.Equal((4, 1), (started.Status.Attempt, started.Status.Tries));

        var invalidated = NodeLifecycle.Apply(node, At(NodeStates.Reached, attempt: 3, tries: 3), new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        Assert.Equal(NodeStates.Invalidated, invalidated.Status.State);
        Assert.Null(invalidated.Status.Current);
        Assert.Equal(Accept(3).Id, invalidated.Status.Acceptance?.Id);
        var judged = NodeLifecycle.Apply(node, invalidated.Status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        Assert.Equal((NodeStates.Ready, 0), (judged.Status.State, judged.Status.Tries));
        Assert.Null(judged.Status.Acceptance);
    }

    /// <summary>
    /// An unknown outcome is reconciled before any new attempt, through a stop and the person's ask alike. A dispatched attempt found
    /// still running stays unreconciled until its outcome (FABRIC-NODE1c).
    /// </summary>
    [Fact]
    public void An_unknown_outcome_is_reconciled_before_any_new_attempt()
    {
        var node = Declared("acme.deploy/deploy");
        var unknown = NodeLifecycle.Apply(node, At(NodeStates.Active, attempt: 2), new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = 2 });
        Assert.Equal((NodeStates.Unknown, (int?)2), (unknown.Status.State, unknown.Status.Unreconciled));

        var stopped = NodeLifecycle.Apply(node, unknown.Status, new NodeMove(NodeStates.Stopped, NodeCause.Person));
        var asked = NodeLifecycle.Apply(node, stopped.Status, new NodeMove(NodeStates.Ready, NodeCause.Person));
        Assert.Equal(NodeStates.Ready, asked.Status.State);
        var refused = NodeLifecycle.Apply(node, asked.Status, Start(3));
        Assert.Equal((NodeRefusal.Unreconciled, NodeStates.Ready), (refused.Refused, refused.Status.State));

        var reconciled = NodeLifecycle.Reconciled(asked.Status, 2);
        Assert.Null(reconciled.Unreconciled);
        var started = NodeLifecycle.Apply(node, reconciled, Start(3));
        Assert.Equal((NodeStates.Active, 3), (started.Status.State, started.Status.Attempt));
        Assert.Equal(asked.Status, NodeLifecycle.Reconciled(asked.Status, 1));

        var running = NodeLifecycle.Apply(node, unknown.Status, new NodeMove(NodeStates.Active, NodeCause.Daoris));
        Assert.Equal((NodeStates.Active, 2, (int?)2), (running.Status.State, running.Status.Attempt, running.Status.Unreconciled));
        var observed = NodeLifecycle.Apply(Declared(NodeKinds.Pushed), unknown.Status, new NodeMove(NodeStates.Active, NodeCause.Daoris));
        Assert.Equal((NodeStates.Active, (int?)null), (observed.Status.State, observed.Status.Unreconciled));
    }

    // ---- the contract

    /// <summary>A declaration names a kind this build knows, at its own reach, with a version and bounds that can hold.</summary>
    [Fact]
    public void A_declaration_is_refused_naming_what_does_not_hold()
    {
        Assert.Null(NodeDeclarations.Problem(Declared(NodeKinds.Line) with { Retry = null }));
        Assert.Null(NodeDeclarations.Problem(Declared("acme.deploy/deploy")));
        Assert.Null(NodeDeclarations.Problem(Declared("acme.watch/deployed") with { Reach = NodeReach.Observed, Retry = null, Perform = null }));

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Declared("deploy")));
        Assert.Equal("reach", NodeDeclarations.Problem(Declared(NodeKinds.Line) with { Reach = NodeReach.Performed, Retry = null }));
        Assert.Equal("reach", NodeDeclarations.Problem(Declared("acme.deploy/deploy") with { Reach = NodeReach.Pressed }));
        Assert.Equal("version", NodeDeclarations.Problem(Declared("acme.deploy/deploy") with { Version = 0 }));
        Assert.Equal("retry", NodeDeclarations.Problem(Declared("acme.deploy/deploy", new NodeRetry(0, []))));
        Assert.Equal("retry", NodeDeclarations.Problem(Declared(NodeKinds.Line)));
        Assert.Equal("timeoutSeconds", NodeDeclarations.Problem(Declared("acme.deploy/deploy") with { TimeoutSeconds = 0 }));
        Assert.Equal("rejectable", NodeDeclarations.Problem(Declared(NodeKinds.Line, rejectable: true) with { Retry = null }));
    }

    /// <summary>
    /// FABRIC-UNIT1d (§D6): only a plugin's own performed node says how it is performed, and its retry is one it can take: a transient
    /// code to retry, on a node that is idempotent or reconciled. A retry or a code nothing could ever take is refused, as NODE1 refuses
    /// a retry on a kind nothing can fail.
    /// </summary>
    [Fact]
    public void A_plugin_s_performed_node_says_how_it_is_performed_and_only_a_retry_it_can_take()
    {
        var performed = new NodeDeclaration("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed);
        var close = new NodeDeclaration("close", NodeKinds.QuestClose, 1, NodeReach.Performed);
        var three = new NodeRetry(3, [5]);

        Assert.Null(NodeDeclarations.Problem(performed));
        Assert.Null(NodeDeclarations.Problem(performed with { Perform = Retrying, Retry = three }));
        Assert.Null(NodeDeclarations.Problem(performed with { Perform = Retrying with { Idempotent = false, Reconcile = true }, Retry = new NodeRetry(2, []) }));
        Assert.Null(NodeDeclarations.Problem(performed with { Perform = new NodePerform(false, false, []), Retry = new NodeRetry(1, []) }));
        Assert.Null(NodeDeclarations.Problem(close with { Retry = three }));

        Assert.Equal("perform", NodeDeclarations.Problem(performed with { Reach = NodeReach.Observed, Perform = new NodePerform(false, false, []) }));
        Assert.Equal("perform", NodeDeclarations.Problem(close with { Perform = new NodePerform(true, false, []) }));

        Assert.Equal("transient", NodeDeclarations.Problem(performed with { Perform = Retrying with { Transient = ["Unavailable"] }, Retry = three }));
        Assert.Equal("transient", NodeDeclarations.Problem(performed with { Perform = Retrying with { Transient = ["unavailable", "unavailable"] }, Retry = three }));
        Assert.Equal("transient", NodeDeclarations.Problem(performed with { Perform = Retrying }));
        Assert.Equal("transient", NodeDeclarations.Problem(performed with { Perform = Retrying, Retry = new NodeRetry(1, []) }));

        Assert.Equal("retry", NodeDeclarations.Problem(performed with { Retry = three }));
        Assert.Equal("retry", NodeDeclarations.Problem(performed with { Perform = Retrying with { Transient = [] }, Retry = three }));
        Assert.Equal("retry", NodeDeclarations.Problem(performed with { Perform = Retrying with { Idempotent = false }, Retry = three }));
        Assert.Equal("retry", NodeDeclarations.Problem(performed with { Reach = NodeReach.Observed, Retry = new NodeRetry(2, []) }));
    }

    /// <summary>
    /// FABRIC-UNIT1d: two declarations saying the same are equal, whoever built their collections, so a manifest's step is the one
    /// built by hand and a graph compares declarations by what they say.
    /// </summary>
    [Fact]
    public void Two_declarations_saying_the_same_are_equal()
    {
        static NodeDeclaration Made() => new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed)
        {
            Inputs = new Dictionary<string, string> { ["release"] = "acme/artifact@1" },
            Outputs = new Dictionary<string, string> { ["preview"] = "acme/preview@1", ["log"] = "acme/log@1" },
            Evidence = "acme/receipt@1",
            Retry = new NodeRetry(3, [5, 30]),
            Perform = new NodePerform(true, false, ["unavailable"]),
        };

        Assert.Equal(Made(), Made());
        Assert.Equal(Made().GetHashCode(), Made().GetHashCode());
        Assert.Equal(new NodeRetry(2, [1]), new NodeRetry(2, new List<int> { 1 }));
        Assert.Equal(new NodePerform(true, true, ["a"]), new NodePerform(true, true, new List<string> { "a" }));

        Assert.NotEqual(Made(), Made() with { Inputs = new Dictionary<string, string> { ["release"] = "acme/other@1" } });
        Assert.NotEqual(Made(), Made() with { Outputs = new Dictionary<string, string> { ["preview"] = "acme/preview@1" } });
        Assert.NotEqual(Made(), Made() with { Retry = new NodeRetry(3, [5]) });
        Assert.NotEqual(Made(), Made() with { Perform = new NodePerform(true, false, ["timeout"]) });
        Assert.NotEqual(Made(), Made() with { Perform = null });
        Assert.NotEqual(Made(), Made() with { Skippable = false });
        Assert.NotEqual(Made(), Made() with { Version = 2 });
    }

    // ---- FABRIC-NODE1c: a second maker's review

    /// <summary>
    /// Defect 1: a dispatched attempt's outcome is established before another starts, whatever moved the node off it: a stop, an
    /// invalidation, a skip withdrawn, a rejection. Only a reconciliation of that attempt lifts it. A kind that dispatches nothing (a
    /// person's task) starts again at once.
    /// </summary>
    [Fact]
    public void A_dispatched_attempt_is_reconciled_before_another_starts_whatever_moved_the_node_off_it()
    {
        var node = Declared("acme.deploy/deploy", rejectable: true);
        var dispatched = Moved(node, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1));
        Assert.Equal((NodeStates.Active, 1, (int?)1), (dispatched.State, dispatched.Attempt, dispatched.Unreconciled));

        NodeMove[][] paths =
        [
            [new(NodeStates.Stopped, NodeCause.Person), new(NodeStates.Ready, NodeCause.Person)],
            [new(NodeStates.Invalidated, NodeCause.Daoris), new(NodeStates.Ready, NodeCause.Daoris)],
            [new(NodeStates.Skipped, NodeCause.Person) { Words = "by hand", Scope = Run }, new(NodeStates.Blocked, NodeCause.Person), new(NodeStates.Ready, NodeCause.Daoris)],
            [new(NodeStates.Rejected, NodeCause.Person) { Words = "not this", Attempt = 1 }, new(NodeStates.Ready, NodeCause.Person)],
        ];
        foreach (var path in paths)
        {
            var status = path.Aggregate(dispatched, (each, move) => Moved(node, each, move));

            var refused = NodeLifecycle.Apply(node, status, Start(2));
            Assert.Equal((path[0].To, NodeRefusal.Unreconciled, NodeStates.Ready), (path[0].To, refused.Refused, refused.Status.State));
            var started = NodeLifecycle.Apply(node, NodeLifecycle.Reconciled(status, 1), Start(2));
            Assert.Equal((path[0].To, NodeStates.Active, 2), (path[0].To, started.Status.State, started.Status.Attempt));
        }

        Assert.Equal(dispatched, NodeLifecycle.Reconciled(dispatched, 1));

        var human = Declared(NodeKinds.Human);
        var offered = Moved(human, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1));
        Assert.Null(offered.Unreconciled);
        var asked = Moved(human, Moved(human, offered, new NodeMove(NodeStates.Stopped, NodeCause.Person)), new NodeMove(NodeStates.Ready, NodeCause.Person));
        Assert.Equal(NodeStates.Active, NodeLifecycle.Apply(human, asked, Start(2)).Status.State);
    }

    /// <summary>Defect 2: every outcome names its attempt: a late failure, rejection, unknown or reach of attempt 1 never lands on attempt 2.</summary>
    [Fact]
    public void A_late_outcome_of_an_earlier_attempt_never_lands_on_the_current_one()
    {
        var node = Declared("acme.deploy/deploy", rejectable: true);
        var current = At(NodeStates.Active, attempt: 2, tries: 2);
        var fail = new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Plugin, Code = "deploy-failed" };

        Assert.Equal(NodeRefusal.StaleAttempt, NodeLifecycle.Apply(node, current, fail with { Attempt = 1 }).Refused);
        Assert.Equal(NodeRefusal.NoAttempt, NodeLifecycle.Apply(node, current, fail).Refused);
        Assert.Equal(NodeRefusal.StaleAttempt, NodeLifecycle.Apply(node, At(NodeStates.Unknown, attempt: 2), fail with { Attempt = 1 }).Refused);
        Assert.Equal(NodeRefusal.StaleAttempt,
            NodeLifecycle.Apply(node, current, new NodeMove(NodeStates.Rejected, NodeCause.Person) { Words = "no", Attempt = 1 }).Refused);
        Assert.Equal(NodeRefusal.StaleAttempt, NodeLifecycle.Apply(node, current, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = 1 }).Refused);
        Assert.Equal(NodeRefusal.NoAttempt, NodeLifecycle.Apply(node, current, new NodeMove(NodeStates.Unknown, NodeCause.Daoris)).Refused);

        var failed = NodeLifecycle.Apply(node, current, fail with { Attempt = 2 });
        Assert.Equal((NodeStates.Failed, 2, (string?)null), (failed.Status.State, failed.Status.Attempt, failed.Refused));
    }

    /// <summary>
    /// FABRIC-WIRE1e: a start names the graph and run its attempt belongs to, as a skip does, and the status keeps it through the attempt's
    /// outcome, so a failure or a rejection is a receipt in that run alone. A start naming none, or another graph than its binding's, is
    /// refused.
    /// </summary>
    [Fact]
    public void A_start_names_the_run_its_attempt_belongs_to()
    {
        var node = Declared("acme.deploy/deploy", rejectable: true);
        var ready = At(NodeStates.Ready, attempt: 0, tries: 0);

        Assert.Equal(NodeRefusal.NoScope, NodeLifecycle.Apply(node, ready, Start(1) with { Scope = null }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, ready, Start(1) with { Scope = Run with { GraphDigest = "graph-0" } }).Refused);
        var active = Moved(node, ready, Start(1));
        Assert.Equal(Run, active.Scope);

        var failed = Moved(node, active, new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 1, Code = "deploy-failed" });
        var rejected = Moved(node, active, new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = 1, Words = "not this" });
        Assert.Equal((Run, Run), (failed.Scope, rejected.Scope));

        var again = Moved(node, Moved(node, failed, new NodeMove(NodeStates.Ready, NodeCause.Person)), Start(2) with { Scope = Run with { Run = "run-2" } });
        Assert.Equal(Run with { Run = "run-2" }, again.Scope);
    }

    /// <summary>
    /// Defect 3: a reach is the binding its attempt started on, whole. An old graph, revision or node version, other inputs or receipts,
    /// another node's declaration, or no evidence where the kind needs it is refused; a start brings this node's next attempt at its
    /// declared version.
    /// </summary>
    [Fact]
    public void A_reach_must_be_the_binding_its_attempt_started_on()
    {
        var node = Declared("acme.deploy/deploy") with { Evidence = "acme/receipt@1" };
        var ready = At(NodeStates.Ready, attempt: 0, tries: 0);
        var binding = Bind(1) with { PrerequisiteReceipts = ["acc-build"] };
        var start = new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = binding, Scope = Run };

        Assert.Equal(NodeRefusal.NoBinding, NodeLifecycle.Apply(node, ready, start with { Binding = null }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, ready, start with { Binding = binding with { NodeVersion = 2 } }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, ready, start with { Binding = binding with { Attempt = 3 } }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, ready, start with { Binding = binding with { Node = "n2" } }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(node, ready, start with { Binding = binding with { Unit = "unit-b" } }).Refused);
        var active = Moved(node, ready, start);
        Assert.Equal(binding.Id, active.Binding?.Id);

        var evidence = new NodePort("acme/receipt@1", "deploy#1", "tip-1", null);
        var reach = new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 1, Acceptance = binding with { Evidence = evidence } };
        foreach (var (why, other) in new (string, NodeAcceptance)[]
        {
            ("graph", binding with { GraphDigest = "graph-0" }),
            ("revision", binding with { SubjectRevision = "tip-0" }),
            ("version", binding with { NodeVersion = 2 }),
            ("receipts", binding with { PrerequisiteReceipts = [] }),
            ("inputs", binding with { Inputs = new Dictionary<string, NodePort> { ["release"] = new("acme/artifact@1", "build.artifact", "tip-1", "acc-y") } }),
        })
        {
            Assert.Equal((why, NodeRefusal.NotBound), (why, NodeLifecycle.Apply(node, active, reach with { Acceptance = other with { Evidence = evidence } }).Refused));
        }

        Assert.Equal(NodeRefusal.NoEvidence, NodeLifecycle.Apply(node, active, reach with { Acceptance = binding }).Refused);
        Assert.Equal(NodeRefusal.NoEvidence,
            NodeLifecycle.Apply(node, active, reach with { Acceptance = binding with { Evidence = evidence with { Schema = "acme/other@1" } } }).Refused);
        Assert.Equal(NodeRefusal.NotThisNode, NodeLifecycle.Apply(node with { Id = "n2" }, active, reach).Refused);
        Assert.Equal(NodeRefusal.NoBinding, NodeLifecycle.Apply(node, active with { Binding = null }, reach).Refused);

        var reached = NodeLifecycle.Apply(node, active, reach);
        Assert.Equal((NodeStates.Reached, binding.Id), (reached.Status.State, reached.Status.Current?.Id));
    }

    /// <summary>
    /// Defect 7: a move that clears what the status held returns it as it stood, for the record writer to keep before the reset; one
    /// that clears nothing returns none.
    /// </summary>
    [Fact]
    public void A_move_returns_what_it_cleared_for_the_record_writer()
    {
        var node = Declared("acme.deploy/deploy", new NodeRetry(3, [5]), rejectable: true);

        var rejected = At(NodeStates.Rejected) with { Words = "the copy is wrong", By = NodeSource.Person };
        var asked = NodeLifecycle.Apply(node, rejected, new NodeMove(NodeStates.Ready, NodeCause.Person));
        Assert.Equal(rejected, asked.Cleared);
        Assert.Equal(((string?)null, (NodeAcceptance?)null), (asked.Status.Words, asked.Status.Binding));

        var retried = NodeLifecycle.Apply(node, At(NodeStates.Failed), new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        Assert.Equal(("deploy-failed", Bind(1).Id), (retried.Cleared?.Code, retried.Cleared?.Binding?.Id));

        var invalidated = NodeLifecycle.Apply(node, At(NodeStates.Reached), new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        Assert.Null(invalidated.Cleared);
        var judged = NodeLifecycle.Apply(node, invalidated.Status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        Assert.Equal(Accept(1).Id, judged.Cleared?.Acceptance?.Id);

        Assert.Null(NodeLifecycle.Apply(node, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1)).Cleared);
        Assert.Null(NodeLifecycle.Apply(node, At(NodeStates.Blocked), new NodeMove(NodeStates.Ready, NodeCause.Daoris)).Cleared);
    }

    /// <summary>
    /// Choice 2, changed: a skip holds for its graph and run alone. A new tip keeps it, another graph or run lapses it through the table,
    /// and the person may withdraw it, its words returned as cleared.
    /// </summary>
    [Fact]
    public void A_skip_holds_for_its_graph_and_run_and_the_person_may_withdraw_it()
    {
        var node = Declared(NodeKinds.Look);
        var skip = new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "shipped by hand" };

        Assert.Equal(NodeRefusal.NoScope, NodeLifecycle.Apply(node, At(NodeStates.Ready), skip).Refused);
        var skipped = Moved(node, At(NodeStates.Ready), skip with { Scope = Run });
        Assert.Equal(Run, skipped.Scope);
        Assert.True(NodeLifecycle.Skips(skipped, Run));
        Assert.False(NodeLifecycle.Skips(skipped, Run with { Run = "run-2" }));
        Assert.False(NodeLifecycle.Skips(skipped, Run with { GraphDigest = "graph-2" }));
        Assert.False(NodeLifecycle.Skips(At(NodeStates.Ready), Run));
        Assert.DoesNotContain(NodeStates.Skipped, NodeStates.Bound);

        var lapsed = Moved(node, skipped, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        Assert.False(NodeLifecycle.Skips(lapsed, Run));

        var withdrawn = NodeLifecycle.Apply(node, skipped, new NodeMove(NodeStates.Blocked, NodeCause.Person));
        Assert.Equal((NodeStates.Blocked, (string?)null, (NodeScope?)null), (withdrawn.Status.State, withdrawn.Status.Words, withdrawn.Status.Scope));
        Assert.Equal("shipped by hand", withdrawn.Cleared?.Words);
    }

    /// <summary>
    /// FABRIC-NODE1d: a declaration copies its collections when they are set, so a caller changing what it supplied changes neither the
    /// declaration nor its <see cref="NodeDeclarations.Problem"/>, and a graph's digest over declarations cannot move under it.
    /// </summary>
    [Fact]
    public void A_declaration_keeps_its_collections_when_what_was_supplied_changes()
    {
        var inputs = new Dictionary<string, string> { ["release"] = "acme/artifact@1" };
        var outputs = new Dictionary<string, string> { ["preview"] = "acme/preview@1" };
        var delays = new List<int> { 5, 30 };
        var node = new NodeDeclaration("deploy", "acme.deploy/deploy", 1, NodeReach.Performed)
        {
            Inputs = inputs,
            Outputs = outputs,
            Retry = new NodeRetry(3, delays),
            Perform = Retrying,
        };
        Assert.Null(NodeDeclarations.Problem(node));

        inputs["release"] = "acme/other@1";
        inputs["extra"] = "acme/other@1";
        outputs.Clear();
        delays[0] = -1;
        delays.Add(60);

        Assert.Equal(("acme/artifact@1", 1), (node.Inputs["release"], node.Inputs.Count));
        Assert.Equal("acme/preview@1", Assert.Single(node.Outputs).Value);
        Assert.Equal(new[] { 5, 30 }, node.Retry!.DelaysSeconds);
        Assert.Null(NodeDeclarations.Problem(node));
        Assert.Equal(TimeSpan.FromSeconds(30), NodeLifecycle.DelayBefore(node, 5));

        var widened = node with { Inputs = inputs, Retry = node.Retry with { DelaysSeconds = delays } };
        inputs.Clear();
        delays.Clear();
        Assert.Equal(2, widened.Inputs.Count);
        Assert.Equal(new[] { -1, 30, 60 }, widened.Retry!.DelaysSeconds);
        Assert.Equal("retry", NodeDeclarations.Problem(widened));
    }

    /// <summary>
    /// FABRIC-NODE1d: a reach's evidence and outputs name the reach's own acceptance, since a consumer's invalidation follows the one a
    /// port names. Another is refused; a port that names none is kept naming its own.
    /// </summary>
    [Fact]
    public void A_reach_whose_evidence_or_outputs_name_another_acceptance_is_refused()
    {
        var node = Declared("acme.deploy/deploy") with { Evidence = "acme/receipt@1", Outputs = new Dictionary<string, string> { ["preview"] = "acme/preview@1" } };
        var active = Moved(node, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1));
        var binding = Bind(1);
        var evidence = new NodePort("acme/receipt@1", "deploy#1", "tip-1", null);
        var preview = new NodePort("acme/preview@1", "deploy.preview", "tip-1", null);
        var reach = new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = 1 };
        NodeMoved Reach(NodePort port, NodePort output) =>
            NodeLifecycle.Apply(node, active, reach with { Acceptance = binding with { Evidence = port, Outputs = new Dictionary<string, NodePort> { ["preview"] = output } } });

        Assert.Equal(NodeRefusal.OtherAcceptance, Reach(evidence with { Acceptance = "acc-other" }, preview).Refused);
        Assert.Equal(NodeRefusal.OtherAcceptance, Reach(evidence, preview with { Acceptance = "acc-other" }).Refused);

        var own = Reach(evidence with { Acceptance = binding.Id }, preview with { Acceptance = binding.Id });
        Assert.Equal((binding.Id, binding.Id), (own.Status.Current?.Evidence?.Acceptance, own.Status.Current?.Outputs["preview"].Acceptance));
        var unnamed = Reach(evidence, preview);
        Assert.Equal((binding.Id, binding.Id), (unnamed.Status.Current?.Evidence?.Acceptance, unnamed.Status.Current?.Outputs["preview"].Acceptance));
        Assert.True(own.Status.Current!.SameResult(unnamed.Status.Current!));
    }

    /// <summary>
    /// FABRIC-NODE1e: a reach exposes exactly its node's declared outputs, each at its declared schema, so a <c>data</c> wire's schema checked
    /// when saved (WIRE1a) holds at run time. An undeclared output, a declared one at another schema, and a declared one missing are each
    /// refused with their code; the design's example (a deploy with a declared preview, and a node declaring none) still reaches.
    /// </summary>
    [Fact]
    public void A_reach_exposes_exactly_its_declared_outputs()
    {
        var node = Declared("acme.deploy/deploy") with
        {
            Evidence = "acme/receipt@1",
            Outputs = new Dictionary<string, string> { ["preview"] = "acme/preview@1", ["notes"] = "acme/notes@1" },
        };
        var active = Moved(node, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1));
        var evidence = new NodePort("acme/receipt@1", "deploy#1", "tip-1", null);
        var preview = new NodePort("acme/preview@1", "deploy.preview", "tip-1", null);
        var notes = new NodePort("acme/notes@1", "deploy.notes", "tip-1", null);
        NodeMoved Reach(NodeDeclaration declared, NodeStatus status, Dictionary<string, NodePort> outputs) =>
            NodeLifecycle.Apply(declared, status, new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = NodeSource.Plugin, Attempt = 1, Acceptance = Bind(1) with { Evidence = evidence, Outputs = outputs },
            });

        Assert.Equal(NodeRefusal.UndeclaredOutput, Reach(node, active, new() { ["preview"] = preview, ["notes"] = notes, ["log"] = notes with { Schema = "acme/log@1" } }).Refused);
        Assert.Equal(NodeRefusal.OutputSchema, Reach(node, active, new() { ["preview"] = preview with { Schema = "acme/preview@2" }, ["notes"] = notes }).Refused);
        Assert.Equal(NodeRefusal.NoOutput, Reach(node, active, new() { ["preview"] = preview }).Refused);

        var reached = Reach(node, active, new() { ["preview"] = preview, ["notes"] = notes });
        Assert.Equal((NodeStates.Reached, 2), (reached.Status.State, reached.Status.Current?.Outputs.Count));

        var bare = Declared("acme.deploy/deploy") with { Evidence = "acme/receipt@1" };
        Assert.Equal(NodeRefusal.UndeclaredOutput, Reach(bare, Moved(bare, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1)), new() { ["preview"] = preview }).Refused);
        Assert.Equal(NodeStates.Reached, Reach(bare, Moved(bare, At(NodeStates.Ready, attempt: 0, tries: 0), Start(1)), []).Status.State);
    }

    /// <summary>Choice 3, changed: a plugin's declaration may forbid a skip; a built-in's skip is its kind's, never a declaration's.</summary>
    [Fact]
    public void A_plugins_declaration_may_forbid_a_skip()
    {
        var skip = new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "not needed", Scope = Run };
        var pinned = Declared("acme.deploy/deploy") with { Skippable = false };

        Assert.Equal(NodeStates.Skipped, NodeLifecycle.Apply(Declared("acme.deploy/deploy"), At(NodeStates.Ready), skip).Status.State);
        Assert.Equal(NodeRefusal.NotSkippable, NodeLifecycle.Apply(pinned, At(NodeStates.Ready), skip).Refused);
        Assert.False(NodeAuthorities.Of(pinned)!.PersonSkips);
        Assert.Null(NodeDeclarations.Problem(pinned));
        Assert.Equal("skippable", NodeDeclarations.Problem(Declared(NodeKinds.Look) with { Retry = null, Skippable = false }));
    }
}
