namespace Daoris.Driver;

/// <summary>A wake the landing hears (FABRIC-END7): the topic and subject that woke a look, never a value and never a path.</summary>
public sealed record LandingWake(string Topic, string Subject);

/// <summary>
/// What a due entry waits on (FABRIC-END7), read from its last try alone, so a wake finds the entries it can release without git.
/// </summary>
public static class LandingWaits
{
    /// <summary>No try yet: the first wake after it became due looks at it.</summary>
    public const string First = "first";

    /// <summary>
    /// A refusal kept with its tree's facts (<see cref="AutoLandingCode.OnChange"/>): its tree's tip moving releases it, which any wake
    /// can find from git's files with no process (<see cref="TreeTips"/>). A status that moves alone is left to the whole look.
    /// </summary>
    public const string Tree = "tree";

    /// <summary>Held for the person's yes or evidence's verdict, or for the review: the ledger's <c>quests</c> family carries each release.</summary>
    public const string Quests = "quests";

    /// <summary>
    /// A second opinion: any ledger family (the reviewer's session moving and ending), or the gate's files (the person's answer kept
    /// at the gate, <see cref="OpinionGates"/>).
    /// </summary>
    public const string Opinion = "opinion";

    /// <summary>
    /// A named workflow's hold, kept as a refusal with no tip: the ledger's <c>quests</c> (the run's chain), or the gate's files (a
    /// version saved, a run's binding with the person's say-so).
    /// </summary>
    public const string Workflow = "workflow";

    /// <summary>Anything else: the whole look alone, the deadline.</summary>
    public const string Look = "look";

    /// <summary>What <paramref name="entry"/> waits on, by its last try.</summary>
    public static string Of(AutoLanding entry) => entry.Last switch
    {
        null => First,
        { Code: AutoLandingCode.Refused, Tip: null, Status: null } => Workflow,
        { Code: var code, Tip: not null } when AutoLandingCode.OnChange(code) => Tree,
        { Code: AutoLandingCode.Held or AutoLandingCode.Unreviewed } => Quests,
        { Code: AutoLandingCode.Opinion } => Opinion,
        _ => Look,
    };
}

/// <summary>
/// Which due entries a wake can concern (FABRIC-END7, the fabric events design §A3 and §B4): every release of
/// <see cref="AutoLander.ChooseAsync"/>'s holds has a wake, and a wake looks only at the entries its topic can release, found from the
/// due list and git's files with no process. The whole look, at <c>pollSeconds</c>, stays every entry's deadline.
/// </summary>
/// <remarks>
/// <para><b>The release paths and their wakes.</b> A quest's hold released, and the review's verdict or skip, move the ledger's
/// <c>quests</c> family (FABRIC-EVENT3b). A second opinion settles with its reviewer's session, a ledger family or the loop's own
/// session end, and the person's answer is kept in the gate's files. A workflow's hold is released by a declaration (the
/// configuration, or a version in the gate's files), the run's chain (<c>quests</c>) or the person's say-so on its binding (the gate's
/// files). A tree's tip moving is the <c>refs</c> topic (FABRIC-END3, <see cref="Moved"/>). The configuration (FABRIC-EVENT4) can
/// change the switch and every gate's rule, so it concerns every entry. The person's press nudges, and a plain nudge is the whole look.</para>
///
/// <para><b>A landing's own moves wake nothing.</b> Its branch moves the repository's refs but no due tree's tip, so a refs wake it
/// causes concerns no entry; its due list, landing record and conversation note are files no watch hears.</para>
/// </remarks>
public static class LandingConcerns
{
    /// <summary>
    /// Whether a look woken by <paramref name="woke"/> looks at <paramref name="entry"/>: always for the whole look (null) and the
    /// configuration, otherwise where one wake can release what the entry waits on (<see cref="LandingWaits.Of"/>).
    /// </summary>
    /// <param name="tipOf">A tree's tip as git's files say it, or null where they cannot (<see cref="TreeTips.Read"/>).</param>
    public static bool Concerns(AutoLanding entry, IReadOnlyCollection<LandingWake>? woke, Func<string, string?> tipOf)
    {
        if (woke is null) return true;
        var waits = LandingWaits.Of(entry);
        if (waits == LandingWaits.First || woke.Any(wake => wake.Topic == ChangeTopics.Config)) return true;
        return waits switch
        {
            LandingWaits.Tree => TipMoved(entry, tipOf),
            LandingWaits.Quests => woke.Any(Quests),
            LandingWaits.Opinion => woke.Any(wake => wake.Topic is ChangeTopics.Ledger or ChangeTopics.Gates),
            LandingWaits.Workflow => woke.Any(wake => Quests(wake) || wake.Topic == ChangeTopics.Gates),
            _ => false,
        };
    }

