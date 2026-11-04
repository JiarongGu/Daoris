namespace Daoris.Driver;

/// <summary>Why a save refuses a graph (§E7), beside the wires' own <see cref="WireRefusal"/> codes, which a graph's save names too.</summary>
public static class GraphRefusal
{
    /// <summary>What the file holds is not a graph's shape: a field of the wrong type, a list that is no list, or no JSON at all.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>A schema this build does not read.</summary>
    public const string SchemaVersion = "schema-version";

    /// <summary>More authored nodes than <see cref="NodeGraphs.MaxNodes"/>.</summary>
    public const string NodesBound = "nodes-bound";

    /// <summary>More authored wires than <see cref="NodeGraphs.MaxWires"/>.</summary>
    public const string WiresBound = "wires-bound";

    /// <summary>More synthetic nodes, or wires to or from one, than the authored nodes allow.</summary>
    public const string SyntheticBound = "synthetic-bound";

    /// <summary>A node whose own declaration does not hold (<see cref="NodeDeclarations.Problem"/>).</summary>
    public const string Declaration = "declaration";

    /// <summary>A graph with no entry: nothing would start.</summary>
    public const string NoEntry = "no-entry";

    /// <summary>An entry naming no node, or named twice.</summary>
    public const string Entry = "entry";

    /// <summary>An entry another node of the graph wires into: an entry starts the graph, and waits on nothing in it.</summary>
    public const string EntryWired = "entry-wired";

    /// <summary>No end, or an end naming no node.</summary>
    public const string End = "end";

    /// <summary>An end of a kind that may not be one: the agent's claim (§C2, §E4).</summary>
    public const string EndKind = "end-kind";

    /// <summary>A node no entry reaches.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>A <c>data</c> wire into a step that can start on a route of an outcome that does not carry the output it takes (DECIDE1d).</summary>
    public const string UncarriedOutput = "uncarried-output";

    /// <summary>
    /// D157 §2.2's protected order broken: the opinion and the look come before the landing. Since FABRIC-CUTOVER1b the opinion is not asked
    /// to come before the look (the cutover design §5.1), whose set-up the review gate holds while an agent reads it.
    /// </summary>
    public const string ProtectedOrder = "protected-order";

    /// <summary>
    /// A gate with no wire straight into a later gate of D157 §2.2's order (FABRIC-GRAPH1i): reachability alone lets a person's skip of a
    /// gate between pass an <c>after</c> wire while the earlier gate is unsettled, so each gate is wired into each later one, as the decoder does.
    /// </summary>
    public const string ProtectedWire = "protected-wire";

    /// <summary>Two observed nodes of one plugin's step in one graph (FABRIC-GRAPH1g): an event names one node, so neither would be reached (§C4 rule 2).</summary>
    public const string ObservedTwice = "observed-twice";

    /// <summary>A graph holding <c>quest.close</c> whose end is not it (§E5: in a graph that holds it, it is the end).</summary>
    public const string CloseEnd = "close-end";

    /// <summary>A node after <c>quest.close</c>: only its failure's path leaves it.</summary>
    public const string AfterClose = "after-close";

    /// <summary>Canary 40: the quest's own done needed before its close, which a closure graph never writes before its end.</summary>
    public const string ClosureDeadlock = "closure-deadlock";

    /// <summary>A cleanup of no kind, a bound outside 1-90 days or at <i>held</i>, or a wait for an end at or before the line.</summary>
    public const string Cleanup = "cleanup";

    /// <summary>A second <c>integrate</c> (INTEGRATE1n; §17.4): the graph's one landing is integrated once.</summary>
    public const string IntegrateTwice = "integrate-twice";

    /// <summary>An <c>integrate</c> in a graph holding no <c>land</c>, whose landing it integrates (INTEGRATE1n).</summary>
    public const string IntegrateUnlanded = "integrate-unlanded";

    /// <summary>
    /// A node that observes the landing with a way to it that passes no <c>land</c> (INTEGRATE1n, F47; §17.4): <c>pushed</c>,
    /// <c>pull-request</c>, <c>line</c> or <c>integrate</c> would wait for ever before a landing record. Said in a graph that holds a <c>land</c>.
    /// </summary>
    public const string BeforeLanding = "before-landing";

    /// <summary>A second <c>work.requests</c> (INTEGRATE1o; §17.3): the graph holds the work on its requests once.</summary>
    public const string WorkRequestsTwice = "work-requests-twice";

    /// <summary>A <c>work.requests</c> no <c>claimed</c> comes before (INTEGRATE1o): it holds the work a <c>claimed</c> observes.</summary>
    public const string WorkRequestsUnclaimed = "work-requests-unclaimed";

    /// <summary>
    /// A landing that can start before a <c>work.requests</c> is reached (INTEGRATE1o; §17.3: placed before the landing): a way to it that passes
    /// no such node would land the work with its requests unanswered, a hold dropped.
    /// </summary>
    public const string WorkRequestsLate = "work-requests-late";

    /// <summary>A graph holding a <c>land</c> and another landing (LANDNODE1a; the land node design §6): a graph holds one.</summary>
    public const string LandOnce = "land-once";

    /// <summary>
    /// A <c>land</c> whose method and tail disagree (LANDNODE1a): <c>pull-request</c> brings <c>pushed</c> then <c>pull-request</c>, <c>push</c> brings
    /// <c>pushed</c>, <c>integrate</c> an <c>integrate</c>, and <c>merge</c> and <c>branch</c> nothing.
    /// </summary>
    public const string LandTail = "land-tail";

    /// <summary>A <c>land</c> written with an <c>accept</c> (WORKFLOWCLEAN1 1b): the press is the graph's <c>review</c> before it.</summary>
    public const string Accept = "accept";

    /// <summary>A <c>land</c> saved with no <c>method</c> (WORKFLOWCLEAN1 1b; design §13 point 2): a stored one still reads, and a run resolves it as before.</summary>
    public const string LandMethod = "land-method";

    /// <summary>A second hold of one kind (LANDNODE1k): the graph judges each hold once.</summary>
    public const string HoldTwice = "hold-twice";

    /// <summary>
    /// A landing that can start before a hold is reached (LANDNODE1k): a hold judges the work before it lands, so a way to the landing past it, a hold
    /// after the landing included, would land the work with the hold unjudged.
    /// </summary>
    public const string HoldLate = "hold-late";

    /// <summary>A hold in a graph with no landing (LANDNODE1k): it judges the work before it lands, and nothing lands.</summary>
    public const string HoldUnlanded = "hold-unlanded";
}
