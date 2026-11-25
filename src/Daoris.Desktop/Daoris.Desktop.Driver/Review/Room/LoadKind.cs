namespace Daoris.Driver;

/// <summary>What a heavy thing costs while it runs: memory in bytes and the cores it keeps busy (a mean).</summary>
public sealed record LoadCost(long Memory, double Cores);

/// <summary>
/// The kinds of load a repository puts on the machine: a <c>build</c>, a <c>test</c> run, an <c>environment</c> (a database and
/// services, gigabytes and slow to start) and the <c>gpu</c>. A cost is learned per repository, kind and gate
/// (<see cref="LearnedNeeds"/>); the kind's declared cost stands only until a gate has run.
/// </summary>
public static class LoadKind
{
    public const string Build = "build";
    public const string Test = "test";
    public const string Environment = "environment";
    public const string Gpu = "gpu";

    /// <summary>Every kind a repository's <c>loads</c> may name.</summary>
    public static IReadOnlyList<string> All { get; } = [Build, Test, Environment, Gpu];

    /// <summary>Whether <paramref name="kind"/> is one.</summary>
    public static bool Takes(string kind) => All.Contains(kind);

    /// <summary>What a kind costs before one of its gates has run: a test run is the merge tool's 8 GB for a Process suite.</summary>
    public static LoadCost Declared(string kind) => kind switch
    {
        Build => new LoadCost(4L << 30, 4),
        Environment => new LoadCost(4L << 30, 1),
        Gpu => new LoadCost(2L << 30, 1),
        _ => new LoadCost(8L << 30, 4),
    };

    /// <summary>A leased kind is started by an agent and kept (an environment, the GPU); a run kind ends with its gate.</summary>
    public static bool Leased(string kind) => kind is Environment or Gpu;
}

/// <summary>
/// What a repository's rule says of one kind (<c>loads</c> in its <c>daoris.gates.json</c>): how many of the kind it holds at once
/// (<paramref name="Max"/>, null for the machine's limit), how many one agent holds (<paramref name="PerAgent"/>) and whether agents
/// of the repository share one running instance of it (<paramref name="Reuse"/>) instead of each starting their own.
/// </summary>
public sealed record KindRule(int? Max, int? PerAgent, bool Reuse)
{
    /// <summary>What a kind is held to where its repository says nothing: a repository holds two environments and one GPU lease, an agent one of either, never ten.</summary>
    public static KindRule Default(string kind) => kind switch
    {
        LoadKind.Environment => new KindRule(2, 1, false),
        LoadKind.Gpu => new KindRule(1, 1, false),
        _ => new KindRule(null, null, false),
    };
}
