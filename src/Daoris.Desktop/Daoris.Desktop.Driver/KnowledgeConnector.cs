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

    /// <summary>
    /// The home this driver keeps its rules in (PERM2) — where a session's proposal to change them is
    /// written. 🔴 The service's <c>RuleProposalBox.HomeVariable</c> reads the same name.
    /// </summary>
    public const string RulesHomeVariable = "DAORIS_RULES_HOME";

    public static string ExecutableName =>
        OperatingSystem.IsWindows() ? "daoris-knowledge.exe" : "daoris-knowledge";

    /// <summary>
    /// The folder an install keeps its connector in, under `app/` beside the HTTP host's (CONNECTOR1):
    /// the folder `service-publish` publishes this host into, so one recipe lays out both.
    /// </summary>
    public const string Folder = "daoris-knowledge";

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
    /// <param name="scope">
    /// What THIS session's connector carries beyond the store — an intake's ask and session (D65 §1b),
    /// which is how a publish from a room that is no repository is asked by the ask. Null for a
    /// quest's session, whose repository already says everything.
    /// </param>
    public static AcpMcpServer? Offer(
        string? explicitPath, string? home, string baseDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyDictionary<string, string>? scope = null)
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

        foreach (var (name, value) in scope ?? new Dictionary<string, string>())
        {
            if (value is { Length: > 0 }) carried[name] = value;
        }

        return new AcpMcpServer(ServerName, located, [], carried);
    }

    /// <summary>The first place this machine actually has the host, or null.</summary>
    public static string? Locate(string? explicitPath, string? home, string baseDirectory) =>
        Candidates(explicitPath, home, baseDirectory).FirstOrDefault(File.Exists);

    /// <summary>
    /// Where to look, in the order they deserve trust, which is the HTTP host's (<see cref="ServiceHostLocator"/>):
    /// what the person said, then the connector the install carries beside the running application
    /// (`desktop-publish --service` lays it under `app/daoris-knowledge/`), then the home's `bin/`,
    /// where `publish:service --install` puts one, then a development build.
    /// </summary>
    /// <remarks>
    /// 🔴 CONNECTOR1. The install's own copy outranks the home's, as the install's HTTP host does.
    /// An install carried its HTTP host and no connector, so every session was handed the home's
    /// `bin/` copy, which one `publish:service --install` laid down and no republish refreshed. Eight
    /// days old on the install that found it, it opened the shared store, rebuilt it at its own older
    /// schema, and the running host failed every knowledge route after. What the install carries is
    /// built with the shell it runs beside, and a shell published without its connector still falls
    /// through to the home's next.
    /// </remarks>
    /// <param name="home">The Daoris home (D63), whose `bin/` is the CLI's install landing place; null when the machine has none.</param>
    /// <param name="baseDirectory">The running application's folder: an install's `app/` (D93), or a build's output.</param>
    public static IReadOnlyList<string> Candidates(
        string? explicitPath, string? home, string baseDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath)) candidates.Add(explicitPath);

        // Both shapes, as the HTTP host's locator looks: beside the application, where an install's
        // application in `app/` finds it, and under `app/` of the folder it runs from. The install
        // layout (`tools/desktop-publish.mjs`, CONNECTOR_HOME) and this list are a counterpart set,
        // and `deployment-rehearsal.test.ts` reads this file for the other half.
        foreach (var relative in new[] { Folder, Path.Combine("app", Folder) })
        {
            candidates.Add(Path.Combine(baseDirectory, relative, ExecutableName));
        }

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
