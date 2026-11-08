using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What the door's build did: its exit, the full paths of the files its own account names, and what it said.</summary>
internal sealed record BuildAccount(int Exit, IReadOnlySet<string> Files, string Said);

/// <summary>
/// The door's one build of a base's producer (the tree-pool design §38.2, 2): the signature's command, <c>dotnet build -c
/// &lt;configuration&gt;</c>, run in the project's folder with MSBuild asked in the same run for its <c>FileWrites</c> and
/// <c>FileWritesShareable</c> items, every file the build wrote or found up to date, which <c>-getItem</c> reports once its targets
/// ran and <c>BuildAccountProcessTests</c> holds whole on a build with nothing to do. It is the build's report on the run the door saw
/// exit, never a file under <c>obj/</c> read afterwards; a run that does not exit 0 accounts for nothing.
/// </summary>
internal static class BuildAccounts
{
    internal static readonly TimeSpan Bound = TimeSpan.FromMinutes(30);

    /// <summary>The words the door runs, after <c>dotnet</c>.</summary>
    internal static IReadOnlyList<string> Words(string project, string configuration) =>
        ["build", project, "-c", configuration, "-nodeReuse:false", "--nologo", "-t:Build", "-getItem:FileWrites", "-getItem:FileWritesShareable"];

    internal static async Task<BuildAccount> RunAsync(string folder, string project, string configuration, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var word in Words(project, configuration)) info.ArgumentList.Add(word);
        Tools.Hand(info);
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        var run = await ChildProcess.RunAsync(info, Bound, ct, closeInput: true).ConfigureAwait(false);
        if (!run.Started) return new BuildAccount(-1, new HashSet<string>(), $"dotnet did not start: {run.Error?.Message}");
        if (run.TimedOut) return new BuildAccount(-1, new HashSet<string>(), $"the build ran past {Bound.TotalMinutes} minutes and was ended");
        if (run.ExitCode != 0) return new BuildAccount(run.ExitCode ?? -1, new HashSet<string>(), $"{run.Stdout}{run.Stderr}".Trim());
        return Parse(run.Stdout) is { } files ? new BuildAccount(0, files, "") : new BuildAccount(-1, new HashSet<string>(), "the build gave no account of its files");
    }

    /// <summary>The full paths <c>-getItem</c>'s answer names, or null where it does not read as one.</summary>
    internal static IReadOnlySet<string>? Parse(string said)
    {
        try
        {
            using var json = JsonDocument.Parse(said.TrimStart('﻿'));
            if (!json.RootElement.TryGetProperty("Items", out var items)) return null;
            var files = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var name in new[] { "FileWrites", "FileWritesShareable" })
            {
                if (!items.TryGetProperty(name, out var each)) continue;
                foreach (var item in each.EnumerateArray()) files.Add(Path.GetFullPath(item.GetProperty("FullPath").GetString()!));
            }

            return files;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
