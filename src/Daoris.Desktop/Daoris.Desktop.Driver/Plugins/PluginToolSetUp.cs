using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>What a plugin tool's set-up press came to: a refusal's code with its words, or the console's exit and the checks read again.</summary>
/// <param name="Code">
/// Null where the console ran: otherwise <c>unknown_tool</c>, <c>unknown_check</c>, <c>no_fix</c>, <c>fix_not_declared</c>,
/// <c>not_found</c> (the tool is not on this machine; <see cref="Install"/> says how to get it), <c>refused</c> (a start the
/// platform would read again) or <c>not_started</c>.
/// </param>
/// <param name="Said">The sentence: the refusal's words, or what the pressed check answered when read again.</param>
/// <param name="ExitCode">The console's exit; null where nothing ran.</param>
/// <param name="Install">The plugin's install words for this platform, where the tool was not found.</param>
/// <param name="Found">The tool read again after the console ended; null where nothing ran.</param>
public sealed record ToolSetUp(string? Code, string Said, int? ExitCode, string? Install, PluginToolFound? Found);

/// <summary>The person's console: starts a program in a window they see and answers its exit code, null where it did not start.</summary>
internal delegate Task<int?> ConsoleRunner(ProcessStartInfo info, CancellationToken ct);

/// <summary>
/// A plugin tool set up or signed into on the person's press (ADOSIGNIN1, D150 point 7; the UX6 design §7.2): a check's
/// <c>fix</c> run in a console window the person sees, then the tool's checks read again.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Only a press starts it</b>, never a load, a look or an install, and the press is the person's alone (the pipe's
/// census marks <c>PLUGIN_TOOL_FIX</c> <c>Person</c>). 🔴 <b>Daoris never signs in itself</b>: the window is the program's own, and
/// the person types what it asks.</para>
///
/// <para><b>Exactly the manifest's command.</b> The fix's words are split by <see cref="PluginTools.FixWords"/> and handed
/// to the program as separate arguments, so no shell reads them; a first word that is not a command of the plugin's own
/// <c>tools</c> is refused. The program is found as a check's first word is, and a missing tool says the plugin's install
/// words instead of starting anything.</para>
/// </remarks>
public static class PluginToolSetUp
{
    /// <summary>Set one check of one tool of an installed plugin, with the home's tools, the console starting for real.</summary>
    public static async Task<ToolSetUp> RunAsync(string home, IReadOnlyList<PluginTool> tools, string toolId, int check, CancellationToken ct = default)
    {
        var scratch = PluginKitFiles.ScratchIn(PluginTrials.TrialsOf(home));
        try
        {
            var root = Path.Combine(scratch, "root");
            PluginFolders.Make(root);
            return await RunAsync(home, tools, toolId, check, root, path: null, InConsoleAsync, PluginToolRuns.RunAsync, ct).ConfigureAwait(false);
        }
        finally
        {
            PluginKitFiles.Remove(scratch);
        }
    }

    /// <summary>
    /// Each declared tool found and checked, on the person's press of <i>Check</i>: the plugin page's read of where its tools stand.
    /// Never at a look (§7.2); a check may reach the platform or read another tool's sign-in.
    /// </summary>
    public static async Task<IReadOnlyList<PluginToolFound>> CheckAsync(string home, IReadOnlyList<PluginTool> tools, CancellationToken ct = default)
    {
        var scratch = PluginKitFiles.ScratchIn(PluginTrials.TrialsOf(home));
        try
        {
            var root = Path.Combine(scratch, "root");
            PluginFolders.Make(root);
            var found = new List<PluginToolFound>();
            foreach (var tool in tools) found.Add(await PluginToolChecks.CheckAsync(home, tool, root, ct).ConfigureAwait(false));
            return found;
        }
        finally
        {
            PluginKitFiles.Remove(scratch);
        }
    }

