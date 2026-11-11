namespace Daoris.Driver;

/// <summary>
/// Where a server's start runs a package through <c>npx</c>: the start itself, or what follows the <c>--</c> of a start by node that
/// hands an <c>npx</c> command on (<c>node server.mjs -- npx …</c>), its own words before it kept. Either way the npx command is read as
/// <see cref="NpxCommand"/> reads one, and the package's own start stands where <c>npx</c> stood.
/// </summary>
/// <param name="Before">A node start's words up to and with its <c>--</c>; null where the start is <c>npx</c> itself.</param>
/// <param name="Npx">The npx command read.</param>
public sealed record ServerNpx(IReadOnlyList<string>? Before, NpxCommand Npx)
{
    /// <summary>The start read, or why its npx command cannot be: null with no reason where the start runs no <c>npx</c>.</summary>
    public static (ServerNpx? Start, string? Refusal) Read(string command, IReadOnlyList<string> arguments)
    {
        if (NpxCommand.Names(command)) return Of(null, command, arguments);
        if (!Node(command)) return (null, null);
        for (var at = 0; at + 1 < arguments.Count; at++)
        {
            if (arguments[at] == "--" && NpxCommand.Names(arguments[at + 1]))
            {
                return Of([.. arguments.Take(at + 1)], arguments[at + 1], [.. arguments.Skip(at + 2)]);
            }
        }

        return (null, null);
    }

    /// <summary><paramref name="server"/> with <paramref name="start"/>, node and what it runs, where <c>npx</c> stood.</summary>
    public ServerEntry Handed(ServerEntry server, string node, IReadOnlyList<string> start) => Before is null
        ? server with { Command = node, Arguments = [.. start] }
        : server with { Arguments = [.. Before, node, .. start] };

    private static (ServerNpx?, string?) Of(IReadOnlyList<string>? before, string npx, IReadOnlyList<string> arguments)
    {
        var (read, refusal) = NpxCommand.Read(npx, arguments);
        return read is null ? (null, refusal) : (new ServerNpx(before, read), null);
    }

    /// <summary>Whether <paramref name="command"/> is node: by its name, or a path to <c>node</c> or <c>node.exe</c>.</summary>
    private static bool Node(string command)
    {
        var name = command.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return string.Equals(name, "node", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "node.exe", StringComparison.OrdinalIgnoreCase);
    }
}
