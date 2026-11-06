namespace Daoris.Driver;

/// <summary>
/// A plugin's step, <c>&lt;plugin&gt;/&lt;step&gt;</c>, as the kit reads it (D247 point 4): its contract row from its declaration (its plugin's
/// believed answer reaches and fails it, a person rejects or skips it as declared), bound by its place after the landing, carried out by its
/// plugin and titled by its step and its plugin. A step name Daoris has a half of reads as that half (<see cref="PullRequestStep"/>).
/// </summary>
public record PluginStep : INodeKind, IStarts
{
    /// <summary>The step's kind, <c>&lt;plugin&gt;/&lt;step&gt;</c>; empty on a registered half, which <see cref="NodeKit.Find"/> names as it hands it out.</summary>
    public string Kind { get; init; } = "";

    /// <summary>
    /// The node's authority from its declaration. A plugin's answer is named <see cref="NodeSource.Plugin"/> here alone: that the answer came from
    /// the declaring plugin is <see cref="WorkBelief.Completion"/>'s sender check, not the lifecycle's.
    /// </summary>
    public NodeAuthority Authority(NodeDeclaration node) =>
        new(node.Kind, node.Reach, NodeSource.Plugin, NodeSource.Plugin, node.Rejectable, node.Skippable, MayEnd: true, Evidenced: true);

    public RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubjects.Placed(node, afterHanded);

    public IReadOnlyList<NodeOptionField> Options => [];

    /// <summary>None of its own: a plugin's declaration is held to the checks every declaration is.</summary>
    public string? Problem(NodeDeclaration node) => null;

    public IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node) => [];

    public virtual NodePart Part(NodeDeclaration node) => new(node.Kind, WorkflowParticipation.Automatic, WorkflowExecutor.Plugin);

    public string Offered => PaletteKind.Alone;

    public IReadOnlyList<string> RoleTakes => [];

    /// <summary>None beyond its declaration's own <see cref="NodeDeclaration.Evidence"/>.</summary>
    public string? Evidence => null;

    public string? Title => Kind.IndexOf('/') is var slash and > 0 ? $"{Kind[(slash + 1)..]} by `{Kind[..slash]}`" : null;

    public NodeLook Look => NodeLook.Plugin;

    public bool ReadsTheLanding => false;

    public LandingGuard Guard => LandingGuard.None;

    public bool EndsByTheLine => false;

    /// <summary>None: its door is its plugin's port, the runtime's own.</summary>
    INodeDoor? IStarts.Door(NodeDoorWiring wiring) => null;

    /// <summary>
    /// Observed, it is begun as a built-in is, ready to active with its firings written, and published to its plugin only after; performed, it is
    /// sent to its plugin within the pass's sends, once its process is up (a process not up yet is awaited, and the start made at a later pass:
    /// never sent blind). Either waits where its branch's tip was not read this pass: nothing is bound or sent on a guess.
    /// </summary>
    async Task<WorkPerformed?> IStarts.StartAsync(NodeStarting starting, CancellationToken ct)
    {
        var (governed, node) = (starting.Governed, starting.Node);
        var unread = starting.Tipped && starting.Tip is null;
        if (node.Reach == NodeReach.Observed && governed.Observable.Contains(node.Id)) return unread ? null : starting.Begin();
        if (!governed.Dispatchable.Contains(node.Id) || unread || starting.Sends.Left <= 0
            || starting.Port.GenerationOf(NodeGoverning.PluginOf(node.Kind)) is null || !starting.Sends.Take())
        {
            return null;
        }

        // A bound quest's revision is its chain's and branches', from the last full read, which a wake reuses: a start on a wake reads them
        // again first, and a start whose revision moved is not made, so no attempt is bound to an old tip.
        if (governed.Closes && !starting.ReadThisPass && governed.Unit.Records.Quest is { } bound)
        {
            var again = await starting.Units(ct).ConfigureAwait(false);
            if (NodeClosing.Moved(governed, bound, again, NodeGoverning.Quests(again)) is { } moved)
            {
                starting.Drop();
                var line = $"work  {governed.Unit.Id}: its `{node.Id}` was not started: {moved}, so it is judged again on what is there now.";
                if (!starting.Said.Contains(line, StringComparer.Ordinal)) starting.Said.Add(line);
                return null;
            }
        }

        return await starting.Performer.StartAsync(node, starting.Start, ct).ConfigureAwait(false);
    }
}
