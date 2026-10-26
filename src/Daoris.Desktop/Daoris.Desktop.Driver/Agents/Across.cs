namespace Daoris.Driver;

/// <summary>
/// What a session in one repository may reach outside its own tree (D107): the checkouts it may read, the
/// ones it may also write into, and every other checkout here, which it may not read.
/// </summary>
/// <param name="Reads">Every checkout it may read, the declared write targets among them.</param>
/// <param name="Writes">The declared write targets with a checkout here, in its workspace.</param>
/// <param name="Unread">Every other registered checkout here: switched off, or in another workspace.</param>
public sealed record AcrossReach(
    IReadOnlyList<AcrossCheckout> Reads, IReadOnlyList<AcrossCheckout> Writes, IReadOnlyList<AcrossCheckout> Unread)
{
    public static AcrossReach None { get; } = new([], [], []);

    /// <summary>The checkouts it may read and not write: what the instruction's reading paragraph lists.</summary>
    public IReadOnlyList<AcrossCheckout> ReadOnly =>
        [.. Reads.Where(read => !Writes.Any(write => string.Equals(write.Repository, read.Repository, StringComparison.OrdinalIgnoreCase)))];
}

/// <summary>
/// Reading and writing across repositories (READ1, D107): the setting in <c>driver.json</c> resolved, and the
/// harness rules it becomes.
/// </summary>
/// <remarks>
/// <para><b>Reading belongs to the checkout that is read.</b> A repository's own value wins, then its
/// workspace's, then on. It is the checkout's rather than the reader's because the reason to switch it off is
/// a repository whose code should stay its own, and because Ask Daoris belongs to no repository.</para>
///
/// <para><b>Within one workspace</b> (D48): a session reads its own workspace's checkouts, and a declared
/// relationship reaches only into its workspace. Ask Daoris's room for <c>default</c> is the whole machine's and reads every
/// readable one; a workspace's room reads its own workspace's (ASKSCOPE1c, D158's note).</para>
///
/// <para><b>Only the driver resolves.</b> <c>daoris-driver driver across</c> edits and lists the same keys (the CLI's
/// <c>driverconfig.ts</c> did until PUSH1f2b), as it does a landing rule the driver alone chooses.</para>
/// </remarks>
public static class AcrossRules
{
    /// <summary>What may stand in a path for git without quotes; anything else is double-quoted.</summary>
    private const string Plain = "_./:@+,=-~%";

    /// <summary>Whether <paramref name="repository"/>'s checkout may be read across, and what said so (<see cref="AcrossReads.Reading"/>).</summary>
    /// <param name="workspace">Its circle; unstated is the default circle, as everywhere.</param>
    public static AcrossReading Reading(DriverConfig config, string repository, string? workspace) => AcrossReads.Reading(config, repository, workspace);

    /// <summary>The repositories <paramref name="repository"/>'s sessions were declared to write into, as written.</summary>
    public static IReadOnlyList<string> WritesTo(DriverConfig config, string repository) =>
        config.WriteAcross.GetValueOrDefault(repository.Trim()) ?? [];

    /// <summary>Why a relationship from one repository to another cannot be declared, or null when it can.</summary>
    /// <remarks>The CLI's <c>writeAcrossProblem</c> says the same two sentences.</remarks>
    public static string? Problem(string repository, string to) => AcrossDeclarations.Problem(repository, to);

