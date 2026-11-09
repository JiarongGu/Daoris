using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the door's build did: its exit, the full paths of the files its own account names, what it said, the frameworks it built one
/// by one (empty for a project of one) and each command it ran.
/// </summary>
internal sealed record BuildAccount(int Exit, IReadOnlySet<string> Files, string Said)
{
    internal IReadOnlyList<string> Frameworks { get; init; } = [];

    internal IReadOnlyList<string> Commands { get; init; } = [];
}

/// <summary>
/// The door's one build of a base's producer (the tree-pool design §38.2, 1-2): the rule's <c>builds</c>, its configuration filled in,
/// run in the project's folder with MSBuild asked in the same run for its <c>FileWrites</c> and <c>FileWritesShareable</c> items, every
/// file the build wrote or found up to date, which <c>-getItem</c> reports once its targets ran and <c>BuildAccountProcessTests</c> holds
/// whole on a build with nothing to do. A project the SDK's evaluation reads as building several frameworks is built once for each
/// (<c>-f</c>), since an outer build's account names none of its inner builds' files. It is the build's report on the run the door saw
/// exit, never a file under <c>obj/</c> read afterwards; a run that does not exit 0 accounts for nothing.
/// </summary>
internal static class BuildAccounts
{
    internal static readonly TimeSpan Bound = TimeSpan.FromMinutes(30);

    private static readonly string[] Account = ["-nodeReuse:false", "--nologo", "-t:Build", "-getItem:FileWrites", "-getItem:FileWritesShareable"];

    /// <summary>The words the door runs for one framework (null for the project's one), the program first.</summary>
    internal static IReadOnlyList<string> Words(string builds, string project, string configuration, string? framework) =>
        [.. builds.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word.Replace("{configuration}", configuration, StringComparison.Ordinal)),
            project, .. framework is null ? Array.Empty<string>() : ["-f", framework], .. Account];

    /// <summary>The frameworks a build is run for, one by one: those <c>TargetFrameworks</c> names where no single <c>TargetFramework</c> is set; else none.</summary>
    internal static IReadOnlyList<string>? Frameworks(string said)
    {
        try
        {
            using var json = JsonDocument.Parse(said.TrimStart('﻿'));
            var properties = json.RootElement.GetProperty("Properties");
            if (properties.TryGetProperty("TargetFramework", out var one) && one.GetString() is { Length: > 0 }) return [];
            return properties.TryGetProperty("TargetFrameworks", out var several) && several.GetString() is { } named
                ? [.. named.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
                : [];
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    internal static async Task<BuildAccount> RunAsync(
        string folder, string project, string configuration, string builds, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
    {
        var program = builds.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var evaluated = await RunOneAsync(folder, [program, "msbuild", project, "-nologo", $"-p:Configuration={configuration}", "-getProperty:TargetFramework", "-getProperty:TargetFrameworks"], environment, ct)
            .ConfigureAwait(false);
        if (evaluated.Exit != 0) return evaluated;
        if (Frameworks(evaluated.Said) is not { } frameworks) return Failed(-1, "the SDK's evaluation gave no frameworks");

        var files = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var commands = new List<string>();
        foreach (var framework in frameworks.Count == 0 ? [null] : frameworks.Select(each => (string?)each))
        {
            var words = Words(builds, project, configuration, framework);
            commands.Add(string.Join(' ', words));
            var built = await RunOneAsync(folder, words, environment, ct).ConfigureAwait(false);
            if (built.Exit != 0) return built with { Commands = commands };
            if (Parse(built.Said) is not { } account) return Failed(-1, "the build gave no account of its files") with { Commands = commands };
            files.UnionWith(account);
        }

        return new BuildAccount(0, files, "") { Frameworks = frameworks, Commands = commands };
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

    /// <summary>One run of <paramref name="words"/>, the program first, its output as <see cref="BuildAccount.Said"/> on exit 0.</summary>
    private static async Task<BuildAccount> RunOneAsync(string folder, IReadOnlyList<string> words, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
    {
        var info = new ProcessStartInfo(words[0])
        {
            WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var word in words.Skip(1)) info.ArgumentList.Add(word);
        Tools.Hand(info);
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        var run = await ChildProcess.RunAsync(info, Bound, ct, closeInput: true).ConfigureAwait(false);
        if (!run.Started) return Failed(-1, $"{words[0]} did not start: {run.Error?.Message}");
        if (run.TimedOut) return Failed(-1, $"the build ran past {Bound.TotalMinutes} minutes and was ended");
        return run.ExitCode != 0 ? Failed(run.ExitCode ?? -1, $"{run.Stdout}{run.Stderr}".Trim()) : new BuildAccount(0, new HashSet<string>(), run.Stdout);
    }

    private static BuildAccount Failed(int exit, string said) => new(exit, new HashSet<string>(), said);
}
