namespace Daoris.Driver.Host;

/// <summary>
/// The registry from a terminal (ONEWAY1ai-i; the oneway design §6 X3, §12): <c>daoris repository connect|retire|wire|import</c>, on the
/// typed operations the window's registry calls (<see cref="RegistryConnect"/>, <see cref="ServiceClient"/>), said by
/// <see cref="RegistryWords"/>. <c>connect</c> registers this checkout's repository from its working tree with any service the
/// terminal names (<c>DAORIS_SERVICE_URL</c>, else the running Daoris's); <c>register</c> is the host's own re-following, beside it.
/// </summary>
internal static class RepositoryConsole
{
    private static readonly string Usage =
        "usage: daoris repository connect [--workspace <name>] [--dry-run]  ·  daoris repository retire [<name>] [--dry-run]\n"
        + "       daoris repository add <folder> [--workspace <name>] [--exclude <glob>]... [--dry-run]\n"
        + "       daoris repository add --system <plugin>/<system> [<name>] [--workspace <name>] [--dry-run]\n"
        + "       daoris repository excludes <folder> [--add <glob>]... [--remove <glob>]...   (none: says them)\n"
        + "       daoris repository wire <repository> --workspace <name> [--dry-run]  ·  daoris repository import [<folder>] [--workspace <name>] [--dry-run]\n"
        + "       " + DeployablesCommand.Usage["usage: ".Length..] + "   (a read: its line's deployables and their release nodes)\n"
        + "       " + RepositoryCreateCommand.Usage["usage: ".Length..];

    public static async Task<int> RunAsync(string[] args)
    {
        var verb = args.Length > 0 ? args[0] : null;
        var words = args.Skip(1).ToArray();
        var dry = words.Contains("--dry-run");
        var workspace = Flag(words, "--workspace");
        var operands = Operands(words);
        var root = Environment.CurrentDirectory;
        try
        {
            using var service = ServiceClient.FromEnvironment();
            switch (verb)
            {
                case "connect" when operands.Count == 0:
                    if (dry)
                    {
                        foreach (var line in RegistryWords.ConnectDry(RegistryConnect.Plan(root, workspace, service.BaseUrl))) Console.WriteLine(line);
                        return 0;
                    }

                    foreach (var line in RegistryWords.Connected(await service.ConnectAsync(root, workspace).ConfigureAwait(false))) Console.WriteLine(line);
                    return 0;
                case "add" when Flag(words, "--system") is { } system && operands.Count <= 1:
                {
                    var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
                    var name = operands.Count == 1 ? operands[0] : null;
                    var plan = SystemAdd.Plan(home, system, name, workspace, service.BaseUrl);
                    if (dry)
                    {
                        Console.WriteLine(RegistryWords.SystemAddDry(plan, workspace));
                        return 0;
                    }

                    foreach (var line in RegistryWords.SystemAdded(await service.AddSystemAsync(plan).ConfigureAwait(false))) Console.WriteLine(line);
                    return 0;
                }

                case "add" when operands.Count == 1:
                {
                    var folder = Path.GetFullPath(operands[0], root);
                    var excludes = Flags(words, "--exclude");
                    if (dry)
                    {
                        Console.WriteLine(RegistryWords.AddDry(folder, workspace));
                        return 0;
                    }

                    var added = await service.AddFolderAsync(folder, workspace, excludes).ConfigureAwait(false);
                    var followed = RegistryConnect.IsLocal(service.BaseUrl);
                    if (followed)
                    {
                        FolderFirstManifest.Keep(
                            DriverConfig.HomeOf(DriverConfig.ResolvePath()), RemoteTarget.Workspace(added.Workspace ?? workspace), added.Plan.Repository, folder, excludes);
                    }

                    foreach (var line in RegistryWords.FolderAdded(added, followed)) Console.WriteLine(line);
                    return 0;
                }

                case "retire" when operands.Count <= 1:
                {
                    var repository = operands.Count == 1 ? operands[0] : Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
                    if (dry)
                    {
                        Console.WriteLine(RegistryWords.RetireDry(repository));
                        return 0;
                    }

                    return await RepositoryPresses.PressRetireAsync(repository, service, Console.Out, InstanceHost.World("repository")).ConfigureAwait(false);
                }

                case "wire" when operands.Count == 1 && workspace is not null:
                {
                    if (dry)
                    {
                        Console.WriteLine(RegistryWords.WireDry(operands[0], workspace));
                        return 0;
                    }

                    return await RepositoryPresses.PressWireAsync(operands[0], workspace, service, Console.Out, InstanceHost.World("repository")).ConfigureAwait(false);
                }

                case "excludes" when operands.Count == 1:
                {
                    var (add, remove) = (Flags(words, "--add"), Flags(words, "--remove"));
                    if (add.Count == 0 && remove.Count == 0) return await RepositoryPresses.ExcludesListAsync(operands[0], service, Console.Out, Console.Error).ConfigureAwait(false);
                    return await RepositoryPresses.PressExcludesAsync(
                        operands[0], add, remove, DriverConfig.HomeOf(DriverConfig.ResolvePath()), service, Console.Out, InstanceHost.World("repository")).ConfigureAwait(false);
                }

                case "import" when operands.Count <= 1:
                {
                    // Absolute, always: the service is another process, which may sit in another folder.
                    var folder = operands.Count == 1 ? Path.GetFullPath(operands[0], root) : null;
                    if (dry)
                    {
                        Console.WriteLine(RegistryWords.ImportDry(folder, workspace));
                        return 0;
                    }

                    return await RepositoryPresses.PressImportAsync(folder, workspace, service, Console.Out, InstanceHost.World("repository")).ConfigureAwait(false);
                }

                case "create":
                    return await RepositoryCreateConsole.RunAsync(words, root, service).ConfigureAwait(false);

                case "deployables" when operands.Count == 1:
                {
                    var configPath = DriverConfig.ResolvePath();
                    var config = DriverConfig.Load(configPath);
                    var world = new RegistrationWorld(service, DriverConfig.HomeOf(configPath), config);
                    return await DeployablesCommand.RunAsync(operands[0], Console.Out, world, config).ConfigureAwait(false);
                }

                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (RegistryRefusal refused)
        {
            Console.Error.WriteLine($"daoris: {refused.Message}");
            return refused.Exit;
        }
        catch (Exception failed) when (failed is DriverException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Console.Error.WriteLine($"daoris: {failed.Message}");
            return 2;
        }
    }

    private static string? Flag(string[] words, string name)
    {
        var at = Array.IndexOf(words, name);
        return at >= 0 && at + 1 < words.Length ? words[at + 1] : null;
    }

    /// <summary>Every value given to a repeated flag.</summary>
    private static List<string> Flags(string[] words, string name) =>
        [.. words.Select((word, at) => (word, at)).Where(each => each.word == name && each.at + 1 < words.Length).Select(each => words[each.at + 1])];

    /// <summary>The words that are neither a flag nor a value of <c>--workspace</c>, <c>--exclude</c>, <c>--system</c>, <c>--add</c> or <c>--remove</c>.</summary>
    private static List<string> Operands(string[] words)
    {
        List<string> found = [];
        for (var at = 0; at < words.Length; at++)
        {
            if (words[at] is "--workspace" or "--exclude" or "--system" or "--add" or "--remove") at++;
            else if (!words[at].StartsWith('-')) found.Add(words[at]);
        }

        return found;
    }
}
