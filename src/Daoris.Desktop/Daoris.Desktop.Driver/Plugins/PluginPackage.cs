namespace Daoris.Driver;

/// <summary>
/// A Daoris plugin package and its reader, offline (PLUGDIST1a, D120; the distribution design §5.1 and §5.7). A
/// package is a NuGet <c>.nupkg</c>, a zip, of the custom package type <see cref="Type"/> alone, whose type version's
/// major number is the plugin API it speaks, with no dependencies, and its plugin folder under <see cref="Root"/>.
/// </summary>
/// <remarks>
/// <para><b>Read before extract.</b> The nuspec's id, version, type and dependencies, and every entry under
/// <c>plugin/</c>, are judged before anything is written: a refusal leaves the home as it was. The file is opened
/// once, shared for reading only, so what is hashed is what is extracted.</para>
///
/// <para><b>The <c>plugin/</c> guard.</b> Only <c>plugin/**</c> is extracted, into a stage under the home that the
/// catalogue skips; NuGet's own parts at the package root are never written. An entry's name is read as NuGet reads
/// it (unescaped, either slash), and one that is rooted, holds <c>..</c> or a drive, or would land outside the stage
/// refuses the whole package. A name twice, as Windows compares names, refuses it too.</para>
///
/// <para><b>Then the catalogue's own reader</b> (<c>PluginCatalog.ReadAsWritten</c>,
/// <c>PluginCatalog.RefusedByThisBuild</c>) reads the stage, the plugin's version is held to the package's and
/// its API to the type's, and <see cref="PluginInstall"/> adds it, never replacing an installed plugin, with the
/// package recorded in <see cref="PluginSource.FileName"/>.</para>
///
/// <para>No NuGet client, and no network: a package source over HTTP is PLUGDIST1c's, and hands this the file it
/// fetched. 🔴 Nothing a plugin declares runs here; the loop starts it at its next look.</para>
///
/// <para>The use case (STRUCT3a): what the package's words are is the Domain's <see cref="PluginPackageRules"/>, the file
/// <see cref="PluginPackageFile"/>'s.</para>
/// </remarks>
public static class PluginPackage
{
    /// <summary>What a package file says it is, or why it is no plugin this build installs. Nothing is written.</summary>
    public static (PluginPackageRead? Package, string? Refusal) Read(string file)
    {
        try
        {
            using var opened = PluginPackageFile.Open(file);
            return (opened.Read, null);
        }
        catch (PluginPackageRefusal refused)
        {
            return (null, refused.Message);
        }
    }

