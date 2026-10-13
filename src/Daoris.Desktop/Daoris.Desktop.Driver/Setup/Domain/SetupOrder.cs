namespace Daoris.Driver;

/// <summary>
/// The order a workspace plan works its repositories in (WSSETUP6, D124 §4.2): what other work touches first, from the facts
/// this machine keeps, highest first, then by name.
/// </summary>
/// <remarks>
/// <para><b>Three facts, in this order</b>: quests addressed to it by anyone but itself (another repository, an ask, which
/// the intake or the person routed, or the person); reads into its checkout by other repositories' sessions, from the
/// conversation records' tool calls and the files they touched (D76); and sessions run in it. A set-up is not work asked of
/// it, and a session reading its own repository is not another's read. Value lands first that way, and a plan stopped at
/// any point has set up what other work needed most (§4.2).</para>
///
/// <para><b>An ask's quest counts whichever tier routed it</b>: the quest says only that an ask sent it (<c>ask #id</c>),
/// not whether the intake or the person chose the receiver, and either way it is work another asked of the repository.</para>
///
/// <para><b>Paths as Windows sees them</b>, as the planner compares trees: separators and case aside. A path that is not
/// rooted names no checkout, and the deepest root owns a path, so a repository held inside another is its own.</para>
/// </remarks>
public static class SetupOrder
{
    /// <summary>What touched each of <paramref name="repositories"/>, by name.</summary>
    /// <param name="rows">Every registry row with its root, in any workspace: what a touched file is owned by.</param>
    /// <param name="touched">The files a session's tool calls touched, by the session's id.</param>
    public static IReadOnlyDictionary<string, SetupTouches> Count(
        IReadOnlyCollection<string> repositories, IReadOnlyList<RegistrationRow> rows, IReadOnlyList<QuestView> quests,
        IReadOnlyList<SessionRun> runs, Func<string, IReadOnlyList<ToolTouch>> touched)
    {
        var names = new HashSet<string>(repositories, StringComparer.OrdinalIgnoreCase);

        var asked = quests
            .Where(quest => names.Contains(quest.To) && !Same(quest.From, quest.To) && !SetupQuests.IsSetup(quest.Title))
            .GroupBy(quest => quest.To, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var sessions = runs
            .Where(run => names.Contains(run.Repository))
            .GroupBy(run => run.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        // The deepest root first, so a repository held inside another owns its own files.
        var roots = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .Select(row => (row.Repository, Root: Normal(row.Root!)))
            .Where(row => Rooted(row.Root))
            .OrderByDescending(row => row.Root.Length)
            .ToList();

        var read = new Dictionary<string, HashSet<(string Session, string Call)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            foreach (var touch in touched(run.Session))
            {
                var path = Normal(touch.Path);
                if (!Rooted(path)) continue;

                var owner = roots.FirstOrDefault(root =>
                    string.Equals(path, root.Root, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(root.Root + "/", StringComparison.OrdinalIgnoreCase)).Repository;
                if (owner is null || Same(owner, run.Repository) || !names.Contains(owner)) continue;

                if (!read.TryGetValue(owner, out var calls)) read[owner] = calls = [];
                calls.Add((run.Session, touch.Call));
            }
        }

        return names.ToDictionary(
            name => name,
            name => new SetupTouches(
                asked.GetValueOrDefault(name), read.TryGetValue(name, out var calls) ? calls.Count : 0, sessions.GetValueOrDefault(name)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The order: the ones the person named to go first, in the order they named them, then the rest by what touched them,
    /// highest first, then by name; the unticked left out.
    /// </summary>
    public static IReadOnlyList<string> Order(
        IReadOnlyList<string> repositories, IReadOnlyDictionary<string, SetupTouches> touches, IReadOnlyList<string> first,
        IReadOnlyList<string> skip)
    {
        var left = new HashSet<string>(skip, StringComparer.OrdinalIgnoreCase);
        var firsts = first
            .Select(named => repositories.FirstOrDefault(repository => Same(repository, named)))
            .OfType<string>()
            .Where(repository => !left.Contains(repository))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SetupTouches Of(string repository) => touches.GetValueOrDefault(repository) ?? SetupTouches.None;
        var rest = repositories
            .Where(repository => !left.Contains(repository) && !firsts.Contains(repository, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(repository => Of(repository).Asked)
            .ThenByDescending(repository => Of(repository).Read)
            .ThenByDescending(repository => Of(repository).Sessions)
            .ThenBy(repository => repository, StringComparer.OrdinalIgnoreCase);

        return [.. firsts, .. rest];
    }

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>One spelling of a path: forward slashes, no trailing one.</summary>
    private static string Normal(string path) => path.Trim().Replace('\\', '/').TrimEnd('/');

    /// <summary>Rooted on either platform: <c>/…</c>, or a drive's <c>C:/…</c>, read alike wherever the suite runs.</summary>
    private static bool Rooted(string path) =>
        path.StartsWith('/') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/');
}
