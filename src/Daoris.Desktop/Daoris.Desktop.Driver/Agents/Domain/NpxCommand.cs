using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A command that runs a package's program through <c>npx</c>, read as npm's own <c>exec</c> reads it: the package, its version as
/// written (exact, a tag, a range, or none), the program named where <c>-p</c> named the package, and the arguments the program is
/// handed. A session never starts one as written (LAUNCHCHAIN1): on Windows each <c>npx</c> is two <c>cmd</c>s, npm's own node and
/// a console each before the program's node, so the package is resolved once into the toolchain and its script started by node.
/// </summary>
/// <param name="Package">The package's name, as npm spells it: <c>@scope/name</c> or <c>name</c>.</param>
/// <param name="Version">The version as written after the name's <c>@</c>; null where none was.</param>
/// <param name="Bin">The program named after a <c>-p</c> package; null where the package itself was named, and npm's rule picks.</param>
/// <param name="Arguments">What follows the package or the program, handed to the program as written.</param>
public sealed record NpxCommand(string Package, string? Version, string? Bin, IReadOnlyList<string> Arguments)
{
    // npm's name rule: lowercase letters, digits and `-._~`, under one optional scope; nothing a path could climb out by.
    private static readonly Regex Name = new(@"^(@[a-z0-9~-][a-z0-9._~-]*/)?[a-z0-9~-][a-z0-9._~-]*$", RegexOptions.CultureInvariant);

    // A version, a tag or a range as npm takes one after the `@`: no space, no quote, nothing a shell reads.
    private static readonly Regex Spec = new(@"^[A-Za-z0-9._~^<>=|+*-]+$", RegexOptions.CultureInvariant);

    /// <summary>Whether <paramref name="command"/> is <c>npx</c>: by its name, or a path to <c>npx</c> or <c>npx.cmd</c>.</summary>
    public static bool Names(string command)
    {
        var name = command.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return string.Equals(name, "npx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "npx.cmd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "npx.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The command read, or why it cannot be: null with no reason where the command is no <c>npx</c>. An option this reading does not
    /// know is refused rather than guessed at, since what it changes is npm's to say.
    /// </summary>
    public static (NpxCommand? Command, string? Refusal) Read(string command, IReadOnlyList<string> arguments)
    {
        if (!Names(command)) return (null, null);

        string? package = null;
        var at = 0;
        for (; at < arguments.Count; at++)
        {
            var word = arguments[at];
            if (word == "--")
            {
                at++;
                break;
            }

            if (!word.StartsWith('-')) break;
            if (word is "-y" or "--yes" or "-q" or "--quiet" or "--prefer-offline" or "--prefer-online" or "--offline") continue;
            if (word is "-p" or "--package")
            {
                if (package is not null || at + 1 >= arguments.Count) return (null, $"`npx {word}` names one package here, and only one");
                package = arguments[++at];
                continue;
            }

            if (word.StartsWith("--package=", StringComparison.Ordinal))
            {
                if (package is not null) return (null, "`npx --package` names one package here, and only one");
                package = word["--package=".Length..];
                continue;
            }

            return (null, $"`npx {word}` is not an option a session's server starts with");
        }

        if (at >= arguments.Count) return (null, "`npx` names no program to run");
        var named = arguments[at];
        var rest = arguments.Skip(at + 1).ToList();
        if (package is null)
        {
            return Spelled(named) is { } spelled
                ? (new NpxCommand(spelled.Name, spelled.Version, null, rest), null)
                : (null, $"`{named}` is not a package npm would install");
        }

        if (Spelled(package) is not { } from) return (null, $"`{package}` is not a package npm would install");
        if (!Name.IsMatch(named)) return (null, $"`{named}` is not a program a package names");
        return (new NpxCommand(from.Name, from.Version, named, rest), null);
    }

    /// <summary>A package as written, <c>name</c>, <c>name@version</c>, <c>@scope/name</c> or <c>@scope/name@version</c>; null where it is none.</summary>
    public static (string Name, string? Version)? Spelled(string written)
    {
        var split = written.LastIndexOf('@');
        var (name, version) = split > 0 ? (written[..split], written[(split + 1)..]) : (written, null);
        if (!Name.IsMatch(name)) return null;
        if (version is not null && (version.Length == 0 || !Spec.IsMatch(version))) return null;
        return (name, version);
    }

    /// <summary>The package's name without its scope: the program npm runs where the package names several.</summary>
    public static string Unscoped(string package) => package[(package.LastIndexOf('/') + 1)..];
}
