using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>How a node starts (the fabric events design §E2's <c>reach</c>; §D4, with <c>waited</c> added by §E1).</summary>
public static class NodeReach
{
    /// <summary>Git or a plugin reports it: every built-in proof (§C2), a plugin's events (§C4).</summary>
    public const string Observed = "observed";

    /// <summary>Daoris asks a plugin to do it (§D6), or does it itself (<c>quest.close</c>, §E5).</summary>
    public const string Performed = "performed";

    /// <summary>A person reaches it at D156's doors (§D5).</summary>
    public const string Pressed = "pressed";

    /// <summary>It waits on other work through its adapter (<c>wait.unit</c>, §E6).</summary>
    public const string Waited = "waited";

    public static readonly IReadOnlyList<string> All = [Observed, Performed, Pressed, Waited];
}

/// <summary>A node's declared retry (§D4, §D6): how many attempts it may take on one binding, and the delay before each new one.</summary>
/// <param name="MaxAttempts">Attempts on one binding, the first included; a person's new attempt or a fresh binding counts afresh.</param>
/// <param name="DelaysSeconds">The delay before the second attempt, the third, and so on; the last repeats. A copy of what was set.</param>
public sealed record NodeRetry(int MaxAttempts, IReadOnlyList<int> DelaysSeconds)
{
    // FABRIC-NODE1d: copied when set, so a caller's list changing later moves neither the retry nor its declaration's Problem.
    private readonly IReadOnlyList<int> delaysSeconds = Copied(DelaysSeconds);

    public IReadOnlyList<int> DelaysSeconds { get => delaysSeconds; init => delaysSeconds = Copied(value); }

    private static ImmutableArray<int> Copied(IReadOnlyList<int> delays) =>
        ImmutableArray.CreateRange(delays ?? throw new ArgumentNullException(nameof(delays)));

    // FABRIC-UNIT1d: equal by what it says, so a manifest's declaration is the one built by hand.
    public bool Equals(NodeRetry? other) =>
        other is not null && MaxAttempts == other.MaxAttempts && DelaysSeconds.SequenceEqual(other.DelaysSeconds);

    public override int GetHashCode() => DelaysSeconds.Aggregate(MaxAttempts.GetHashCode(), HashCode.Combine);
}

/// <summary>
/// How a plugin's performed node is performed (FABRIC-UNIT1d; the fabric events design §D6, <i>the performer's contract</i>): what the
/// kit promises and PERFORM1 dispatches by. Only a plugin's own <c>performed</c> node declares one; a manifest spells it in the step's
/// <c>perform</c>, its point <c>work/perform</c>.
/// </summary>
/// <param name="Idempotent">The plugin deduplicates a redelivered idempotency key durably: the same key never makes a second external effect.</param>
/// <param name="Reconcile">The plugin answers <c>work/reconcile</c>: what became of an attempt whose outcome is unknown.</param>
/// <param name="Transient">
/// The failure codes worth retrying, under the node's <see cref="NodeDeclaration.Retry"/>; any other code stops it for the person. A
/// copy of what was set.
/// </param>
public sealed record NodePerform(bool Idempotent, bool Reconcile, IReadOnlyList<string> Transient)
{
    /// <summary>A failure code's shape (§D6's <c>unavailable</c>, <c>rate-limited</c>): lowercase letters, digits and dashes, at most 40.</summary>
    public static readonly System.Text.RegularExpressions.Regex CodeShape =
        new(@"^[a-z0-9][a-z0-9-]{0,39}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> transient = Copied(Transient);

    public IReadOnlyList<string> Transient { get => transient; init => transient = Copied(value); }

    private static ImmutableArray<string> Copied(IReadOnlyList<string> codes) =>
        ImmutableArray.CreateRange(codes ?? throw new ArgumentNullException(nameof(codes)));

    public bool Equals(NodePerform? other) =>
        other is not null && Idempotent == other.Idempotent && Reconcile == other.Reconcile
        && Transient.SequenceEqual(other.Transient, StringComparer.Ordinal);

    public override int GetHashCode() =>
        Transient.Aggregate(HashCode.Combine(Idempotent, Reconcile), (hash, code) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(code)));
}

/// <summary>
/// One node (FABRIC-NODE1; the fabric events design §E2): <c>{ id, kind, version, reach, inputs, outputs, evidence, retry?,
/// timeoutSeconds?, options? }</c>. Its <c>kind</c> chooses an adapter and its <c>reach</c> says how it starts; its lifecycle is
/// <see cref="NodeLifecycle"/>'s, the same for every kind. The unit it acts on is its run's binding (<see cref="NodeStatus.Unit"/>).
/// </summary>
/// <param name="Id">Its id in its graph.</param>
/// <param name="Kind">One of <see cref="NodeKinds"/>, or a plugin's own <c>&lt;plugin&gt;/&lt;node&gt;</c>.</param>
/// <param name="Version">Its declaration's version: an acceptance binds it, so a new version invalidates the old acceptances.</param>
/// <param name="Reach">One of <see cref="NodeReach"/>: a built-in kind's own, a plugin's observed or performed.</param>
public sealed record NodeDeclaration(string Id, string Kind, int Version, string Reach)
{
    /// <summary>No ports: a declaration's inputs or outputs before any are named.</summary>
    public static readonly IReadOnlyDictionary<string, string> NoPorts = ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);

    // FABRIC-NODE1d: each collection is copied when it is set, in name order, so a graph's digest over declarations never moves with a
    // caller's dictionary.
    private readonly IReadOnlyDictionary<string, string> inputs = NoPorts;
    private readonly IReadOnlyDictionary<string, string> outputs = NoPorts;

    /// <summary>
    /// Its inputs by name, each with the schema it takes: filled by <c>data</c> wires with references, never payloads. Each is mandatory
    /// (§E3). A copy of what was set.
    /// </summary>
    public IReadOnlyDictionary<string, string> Inputs { get => inputs; init => inputs = Frozen(value); }

    /// <summary>Its declared outputs by name, each with its schema: a reached node exposes each as a <see cref="NodePort"/>. A copy of what was set.</summary>
    public IReadOnlyDictionary<string, string> Outputs { get => outputs; init => outputs = Frozen(value); }

    private static IReadOnlyDictionary<string, string> Frozen(IReadOnlyDictionary<string, string> ports) =>
        (ports ?? throw new ArgumentNullException(nameof(ports))).ToImmutableSortedDictionary(StringComparer.Ordinal);

    /// <summary>The schema of the evidence it is reached on, or null for a built-in, whose evidence is its record's.</summary>
    public string? Evidence { get; init; }

    public NodeRetry? Retry { get; init; }

    /// <summary>A persisted deadline (§D4), delivered as an event: it never means success, failure, or a deletion.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// The option a plugin's own node declares to let a person reject it (§E2: <i>only where the node declares it</i>). A built-in
    /// kind's judgement is the kind's, never a declaration's.
    /// </summary>
    public bool Rejectable { get; init; }

    /// <summary>
    /// Whether the person may pass a plugin's own node over (D157's skip): a plugin's declaration may forbid it with <c>false</c>
    /// (FABRIC-NODE1c). A built-in kind's skip is the kind's, never a declaration's.
    /// </summary>
    public bool Skippable { get; init; } = true;

    /// <summary>
    /// How a plugin's own <c>performed</c> node is performed (FABRIC-UNIT1d, §D6), or null: none declared, which is dispatched and never
    /// redelivered by itself. Every other node declares none.
    /// </summary>
    public NodePerform? Perform { get; init; }

    private readonly IReadOnlyDictionary<string, string> options = NoPorts;

    /// <summary>
    /// Its adapter's options by name (§E2's <c>options</c>, as a human node's <c>brief</c>), each its JSON value's text as written: what
    /// the adapter reads, never a condition (FABRIC-GRAPH1). A copy of what was set, in name order, so a graph's digest holds it.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get => options; init => options = Frozen(value); }

    /// <summary>
    /// Made by decoding a D157 version (a sink, a person's task or a join, §E7), never authored: counted apart from a graph's 24-node
    /// bound (FABRIC-GRAPH1, <see cref="NodeGraphs.MaxNodes"/>). A graph read from a person's file declares none.
    /// </summary>
    public bool Synthetic { get; init; }

    /// <summary>
    /// FABRIC-UNIT1d: two declarations are equal where they say the same, whoever built their collections, so a manifest's step is the
    /// declaration built by hand and a graph compares declarations by what they say (GRAPH1's options and synthetic mark included).
    /// </summary>
    public bool Equals(NodeDeclaration? other) =>
        other is not null
        && string.Equals(Id, other.Id, StringComparison.Ordinal) && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
        && Version == other.Version && string.Equals(Reach, other.Reach, StringComparison.Ordinal)
        && Same(Inputs, other.Inputs) && Same(Outputs, other.Outputs)
        && string.Equals(Evidence, other.Evidence, StringComparison.Ordinal) && Equals(Retry, other.Retry)
        && TimeoutSeconds == other.TimeoutSeconds && Rejectable == other.Rejectable && Skippable == other.Skippable
        && Equals(Perform, other.Perform) && Same(Options, other.Options) && Synthetic == other.Synthetic;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(Kind, StringComparer.Ordinal);
        hash.Add(Version);
        hash.Add(Reach, StringComparer.Ordinal);
        foreach (var (name, schema) in Inputs.Concat(Outputs).Concat(Options))
        {
            hash.Add(name, StringComparer.Ordinal);
            hash.Add(schema, StringComparer.Ordinal);
        }

        hash.Add(Evidence, StringComparer.Ordinal);
        hash.Add(Retry);
        hash.Add(TimeoutSeconds);
        hash.Add(Rejectable);
        hash.Add(Skippable);
        hash.Add(Perform);
        hash.Add(Synthetic);
        return hash.ToHashCode();
    }

    // Both are ordinal-sorted copies (FABRIC-NODE1d), so the same ports are in the same order.
    private static bool Same(IReadOnlyDictionary<string, string> ports, IReadOnlyDictionary<string, string> others) =>
        ports.Count == others.Count && ports.Zip(others).All(pair =>
            string.Equals(pair.First.Key, pair.Second.Key, StringComparison.Ordinal)
            && string.Equals(pair.First.Value, pair.Second.Value, StringComparison.Ordinal));
}
