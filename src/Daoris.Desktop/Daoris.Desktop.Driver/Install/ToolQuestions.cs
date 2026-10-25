using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A tool asked one question and its answer read whole (TOOLS7, D121 §4.1): the version a file answers before it is
/// named, the version each tool runs at, and git's own system configuration, which says how that git reads a checkout.
/// </summary>
/// <remarks>
/// Asked of the resolved file by its whole path, with fixed arguments only, and handed the tools' environment as every
/// child is (TOOLS5): a version is asked on the <c>PATH</c> a session would be started on. Bounded, since a file the
/// person names may be any program, and one that waits for input answers nothing in time. The driver library's since PUSH1f2c, so
/// <c>daoris-driver tool use … file</c> asks a named file its version as the window's route does (<see cref="ToolChoices"/>).
/// </remarks>
public static partial class ToolQuestions
{
    /// <summary>How long one answer may take before it counts as none.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The keys that make two gits read one checkout differently (§4.1), in the order a switch names them: line endings
    /// twice, links, and long paths, which Daoris's own calls set and a session's git does not.
    /// </summary>
    public static readonly IReadOnlyList<string> CheckoutKeys = ["core.autocrlf", "core.eol", "core.symlinks", "core.longpaths"];

    /// <summary>What a program said, both streams, or why it said nothing.</summary>
    /// <param name="Started">Whether the program started at all.</param>
    public sealed record Answer(bool Started, int? ExitCode, string Output, string? Problem);

    /// <summary>Ask a program one question: its whole path, fixed arguments, both streams read as UTF-8.</summary>
    public static async Task<Answer> AskAsync(string file, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            // Read as the tools write, not in the console's code page (PLUG8); nothing is typed, and the end is UTF-8 too.
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true, // a question from a window must not open a console (Adapters.Shell)
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // Asked as a child is started (TOOLS5): on the PATH a session would be given.
        Tools.Hand(info);

        // Nothing is typed to it: a program that waits for input reads its end at once.
        var run = await ChildProcess.RunAsync(info, Patience, ct, closeInput: true).ConfigureAwait(false);
        if (!run.Started)
        {
            return new Answer(false, null, "", run.Error is null ? $"{file} did not start" : $"{file} did not start — {run.Error.Message}");
        }

        return run.TimedOut
            ? new Answer(true, null, "", $"{file} did not answer within {Patience.TotalSeconds:0} seconds")
            : new Answer(true, run.ExitCode, $"{run.Stdout}\n{run.Stderr}", null);
    }

    /// <summary>
    /// The version a file answers to its tool's version question, or why it answered none: a file that did not start, did not answer
    /// in time, or said no dotted number.
    /// </summary>
    public static async Task<(string? Version, string? Problem)> VersionOfAsync(string file, ToolDeclaration tool, CancellationToken ct)
    {
        var answer = await AskAsync(file, tool.Version, ct).ConfigureAwait(false);
        var version = answer.Started ? VersionIn(answer.Output) : null;
        return (version, answer.Problem ?? (version is null ? $"{file} answered no version to `{string.Join(' ', tool.Version)}`" : null));
    }

    /// <summary>
    /// The version a program said: its first dotted number, the way each declared tool says it (<c>git version
    /// 2.51.0.windows.1</c>, <c>v22.11.0</c>, az's JSON). Null when it said none.
    /// </summary>
    public static string? VersionIn(string said) => DottedNumber().Match(said) is { Success: true } found ? found.Value : null;

    /// <summary>
    /// <c>git config --list</c>'s lines, by key: a key in lower case as git spells it, and its last value, which is the
    /// one git uses. A line with no <c>=</c> is a key set with no value, which git reads as true.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ConfigIn(string said)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in said.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            var at = line.IndexOf('=');
            var key = (at < 0 ? line : line[..at]).Trim().ToLowerInvariant();
            if (key.Length == 0 || key.Contains(' ')) continue;
            keys[key] = at < 0 ? "true" : line[(at + 1)..];
        }

        return keys;
    }

    /// <summary>The checkout keys two gits read differently (§4.1), in <see cref="CheckoutKeys"/>' order; unset is null.</summary>
    public static IReadOnlyList<(string Key, string? Now, string? Then)> Differences(
        IReadOnlyDictionary<string, string> now, IReadOnlyDictionary<string, string> then) =>
        [
            .. CheckoutKeys
                .Select(key => (Key: key, Now: now.GetValueOrDefault(key), Then: then.GetValueOrDefault(key)))
                .Where(pair => !string.Equals(pair.Now, pair.Then, StringComparison.OrdinalIgnoreCase)),
        ];

    [GeneratedRegex(@"\d+(?:\.\d+)+")]
    private static partial Regex DottedNumber();
}
