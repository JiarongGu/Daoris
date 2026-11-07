using System.Text.RegularExpressions;

namespace Daoris.Driver.Host;

/// <summary>
/// This machine's remotes from a terminal (ONEWAY1ai-i; the oneway design §6 X3): <c>daoris remote list|add|remove</c>, on the typed
/// operations the window's Remotes screen calls (<see cref="RemoteOperations"/>), said by <see cref="RemoteWords"/>. The map is the home's
/// <c>remotes.json</c>, or the file <c>DAORIS_REMOTE_CONFIG</c> names; the key is never printed, only its audit prefix.
/// </summary>
internal static partial class RemoteConsole
{
    private const string Usage =
        "usage: daoris remote list  ·  daoris remote add <workspace> --url <url> [--key <key>]  ·  daoris remote remove <workspace>";

    [GeneratedRegex("^https?://[^/]", RegexOptions.IgnoreCase)]
    private static partial Regex Scheme();

    public static int Run(string[] args)
    {
        var path = RemoteTarget.ResolvePath();
        try
        {
            switch (args)
            {
                case ["list"]:
                    return Say(RemoteWords.List(RemoteWiring.Read(Environment.GetEnvironmentVariable, path)));
                case ["add", var named, .. var flags] when !named.StartsWith('-'):
                    return Add(RemoteTarget.Workspace(named), flags, path);
                case ["remove", var named] when !named.StartsWith('-'):
                {
                    var workspace = RemoteTarget.Workspace(named);
                    return Say(FileSaid.Said(RemoteWords.Removed(workspace, RemoteOperations.Remove(Required(path), workspace))));
                }
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (HomeFileRefusal refused)
        {
            return Say(FileSaid.Refused(refused.Message));
        }
        catch (DriverException failed)
        {
            return Say(FileSaid.Refused(failed.Message));
        }
    }

    private static int Add(string workspace, string[] flags, string? path)
    {
        var url = Flag(flags, "--url");
        if (url is null)
        {
            return Say(FileSaid.Refused($"`remote add {workspace}` needs the deployment's address — `--url https://…`. "
                + "The workspace name keys the map; the url and the key are what speak to its server."));
        }

        // An address with no scheme parses as a scheme of its own, and every sync with it fails at the service's door.
        if (!Scheme().IsMatch(url))
        {
            return Say(FileSaid.Refused($"`{url}` is not an address a deployment answers at — one starts http:// or https://, "
                + "e.g. `--url https://team.example.com`."));
        }

        // An edit needs a place to land: the override, or the home's file, never a default under the user profile.
        var target = Required(path);
        Console.WriteLine(RemoteWords.Wiring(workspace));
        var key = Flag(flags, "--key");
        if (key is null && Environment.GetEnvironmentVariable(RemoteTarget.KeyVariable) is { } fromEnvironment && fromEnvironment.Trim().Length > 0)
        {
            Console.WriteLine(RemoteWords.KeyFromEnvironment());
            key = fromEnvironment.Trim();
        }

        if (key is null)
        {
            Console.Write($"  key for `{workspace}` (stored in the map, never tracked): ");
            key = Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrEmpty(key))
        {
            return Say(FileSaid.Refused($"no key for `{workspace}` — pass `--key dk_…`, set {RemoteTarget.KeyVariable}, or pipe it in. "
                + "A deployment mints one per person per machine on its own console (`keys mint`)."));
        }

        return Say(FileSaid.Said(RemoteWords.Added(RemoteOperations.Set(target, workspace, url, key), target)));
    }

    private static string Required(string? path) => path ?? throw new DriverException(DaorisHome.Sentence);

    private static string? Flag(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>What the verb says, on its streams, and its exit.</summary>
    internal static int Say(FileSaid said)
    {
        foreach (var line in said.Out) Console.WriteLine(line);
        foreach (var line in said.Err) Console.Error.WriteLine(line);
        return said.Exit;
    }
}