    /// <summary>Whether the tree's tip moved since the try kept it: a tip git's files cannot say is git's to read, so it is looked at.</summary>
    private static bool TipMoved(AutoLanding entry, Func<string, string?> tipOf) =>
        tipOf(entry.Tree) is not { } tip || !string.Equals(tip, entry.Last?.Tip, StringComparison.OrdinalIgnoreCase);

    /// <summary>The ledger's <c>quests</c> family among the families a wake names (<c>LedgerChanges</c> joins them with commas).</summary>
    private static bool Quests(LandingWake wake) =>
        wake.Topic == ChangeTopics.Ledger && wake.Subject.Split(',', StringSplitOptions.TrimEntries).Contains("quests", StringComparer.Ordinal);
}

/// <summary>
/// A tree's tip read from git's own files, with no process (FABRIC-END7): the <c>HEAD</c> of its git directory, and the branch it names
/// as a loose ref or a line of <c>packed-refs</c> in the common directory. Null wherever the files cannot say it (an unborn branch, the
/// reftable format, a file being replaced), so the caller asks git.
/// </summary>
public static class TreeTips
{
    /// <summary>The commit <paramref name="tree"/>'s <c>HEAD</c> stands at, lower case, or null.</summary>
    public static string? Read(string tree)
    {
        try
        {
            if (GitBranches.GitDirectory(tree) is not { } own) return null;
            var head = File.ReadAllText(Path.Combine(own, "HEAD")).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal)) return Commit(head);

            var name = head["ref:".Length..].Trim();
            var common = Common(own);
            if (!name.StartsWith("refs/", StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal)
                || Directory.Exists(Path.Combine(common, "reftable")))
            {
                return null;
            }

            var loose = Path.Combine(common, name.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(loose)) return Commit(File.ReadAllText(loose).Trim());

            var packed = Path.Combine(common, "packed-refs");
            if (!File.Exists(packed)) return null;
            foreach (var line in File.ReadLines(packed))
            {
                var space = line.IndexOf(' ', StringComparison.Ordinal);
                if (space > 0 && string.Equals(line[(space + 1)..].TrimEnd(), name, StringComparison.Ordinal)) return Commit(line[..space]);
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? Commit(string text) => EvidenceCodes.IsObjectId(text) ? text.ToLowerInvariant() : null;

    /// <summary>A linked tree's git directory names the repository's in <c>commondir</c>; any other is its own.</summary>
    private static string Common(string own)
    {
        var file = Path.Combine(own, "commondir");
        if (!File.Exists(file)) return own;
        var named = File.ReadAllText(file).Trim();
        return Path.GetFullPath(Path.IsPathRooted(named) ? named : Path.Combine(own, named));
    }
}

/// <summary>
/// The wakes the loop heard since its last look (FABRIC-END7), which the next look's landing looks at alone: kept from each topic's
/// nudge, taken by the look, so a wake heard while a look runs is the next look's.
/// </summary>
public sealed class LandingWakes
{
    /// <summary>A plan no wake asked for: the loop's own session end, which the ledger's <c>sessions</c> family carries too.</summary>
    private static readonly LandingWake Ended = new(ChangeTopics.Ledger, "sessions");

    private readonly object _gate = new();
    private List<LandingWake> _heard = [];

    /// <summary>A topic's wake, kept once until a look takes it.</summary>
    public void Hear(string topic, string subject)
    {
        var wake = new LandingWake(topic, subject);
        lock (_gate)
        {
            if (!_heard.Contains(wake)) _heard.Add(wake);
        }
    }

    /// <summary>
    /// What a look lands on: every due entry (null) for the <paramref name="whole"/> look; the wakes heard since the last look for a
    /// plan, or a session's end where none was. Either way the wakes are taken.
    /// </summary>
    public IReadOnlyCollection<LandingWake>? For(bool whole)
    {
        List<LandingWake> taken;
        lock (_gate)
        {
            taken = _heard;
            _heard = [];
        }

        if (whole) return null;
        return taken.Count == 0 ? [Ended] : taken;
    }

    /// <summary>
    /// The ref watch judged <paramref name="repository"/> (FABRIC-END3, <see cref="RefWatch.Heard"/>): its <c>refs</c> wake asked of
    /// <paramref name="wake"/> only where a due entry of that repository is concerned (<see cref="LandingConcerns"/>), read from the
    /// due list and git's files, so a ref move that releases nothing starts no look and no git. Never throws.
    /// </summary>
    public static void Moved(string home, string repository, Action<string, string> wake)
    {
        try
        {
            LandingWake[] heard = [new(ChangeTopics.Refs, repository)];
            var concerned = new AutoLandings(home).Open().Any(entry =>
                string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && LandingConcerns.Concerns(entry, heard, TreeTips.Read));
            if (concerned) wake(ChangeTopics.Refs, repository);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Unread: the whole look finds what this missed, at its deadline.
        }
    }
}