    /// <summary>
    /// Install a package file's plugin: read, extract <c>plugin/**</c> into a stage under the home, read the stage
    /// with the catalogue's reader, and add it through <see cref="PluginInstall"/>, recording the package and the
    /// folder that held the file as its source. The stage is gone either way.
    /// </summary>
    /// <exception cref="DriverException">Refused, installed already, or the extraction failed; nothing is installed.</exception>
    public static PluginPackageInstalled Install(string home, string file, IEnumerable<string> reservedHarnesses)
    {
        var reserved = reservedHarnesses.ToList();
        var root = Path.Combine(home, PluginCatalog.Folder);
        string? stage = null;
        try
        {
            using var opened = PluginPackageFile.Open(file);
            var read = opened.Read;
            PluginFolders.Make(root);
            stage = Path.Combine(root, $".unpacking-{Guid.NewGuid():N}");
            opened.Extract(stage);

            var manifest = ReadStage(stage, read, reserved);
            var target = Path.Combine(root, manifest.Id);
            if (PluginFolders.There(target)) throw new PluginPackageRefusal(Installed(manifest.Id, target));

            var origin = new PluginPackageOrigin(read.Package, read.Version, read.Sha512, Path.GetDirectoryName(PluginFolders.WholePath(file))!);
            return new(PluginInstall.Add(home, stage, origin, reserved), origin);
        }
        catch (PluginPackageRefusal refused)
        {
            throw new DriverException($"{refused.Message} Nothing was installed.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            // A name the file system will not take (ArgumentException) is the package's, said as a refusal, never a crash.
            throw new DriverException($"{Path.GetFileName(file)} was not installed: {error.Message} Nothing was installed.");
        }
        finally
        {
            if (stage is not null) PluginFolders.Forget(stage);
        }
    }

    /// <summary>§5.7 step 3: the stage read as the catalogue reads a plugin, then held to the package that carried it.</summary>
    private static PluginManifest ReadStage(string stage, PluginPackageRead read, IReadOnlyCollection<string> reserved)
    {
        var said = PluginPackageRules.Said(read.Package, read.Version);
        var path = Path.Combine(stage, PluginCatalog.ManifestName);
        var (manifest, problem) = PluginCatalog.ReadAsWritten(PluginInstall.IdOf(path), path);
        if (problem is not null) throw new PluginPackageRefusal($"{said}'s `{PluginCatalog.ManifestName}`: {problem}");
        if (PluginCatalog.RefusedByThisBuild(manifest, reserved) is { } refused) throw new PluginPackageRefusal($"{said}'s plugin `{manifest.Id}` {refused}");
        if (!string.Equals(manifest.Version, read.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginPackageRefusal($"{said} holds plugin `{manifest.Id}` "
                + (manifest.Version.Length > 0 ? $"at version `{manifest.Version}`" : "with no version")
                + " — a package's version is its plugin's, so the two must be one.");
        }

        if (manifest.ApiVersion != read.ApiVersion)
        {
            throw new PluginPackageRefusal($"{said}: its type says plugin API {read.ApiVersion}, and its `{PluginCatalog.ManifestName}` says "
                + $"{manifest.ApiVersion} — the type's version is the API its plugin speaks.");
        }

        return manifest;
    }

    /// <summary>§5.7 step 4: an add never replaces, and the refusal names where the installed one came from.</summary>
    private static string Installed(string id, string folder)
    {
        var (source, problem) = PluginSource.Read(folder);
        var from = source is not null
            ? $", from {source.Said}"
            : problem is not null ? ", and its record of where it came from does not read" : ", with no record of where it came from";
        return $"plugin `{id}` is already installed on this machine{from} — a package installs a plugin not yet here. "
            + $"`daoris plugin remove {id}` takes it out first, and what it kept stays where it is.";
    }
}

/// <summary>
/// `daoris-driver plugins install &lt;file.nupkg&gt;` (PLUGDIST1a, the distribution design §6.4): a package file's plugin
/// installed from a terminal, through <see cref="PluginPackage.Install"/>. A package source's <c>install &lt;Id&gt;</c>
/// is PLUGDIST1c's.
/// </summary>
/// <remarks>
/// <para><b>Why the driver's.</b> The package reader is the driver's alone (D120 §4): the CLI reads and lists the record
/// a package leaves, and its <c>daoris plugin install</c> says this door is where a package is installed.</para>
///
/// <para>The printing lives in the library so a test runs the whole door in-process. Exit codes keep the family
/// contract: 0 installed · 2 it could not do what was asked, a refused package among them.</para>
/// </remarks>
public static class PluginPackageCommand
{
    public const string Usage = "usage: daoris-driver plugins install <file.nupkg>";

    /// <param name="reservedHarnesses">The harnesses this build carries; null is <see cref="AdapterSet.Built"/>'s.</param>
    public static int Install(string[] args, TextWriter output, string? home, IEnumerable<string>? reservedHarnesses = null)
    {
        try
        {
            string? file = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith("--", StringComparison.Ordinal) || file is not null)
                {
                    throw new DriverException($"`{arg}` is not something `plugins install` takes.\n{Usage}");
                }

                file = arg;
            }

            if (file is null)
            {
                throw new DriverException($"`plugins install` needs a package file — e.g. `daoris-driver plugins install ./Acme.Gate.1.0.0{PluginPackageRules.Extension}`.\n{Usage}");
            }

            if (!file.EndsWith(PluginPackageRules.Extension, StringComparison.OrdinalIgnoreCase) && !PluginFolders.HasFile(file))
            {
                throw new DriverException($"`{file}` is not a package file — `plugins install` takes a `{PluginPackageRules.Extension}` file, "
                    + "and `daoris plugin add <folder>` a plugin's folder.");
            }

            if (home is null) throw new DriverException($"no Daoris home: a plugin is installed under it. Set {DaorisHome.Variable}.");

            var installed = PluginPackage.Install(home, PluginFolders.WholePath(file), reservedHarnesses ?? AdapterSet.Built().Names);
            var manifest = installed.Manifest;
            var folder = Path.Combine(home, PluginCatalog.Folder, manifest.Id);
            output.WriteLine($"plugins: installed `{manifest.Id}`{(manifest.Version.Length > 0 ? " " + manifest.Version : "")} at {folder}");
            output.WriteLine($"  {Describe(manifest)}.");
            output.WriteLine($"  From {PluginSource.FromPackage(installed.Origin).Said}, sha512 {installed.Origin.Sha512}.");
            var data = Path.Combine(home, PluginCatalog.Folder, PluginCatalog.DataFolder, manifest.Id);
            if (PluginFolders.There(data)) output.WriteLine($"  {data} kept, untouched.");
            output.WriteLine("  It takes effect at the driver's next look — a harness on the roster, a hook at the next tick.");
            return 0;
        }
        catch (DriverException error)
        {
            output.WriteLine($"plugins: {error.Message}");
            return 2;
        }
    }

    /// <summary>What a plugin declares and speaks, as the CLI's <c>describe</c> says it.</summary>
    private static string Describe(PluginManifest manifest)
    {
        var parts = new List<string>();
        if (manifest.Harnesses.Count > 0) parts.Add($"declares {string.Join(", ", manifest.Harnesses.Select(harness => harness.Name))}");
        if (manifest.Hooks is { } hooks) parts.Add($"speaks on {string.Join(", ", hooks.Points)}");
        var sessions = manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Sessions)).ToList();
        if (sessions.Count > 0) parts.Add($"hands sessions {string.Join(", ", sessions.Select(server => server.Name))}");
        // HELPPLUG1a (D170 point 3): what it offers Ask Daoris, its tools reaching it only once the person approves them.
        var help = manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)).ToList();
        if (help.Count > 0)
        {
            static List<string> Tools(PluginServer server) => [.. server.Allow, .. server.Writes.Select(tool => $"{tool}, writes")];
            parts.Add($"offers Ask Daoris {string.Join(", ", help.Select(server =>
                Tools(server).Count > 0 ? $"{server.Name} ({string.Join("; ", Tools(server))})" : server.Name))}");
        }

        // HELPPLUG1b (D170 point 4): the skills it hands each, as the CLI's `describe` says them.
        foreach (var (to, who) in new[] { (PluginServerTo.Sessions, "sessions"), (PluginServerTo.Help, "Ask Daoris") })
        {
            var skills = manifest.Skills.Where(skill => skill.HandedTo(to)).Select(skill => skill.Name).ToList();
            if (skills.Count > 0) parts.Add($"hands {who} skill{(skills.Count == 1 ? "" : "s")} {string.Join(", ", skills)}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "declares nothing and speaks nothing";
    }
}
