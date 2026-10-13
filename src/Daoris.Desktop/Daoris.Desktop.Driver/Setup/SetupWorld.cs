using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The world a set-up press reads on this machine (LAYOUT7): the service's doors, the repository's line read as git
/// objects, and the <c>node</c> and <c>daoris</c> a child of Daoris would find. The press itself is
/// <see cref="SetupPress"/>; this is only where its facts come from.
/// </summary>
public sealed class SetupWorld(ServiceClient service, string home) : ISetupWorld
{
    /// <summary>How long a version question may take before the program is called unanswering.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The door this machine's driven sessions ride: the configured agent's wire, read against the plugins' agents as
    /// a tick reads it. An agent nobody knows is the pipe, the stricter door, as the driver's own answer is.
    /// </summary>
    public static SessionWire DoorOf(DriverConfig config, string home)
    {
        var built = AdapterSet.Built();
        try
        {
            return built.WithPlugins(PluginCatalog.Load(home, built.Names)).Resolve(config.Adapter).Wire;
        }
        catch (DriverException)
        {
            return SessionWire.Pipe;
        }
    }

    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => service.RegistryAsync(ct);

    public async Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct)
    {
        var line = await CanonicalLine.ResolveAsync(repository.Root!, repository.Repository, repository.Workspace, config, ct).ConfigureAwait(false);
        return line.Branch is null
            ? new LineReading(null, "it has no line: none is set, and git names none")
            : await LayoutReader.ReadAsync(repository.Root!, line.Branch, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>node</c> Tools resolves and the <c>daoris</c> the <c>PATH</c> a child starts with finds (TOOLS5's one
    /// environment, D124 §1.3), each asked its version as a child would ask it. In an install that <c>PATH</c> begins
    /// with the install's own launchers (WSSETUP3), so the <c>daoris</c> found is the install's.
    /// </summary>
    public async Task<SetupTools> ToolsAsync(CancellationToken ct)
    {
        var read = Tools.Read(home);
        var inherited = Environment.GetEnvironmentVariable(Tools.PathVariable);
        var path = Tools.ChildPath(read, home, inherited) ?? inherited;

        var node = Tools.Resolve(read, home, "node", path);
        var (nodeVersion, nodeProblem) = node.File is null
            ? (null, node.Problem ?? "`node` is not on the PATH Daoris's children start with")
            : await VersionAsync(node.File, read, ct).ConfigureAwait(false);

        var daoris = CommandPresence.Resolve("daoris", path, startable: true);
        var (daorisVersion, daorisProblem) = daoris is null
            ? (null, (string?)null)
            : await VersionAsync(daoris, read, ct).ConfigureAwait(false);

        return new SetupTools(node.File, nodeVersion, nodeProblem, daoris, daorisVersion, daorisProblem);
    }

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

    public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => service.EntryPathsAsync(repository, ct);

    public RepositoryDescription? Describe(string root) => SelfDescription.Read(root);

    public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct) =>
        service.AskAsync(workspace, sentence, [], [], to, ct);

    /// <summary>A program's first line to <c>--version</c>, or what it said in place of one.</summary>
    private async Task<(string? Version, string? Problem)> VersionAsync(string file, ToolsRead read, CancellationToken ct)
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
        info.ArgumentList.Add("--version");
        // Asked as a child is started (TOOLS5): the launcher runs on the node a child's PATH finds.
        Tools.Hand(info, read, home);

        try
        {
            using var process = Process.Start(info);
            if (process is null) return (null, $"`{Path.GetFileName(file)}` did not start");
            using var patience = CancellationTokenSource.CreateLinkedTokenSource(ct);
            patience.CancelAfter(Patience);
            var stdout = process.StandardOutput.ReadToEndAsync(patience.Token);
            var stderr = process.StandardError.ReadToEndAsync(patience.Token);
            try
            {
                await process.WaitForExitAsync(patience.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await Task.WhenAll(stdout, stderr).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                return (null, $"`{Path.GetFileName(file)}` did not answer within {Patience.TotalSeconds:0}s");
            }

            var said = (await stdout.ConfigureAwait(false)).Trim();
            var first = said.Split('\n')[0].Trim();
            return process.ExitCode == 0 && first.Length > 0
                ? (first, null)
                : (null, first.Length > 0 ? first : (await stderr.ConfigureAwait(false)).Trim().Split('\n')[0].Trim());
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (null, $"`{Path.GetFileName(file)}` would not start: {error.Message}");
        }
    }
}
