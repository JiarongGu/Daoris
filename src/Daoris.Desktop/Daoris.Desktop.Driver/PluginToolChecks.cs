using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What one readiness check answered: whether it passed, and its sentence.</summary>
public sealed record ReadyAnswer(ReadyCheck Check, bool Ready, string Sentence);

/// <summary>What a declared tool's check found (PLUGTOOL1a).</summary>
/// <param name="Ok">Found, and its version in its range or not one the range needs; each check is its own answer.</param>
/// <param name="File">The file found, or null where none was.</param>
/// <param name="Way">How it is run: Daoris's way for a known tool, the system's for one found on the PATH.</param>
/// <param name="Version">The version it said, its first dotted number; null where it was not asked or said none.</param>
/// <param name="Sentence">The tool's own sentence: where it was found, its version against its range, or its problem.</param>
/// <param name="Ready">Each check's answer, in the manifest's order; none where the tool was not found.</param>
public sealed record PluginToolFound(
    PluginTool Tool, bool Ok, string? File, ToolWay? Way, string? Version, string Sentence, IReadOnlyList<ReadyAnswer> Ready);

/// <summary>One program started and its answer read whole; no exit code from one that started means it did not answer in time.</summary>
internal sealed record CheckRun(bool Started, int? ExitCode, string Stdout, string Stderr);

/// <summary>What starts a program for a check and reads its answer, bounded: a stand-in in the fast tests, a real start otherwise.</summary>
internal delegate Task<CheckRun> CheckRunner(ProcessStartInfo info, TimeSpan patience, CancellationToken ct);

/// <summary>
/// A plugin's tools, found and checked (PLUGTOOL1a, D150 point 7; the UX6 design §7.2–§7.3): each where the tools say, its
/// version against its range, then each of its checks, one at a time, with <see cref="PluginTools.CheckPatience"/> each.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Only on a press or at a trial.</b> Nothing here runs at a load, at a look or on a timer: a check may reach the
/// platform or read another tool's sign-in, and that is the person's to ask for (§7.2). Its callers are the kit's trial
/// (<see cref="PluginKit"/>) and, with PLUGTOOL1c, the plugin page's <i>Check</i>.</para>
///
/// <para><b>Where a tool is found is D121's, unchanged</b> (§7.3): a known tool is the file its way in <c>tools.json</c>
/// resolves, which never falls back to <c>PATH</c>; one Daoris does not know is its <c>command</c> on the tools'
/// <c>PATH</c>. A check's first word is found as a hook's is: the tool's file where a tool answers for it, else the
/// <c>PATH</c>'s. Every start is handed the tools' environment, runs in the folder it is given with git told no repository
/// is there (git walks up), and is typed nothing. 🔴 <b>Daoris never runs <c>fix</c></b>: it is the person's.</para>
/// </remarks>
public static partial class PluginToolChecks
{
    /// <summary>UTF-8 with no byte-order mark: a mark is not part of what a program is told.</summary>
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Find and check one declared tool, with the home's tools, starting each program for real.</summary>
    /// <param name="root">Where each check runs: a scratch folder, which git is told holds no repository.</param>
    public static Task<PluginToolFound> CheckAsync(string home, PluginTool tool, string root, CancellationToken ct = default) =>
        CheckAsync(home, tool, root, path: null, RunAsync, ct);

    /// <param name="path">The <c>PATH</c> the system's tools are found on; null for this process's own.</param>
    /// <param name="run">What starts each program.</param>
    internal static async Task<PluginToolFound> CheckAsync(
        string home, PluginTool tool, string root, string? path, CheckRunner run, CancellationToken ct)
    {
        if (tool.Problem is { } problem) return new(tool, false, null, null, null, problem, []);

        var read = Tools.Read(home);
        var inherited = path ?? Environment.GetEnvironmentVariable(Tools.PathVariable);
        var childPath = Tools.ChildPath(read, home, inherited) ?? inherited;
        var declared = tool.Kind == PluginToolKind.Other ? null : Tools.Find(tool.Id!);
        var name = declared?.Name ?? tool.Name ?? tool.Id!;
        var notRun = tool.Ready.Count > 0 ? " Its checks were not run." : "";

        // Where it is found (§7.3): its way for a known tool, the tools' PATH for any other.
        string file;
        ToolWay way;
        if (declared is not null)
        {
            var resolution = Tools.Resolve(read, home, declared.Id, path);
            if (resolution.File is null) return new(tool, false, null, resolution.Way, null, $"{resolution.Problem}.{notRun}", []);
            file = resolution.File;
            way = resolution.Way ?? ToolWay.System;
        }
        else if (CommandPresence.Resolve(tool.Command!, childPath, startable: true) is { } found)
        {
            file = found;
            way = ToolWay.System;
        }
        else
        {
            return new(tool, false, null, null, null,
                $"`{tool.Command}` is not on this machine's PATH: a tool Daoris does not know is found there, and never downloaded.{notRun}", []);
        }

        // Its version, where there is a question to ask, against its range (§7.2).
        var question = declared?.Version ?? tool.VersionArguments;
        var range = tool.Versions is { } written ? VersionRange.Parse(written) : null;
        string? version = null;
        string? unread = null;
        if (question is not null)
        {
            var asked = string.Join(' ', [declared?.Answers[0] ?? tool.Command!, .. question]);
            CheckRun answer;
            try
            {
                answer = await StartAsync(file, question, root, read, home, run, ct).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                // A question a Windows command shim would read again is not asked (the shim rule, USE1f).
                answer = new CheckRun(false, null, "", "");
            }

            version = answer is { Started: true, ExitCode: not null } ? VersionIn(answer.Stdout) ?? VersionIn(answer.Stderr) : null;
            unread = version is not null ? null
                : !answer.Started ? "it could not be started"
                : answer.ExitCode is null ? $"it did not answer `{asked}` within {Seconds}"
                : $"it answered no version to `{asked}`";
        }

        var head = $"{name}{(version is null ? "" : " " + version)}, {Words(way)}";
        var (ok, sentence) = range is null
            ? (true, unread is null ? $"{head}." : $"{head}; {unread}.")
            : version is not null
                ? range.Holds(version) ? (true, $"{head}, in its range ({range.Needs}).") : (false, $"{head}: needs {range.Needs}; this is {version}.")
                : question is null
                    ? (true, $"{head}; its version is not read, so `{tool.Versions}` is not checked.")
                    : (false, $"{head}: {unread}, so `{tool.Versions}` is not checked.");

        // Each check, one at a time, in the manifest's order.
        var answers = new List<ReadyAnswer>();
        foreach (var check in tool.Ready)
        {
            answers.Add(await ReadyAsync(check, read, home, root, path, childPath, run, ct).ConfigureAwait(false));
        }

        return new(tool, ok, file, way, version, sentence, answers);
    }

