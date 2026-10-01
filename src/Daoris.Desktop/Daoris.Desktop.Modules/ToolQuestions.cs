using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A tool asked one question and its answer read whole (TOOLS7, D121 §4.1): the version a file answers before it is
/// named, the version each tool runs at, and git's own system configuration, which says how that git reads a checkout.
/// </summary>
/// <remarks>
/// Asked of the resolved file by its whole path, with fixed arguments only, and handed the tools' environment as every
/// child is (TOOLS5): a version is asked on the <c>PATH</c> a session would be started on. Bounded, since a file the
/// person names may be any program, and one that waits for input answers nothing in time.
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

        try
        {
            using var process = Process.Start(info);
            if (process is null) return new Answer(false, null, "", $"{file} did not start");
            // Nothing is typed to it: a program that waits for input reads its end at once.
            process.StandardInput.Close();

            using var patience = CancellationTokenSource.CreateLinkedTokenSource(ct);
            patience.CancelAfter(Patience);
            var stdout = process.StandardOutput.ReadToEndAsync(patience.Token);
            var stderr = process.StandardError.ReadToEndAsync(patience.Token);
            try
            {
                await process.WaitForExitAsync(patience.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It ended as it was stopped.
                }

                // The reads are awaited and dropped, so nothing faults unobserved: the answer is already "none in time".
                await Task.WhenAll(stdout, stderr).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                return new Answer(true, null, "", $"{file} did not answer within {Patience.TotalSeconds:0} seconds");
            }

            return new Answer(true, process.ExitCode, $"{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}", null);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return new Answer(false, null, "", $"{file} did not start — {error.Message}");
        }
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
