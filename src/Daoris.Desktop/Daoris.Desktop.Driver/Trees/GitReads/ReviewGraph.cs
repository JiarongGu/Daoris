namespace Daoris.Driver;

/// <summary>
/// One node of Review's graph (GITAPP1a-ii, the git-app design P1.2): a read, the nodes whose answers it needs, whether it can run a
/// configured program (Π) and whether it was started before the condition that decides its use (speculative). What the census
/// generates depth and starts from.
/// </summary>
internal sealed class ReviewNode(string name, IReadOnlyList<ReviewNode> needs, bool program, bool speculative, GitBudget budget)
{
    private readonly TaskCompletionSource _answered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _order = -1;
    private int _used = -1;

    /// <summary>The design's name for it: <c>T</c>, <c>P</c>, <c>K</c>, <c>E</c>, <c>K′</c>, <c>G1</c>-<c>G3</c>, <c>B1</c>, <c>B2</c>, <c>N↑</c>, …</summary>
    public string Name { get; } = name;

    /// <summary>The nodes whose answers it waits for; a Π node's Π predecessor among them.</summary>
    public IReadOnlyList<ReviewNode> Needs { get; } = needs;

    /// <summary>Whether it reads file content through git's diff or merge machinery, so a configured program may run (Π).</summary>
    public bool Program { get; } = program;

    /// <summary>Whether it was started before the condition that decides whether its answer is used (P1.1's one rule).</summary>
    public bool Speculative { get; } = speculative;

    /// <summary>Whose its starts are: the review's, the untracked tail's, the landed review's or the offer's.</summary>
    public GitBudget Budget { get; } = budget;

    /// <summary>Its place among the graph's starts, from 0; -1 until it starts.</summary>
    public int Order => Volatile.Read(ref _order);

    /// <summary>The longest chain of dependent nodes ending at it, itself counted: 1 + its deepest need's.</summary>
    public int Depth { get; private set; }

    /// <summary>Whether its answer was used: a node not speculative always is; a speculative one only where its condition went its way.</summary>
    public bool Used => !Speculative || Volatile.Read(ref _used) == 1;

    /// <summary>Whether it has answered (or failed): what a node needing it waits for.</summary>
    public Task Answered => _answered.Task;

    internal void Start(int order)
    {
        Depth = 1 + Needs.Select(need => need.Depth).DefaultIfEmpty(0).Max();
        Volatile.Write(ref _order, order);
    }

    internal void Answer() => _answered.TrySetResult();

    internal void Use(bool used) => Interlocked.CompareExchange(ref _used, used ? 1 : 0, -1);

    public override string ToString() => Name;
}

/// <summary>
/// The executor of one open of Review (GITAPP1a-ii, the git-app design P1.1-P1.2): each git start a node, started the moment the
/// nodes it needs have answered. The reads that can run a configured program (Π) are one chain in today's order: each Π node also
/// needs the Π node before it, so no two run together and none is speculative. Everything else runs at once and ahead.
/// </summary>
/// <remarks>
/// <para>🔴 <b>It decides nothing.</b> Which nodes run, on which answers, is the review's (<see cref="ReviewGraphReads"/>) by today's
/// choices (<see cref="ReviewChoices"/>); the executor only starts each node when its needs have answered and keeps the record the
/// census and <c>review.read</c> read (its depth, its starts, the speculative ones used and wasted).</para>
///
/// <para><b>Nothing kept</b>: one graph per open, dropped with it.</para>
/// </remarks>
internal sealed class ReviewGraph
{
    private readonly object _gate = new();
    private readonly List<ReviewNode> _nodes = [];
    private readonly List<Task> _tracked = [];
    private ReviewNode? _lastProgram;
    private int _started;

    /// <summary>Every node made so far, in the order they were made.</summary>
    public IReadOnlyList<ReviewNode> Nodes
    {
        get
        {
            lock (_gate) return [.. _nodes];
        }
    }

    /// <summary>The longest chain of dependent starts among the review's own nodes (its budget and the untracked tail's).</summary>
    public int Depth => Nodes.Where(node => node.Order >= 0 && node.Budget is GitBudget.Review or GitBudget.Untracked)
        .Select(node => node.Depth).DefaultIfEmpty(0).Max();

    /// <summary>The speculative nodes started whose answers were not used.</summary>
    public int Wasted => Nodes.Count(node => node.Order >= 0 && !node.Used);

    /// <summary>
    /// A node outside Π: started once each of <paramref name="needs"/> has answered (a null need is one that did not run), counted in
    /// <paramref name="budget"/>. Its answer is <paramref name="read"/>'s.
    /// </summary>
    internal (ReviewNode Node, Task<T> Answer) Run<T>(
        string name, IEnumerable<ReviewNode?> needs, Func<Task<T>> read, bool speculative = false, GitBudget budget = GitBudget.Review)
    {
        var node = Add(name, needs, program: false, speculative, budget);
        return (node, StartAsync(node, read));
    }

    /// <summary>
    /// A Π node: asked by the review's Π chain where today's chain would ask it, after its <paramref name="needs"/> and after the Π
    /// node before it, never speculatively.
    /// </summary>
    internal async Task<T> ProgramAsync<T>(
        string name, IEnumerable<ReviewNode?> needs, Func<Task<T>> read, GitBudget budget = GitBudget.Review)
    {
        ReviewNode node;
        lock (_gate)
        {
            node = Add(name, [.. needs, _lastProgram], program: true, speculative: false, budget);
            _lastProgram = node;
        }

        return await StartAsync(node, read).ConfigureAwait(false);
    }

    /// <summary>A flow of the review's that may start nodes after the answer is decided: awaited by <see cref="SettledAsync"/>.</summary>
    internal T Track<T>(T flow) where T : Task
    {
        lock (_gate) _tracked.Add(flow);
        return flow;
    }

    /// <summary>
    /// Every node started has answered and every tracked flow has ended, whatever each answered: nothing of the open outlives it
    /// (a speculative node's answer unused is dropped here, its git already ended).
    /// </summary>
    internal async Task SettledAsync()
    {
        while (true)
        {
            Task[] all;
            lock (_gate) all = [.. _tracked, .. _nodes.Select(node => node.Answered)];
            try
            {
                await Task.WhenAll(all).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Each failure is the flow's own, said where its answer was used; an unused one is dropped with it.
            }

            lock (_gate)
            {
                if (_tracked.Count + _nodes.Count == all.Length) return;
            }
        }
    }

    private ReviewNode Add(string name, IEnumerable<ReviewNode?> needs, bool program, bool speculative, GitBudget budget)
    {
        var node = new ReviewNode(name, [.. needs.OfType<ReviewNode>().Distinct()], program, speculative, budget);
        lock (_gate) _nodes.Add(node);
        return node;
    }

    private async Task<T> StartAsync<T>(ReviewNode node, Func<Task<T>> read)
    {
        try
        {
            // Started the moment its needs have answered, each in its own flow, so a budget set inside one reaches no other.
            await Task.WhenAll(node.Needs.Select(need => need.Answered)).ConfigureAwait(false);
            node.Start(Interlocked.Increment(ref _started) - 1);
            using (GitLaunch.Within(node.Budget))
            {
                return await read().ConfigureAwait(false);
            }
        }
        finally
        {
            node.Answer();
        }
    }
}
