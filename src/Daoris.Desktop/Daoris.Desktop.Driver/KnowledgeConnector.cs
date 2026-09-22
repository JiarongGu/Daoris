namespace Daoris.Driver;

/// <summary>
/// The knowledge server a driven session is handed over the protocol door (ACP4).
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The composed target tells every session to claim its quest and close
/// it over its own connector. The PIPE door manages that only because the repository's own
/// `.mcp.json` wires the tools — and an adopted repository may not have one, while the driver may
/// never reach in and write it (`reaching-in`, D32). ACP carries the wiring on `session/new`, so the
/// protocol door hands the session its voice with <b>nothing written anywhere</b>.</para>
///
/// <para>Measured before it was built: without this a real driven session came up, streamed "I'll
/// start by taking the quest", called `take`, found no such tool and ended its turn having touched
/// nothing.</para>
/// </remarks>
public static class KnowledgeConnector
{
    /// <summary>An explicit path to the MCP host, for a machine that keeps it somewhere of its own.</summary>
    public const string PathVariable = "DAORIS_MCP_HOST";

    /// <summary>What the agent calls it — its tools arrive as <c>mcp__daoris-knowledge__*</c>.</summary>
    public const string ServerName = "daoris-knowledge";

    public static string ExecutableName =>
        OperatingSystem.IsWindows() ? "daoris-knowledge.exe" : "daoris-knowledge";

    /// <summary>
    /// Everything the host needs to find the same store this driver is reading.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Passed through, never invented.</b> A scratch run overrides these, and a session that
    /// wrote to the machine's real store because the overrides did not travel is the failure that
    /// would be hardest to see — the run would look right and the wrong database would change.
    /// </remarks>
    private static readonly string[] Passed =
    [
        "DAORIS_KNOWLEDGE_ROOT",
        "DAORIS_KNOWLEDGE_DB",
        "DAORIS_REMOTE_CONFIG",
        "DAORIS_REMOTE_URL",
        "DAORIS_REMOTE_KEY",
        "DAORIS_REMOTE_WORKSPACE",
    ];

    /// <summary>
    /// The server to offer, or <c>null</c> when this machine has no host to offer.
    /// </summary>
    /// <remarks>
    /// Null is an answer, not a failure: the session drives exactly as it did before ACP4, which is
    /// without a connector. Refusing to start would make a missing optional binary fatal to a loop
    /// that has other work to do.
    /// </remarks>
    public static AcpMcpServer? Offer(
        string? explicitPath, string? home, string baseDirectory,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var located = Locate(explicitPath, home, baseDirectory);
        if (located is null) return null;

        var carried = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Passed)
        {
            var value = environment is not null
                ? (environment.TryGetValue(name, out var held) ? held : null)
                : Environment.GetEnvironmentVariable(name);
            if (value is { Length: > 0 }) carried[name] = value;
        }

        return new AcpMcpServer(ServerName, located, [], carried);
    }

    /// <summary>The first place this machine actually has the host, or null.</summary>
    public static string? Locate(string? explicitPath, string? home, string baseDirectory) =>
        Candidates(explicitPath, home, baseDirectory).FirstOrDefault(File.Exists);

    /// <summary>
    /// Where to look, in the order they deserve trust — the same order the HTTP host's locator uses,
    /// because a machine that installed one installed both.
    /// </summary>
    /// <param name="home">The Daoris home (D63), whose `bin/` is the CLI's install landing place; null when the machine has none.</param>
    public static IReadOnlyList<string> Candidates(
        string? explicitPath, string? home, string baseDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath)) candidates.Add(explicitPath);

        if (home is not null) candidates.Add(Path.Combine(home, "bin", ExecutableName));

        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        if (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "src", "Daoris.Service", "Daoris.Service.Mcp");
            foreach (var flavour in new[] { "Debug", "Release" })
            {
                candidates.Add(Path.Combine(project, "bin", flavour, "net10.0", ExecutableName));
            }
        }

        return candidates;
    }
}
