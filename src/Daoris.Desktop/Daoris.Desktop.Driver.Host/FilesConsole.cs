namespace Daoris.Driver.Host;

/// <summary>
/// <c>daoris files</c> from a terminal: the library's verb (<see cref="FilesCommand"/>) over the named repository's checkout, the
/// registry's root for it, else this terminal's own checkout where its folder bears the name.
/// </summary>
internal static class FilesConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine(FilesCommand.Usage);
            return 2;
        }

        if (await RootAsync(args[0]).ConfigureAwait(false) is not { } root)
        {
            Console.Error.WriteLine($"daoris: `{args[0]}` is no repository this machine has registered, and this terminal is not in its checkout.");
            return 1;
        }

        var session = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DriverChoicesCommand.SessionVariable));
        return await FilesCommand.RunAsync(args, DaorisHome.Resolve(), root, session, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<string?> RootAsync(string repository)
    {
        try
        {
            using var service = ServiceClient.FromEnvironment();
            var row = (await service.RegistryAsync().ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
            if (row?.Root is { Length: > 0 } root && Directory.Exists(root)) return root;
        }
        catch (Exception unreachable) when (unreachable is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // With no service to ask, the terminal's own checkout still answers for itself.
        }

        var here = Environment.CurrentDirectory;
        return string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(here)), repository, StringComparison.OrdinalIgnoreCase) ? here : null;
    }
}