    private static async Task<ReadyAnswer> ReadyAsync(
        ReadyCheck check, ToolsRead read, string home, string root, string? path, string? childPath, CheckRunner run, CancellationToken ct)
    {
        var says = check.Says.TrimEnd().TrimEnd('.');
        ReadyAnswer Not(string why) => new(check, false, $"not ready: {says}. {why}");

        // Its first word as a hook's is found (TOOLS5): the file a tool answers with, else the PATH's.
        var first = check.Run[0];
        string file;
        if (Tools.ResolveCommand(read, home, first, path) is { } tool)
        {
            if (tool.File is null) return Not($"`{first}`: {tool.Problem}.");
            file = tool.File;
        }
        else if (CommandPresence.Resolve(first, childPath, startable: true) is { } found)
        {
            file = found;
        }
        else
        {
            return Not($"`{first}` is not on this machine's PATH.");
        }

        CheckRun answer;
        try
        {
            answer = await StartAsync(file, check.Run.Skip(1), root, read, home, run, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            // An argument a Windows command shim would read again (the shim rule, USE1f): refused, never passed.
            return Not(error.Message);
        }

        var command = string.Join(' ', check.Run);
        if (!answer.Started) return Not($"`{first}` could not be started.");
        if (answer.ExitCode is not { } code) return Not($"`{command}` did not answer within {Seconds}.");
        if (code == 0) return new(check, true, $"ready: {says}.");

        var last = LastLine(answer.Stderr) ?? LastLine(answer.Stdout);
        return Not($"`{command}` exited {code}{(last is null ? "" : $": `{Clip(last)}`")}."
            + (check.Fix is { } fix ? $" Run `{fix}` to put it right." : ""));
    }

    /// <summary>A start, handed the tools' environment, in <paramref name="root"/>, and its answer.</summary>
    /// <exception cref="DriverException">An argument a Windows command shim would read again.</exception>
    private static async Task<CheckRun> StartAsync(
        string file, IEnumerable<string> arguments, string root, ToolsRead read, string home, CheckRunner run, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // git walks up: a scratch folder under a checkout answers for the checkout, so git is told no repository is here,
        // as a trial's sample frames are (D101).
        info.Environment["GIT_DIR"] = Path.Combine(root, ".git");
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        Tools.Hand(info, read, home);
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue(Tools.PathVariable, out var childPath) ? childPath : null);
        return await run(info, PluginTools.CheckPatience, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Start a program, type it nothing, and read both streams whole, waiting <paramref name="patience"/> at most; a program
    /// still running then is ended, and answered no exit code.
    /// </summary>
    internal static async Task<CheckRun> RunAsync(ProcessStartInfo info, TimeSpan patience, CancellationToken ct)
    {
        Process? process;
        try
        {
            process = Process.Start(info);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        {
            // Said in Daoris's words by the caller: the system's message names the working directory.
            return new(false, null, "", "");
        }

        if (process is null) return new(false, null, "", "");
        using (process)
        {
            // Nothing is typed to it: a program that waits for input reads its end at once.
            process.StandardInput.Close();
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bound.CancelAfter(patience);
            var stdout = process.StandardOutput.ReadToEndAsync(bound.Token);
            var stderr = process.StandardError.ReadToEndAsync(bound.Token);
            try
            {
                await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
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

                await Task.WhenAll(stdout, stderr).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                return new(true, null, "", "");
            }

            return new(true, process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
    }

    /// <summary>
    /// The version a program said: its first dotted number, the way each tool says it (<c>git version 2.51.0.windows.1</c>,
    /// <c>v22.11.0</c>, az's JSON). The modules' <c>ToolQuestions.VersionIn</c> reads Settings → Tools' versions by the same
    /// rule, which the driver library cannot reference.
    /// </summary>
    internal static string? VersionIn(string said) => DottedNumber().Match(said) is { Success: true } found ? found.Value : null;

    private static string Seconds => $"{PluginTools.CheckPatience.TotalSeconds:0}s";

    private static string Words(ToolWay way) => way switch
    {
        ToolWay.Managed => "managed",
        ToolWay.File => "the file you named",
        _ => "the system's",
    };

    private static string? LastLine(string said) =>
        said.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.Length > 0);

    private static string Clip(string line) => line.Length <= 120 ? line : line[..117] + "…";

    [GeneratedRegex("[0-9]+(?:\\.[0-9]+)+")]
    private static partial Regex DottedNumber();
}