    /// <summary>
    /// What a session in <paramref name="repository"/> may reach across, from the registry as the service
    /// answered it: every checkout here but its own repository's, sorted by name.
    /// </summary>
    public static AcrossReach Reach(DriverConfig config, IEnumerable<RepoView> registry, string repository, string? workspace)
    {
        var circle = Circle(workspace);
        var targets = WritesTo(config, repository);
        var others = Checkouts(registry)
            .Where(known => !string.Equals(known.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool Mine(RepoView known) => string.Equals(Circle(known.Workspace), circle, StringComparison.OrdinalIgnoreCase);
        bool Target(RepoView known) => targets.Contains(known.Repository, StringComparer.OrdinalIgnoreCase);

        var writes = others.Where(known => Mine(known) && Target(known)).ToList();
        var reads = others.Where(known => Mine(known) && (Target(known) || Reading(config, known.Repository, known.Workspace).Read)).ToList();
        var unread = others.Except(reads).ToList();

        return new(Of(reads), Of(writes), Of(unread));
    }

    /// <summary>The checkouts Ask Daoris may read (<see cref="AcrossReads.Readable"/>).</summary>
    public static IReadOnlyList<AcrossCheckout> Readable(DriverConfig config, IEnumerable<RepoView> registry) => AcrossReads.Readable(config, registry);

    /// <summary>
    /// The rules a session with <paramref name="reach"/> is handed beside the person's: a read and two read-only
    /// git commands for each checkout it may read, an edit and a commit in each declared target, and a refused
    /// edit in every other checkout, with a refused read in each it may not read.
    /// </summary>
    /// <param name="own">
    /// The session's own tree and the folder its kept files are in. 🔴 No refusal lands on a checkout that
    /// holds one of them, or a checkout the session may use: deny beats allow, so it would refuse the session
    /// exactly what it was given.
    /// </param>
    public static RuleLists Rules(AcrossReach reach, IEnumerable<string> own)
    {
        var mine = own.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        var allow = new List<string>();
        foreach (var checkout in reach.Reads) allow.AddRange(ReadOnlyRules(checkout));
        foreach (var checkout in reach.Writes)
        {
            var git = GitPath(checkout.Path);
            allow.AddRange([PermissionRules.EditRule(checkout.Path), $"Bash(git -C {git} add:*)", $"Bash(git -C {git} commit:*)"]);
        }

        bool Spared(AcrossCheckout checkout, IEnumerable<AcrossCheckout> used) =>
            mine.Any(path => Holds(checkout.Path, path)) || used.Any(other => Holds(checkout.Path, other.Path));

        var deny = new List<string>();
        foreach (var checkout in reach.ReadOnly.Where(checkout => !Spared(checkout, reach.Writes)))
        {
            deny.Add(PermissionRules.EditRule(checkout.Path));
        }

        foreach (var checkout in reach.Unread)
        {
            if (!Spared(checkout, reach.Writes)) deny.Add(PermissionRules.EditRule(checkout.Path));
            if (!Spared(checkout, reach.Reads)) deny.Add(PermissionRules.ReadRule(checkout.Path));
        }

        return new RuleLists([.. allow.Distinct(StringComparer.Ordinal)], [], [.. deny.Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// A checkout read and looked at, never written: its files, then <c>git status</c> and the branch list by
    /// exact prefix. Not <c>log</c>, <c>diff</c> or <c>show</c>: each takes <c>--output=&lt;file&gt;</c>, which
    /// writes wherever it names, and a prefix rule cannot refuse a flag appended to it.
    /// </summary>
    public static IReadOnlyList<string> ReadOnlyRules(AcrossCheckout checkout)
    {
        var git = GitPath(checkout.Path);
        return [PermissionRules.ReadRule(checkout.Path), $"Bash(git -C {git} status:*)", $"Bash(git -C {git} branch --list:*)"];
    }

    /// <summary>
    /// A checkout's path as a git command and its rule both spell it: forward slashes, which Git Bash,
    /// PowerShell and git read alike, and double quotes where a shell would split or read the path.
    /// </summary>
    public static string GitPath(string path)
    {
        var forward = path.Replace('\\', '/');
        if (forward.Length > 1) forward = forward.TrimEnd('/');
        return forward.All(c => char.IsLetterOrDigit(c) || Plain.Contains(c)) ? forward : $"\"{forward}\"";
    }

    /// <summary>Whether <paramref name="inner"/> is <paramref name="outer"/> or lies under it, on this machine's paths (<see cref="AcrossReads.Holds(string, string)"/>).</summary>
    public static bool Holds(string outer, string inner) => AcrossReads.Holds(outer, inner);

    /// <summary>The same question with the platform named, so the answer is the same on any machine that tests it.</summary>
    public static bool Holds(string outer, string inner, bool windows) => AcrossReads.Holds(outer, inner, windows);

    private static IEnumerable<RepoView> Checkouts(IEnumerable<RepoView> registry) => AcrossReads.Checkouts(registry);

    private static IReadOnlyList<AcrossCheckout> Of(IEnumerable<RepoView> known) => AcrossReads.Of(known);

    private static string Circle(string? workspace) => AcrossReads.Circle(workspace);
}
