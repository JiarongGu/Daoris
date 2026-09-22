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
/// nothing is answering yet. Ordered: what the person said, then the installed home (`~/.daoris/bin`,
/// the publish script's landing place), then the workspace build for development.
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
    public static IReadOnlyList<HostLocation> Candidates(string? explicitPath, string userProfile, string baseDirectory)
    {
        var candidates = new List<HostLocation>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(new(explicitPath, Path.GetDirectoryName(Path.GetFullPath(explicitPath)) ?? "."));
        }

        var bin = Path.Combine(userProfile, ".daoris", "bin");

        // A binary someone placed here by hand — the most deliberate thing short of naming a path.
        var installed = Path.Combine(bin, ExecutableName);
        candidates.Add(new(installed, Path.GetDirectoryName(installed)!));

        // 🔴 Where the installer actually puts it. `service-publish --install` gives the HTTP host a
        // directory of its own because its `wwwroot` must travel BESIDE the executable, while the MCP
        // host installs flat. Looking only flat made a correctly installed host invisible — masked on
        // every developer machine by the workspace candidate below, and fatal on a deployed one,
        // which has no workspace to fall through to. Found by deploying.
        var packaged = Path.Combine(bin, "daoris-knowledge-http", ExecutableName);
        candidates.Add(new(packaged, Path.GetDirectoryName(packaged)!));

        // A DEPLOYED shell carries its own host beside it (`desktop-publish --service`), and an
        // install folder has no workspace below to fall through to. Ranked under the installed home
        // on purpose: that one is the machine's and is upgraded once for every shell on it, so a
        // deployed copy quietly outranking it would make a service upgrade invisible.
        var beside = Path.Combine(baseDirectory, "daoris-knowledge-http", ExecutableName);
        candidates.Add(new(beside, Path.GetDirectoryName(beside)!));

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
    public static HostLocation? Locate(string? explicitPath, string userProfile, string baseDirectory) =>
        Candidates(explicitPath, userProfile, baseDirectory).FirstOrDefault(c => File.Exists(c.Executable));
}