    /// <param name="check">The check's number, from 1, in the manifest's order.</param>
    /// <param name="path">The <c>PATH</c> the system's tools are found on; null for this process's own.</param>
    /// <param name="console">What starts the window.</param>
    /// <param name="run">What starts each check's program when the checks are read again.</param>
    internal static async Task<ToolSetUp> RunAsync(
        string home, IReadOnlyList<PluginTool> tools, string toolId, int check, string root, string? path, ConsoleRunner console, CheckRunner run,
        CancellationToken ct)
    {
        static ToolSetUp Refused(string code, string said, string? install = null) => new(code, said, null, install, null);

        var tool = tools.FirstOrDefault(each => each.Problem is null && string.Equals(each.Id, toolId, StringComparison.Ordinal));
        if (tool is null) return Refused("unknown_tool", $"this plugin declares no tool `{toolId}`.");
        if (check < 1 || check > tool.Ready.Count) return Refused("unknown_check", $"tool `{toolId}` has no check {check}.");

        var ready = tool.Ready[check - 1];
        if (ready.Fix is null) return Refused("no_fix", $"tool `{toolId}`'s check {check} names no `fix`, so there is nothing to run.");

        var (words, problem) = PluginTools.FixWords(tools, ready);
        if (words is null) return Refused("fix_not_declared", problem!);

        // Its first word is found as a check's is (TOOLS5): the file a tool answers with, else the PATH's.
        var read = Tools.Read(home);
        var inherited = path ?? PluginToolRuns.InheritedPath();
        var childPath = Tools.ChildPath(read, home, inherited) ?? inherited;
        var first = words[0];
        string file;
        if (Tools.ResolveCommand(read, home, first, path) is { } resolved)
        {
            if (resolved.File is null) return Refused("not_found", $"`{first}`: {resolved.Problem}.", PluginTools.InstallHere(tool));
            file = resolved.File;
        }
        else if (CommandPresence.Resolve(first, childPath, startable: true) is { } found)
        {
            file = found;
        }
        else
        {
            return Refused("not_found", $"`{first}` is not on this machine's PATH.", PluginTools.InstallHere(tool));
        }

        ProcessStartInfo info;
        try
        {
            info = ConsoleStart(file, words.Skip(1), root, read, home);
        }
        catch (DriverException error)
        {
            // An argument a Windows command shim would read again (the shim rule, USE1f): refused, never passed.
            return Refused("refused", error.Message);
        }

        var exit = await console(info, ct).ConfigureAwait(false);
        if (exit is null) return Refused("not_started", $"`{first}` could not be started.");

        // The checks are read again whatever the console's exit was: the standing is what they say, not what the window did.
        var again = await PluginToolChecks.CheckAsync(home, tool, root, path, run, ct).ConfigureAwait(false);
        var said = check <= again.Ready.Count ? again.Ready[check - 1].Sentence : again.Sentence;
        return new(null, said, exit, null, again);
    }

    /// <summary>The start of a fix in a console the person sees: the program alone with its arguments, the tools' environment, nothing redirected.</summary>
    /// <exception cref="DriverException">An argument a Windows command shim would read again.</exception>
    internal static ProcessStartInfo ConsoleStart(string file, IEnumerable<string> arguments, string root, ToolsRead read, string home)
    {
        // console-window: the person sees the program's own console, which is where they sign in or watch it set up.
        var info = new ProcessStartInfo { FileName = file, WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        Tools.Hand(info, read, home);
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue(Tools.PathVariable, out var childPath) ? childPath : null);
        return info;
    }

    /// <summary>The window started for real and waited for. Never ended by the token: the person's console is theirs to close.</summary>
    private static async Task<int?> InConsoleAsync(ProcessStartInfo info, CancellationToken ct)
    {
        Process? process;
        try
        {
            // Through the start gate (PROCSTART2): a plugin tool's set-up is a press, so a person's start (PARALLEL1d).
            process = await ProcessStarts.Shared.StartAsync(info, ct).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return null;
        }

        if (process is null) return null;
        using (process)
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return process.ExitCode;
        }
    }
}
