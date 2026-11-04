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
        $$"""{"method":"branch","plugin":"none","pattern":"{{pattern}}"{{(squash ? ",\"squash\":true" : "")}}}""";

    /// <summary>A pull request <paramref name="plugin"/> opens for a branch made by <paramref name="pattern"/>.</summary>
    public static string PullRequest(string pattern, string plugin, bool squash = false) =>
        $$"""{"method":"pull-request","plugin":"{{plugin}}","pattern":"{{pattern}}"{{(squash ? ",\"squash\":true" : "")}}}""";

    /// <summary>
    /// The graph a run lands by: <c>claimed</c>, then <c>land</c> by <paramref name="land"/>, with the tail a pull request brings, or, where
    /// <paramref name="integrate"/> is given, an <c>integrate</c> step asking with that request, then the line.
    /// </summary>
    public static string Graph(string land, string? integrate = null) => integrate is not null
        ? $$$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"line",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{{land}}}},
                      {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{integrate}}}"}},
                      {"id":"line","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>i","kind":"after","from":"landing","to":"integrate"},
                      {"id":"i>n","kind":"after","from":"integrate","to":"line"}]}
            """
        : land.Contains("\"pull-request\"", StringComparison.Ordinal)
        ? $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"pr",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{land}}},
                      {"id":"pushed","kind":"pushed","version":1,"reach":"observed"},{"id":"pr","kind":"pull-request","version":1,"reach":"observed"}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>p","kind":"after","from":"landing","to":"pushed"},
                      {"id":"p>pr","kind":"after","from":"pushed","to":"pr"}]}
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
        string? integrate = null)
    {
        // A workflow of its own per landing, so a second landing's graph saved in one home never replaces the version an earlier gate bound.
        var workflow = "lands-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(land + integrate)))[..8].ToLowerInvariant();
        var digest = RunHome.SaveGraph(home, Graph(land, integrate), workflow);
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
