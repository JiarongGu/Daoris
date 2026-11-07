namespace Daoris.Driver;

/// <summary>
/// The work's and the clean-up's halves of <see cref="HelpFacts"/>: each repository's part of the work and of
/// the clean-up's list as the service's answers name it, what moves a part's judgement, its judge and its reading, and the merged
/// reads the room and the slices take. It holds no state; the topic names and the parts' order live on <see cref="HelpFacts"/>.
/// </summary>
internal static class HelpWorkFacts
{
    /// <summary>
    /// What <paramref name="subject"/>'s part of the work reads from the service's answers: a repository's, its own
    /// checkout; the rest's, which repositories have one. Each with the trees the sessions still running or waiting hold, as
    /// <c>/api/sessions</c> answered them. Null for a repository not named with a checkout. With the sessions' look, the trees its
    /// repositories' sessions recorded, each of which joins its session.
    /// </summary>
    public static HelpWorkPart? PartOf(HelpMachineWhole whole, string subject, IKnowledgeSessionLook? look = null)
    {
        var checkouts = HelpFacts.Checkouts(whole);
        var inUse = whole.Snapshot.Active.Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HelpWorkPart? part = null;
        if (subject == HelpFacts.NoCheckout)
        {
            part = new HelpWorkPart(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), checkouts, inUse);
        }
        else if (whole.Snapshot.Repositories.FirstOrDefault(known => string.Equals(known.Repository, subject, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(known.Root)) is { } checkout)
        {
            var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [checkout.Repository] = checkout.Root! };
            part = new HelpWorkPart(checkout.Repository, roots, checkouts, inUse);
        }

