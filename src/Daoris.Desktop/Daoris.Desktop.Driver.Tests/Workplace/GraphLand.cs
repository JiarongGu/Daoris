using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound to a saved graph whose <c>land</c> carries the options a landing test needs, and the gate a door hands the landing for it: the
/// process read from that binding, with nothing holding. How the work lands is the bound version's <c>land</c>, as the runtime's performer lands it.
/// </summary>
internal static class GraphLand
{
    /// <summary>A merge into the line.</summary>
    public const string Merge = """{"method":"merge"}""";

    /// <summary>A branch made by <paramref name="pattern"/> for the line to integrate, which the graph's <c>integrate</c> step asks for.</summary>
    public static string Integrate(string pattern) => $$"""{"method":"integrate","pattern":"{{pattern}}"}""";

    /// <summary>A branch made for the person by <paramref name="pattern"/>, no plugin asked; squashed to one commit where <paramref name="squash"/>.</summary>
    public static string Branch(string pattern, bool squash = false) =>
        $$"""{"method":"branch","pattern":"{{pattern}}"{{(squash ? ",\"squash\":true" : "")}}}""";

    /// <summary>
    /// The graph a run lands by: <c>claimed</c>, then <c>land</c> by <paramref name="land"/>, with the <c>push</c> and the plugin's
    /// <c>&lt;plugin&gt;/pull-request</c> step after it where <paramref name="pullRequest"/> names that plugin, or, where
    /// <paramref name="integrate"/> is given, an <c>integrate</c> step asking with that request, then the line.
    /// </summary>
    public static string Graph(string land, string? integrate = null, string? pullRequest = null) => integrate is not null
        ? $$$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"line",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{{land}}}},
                      {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{integrate}}}"}},
                      {"id":"line","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>i","kind":"after","from":"landing","to":"integrate"},
                      {"id":"i>n","kind":"after","from":"integrate","to":"line"}]}
            """
        : pullRequest is not null
        ? $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"pr",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{land}}},
                      {"id":"push","kind":"push","version":1,"reach":"performed"},{"id":"pr","kind":"{{pullRequest}}/pull-request","version":1,"reach":"performed"}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>p","kind":"after","from":"landing","to":"push"},
                      {"id":"p>pr","kind":"after","from":"push","to":"pr"}]}
            """
        : $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"landing",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{{land}}}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
            """;

    /// <summary>
    /// Run <paramref name="run"/> in <paramref name="repository"/> bound to a saved graph landing by <paramref name="land"/>, and the gate that lands
    /// by it: its process read from the binding, as every door reads it.
    /// </summary>
    public static IWorkplaceGate Gate(string home, string land, string run = "q1", string repository = RunHome.Repository, string? workspace = null,
        string? integrate = null, string? pullRequest = null)
    {
        // A workflow of its own per landing, so a second landing's graph saved in one home never replaces the version an earlier gate bound.
        var workflow = "lands-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(land + integrate + pullRequest)))[..8].ToLowerInvariant();
        var digest = RunHome.SaveGraph(home, Graph(land, integrate, pullRequest), workflow);
        var binding = new WorkflowRunBinding(run, repository, "default", workflow, WorkflowLevels.Repository, DateTimeOffset.UtcNow)
        {
            Version = 1, Digest = digest,
        };
        new WorkflowRunFiles(home).Bind(binding);
        var process = WorkflowProcesses.Bound(home, DriverConfig.Load(DaorisHome.Entry(home, HomeEntry.DriverConfig)), repository, workspace, run, binding);
        Assert.Null(process.Problem);
        return new Lets(process);
    }

    /// <summary>A gate whose every part lets the work go, read with a bound run's process.</summary>
    private sealed record Lets(IWorkplaceProcess Process) : IWorkplaceGate
    {
        public string? Tip { get; init; }

        public IWorkplaceGate WithTip(string? tip) => this with { Tip = tip };

        public TreeLanding? Refusal => null;

        public LandingReview? ReviewLanding => null;

        public LandingOpinion? OpinionLanding => null;

        public bool GraphAsksPass => false;

        public IReadOnlyList<string> PlanLines() => [];

        public LandingLine? Held(string session, (string Workspace, string Repository) owner, TreeLanding landed, string door) => null;
    }
}
