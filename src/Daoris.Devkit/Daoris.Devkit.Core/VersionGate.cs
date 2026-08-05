using System.Text.RegularExpressions;

namespace Daoris.Devkit;

/// <summary>
/// One file owns the version; everything that restates it must agree.
/// </summary>
/// <remarks>
/// <para>The failure this exists for is not inconsistency — it is <b>authorship</b>. A version edited by
/// hand leaves every file perfectly consistent and still wrong, because the number no longer corresponds
/// to anything that was released. A sibling in this family burned a version outright that way, and the
/// tell was that nothing disagreed.</para>
///
/// <para>So this gate does two different jobs. It reports disagreement between the source and its
/// mirrors, which is the cheap half. And it reports a version that does not appear in the changelog,
/// which is the half that catches a hand-bump: the release tooling stamps both together, so a version
/// with no entry was written by a person.</para>
/// </remarks>
public sealed class VersionGate : IGate
{
    public string Name => "version";

    public GateResult Run(GateContext context)
    {
        var options = context.Declaration.Version;
        if (options.Source is null)
        {
            return GateResult.Skip(Name, "no 'version.source' declared — nothing owns a version here");
        }

        var source = context.Path(options.Source);
        if (!File.Exists(source)) return GateResult.Fail(Name, $"version source '{options.Source}' does not exist");

        var pattern = options.Pattern ?? DefaultPatternFor(options.Source);
        if (pattern is null)
        {
            return GateResult.Fail(Name,
                $"no 'version.pattern' declared and '{options.Source}' is not a file shape with a known one "
                + "(.props, package.json, .csproj) — declare a regex with one capturing group");
        }

        var version = Capture(File.ReadAllText(source), pattern);
        if (version is null)
        {
            return GateResult.Fail(Name, $"'{options.Source}' does not match the version pattern /{pattern}/");
        }

        var problems = new List<string>();
        foreach (var mirror in options.Mirrors ?? [])
        {
            var file = context.Path(mirror);
            if (!File.Exists(file))
            {
                problems.Add($"{mirror}: declared as a version mirror but does not exist");
                continue;
            }

            var mirrored = Capture(File.ReadAllText(file), DefaultPatternFor(mirror) ?? pattern);
            if (mirrored is null) problems.Add($"{mirror}: no version found");
            else if (mirrored != version) problems.Add($"{mirror}: says {mirrored}, source says {version}");
        }

        // The authorship half. Only meaningful once a changelog is declared, and deliberately silent
        // about pre-release versions — 0.0.x is development, where the version is not yet a claim.
        var changelog = context.Declaration.Docs.Changelog;
        if (changelog is not null && !IsPreRelease(version))
        {
            var file = context.Path(changelog);
            if (!File.Exists(file)) problems.Add($"{changelog}: declared as the changelog but does not exist");
            else if (!File.ReadAllText(file).Contains(version, StringComparison.Ordinal))
            {
                problems.Add(
                    $"{changelog} has no entry for {version}. The release tooling stamps the version and its "
                    + "heading together, so a version with no entry was written by hand — which is the failure "
                    + "this gate exists for, not the consistency ones above.");
            }
        }

        return problems.Count == 0
            ? GateResult.Pass(Name, $"{version}, consistent across {(options.Mirrors?.Count ?? 0) + 1} file(s)")
            : GateResult.Fail(Name, string.Join('\n', problems));
    }

    /// <summary>0.x and anything with a pre-release suffix. Development, where a version is not a claim yet.</summary>
    private static bool IsPreRelease(string version) =>
        version.StartsWith("0.", StringComparison.Ordinal) || version.Contains('-', StringComparison.Ordinal);

    /// <summary>
    /// The shapes that are the same in every repository, so the common case declares no pattern at all.
    /// </summary>
    private static string? DefaultPatternFor(string path) => path switch
    {
        var p when p.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                || p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            => @"<Version(?:Prefix)?>([^<]+)</Version(?:Prefix)?>",
        var p when p.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            => @"""version""\s*:\s*""([^""]+)""",
        _ => null,
    };

    private static string? Capture(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}
