using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The two questions the driver asks a working tree: is it clean enough to spawn into, and what
/// landed while a session ran. Asked of git itself, because git's answer is the one that matters.
/// </summary>
/// <remarks>
/// A session spawns only onto a clean tree (D46 §3): uncommitted changes are somebody's work in
/// flight — the lesson `reaching-in` was written from — and a tree that is not a git repository at
/// all cannot make that promise, so it refuses too.
/// </remarks>
public static class WorkingTree
{
    /// <summary>Clean means: a git repository, with nothing uncommitted.</summary>
    public static async Task<(bool Clean, string Detail)> CleanAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await GitAsync(root, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return (false, $"not a spawnable tree — git status failed: {FirstLine(stderr)}");
        }

        return string.IsNullOrWhiteSpace(stdout)
            ? (true, "clean")
            : (false, $"the working tree has uncommitted changes ({stdout.Trim().Split('\n').Length} paths) — "
                      + "somebody's work in flight; the driver holds rather than entangling a session with it.");
    }

    /// <summary>Where HEAD is now — the "before" a session's evidence is measured from.</summary>
    public static async Task<string?> HeadAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Trim() : null;
    }

    /// <summary>What landed since — the reviewable half of the record (D46 §4).</summary>
    public static async Task<string> CommitsSinceAsync(string root, string? before, CancellationToken ct = default)
    {
        var range = before is null ? "HEAD" : $"{before}..HEAD";
        var (code, stdout, _) = await GitAsync(root, ["log", "--oneline", range], ct).ConfigureAwait(false);
        if (code != 0) return "no commits readable";

        var commits = stdout.Trim();
        return commits.Length == 0 ? "no commits landed" : $"commits landed:\n{commits}";
    }

    private static async Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(info)
                ?? throw new DriverException("git did not start");
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (-1, "", error.Message);
        }
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }
}
