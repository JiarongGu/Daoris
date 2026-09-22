namespace Daoris.Driver;

/// <param name="Executable">The host binary to start.</param>
/// <param name="WorkingDirectory">
/// Where to start it — load-bearing, not a nicety: the host serves the platform bundle from beside
/// its working directory, so an installed host runs beside its own bundle while a development build
/// runs from the PROJECT directory, whose `wwwroot` is where the web build lands. Spawned from its
/// `bin`, a dev host answers every API call and serves no page, which reads as "the app is broken".
/// </param>
public sealed record HostLocation(string Executable, string WorkingDirectory);

/// <summary>
/// Where the service's HTTP host lives on this machine — the shell's question when it starts and
/// nothing is answering yet. Ordered: what the person said, then what the install carries (the host
/// `desktop-publish --service` puts beside the shell), then the installed home (`$DAORIS_HOME/bin`,
/// the service publish's landing place — D63), then the workspace build for development.
/// </summary>
/// <remarks>
/// Location only — spawning and probing stay with the supervisor. Kept beside the driver because it
/// is portable path logic the tests can hold still, and the shell should stay thin.
/// </remarks>
public static class ServiceHostLocator
{
    public const string PathVariable = "DAORIS_HTTP_HOST";

    public static string ExecutableName =>
        OperatingSystem.IsWindows() ? "daoris-knowledge-http.exe" : "daoris-knowledge-http";

    /// <summary>Every place worth looking, in the order they deserve trust.</summary>
    /// <param name="home">The Daoris home (D63), or null on a machine with none — then there is no installed home to look in.</param>
    public static IReadOnlyList<HostLocation> Candidates(string? explicitPath, string? home, string baseDirectory)
    {
        var candidates = new List<HostLocation>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(new(explicitPath, Path.GetDirectoryName(Path.GetFullPath(explicitPath)) ?? "."));
        }

        // A DEPLOYED shell carries its own host with it (`desktop-publish --service`), and an install
        // folder has no workspace below to fall through to.
        //
        // 🔴 Ranked ABOVE the machine's installed home, and it was the other way round once, with a
        // reason: the home's `bin/` is upgraded once for every shell on the machine, so a deployed copy
        // outranking it would make a service upgrade invisible. The second deployment showed the
        // inverse and it was worse: `--service` published a NEWER host beside the shell, the shell
        // spawned the OLDER machine-wide one, and the window served a bundle that existed nowhere on
        // disk but there. What the install carries is what it runs — the install's own upgrade path
        // lands here, in one command with the shell — and a shell published without `--service` still
        // falls through to the machine's home next.
        //
        // 🔴 Both shapes, because the INSTALL LAYOUT and this list are a counterpart set: an install
        // folder shows one launcher at its root and keeps supporting binaries under `app/`, so the
        // host is normally a level down — and a hand-assembled folder puts it beside the executable.
        foreach (var relative in new[] { "daoris-knowledge-http", Path.Combine("app", "daoris-knowledge-http") })
        {
            var beside = Path.Combine(baseDirectory, relative, ExecutableName);
            candidates.Add(new(beside, Path.GetDirectoryName(beside)!));
        }

        if (home is not null)
        {
            var bin = Path.Combine(home, "bin");

            // A binary someone placed here by hand — the most deliberate thing short of naming a path.
            var installed = Path.Combine(bin, ExecutableName);
            candidates.Add(new(installed, Path.GetDirectoryName(installed)!));

            // 🔴 Where the installer actually puts it. `service-publish --install` gives the HTTP host a
            // directory of its own because its `wwwroot` must travel BESIDE the executable, while the
            // MCP host installs flat. Looking only flat made a correctly installed host invisible —
            // masked on every developer machine by the workspace candidate below, and fatal on a
            // deployed one, which has no workspace to fall through to. Found by deploying.
            var packaged = Path.Combine(bin, "daoris-knowledge-http", ExecutableName);
            candidates.Add(new(packaged, Path.GetDirectoryName(packaged)!));
        }

        // Development: walk up from the running binary to the workspace manifest, then take the HTTP
        // host's own build output — run from the PROJECT directory, where the built bundle lives.
        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        if (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "src", "Daoris.Service", "Daoris.Service.Http");
            foreach (var flavour in new[] { "Debug", "Release" })
            {
                candidates.Add(new(
                    Path.Combine(project, "bin", flavour, "net10.0", ExecutableName),
                    project));
            }
        }

        return candidates;
    }

    /// <summary>The first candidate whose binary exists, or null — and null is a message, not a crash.</summary>
    public static HostLocation? Locate(string? explicitPath, string? home, string baseDirectory) =>
        Candidates(explicitPath, home, baseDirectory).FirstOrDefault(c => File.Exists(c.Executable));
}
