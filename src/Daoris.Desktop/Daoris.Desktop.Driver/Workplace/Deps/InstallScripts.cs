using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Which installs leave the repository's lifecycle scripts out. A dependency whose rule declares a <c>setup</c> has taken its own
/// scripts over, so its npm install runs <c>--ignore-scripts</c> and can seal, but only where the lockfile shows no package needing a
/// script: none marks <c>hasInstallScript</c>, and none resolves from git, whose <c>prepare</c> npm runs without the lockfile marking it.
/// </summary>
internal static class InstallScripts
{
    private const string Ci = "npm ci";
    private const string Flag = " --ignore-scripts";

    /// <summary><paramref name="install"/> with the flag added where it is exactly <c>npm ci</c> and <paramref name="lockfile"/> reads and needs no script.</summary>
    internal static string Rewritten(string install, string? lockfile) =>
        install == Ci && lockfile is not null && NeedsNoScript(lockfile) ? install + Flag : install;

    /// <summary>The dependencies with each npm install of a rule that declares a setup rewritten, from the lockfile at <paramref name="commit"/>.</summary>
    internal static async Task<IReadOnlyList<StoreDependency>> ApplyAsync(string tree, string commit, IReadOnlyList<StoreDependency> dependencies, CancellationToken ct)
    {
        var applied = new List<StoreDependency>();
        foreach (var dependency in dependencies)
        {
            if (dependency.Rule is not { Setup.Count: > 0, Install: Ci })
            {
                applied.Add(dependency);
                continue;
            }

            var lock_ = dependency.Runs.Length == 0 ? "package-lock.json" : dependency.Runs + "/package-lock.json";
            var (code, text, _) = await WorkingTree.GitAsync(tree, ["show", $"{commit}:{lock_}"], ct).ConfigureAwait(false);
            var install = Rewritten(Ci, code == 0 ? text : null);
            applied.Add(dependency with { Rule = dependency.Rule with { Install = install } });
        }

        return applied;
    }

    private static bool NeedsNoScript(string lockfile)
    {
        try
        {
            using var document = JsonDocument.Parse(lockfile);
            if (!document.RootElement.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Object) return false;
            foreach (var package in packages.EnumerateObject())
            {
                if (package.Value.TryGetProperty("hasInstallScript", out var marked) && marked.ValueKind == JsonValueKind.True) return false;
                if (package.Value.TryGetProperty("resolved", out var resolved) && resolved.ValueKind == JsonValueKind.String && FromGit(resolved.GetString()!)) return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool FromGit(string resolved) =>
        resolved.StartsWith("git", StringComparison.OrdinalIgnoreCase) || resolved.StartsWith("github:", StringComparison.OrdinalIgnoreCase)
        || resolved.StartsWith("gitlab:", StringComparison.OrdinalIgnoreCase) || resolved.StartsWith("bitbucket:", StringComparison.OrdinalIgnoreCase)
        || resolved.Contains(".git#", StringComparison.OrdinalIgnoreCase) || resolved.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
}
