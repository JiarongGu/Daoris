using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GitLibraryBench;

/// <summary>
/// One start of git: its wall time, how long <see cref="Process.Start()"/> took, and git's own time from its trace, split where the
/// trace says: up to its <c>start</c> event, the process walk Git for Windows' trace adds (<c>windows/ancestry</c>), and the command's work.
/// </summary>
internal sealed record StartSample(double WallMs, double CreateMs, double? GitMs, double? SetupMs = null, double? WalkMs = null, double? WorkMs = null)
{
    /// <summary>What the start cost outside git's own work: process creation, the loader and whatever a scanner adds.</summary>
    public double? OutsideMs => GitMs is { } own ? WallMs - own : null;
}

/// <summary>git's binary, started as Daoris starts it: the real git behind Git for Windows' launcher, pipes redirected, no window.</summary>
internal sealed class GitBinary
{
    private readonly string _file;
    private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);

    private GitBinary(string file) => _file = file;

    public string File => _file;

    /// <summary>git on <c>PATH</c>, past the launcher where it is one, with the variables the launcher would set.</summary>
    public static GitBinary Resolve(string? named)
    {
        var file = named ?? Which("git.exe") ?? Which("git") ?? throw new InvalidOperationException("no git on PATH");
        var folder = Path.GetDirectoryName(Path.GetFullPath(file))!;
        if (OperatingSystem.IsWindows() && Path.GetFileName(folder).Equals("cmd", StringComparison.OrdinalIgnoreCase))
        {
            var top = Path.GetDirectoryName(folder)!;
            var real = Path.Combine(top, "mingw64", "bin", "git.exe");
            if (System.IO.File.Exists(real))
            {
                var binary = new GitBinary(real);
                var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
                binary._environment["MSYSTEM"] = "MINGW64";
                binary._environment["HOME"] = home;
                binary._environment["PATH"] = $"{Path.Combine(top, "mingw64", "bin")};{Path.Combine(top, "usr", "bin")};{home}\\bin;{Environment.GetEnvironmentVariable("PATH")}";
                return binary;
            }
        }

        return new GitBinary(file);
    }

    private static string? Which(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(each => Path.Combine(each, name))
            .FirstOrDefault(System.IO.File.Exists);

    /// <summary>One start in <paramref name="folder"/>: exit code, stdout, and the sample; <paramref name="trace"/> names git's trace file.</summary>
    public async Task<(int Code, string Output, StartSample Sample)> RunAsync(
        string folder, IReadOnlyList<string> arguments, string? trace = null, CancellationToken ct = default)
    {
        var info = new ProcessStartInfo
        {
            FileName = _file,
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.longpaths=true");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in _environment) info.Environment[name] = value;
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment.Remove("GIT_TRACE2_EVENT");
        if (trace is not null) info.Environment["GIT_TRACE2_EVENT"] = trace;

        var wall = Stopwatch.StartNew();
        using var process = new Process { StartInfo = info };
        var create = Stopwatch.StartNew();
        process.Start();
        create.Stop();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var text = await output.ConfigureAwait(false);
        await error.ConfigureAwait(false);
        wall.Stop();

        var sample = new StartSample(wall.Elapsed.TotalMilliseconds, create.Elapsed.TotalMilliseconds, null);
        return (process.ExitCode, text, trace is null ? sample : Traced(sample, trace));
    }

    /// <summary>
    /// git's own time, from its trace2 event stream of the top process (a child's <c>sid</c> carries a slash): <c>t_abs</c> counts from
    /// git's <c>main</c>, so the exit event's is all of git's, and what is outside it is process creation, the loader and the exit.
    /// </summary>
    private static StartSample Traced(StartSample sample, string trace)
    {
        if (!System.IO.File.Exists(trace)) return sample;
        double? start = null, walk = null, exit = null;
        foreach (var line in System.IO.File.ReadLines(trace))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (root.TryGetProperty("sid", out var sid) && sid.GetString()!.Contains('/')) continue;
            if (!root.TryGetProperty("t_abs", out var at)) continue;
            var ms = at.GetDouble() * 1000;
            switch (root.GetProperty("event").GetString())
            {
                case "start":
                    start = ms;
                    break;
                case "data_json" when root.TryGetProperty("key", out var key) && key.GetString() == "windows/ancestry":
                    walk = ms;
                    break;
                case "exit":
                    exit = ms;
                    break;
            }
        }

        return sample with
        {
            GitMs = exit,
            SetupMs = start,
            WalkMs = walk is { } walked && start is { } started ? walked - started : null,
            WorkMs = exit is { } ended ? ended - (walk ?? start ?? 0) : null,
        };
    }
}
