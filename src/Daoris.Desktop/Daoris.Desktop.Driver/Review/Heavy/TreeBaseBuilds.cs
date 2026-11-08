namespace Daoris.Driver;

/// <summary>
/// The one release of a kept base, <c>daoris tree build &lt;path&gt;</c> (the tree-pool design §36.3, 2, §37.3, 2, §38.2): for each
/// base the record names at or under the path, or holding it, its producer is built by the door's one command in the project's folder,
/// under the machine's admission as a heavy step; on exit 0 every entry under the base its build's account does not own is set aside,
/// said, the record lets the base go and then its marker goes. Any other exit releases nothing. A marker the record no longer names is
/// removed. A build typed into a shell, a gate's step or a <c>--no-build</c> run never comes here, so none of them releases.
/// </summary>
public static class TreeBaseBuilds
{
    /// <summary>0 where every base asked for is released or none stands; 1 where a build failed; 2 where the path is in no checkout.</summary>
    public static async Task<int> BuildAsync(string home, string path, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var folder = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        if (folder is null || !Directory.Exists(folder))
        {
            error.WriteLine($"trees: {path} is no folder here.");
            return 2;
        }

        var (code, top, _) = await WorkingTree.GitAsync(folder, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (code != 0)
        {
            error.WriteLine($"trees: {path} is in no checkout git reads.");
            return 2;
        }

        var tree = Path.GetFullPath(top.Trim());
        var relative = Path.GetRelativePath(tree, full).Replace('\\', '/');
        relative = relative == "." ? "" : Directory.Exists(full) ? relative.TrimEnd('/') + "/" : relative;
        var bases = (TreeFilesRecord.Read(home, tree)?.Bases ?? [])
            .Where(each => relative.Length == 0 || TreeBases.Under(each.Path, relative) || relative.StartsWith(each.Path, StringComparison.Ordinal)).ToList();
        if (bases.Count == 0)
        {
            if (BaseMarker.Above(full) is { } stray && stray.Folder.StartsWith(tree, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(Path.Combine(stray.Folder, BaseMarker.FileName));
                output.WriteLine($"trees: the marker at {Path.GetRelativePath(tree, stray.Folder).Replace('\\', '/')} named a base the record no longer holds, and was removed.");
            }

            output.WriteLine($"trees: no base of the previous line's stands at {Shown(relative)}; nothing to release.");
            return 0;
        }

        var head = (await WorkingTree.HeadAsync(tree, ct).ConfigureAwait(false)) ?? "";
        var exit = 0;
        foreach (var producer in bases.GroupBy(each => (each.Project, each.Configuration, each.Builds)))
        {
            var (project, configuration, builds) = producer.Key;
            if (builds.Length == 0)
            {
                foreach (var kept in producer)
                {
                    var (count, _) = BaseTrim.Trim(tree, kept, new HashSet<string>());
                    TreeBases.Release(home, tree, kept, new TreeBaseRelease(kept.Path, project, configuration, [], [], head, 0, DateTimeOffset.UtcNow, count));
                    output.WriteLine($"trees: {kept.Path}'s record names no build that releases it, so it was set aside whole with {Line(kept)}'s and is no base.");
                }

                continue;
            }

            BuildAccount account;
            using (var hold = await MachineAdmission.TakeAsync(home, new AdmissionRequest($"the build of {project} -c {configuration}") { Tree = tree }, output, ct).ConfigureAwait(false))
            {
                var inside = hold.IsInside ? null : new Dictionary<string, string> { [MachineAdmission.InsideVariable] = hold.Id };
                var projectFolder = Path.GetDirectoryName(Path.Combine(tree, project.Replace('/', Path.DirectorySeparatorChar)))!;
                account = await BuildAccounts.RunAsync(projectFolder, Path.GetFileName(project), configuration, builds, inside, ct).ConfigureAwait(false);
            }

            var command = account.Commands.Count > 0 ? string.Join("`, `", account.Commands) : $"{builds} (the SDK's evaluation)";
            if (account.Exit != 0)
            {
                error.WriteLine($"trees: `{command}` exited {account.Exit} in {Shown(FolderOf(project))}, so nothing is released: {Tail(account.Said)}");
                exit = 1;
                continue;
            }

            foreach (var kept in producer)
            {
                var (count, whole) = BaseTrim.Trim(tree, kept, account.Files);
                TreeBases.Release(home, tree, kept, new TreeBaseRelease(kept.Path, project, configuration, account.Frameworks, account.Commands, head, 0, DateTimeOffset.UtcNow, count));
                output.WriteLine(whole
                    ? $"trees: {kept.Path} holds nothing {project} -c {configuration} builds, so it was set aside whole with {Line(kept)}'s and is no base."
                    : $"trees: {kept.Path} released, built by {project} -c {configuration} at {Short(head)} (`{command}`, exit 0); "
                        + $"{count} {(count == 1 ? "entry" : "entries")} its build does not own set aside with {Line(kept)}'s.");
            }
        }

        return exit;
    }

    private static string Shown(string relative) => relative.Length == 0 ? "the checkout's top" : relative;

    private static string FolderOf(string project) => project.Contains('/') ? project[..(project.LastIndexOf('/') + 1)] : "";

    private static string Line(TreeBase kept) => kept.From.Length > 0 ? kept.From : "the previous line";

    private static string Short(string commit) => commit.Length > 12 ? commit[..12] : commit;

    private static string Tail(string said) =>
        said.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Contains("error", StringComparison.OrdinalIgnoreCase))
        ?? said.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.Length > 0) ?? "it said nothing";
}
