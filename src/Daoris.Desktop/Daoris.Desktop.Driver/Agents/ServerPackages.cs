using System.Collections.Concurrent;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The packages a session's servers run from, each installed once into the toolchain beside the agents' pins:
/// <c>cache/toolchain/&lt;package&gt;/&lt;version&gt;</c>, by npm's own <c>--prefix</c>, as an agent's pin is, with npm's cache
/// taken before the network. A server then starts as node and the package's script, where <c>npx</c> would have resolved the same
/// package on every start behind two <c>cmd</c>s and a node of npm's own.
/// </summary>
/// <remarks>
/// <para><b>An exact version is its folder.</b> A tag or no version is answered by the newest version installed, a range by the newest
/// inside it (one <see cref="NpmRanges"/> cannot read is answered as a tag is); only a package with none asks npm, once.</para>
///
/// <para><b>Staged, then published.</b> npm installs beside the folder it becomes (<see cref="ReleaseChannels.StagingOf"/>), and the
/// folder is moved into place only once its script is there, so a reader never takes a half-install; one start of this process
/// installs a package while another waits for it, and another process's install met at the move is kept.</para>
/// </remarks>
public static class ServerPackages
{
    /// <summary>A package installed for a session's server: <c>package</c>, <c>version</c>, <c>ms</c>.</summary>
    public const string Installed = "server.installed";

    /// <summary>A server not handed because its package could not be started: <c>server</c>, <c>why</c>.</summary>
    public const string Refused = "server.refused";

    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The package's folder name: its scope's slash made a <c>+</c>, which no agent's name holds.</summary>
    public static string Key(string package) => package.Replace('/', '+');

    /// <summary>Where <paramref name="version"/> of <paramref name="package"/> is installed.</summary>
    public static string Folder(string home, string package, string version) =>
        HarnessSettings.ManagedHome(home, Key(package), version);

    /// <summary>The script <paramref name="npx"/> runs from <paramref name="version"/>, where that version is installed.</summary>
    public static string? Script(string home, NpxCommand npx, string version) =>
        PackageStarts.Script(Path.Combine(Folder(home, npx.Package, version), "node_modules"), npx.Package, npx.Bin);

    /// <summary>
    /// The script <paramref name="npx"/> runs, installed first where no version that answers it is: the start waits for that install,
    /// once, and the log says it.
    /// </summary>
    /// <param name="npm">The package manager an install runs; Tools' npm where null.</param>
    /// <param name="publish">Moves the staged folder into its place; a test hands in one that loses the race.</param>
    /// <exception cref="DriverException">The package could not be installed, or names no program that runs.</exception>
    public static async Task<string> ScriptAsync(
        string home, NpxCommand npx, MachineLog? log, CancellationToken ct, IReadOnlyList<string>? npm = null,
        Action<string, string>? publish = null)
    {
        if (Kept(home, npx) is { } kept) return kept;

        var gate = Gates.GetOrAdd(Path.GetDirectoryName(Folder(home, npx.Package, "0"))!, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return Kept(home, npx) ?? await InstallAsync(home, npx, log, ct, npm, publish).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The script of the version installed that answers <paramref name="npx"/>: its exact version's, the newest one in its range, or the newest.</summary>
    private static string? Kept(string home, NpxCommand npx)
    {
        if (Exact(npx) is { } exact) return Script(home, npx, exact);
        var range = npx.Version is { } written && NpmRanges.Reads(written) ? written : null;
        var versions = Path.GetDirectoryName(Folder(home, npx.Package, "0"))!;
        if (!Directory.Exists(versions)) return null;
        return Directory.EnumerateDirectories(versions)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(version => Tools.IsExactVersion(version) && (range is null || NpmRanges.Satisfies(range, version)))
            .OrderDescending(Comparer<string>.Create(ToolResources.CompareVersions))
            .Select(version => Script(home, npx, version))
            .FirstOrDefault(script => script is not null);
    }

    private static string? Exact(NpxCommand npx) => npx.Version is { } version && Tools.IsExactVersion(version) ? version : null;

    private static async Task<string> InstallAsync(
        string home, NpxCommand npx, MachineLog? log, CancellationToken ct, IReadOnlyList<string>? npm, Action<string, string>? publish)
    {
        var spec = npx.Version is null ? npx.Package : $"{npx.Package}@{npx.Version}";
        var exact = Exact(npx);
        var staging = ReleaseChannels.StagingOf(Folder(home, npx.Package, exact ?? "resolving"), Environment.ProcessId);
        Discard(staging);
        var clock = Stopwatch.StartNew();
        try
        {
            var code = await RunAsync(
                home, [.. npm ?? ["npm"], "install", "--prefix", staging, "--prefer-offline", "--no-audit", "--no-fund", spec], ct)
                .ConfigureAwait(false);
            if (code != 0) throw new DriverException($"npm could not install `{spec}` (it exited {code})");

            // What a tag or a range came to is the version npm installed, and the folder's name.
            var version = exact ?? Version(Path.Combine([staging, "node_modules", .. npx.Package.Split('/'), "package.json"]))
                ?? throw new DriverException($"npm installed `{spec}` and wrote no version for it");
            if (Script(home, npx, version) is { } meanwhile) return meanwhile;

            var target = Folder(home, npx.Package, version);
            try
            {
                (publish ?? Publish)(staging, target);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Another process's install landed at the move, or a leftover there is held: theirs is kept where it runs.
                return Script(home, npx, version)
                    ?? throw new DriverException($"`{spec}` could not be moved into place: {error.Message}");
            }

            var script = Script(home, npx, version)
                ?? throw new DriverException($"`{spec}` names no program `npx` would run");
            log?.Info(Installed, ("package", npx.Package), ("version", version), ("ms", clock.ElapsedMilliseconds));
            return script;
        }
        finally
        {
            Discard(staging);
        }
    }

    private static void Publish(string staging, string target)
    {
        // A folder there holding nothing that runs is what an interrupted move left.
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        AtomicFile.MoveFolder(staging, target);
    }

    /// <summary>The <c>version</c> a package's manifest names, where it names an exact one.</summary>
    private static string? Version(string manifest)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.TryGetProperty("version", out var version)
                && version.GetString() is { } named && Tools.IsExactVersion(named)
                ? named
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Run npm, its tree held in a job of its own and drained, to its exit or <see cref="Patience"/>.</summary>
    private static async Task<int> RunAsync(string home, IReadOnlyList<string> command, CancellationToken ct)
    {
        var resolved = HarnessActions.ThroughTools(home, command);
        var info = new ProcessStartInfo(resolved[0])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var argument in resolved.Skip(1)) info.ArgumentList.Add(argument);
        Tools.Hand(info, home);
        // npm itself starts as its node and script, so a range's `^` reaches it as written, never through `cmd`.
        PackageStarts.Direct(info, "npm");
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue(Tools.PathVariable, out var path) ? path : null);

        Process process;
        try
        {
            process = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Tool, StartClass.Background, ct).ConfigureAwait(false)
                ?? throw new DriverException("npm did not start");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new DriverException("npm could not be started: it is not on this machine's PATH");
        }

        using var _ = process;
        using var job = ProcessJob.Hold(process);
        process.StandardInput.Close();
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(Patience);
        try
        {
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(bound.Token),
                process.StandardError.ReadToEndAsync(bound.Token),
                process.WaitForExitAsync(bound.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DriverException($"npm did not finish installing within {Patience.TotalMinutes:0} minutes");
        }

        return process.ExitCode;
    }

    private static void Discard(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A staging something still holds is this process's alone; the next install starts its own afresh.
        }
    }
}