        return part is not null && look is not null
            ? part with { Recorded = KnowledgeWorkOrders.Current.RecordedOf(look.Records.Where(record => part.Holds(record.Repository))) }
            : part;
    }

    /// <summary>
    /// What of the service's answers moves <paramref name="part"/>'s judgement: its checkout, or for the rest which repositories have
    /// one, and the trees its repositories' sessions hold. A session letting its tree go lands in the answers after the ledger's wake
    /// woke the work, so the part read the trees in use as they were; their landing judges it again, and no other.
    /// </summary>
    public static string Basis(HelpMachineWhole whole, string part)
    {
        var checkouts = HelpFacts.Checkouts(whole);
        bool Ours(string repository) => part == HelpFacts.NoCheckout
            ? !checkouts.Contains(repository)
            : string.Equals(repository, part, StringComparison.OrdinalIgnoreCase);
        var reads = part == HelpFacts.NoCheckout
            ? checkouts.Order(StringComparer.OrdinalIgnoreCase)
            : whole.Snapshot.Repositories.Where(known => Ours(known.Repository) && !string.IsNullOrWhiteSpace(known.Root)).Select(known => known.Root!).Take(1);
        var held = whole.Snapshot.Active.Where(session => Ours(session.Repository)).Select(session => session.Tree).OfType<string>()
            .Where(tree => tree.Length > 0).Order(StringComparer.OrdinalIgnoreCase);
        return string.Join('\n', reads) + "\n\n" + string.Join('\n', held);
    }

    /// <summary>The trees <paramref name="part"/>'s sessions recorded in <paramref name="look"/>, as one string; none without a look.</summary>
    public static string RecordedBasis(HelpMachineWhole whole, IKnowledgeSessionLook? look, string part) =>
        look is not null && PartOf(whole, part, look) is { } of ? string.Join('\n', of.Recorded.Select(each => $"{each.Session}\t{each.Tree}")) : "";

    /// <summary>
    /// One part of the work through <paramref name="work"/>, from what the service's answers say of it (<see cref="PartOf"/>): null
    /// before the answers, or where they no longer name the repository with a checkout here, so it keeps no fact.
    /// </summary>
    public static async Task<object?> JudgeAsync(
        MachineFacts facts, string subject, Func<HelpWorkPart, CancellationToken, Task<HelpWork?>> work, CancellationToken ct)
    {
        var current = facts.Current;
        if (current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is not { } whole
            || PartOf(whole, subject, current.Value<IKnowledgeSessionLook>(HelpFacts.Sessions, HelpFacts.Whole)) is not { } part)
        {
            return null;
        }

        return await work(part, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One part of the work read from <paramref name="home"/>, as the module's judge reads it: the trees, the records
    /// and the checkout's refs of the part's repositories alone, read-only, as `work state` reads the whole; each tree joined to the
    /// session that recorded it.
    /// </summary>
    public static async Task<HelpWork> ReadAsync(string home, HelpWorkPart part, CancellationToken ct)
    {
        return await KnowledgeWorkOrders.Current.ReadWorkAsync(home, part, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What <paramref name="subject"/>'s part of the clean-up's list reads from the service's answers: the work's reading of
    /// its checkout and the trees in use (<see cref="PartOf"/>), and the workspace its line is read under. Null for the rest's part and
    /// for a repository not named with a checkout here.
    /// </summary>
    public static HelpSweepPart? SweepPartOf(HelpMachineWhole whole, string subject)
    {
        if (subject == HelpFacts.NoCheckout || PartOf(whole, subject) is not { Repository: { } repository } part) return null;
        var row = whole.Snapshot.Repositories.First(known => string.Equals(known.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(known.Root));
        return new HelpSweepPart(repository, row.Workspace, part.Roots[repository], part.InUse);
    }

    /// <summary>
    /// What of the service's answers moves <paramref name="part"/>'s part of the clean-up's list: the work's reading (<see cref="Basis"/>)
    /// and its workspace. Their landing moved judges it again, and no other.
    /// </summary>
    public static string SweepBasis(HelpMachineWhole whole, string part) =>
        Basis(whole, part) + "\n\n" + (SweepPartOf(whole, part)?.Workspace ?? "");

    /// <summary>
    /// One part of the clean-up's list through <paramref name="sweep"/>, from what the service's answers say of it
    /// (<see cref="SweepPartOf"/>): null before the answers, or where they no longer name the repository with a checkout here.
    /// </summary>
    public static async Task<object?> JudgeSweepAsync(
        MachineFacts facts, string subject, Func<HelpSweepPart, CancellationToken, Task<IKnowledgeSweep?>> sweep, CancellationToken ct)
    {
        if (facts.Current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is not { } whole || SweepPartOf(whole, subject) is not { } part) return null;
        return await sweep(part, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One part of the clean-up's list read from <paramref name="home"/>, as the module's judge reads it: its repository's
    /// session branches and landings' branches judged by the answers the landing record keeps (<see cref="SessionTrees.SweepPartAsync"/>),
    /// asking no plugin and changing nothing.
    /// </summary>
    public static Task<IKnowledgeSweep> ReadSweepAsync(string home, HelpSweepPart part, CancellationToken ct) =>
        KnowledgeWorkplace.Current.ReadSweepAsync(home, part, ct);

    /// <summary>
    /// The work as the room reads it, merged from the parts the service's answers name, in their order: by repository,
    /// each part's own order kept, the rest's last. Null while any part is never judged, which the room says not read yet, as it
    /// said the whole: a part alone would read as all the work there is.
    /// </summary>
    public static HelpWork? Merged(MachineFactsSnapshot facts, HelpMachineWhole whole)
    {
        var parts = new List<HelpWork>();
        foreach (var (_, part) in Parts(facts, whole))
        {
            if (part is null) return null;
            parts.Add(part);
        }

        return new HelpWork(
            [.. parts.SelectMany(part => part.Units)], [.. parts.SelectMany(part => part.Unread)], [.. parts.SelectMany(part => part.Recovery)]);
    }

    /// <summary>
    /// The work's parts the service's answers name, as a slice pushes them: each by its subject in the merge's order
    /// (each repository with a checkout here by name, <see cref="HelpFacts.NoCheckout"/> last), with its part as last judged, or null
    /// where it never is, so a part never judged differs from one judged empty. Null where the answers were never judged.
    /// </summary>
    public static IReadOnlyList<(string Part, HelpWork? Work)>? Parts(MachineFactsSnapshot facts) =>
        facts.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole ? Parts(facts, whole) : null;

    private static IReadOnlyList<(string Part, HelpWork? Work)> Parts(MachineFactsSnapshot facts, HelpMachineWhole whole) =>
        [.. HelpFacts.Parts(whole).Select(subject => (subject, facts.Value<HelpWork>(HelpFacts.Work, subject)))];

    /// <summary>
    /// The clean-up's list's parts the service's answers name, as a slice pushes them: each repository with a checkout
    /// here by name, with its plan as last judged, or null where it never is, so a part never judged differs from one judged empty.
    /// Null where the service's answers were never judged.
    /// </summary>
    public static IReadOnlyList<(string Part, IKnowledgeSweep? Plan)>? SweepParts(MachineFactsSnapshot facts) =>
        facts.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole
            ? [.. HelpFacts.SweepSubjects(whole).Select(subject => (subject, facts.Value<IKnowledgeSweep>(HelpFacts.Sweep, subject)))]
            : null;
}
