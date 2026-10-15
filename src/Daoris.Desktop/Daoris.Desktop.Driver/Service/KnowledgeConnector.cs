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
///
/// <para>Its names, the order of the places to look and the server's surroundings, and <c>Offer</c>,
/// <c>Locate</c> and <c>Candidates</c>, which look at this machine, are one type (FLAT1b-iii-a).</para>
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

    /// <summary>What marks the workspace a development build runs below: its manifest.</summary>
    public const string WorkspaceMarker = "daoris.json";

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
    /// The server to offer for a connector found at <paramref name="located"/>: the store's surroundings passed through, then
    /// what this session's connector carries beyond them, then the home.
    /// </summary>
    /// <param name="variable">A variable of the surroundings the session runs in: this process's, or a test's.</param>
    /// <param name="scope">
    /// What THIS session's connector carries beyond the store — an intake's ask and session (D65 §1b),
    /// which is how a publish from a room that is no repository is asked by the ask. Null for a
    /// quest's session, whose repository already says everything.
    /// </param>
    public static AcpMcpServer Server(
        string located, Func<string, string?> variable, string? home, IReadOnlyDictionary<string, string>? scope)
    {
        var carried = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Passed)
        {
            var value = variable(name);
            if (value is { Length: > 0 }) carried[name] = value;
        }

        foreach (var (name, value) in scope ?? new Dictionary<string, string>())
        {
            if (value is { Length: > 0 }) carried[name] = value;
        }

        // 🔴 CONNECTOR2. Codex hands an MCP server only its entry's own variables, and the host refuses to start
        // with no home (exit 2, before it answers `initialize`); Claude hands its whole environment, which hid it.
        // The scope's value wins (set above), then the environment's (read here), then the home this driver resolved.
        if (!carried.ContainsKey(DaorisHome.Variable))
        {
            var named = variable(DaorisHome.Variable);
            var chosen = named is { Length: > 0 } ? named : home;
            if (chosen is { Length: > 0 }) carried[DaorisHome.Variable] = chosen;
        }

        return new AcpMcpServer(ServerName, located, [], carried);
    }

    /// <summary>
    /// What a connector carries beyond the store (PERM2): the session, so a rule proposal says who made
    /// it, and this driver's home, so it lands beside the rules it would change — which is the folder
    /// `driver.json` sits in and need not be the account's home. An intake's ask rides too (D65 §1b).
    /// A driven session's and a conversation's alike, so it is the connector's, not the loop's (LOOPOUT1).
    /// </summary>
    internal static IReadOnlyDictionary<string, string> SessionScope(
        string home, string sessionId, IReadOnlyDictionary<string, string>? scope)
    {
        var carried = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RulesHomeVariable] = home,
            [IntakeRoom.SessionVariable] = sessionId,
        };
        foreach (var (name, value) in scope ?? new Dictionary<string, string>()) carried[name] = value;
        return carried;
    }

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
    /// <param name="workspace">The workspace a development build runs below, the folder holding <see cref="WorkspaceMarker"/>; null for none.</param>
    public static IReadOnlyList<string> Ranked(
        string? explicitPath, string? home, string baseDirectory, string? workspace)
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

        if (home is not null) candidates.Add(DaorisHome.Entry(home, HomeEntry.Bin, ExecutableName));

        if (workspace is not null)
        {
            var project = Path.Combine(workspace, "src", "Daoris.Service", "Daoris.Service.Mcp");
            foreach (var flavour in new[] { "Debug", "Release" })
            {
                candidates.Add(Path.Combine(project, "bin", flavour, "net10.0", ExecutableName));
            }
        }

        return candidates;
    }

    /// <summary>
    /// The server to offer, or <c>null</c> when this machine has no host to offer.
    /// </summary>
    /// <remarks>
    /// Null is an answer, not a failure: the session drives exactly as it did before ACP4, which is
    /// without a connector. Refusing to start would make a missing optional binary fatal to a loop
    /// that has other work to do.
    /// </remarks>
    /// <param name="environment">The surroundings to pass through; null reads this process's.</param>
    /// <param name="scope">What this session's connector carries beyond the store (<see cref="Server"/>).</param>
    public static AcpMcpServer? Offer(
        string? explicitPath, string? home, string baseDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyDictionary<string, string>? scope = null)
    {
        var located = Locate(explicitPath, home, baseDirectory);
        if (located is null) return null;

        Func<string, string?> variable = environment is not null
            ? name => environment.TryGetValue(name, out var held) ? held : null
            : Environment.GetEnvironmentVariable;
        return Server(located, variable, home, scope);
    }

    /// <summary>The first place this machine actually has the host, or null.</summary>
    public static string? Locate(string? explicitPath, string? home, string baseDirectory) =>
        Candidates(explicitPath, home, baseDirectory).FirstOrDefault(File.Exists);

    /// <summary>
    /// Where to look, in the order they deserve trust (<see cref="Ranked"/>), a development build's
    /// workspace found by walking up from the running application to the folder that holds its manifest.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string? explicitPath, string? home, string baseDirectory) =>
        Ranked(explicitPath, home, baseDirectory, FolderWith(baseDirectory, WorkspaceMarker));

    /// <summary>The nearest folder at or above <paramref name="from"/> holding a file named <paramref name="marker"/>, whole; null for none.</summary>
    private static string? FolderWith(string from, string marker)
    {
        var directory = new DirectoryInfo(from);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, marker)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
