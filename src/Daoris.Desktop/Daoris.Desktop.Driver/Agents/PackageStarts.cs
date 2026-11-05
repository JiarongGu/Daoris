using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// How a program npm installed starts (LAUNCHCHAIN1): as node, with the package's own script first and the program's arguments
/// after, for a session's server and for an agent alike. Never through npm's <c>.cmd</c> shim, which Windows runs in a <c>cmd</c>
/// with a console of its own before it reaches the same node, and whose <c>cmd</c> reads every argument again.
/// </summary>
/// <remarks>
/// Elsewhere npm's shim is a link to the script, whose <c>#!</c> line has the system start node in its place: already one
/// process, so an agent's start is left as it is there.
/// </remarks>
public static class PackageStarts
{
    /// <summary>
    /// The node a script starts with, by npm's shim's own rule: a <c>node</c> beside the shim where there is one, then the one
    /// <paramref name="path"/> finds; null where neither is there.
    /// </summary>
    public static string? Node(string? beside, string? path)
    {
        if (beside is not null)
        {
            var here = Path.Combine(beside, OperatingSystem.IsWindows() ? "node.exe" : "node");
            if (File.Exists(here)) return here;
        }

        return CommandPresence.Resolve("node", path, startable: true) is { } found ? Path.GetFullPath(found) : null;
    }

    /// <summary>The whole path of <paramref name="package"/>'s script under <paramref name="nodeModules"/>, where its manifest names one that is there.</summary>
    /// <param name="bin">The program asked for; null lets npm's rule pick (<see cref="PackageBin"/>).</param>
    public static string? Script(string nodeModules, string package, string? bin)
    {
        var folder = Path.Combine([nodeModules, .. package.Split('/')]);
        var manifest = Path.Combine(folder, "package.json");
        string text;
        try
        {
            text = File.ReadAllText(manifest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (PackageBin.Of(text, package, bin) is not { } inside) return null;
        var script = Path.Combine([folder, .. inside.Split('/')]);
        return File.Exists(script) ? script : null;
    }

    /// <summary>
    /// An agent's start whose file is npm's <c>.cmd</c> shim for <paramref name="package"/>, made the node and script that shim runs,
    /// the arguments after the script as they were. True where it was; a start that is no such shim, or whose package is not beside it,
    /// is left as it is.
    /// </summary>
    /// <remarks>
    /// A pin's shims sit in its <c>node_modules/.bin</c>, the package in the <c>node_modules</c> above; a global install's sit beside
    /// its <c>node_modules</c>.
    /// </remarks>
    internal static bool Direct(ProcessStartInfo info, string? package)
    {
        if (!OperatingSystem.IsWindows() || package is not { Length: > 0 } || NpxCommand.Spelled(package) is null) return false;
        var path = info.Environment.TryGetValue(Tools.PathVariable, out var inherited) ? inherited : null;
        if (CommandPresence.Resolve(info.FileName, path, startable: true) is not { } shim
            || !shim.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || Path.GetDirectoryName(Path.GetFullPath(shim)) is not { } folder)
        {
            return false;
        }

        var nodeModules = string.Equals(Path.GetFileName(folder), ".bin", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(folder)!
            : Path.Combine(folder, "node_modules");
        if (Script(nodeModules, package, Path.GetFileNameWithoutExtension(shim)) is not { } script
            || Node(folder, path) is not { } node)
        {
            return false;
        }

        info.FileName = node;
        info.ArgumentList.Insert(0, script);
        return true;
    }
}
