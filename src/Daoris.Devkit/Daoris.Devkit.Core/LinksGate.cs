using System.Text.RegularExpressions;

namespace Daoris.Devkit;

/// <summary>
/// Every relative link between documents resolves to a file that exists.
/// </summary>
/// <remarks>
/// <para>The cheapest documentation check there is, and the one most worth having: a link that points at
/// a renamed or deleted file is *silently* wrong. Nothing compiles it, the page still renders, and the
/// reader concludes the thing it pointed at was never important.</para>
///
/// <para>It exists because a survey of the family found a hand-written "documentation health" skill in
/// two repositories doing four checks. Three of them had already become gates here — redundancy, index
/// staleness, version agreement. This was the fourth, and a gate that runs is worth more than a skill
/// somebody has to remember to invoke.</para>
///
/// <para><b>Only relative links to files.</b> External URLs need the network, which no gate here may
/// touch; a bare anchor is within the page; an absolute path is a machine path and is the sensitive
/// scan's problem, not this one.</para>
/// </remarks>
public sealed class LinksGate(IGit git) : IGate
{
    public string Name => "links";

    /// <summary>
    /// `[text](target)` — captured up to the first `#`, space, or closing paren.
    /// </summary>
    /// <remarks>
    /// The fragment is dropped rather than checked: verifying an anchor means parsing the target's
    /// headings and reproducing the renderer's slug rules, which differ between renderers. A wrong file
    /// is always a defect; a wrong anchor sometimes is not.
    /// </remarks>
    private static readonly Regex Link = new(@"\[[^\]]*\]\(([^)\s#]+)(?:#[^)\s]*)?\)", RegexOptions.Compiled);

    public GateResult Run(GateContext context)
    {
        var documents = git.TrackedFiles()
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (documents.Count == 0) return GateResult.Skip(Name, "no tracked markdown");

        var problems = new List<string>();
        var checked_ = 0;

        foreach (var document in documents)
        {
            var absolute = context.Path(document);
            if (!File.Exists(absolute)) continue;

            var directory = Path.GetDirectoryName(absolute)!;
            // Plain read: the pattern is line-ending agnostic, so normalizing would buy nothing here.
            foreach (Match match in Link.Matches(File.ReadAllText(absolute)))
            {
                var target = match.Groups[1].Value;
                if (!IsCheckable(target)) continue;

                checked_++;
                var resolved = Path.GetFullPath(Path.Combine(directory, target.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    problems.Add($"{document}: '{target}' does not exist");
                }
            }
        }

        return problems.Count == 0
            ? GateResult.Pass(Name, $"{checked_} link(s) across {documents.Count} document(s)")
            : GateResult.Fail(Name, string.Join('\n', problems));
    }

    /// <summary>A relative path to something on disk — not a URL, an anchor, or an absolute path.</summary>
    private static bool IsCheckable(string target)
    {
        if (target.Length == 0) return false;
        if (target.Contains("://", StringComparison.Ordinal)) return false;
        if (target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;
        if (target.StartsWith('/') || target.StartsWith('#')) return false;

        // A Windows drive letter is an absolute machine path; the sensitive scan reports it, and this
        // gate saying "does not exist" on top of that would be a second, less useful complaint.
        return !(target.Length > 1 && target[1] == ':');
    }
}
