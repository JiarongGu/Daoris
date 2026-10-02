using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The world registration following reads on this machine (WSSETUP5): the registry door, each repository's line read
/// as git objects, the landings' record, and the two records under the home. The follower is
/// <see cref="RegistrationFollow"/>; this is only where its facts come from and where its outcomes go.
/// </summary>
/// <param name="config">The driver's choices, which name each repository's line (D86).</param>
public sealed class RegistrationWorld(ServiceClient service, string home, DriverConfig config) : IRegistrationWorld
{
    public bool LocalService => Uri.TryCreate(service.BaseUrl, UriKind.Absolute, out var uri) && Tools.IsLoopback(uri.Host);

    public Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct) => service.RegistrationsAsync(ct);

    public async Task<LineFiles> ReadLineAsync(RegistrationRow row, CancellationToken ct)
    {
        // A root that is not there asks git nothing: git would start in no folder, or answer for one above it.
        if (string.IsNullOrWhiteSpace(row.Root) || !Directory.Exists(row.Root)) return new LineFiles(null, null, "its checkout is not where its row says");

        try
        {
            var line = await CanonicalLine.ResolveAsync(row.Root, row.Repository, row.Workspace, config, ct).ConfigureAwait(false);
            return line.Branch is null
                ? new LineFiles(null, null, "none is set, and git names none")
                : await LineDeclarationReader.ReadAsync(row.Root, line.Branch, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return new LineFiles(null, null, error.Message);
        }
    }

    public string? WorktreeMain(string root) => LinkedWorktree.MainOf(root);

    /// <summary>The newest standing branch a set-up's landing made for this repository (D102, WSR5), by its quest's title.</summary>
    public string? SetupWaiting(string repository) =>
        new LandedBranches(home).All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && SetupQuests.IsSetup(entry.Title))
            .OrderByDescending(entry => entry.LandedAt)
            .FirstOrDefault()?.Branch;

    public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct) => service.RegisterAsync(body, ct);

    public Task<string?> RefreshAsync(CancellationToken ct) => service.RefreshAsync(ct);

    public void Followed(RegistrationFollowed what)
    {
        RegistryFollowing.Remember(home, what, DateTimeOffset.UtcNow);
        service.Followed(what);
    }
}

/// <summary>
/// The two files a registration reads, from a repository's LINE as git objects (WSSETUP5, D124 §3.2): the line's commit,
/// then <c>daoris.json</c> and <c>daoris.lanes.json</c> at its root, as <see cref="LayoutReader"/> reads the layout.
/// Never the checkout's working files, which may be on another branch or hold edits.
/// </summary>
public static class LineDeclarationReader
{
    /// <summary>How much of one file git is asked for: a manifest or a lanes file is far smaller.</summary>
    private const int TextLimit = 512 * 1024;

    public static async Task<LineFiles> ReadAsync(string root, string line, CancellationToken ct = default)
    {
        // 🔴 git walks UP (FIX-LOG): a folder that is not a repository's top would answer for the one above it.
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(line, null, "its checkout here is not a git repository");
        }

        if (!BranchName.IsValid(line)) return new(line, null, $"its line `{line}` is not a branch name git would take");

        var (found, sha, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        var commit = sha.Trim();
        if (found != 0 || !WorkingTree.IsCommitId(commit)) return new(line, null, $"its line `{line}` names no commit here");

        var (listed, output, problem) = await WorkingTree.GitAsync(
            root, ["ls-tree", "-z", commit, "--", LineRegistration.ManifestFile, LineRegistration.LanesFile], ct).ConfigureAwait(false);
        if (listed != 0) return new(line, null, $"git could not list `{line}` at `{commit[..7]}`: {problem.Trim()}");

        var entries = LineEntry.Parse(output);
        string? manifest = null, lanes = null, odd = null;
        foreach (var (path, assign) in new (string, Action<string>)[]
                 {
                     (LineRegistration.ManifestFile, text => manifest = text),
                     (LineRegistration.LanesFile, text => lanes = text),
                 })
        {
            var entry = entries.FirstOrDefault(each => each.Path == path);
            if (entry is null) continue;
            if (!entry.IsFile || !WorkingTree.IsCommitId(entry.Object))
            {
                odd ??= $"its {path} is {(entry.IsLink ? "a link" : "not a file")}";
                continue;
            }

            var read = await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", entry.Object], TextLimit, ct).ConfigureAwait(false);
            if (read is null)
            {
                odd ??= $"git could not read its {path}";
                continue;
            }

            assign(Encoding.UTF8.GetString(read.Value.Bytes, 0, read.Value.Count));
        }

        return new(line, commit, null) { Manifest = manifest, Lanes = lanes, FileProblem = odd };
    }
}

/// <summary>
/// Whether a checkout is a linked worktree (D51): a twin of the CLI's <c>linkedWorktreeMain</c> (<c>connect.ts</c>), which
/// refuses to register one. Git marks a linked worktree itself: its <c>.git</c> is a FILE naming the main repository's
/// <c>.git/worktrees/&lt;name&gt;</c>. A submodule wears a <c>.git</c> file too, pointing at <c>.git/modules/&lt;name&gt;</c>,
/// and is a repository of its own, so only the worktree marker counts. No spawn.
/// </summary>
public static partial class LinkedWorktree
{
    /// <summary>The main tree <paramref name="root"/> is a linked worktree of, or null for a main tree, a submodule or no repository.</summary>
    public static string? MainOf(string root)
    {
        try
        {
            var marker = Path.Combine(root, ".git");
            if (!File.Exists(marker)) return null;

            var pointed = GitDir().Match(File.ReadAllText(marker)) is { Success: true } match ? match.Groups[1].Value.Trim() : "";
            if (pointed.Length == 0) return null;

            var gitdir = Path.IsPathRooted(pointed) ? pointed : Path.GetFullPath(Path.Combine(root, pointed));
            // <main>/.git/worktrees/<name>, in either separator: git writes forward slashes on every platform.
            var parts = gitdir.Replace('\\', '/').Split('/');
            var at = Array.LastIndexOf(parts, "worktrees");
            if (at < 2 || parts[at - 1] != ".git") return null;
            return string.Join('/', parts[..(at - 1)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An unreadable marker is not this reader's to diagnose: git itself refuses next, in better words.
            return null;
        }
    }

    [GeneratedRegex(@"^gitdir:\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex GitDir();
}
