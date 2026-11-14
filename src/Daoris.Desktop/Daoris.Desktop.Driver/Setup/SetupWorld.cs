using System.Diagnostics;
using System.Text;
using Daoris.Contracts.Knowledge;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The world a set-up press reads on this machine: the service's doors, the repository's line read as git
/// objects, and the <c>node</c> and <c>daoris</c> a child of Daoris would find. The press itself is
/// <see cref="SetupPress"/>; this is only where its facts come from.
/// </summary>
public sealed class SetupWorld(ServiceClient service, string home) : ISetupWorld
{
    public IsolateStep? IsolationOf(DriverConfig config, RepoView repository) =>
        PlaceIsolates.Of(home, config, repository.Repository, repository.Workspace);

    /// <summary>How long a version question may take before the program is called unanswering.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Whether this machine's driven sessions may drive a repository that has not adopted Daoris: where any entry of Build's order may
    /// (<see cref="BuildDoor"/>), the loop's own answer, read against the plugins' agents as a tick reads it, and the terminal's every
    /// console's: a door this build does not know is refused (<see cref="UnknownDoorException"/>), as every surface's read refuses it.
    /// </summary>
    public static bool Unadopted(DriverConfig config, string home)
    {
        var built = ProviderRegistry.Built();
        return BuildDoor.Drives(home, config, built.WithPlugins(PluginCatalog.Load(home, built.Names)));
    }

    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => service.RegistryAsync(ct);

    public async Task<bool> NothingReadsItAsync(RepoView repository, string line, CancellationToken ct) =>
        await NothingReadsIt.JudgeAsync(repository, line, await service.SessionRunsAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct)
    {
        var line = await CanonicalLine.ResolveAsync(repository.Root!, repository.Repository, repository.Workspace, config, ct).ConfigureAwait(false);
        return line.Branch is null
            ? new LineReading(null, "it has no line: none is set, and git names none")
            : await LayoutGit.ReadAsync(repository.Root!, line.Branch, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>node</c> Tools resolves and the doctrine tool the install carries (<see cref="DoctrineTool.OfHome"/>: its bundle
    /// by its place in the install, no <c>PATH</c> lookup), each asked its version as a child would ask it; the bundle runs on that node.
    /// </summary>
    public async Task<SetupTools> ToolsAsync(CancellationToken ct)
    {
        var read = Tools.Read(home);
        var path = ChildPath(read);

        var node = Tools.Resolve(read, home, "node", path);
        var (nodeVersion, nodeProblem) = node.File is null
            ? (null, node.Problem ?? "`node` is not on the PATH Daoris's children start with")
            : await VersionAsync(node.File, read, ct).ConfigureAwait(false);

        var daoris = DoctrineTool.OfHome(home);
        var (daorisVersion, daorisProblem) = daoris is null || node.File is null
            ? (null, (string?)null)
            : await VersionAsync(node.File, read, ct, daoris).ConfigureAwait(false);

        return new SetupTools(node.File, nodeVersion, nodeProblem, daoris, daorisVersion, daorisProblem);
    }

    /// <summary>
    /// The checkout's <c>daoris status --json</c>, asked of the CLI the install carries, run on the tools' node by the bundle's own
    /// path (<see cref="DoctrineTool.OfHome"/>), so the answer is that CLI's own and no table of its is kept here.
    /// </summary>
    public async Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct)
    {
        if (repository.Root is null) return new(null, "it has no checkout here");
        var read = Tools.Read(home);
        if (DoctrineTool.OfHome(home) is not { } bundle) return new(null, "the install carries no doctrine tool: a publish of Daoris that carries it lays it in place");
        var node = Tools.Resolve(read, home, "node", ChildPath(read));
        return node.File is null
            ? new(null, node.Problem ?? "`node` is not on the PATH Daoris's children start with")
            : await DoctrineStatusRead.ReadAsync(node.File, [bundle], repository.Root, read, home, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

    // The repository's own entries alone: canonical doctrine is not what the workspace knows of it.
    public async Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) =>
        [.. (await service.SendAsync(new ListEntries(repository), ct).ConfigureAwait(false)).Answered().Select(entry => entry.Path).OfType<string>()];

    public RepositoryDescription? Describe(string root) => SelfDescriptionFiles.Read(root);

    public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct) =>
        service.AskAsync(workspace, sentence, [], [], to, ct, then: then is null ? null : [new QuestStepWire(to, then.Title, then.Body)]);

    /// <summary>The <c>PATH</c> a child starts with.</summary>
    private string? ChildPath(ToolsRead read)
    {
        var inherited = Environment.GetEnvironmentVariable(Tools.PathVariable);
        return Tools.ChildPath(read, home, inherited) ?? inherited;
    }

    /// <summary>A program's first line to <c>--version</c> (a script's, where it runs one), or what it said in place of one.</summary>
    private async Task<(string? Version, string? Problem)> VersionAsync(string file, ToolsRead read, CancellationToken ct, string? script = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true, // asked from the window's host too: no console may flash (Adapters.Shell)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (script is not null) info.ArgumentList.Add(script);
        info.ArgumentList.Add("--version");
        // Asked as a child is started, with the tools' environment.
        Tools.Hand(info, read, home);

        var run = await ChildProcess.RunAsync(info, Patience, ct, closeInput: false).ConfigureAwait(false);
        if (!run.Started)
        {
            return (null, run.Error is null
                ? $"`{Path.GetFileName(file)}` did not start"
                : $"`{Path.GetFileName(file)}` would not start: {run.Error.Message}");
        }

        if (run.TimedOut) return (null, $"`{Path.GetFileName(file)}` did not answer within {Patience.TotalSeconds:0}s");

        var first = run.Stdout.Trim().Split('\n')[0].Trim();
        return run.ExitCode == 0 && first.Length > 0
            ? (VersionWord(first), null)
            : (null, first.Length > 0 ? first : run.Stderr.Trim().Split('\n')[0].Trim());
    }

    internal static string VersionWord(string first) => first.Split(' ', 2)[0];
}
