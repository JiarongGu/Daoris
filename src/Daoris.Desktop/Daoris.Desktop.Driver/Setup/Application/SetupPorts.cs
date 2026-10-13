namespace Daoris.Driver;

/// <summary>What a set-up press reads (LAYOUT7): the service's doors, the repository's line, the tools a child finds.</summary>
/// <remarks>
/// An interface so the press is held without a service, git or a spawn; <see cref="SetupWorld"/> is the real one. A port of
/// the module's <c>Application</c> (STRUCT3e): the loop and the hosts compose the real one, and the tests hand a stand-in.
/// </remarks>
public interface ISetupWorld
{
    /// <summary>Every repository this machine's host holds, in every circle.</summary>
    Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct);

    /// <summary>The repository's line (D86, as the driver's choices name it), read as git objects.</summary>
    Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct);

    /// <summary>The <c>node</c> and the <c>daoris</c> a child of Daoris would find, and what each answered.</summary>
    Task<SetupTools> ToolsAsync(CancellationToken ct);

    /// <summary>Every quest, closed ones included.</summary>
    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>The path of each entry the workspace's index holds for the repository, once per entry.</summary>
    Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct);

    /// <summary>What its own files say it is (D77), from its checkout; null when they say nothing.</summary>
    RepositoryDescription? Describe(string root);

    /// <summary>An ask at <paramref name="workspace"/> with its receiver named: published at once, as the person's.</summary>
    Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct);
}

/// <summary>What a workspace plan reads beyond the single press's facts (WSSETUP6), and where its lines go.</summary>
/// <remarks>An interface so the plan is held without a service, git or a spawn; <see cref="WorkspaceSetupWorld"/> is the real one.</remarks>
public interface IWorkspaceSetupWorld : ISetupWorld
{
    /// <summary>Every registry row, with the declaration it holds: <i>set up</i> is read from here.</summary>
    Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct);

    /// <summary>How many sessions failed on each quest, by quest id, as the snapshot derives them (DRV6).</summary>
    Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct);

    /// <summary>Every session record this machine keeps, with the repository it ran in: what §4.2 counts.</summary>
    Task<IReadOnlyList<SessionRun>> RunsAsync(CancellationToken ct);

    /// <summary>The files a session's tool calls touched, from its conversation record (D76); none where it has none.</summary>
    IReadOnlyList<ToolTouch> Touched(string session);

    /// <summary>A plan's line, for the machine log.</summary>
    void Said(SetupLine line);
}
