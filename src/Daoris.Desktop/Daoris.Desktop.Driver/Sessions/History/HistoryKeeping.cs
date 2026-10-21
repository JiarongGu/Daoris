namespace Daoris.Driver;

/// <summary>
/// Finished history by an age the person sets (KEEP1g, D195 point 7, decided by the owner; the cleanup design §3.6): once a day, each
/// workspace's finished units whose last move is older than the person's days go through the clear itself, every refusal holding
/// (the history clearing design §1.2). With no age set, the default, it runs nothing: nothing clears finished history by itself unless the
/// person has set an age (the history clearing design §7 as D195 amends it).
/// </summary>
/// <remarks>
/// <para><b>The clear's own two presses</b>: <see cref="HistoryClearing.PlanAsync"/> lists each workspace, and
/// <see cref="HistoryClearing.ClearAsync"/> takes exactly the units past their days that it listed as clearable, judging each again by
/// both halves; its log line says <c>door: rule</c>. A unit past its days that a refusal keeps stays, and is said with its word.</para>
///
/// <para><b>A unit's last move</b> is the newest of its quests' last status moves and its sessions' last moves, as the service answers
/// them. One whose moment is not known (a host that answers none, a record not read) is not past its days: the unknown keeps (the
/// cleanup design §6).</para>
/// </remarks>
public static class HistoryKeeping
{
    /// <summary>The door the look's daily clear names in <c>history.cleared</c> and in <c>keeping.json</c>'s <c>last</c>.</summary>
    public const string Door = "rule";

    /// <summary>
    /// One daily clear at <paramref name="policy"/>'s age: every workspace with finished work listed, what is past its days and may go
    /// cleared, what is past its days and stays said. Nothing at all where no age is set.
    /// </summary>
    /// <exception cref="DriverException">The service did not answer, or has no history door; what was cleared before it stays cleared.</exception>
    public static async Task<HistoryKeepingPass> PassAsync(HistoryWorld world, HistoryPolicy policy, CancellationToken ct = default)
    {
        if (policy.Days is not { } days) return HistoryKeepingPass.None;
        var before = world.Clock() - days;
        var facts = await HistoryClearing.Facts.ReadAsync(world.Service, ct).ConfigureAwait(false);
        var workspaces = facts.Quests.Select(quest => quest.Workspace)
            .Concat(facts.Asks.Select(ask => ask.Workspace))
            .Select(RemoteTarget.Workspace)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var outcomes = new List<HistoryOutcome>();
        var kept = new List<HistoryUnitPlan>();
        foreach (var workspace in workspaces)
        {
            ct.ThrowIfCancellationRequested();
            var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, workspace, ct).ConfigureAwait(false);
            var due = plan.Units.Where(unit => LastMove(unit, facts) is { } moved && moved <= before).ToList();
            kept.AddRange(due.Where(unit => !unit.Clearable));
            var going = due.Where(unit => unit.Clearable).Select(unit => unit.Name).ToList();
            if (going.Count == 0) continue;
            var outcome = await HistoryClearing.ClearAsync(world, HistoryScope.Workspace, workspace, going, Door, ct).ConfigureAwait(false);
            outcomes.Add(outcome);
            kept.AddRange(outcome.Changed);
        }

        return new HistoryKeepingPass(outcomes, kept);
    }

    /// <summary>
    /// When <paramref name="unit"/> last moved: the newest of its quests' and its sessions' moments; null where one of them, or all of
    /// them, is not known, which keeps it.
    /// </summary>
    internal static DateTimeOffset? LastMove(HistoryUnitPlan unit, HistoryClearing.Facts facts)
    {
        DateTimeOffset? last = null;
        foreach (var id in unit.Quests)
        {
            if (facts.Quest(id)?.Updated is not { } moved) return null;
            if (last is null || moved > last) last = moved;
        }

        foreach (var id in unit.Sessions)
        {
            if (facts.Record(id) is not { } record || record.Updated == default) return null;
            if (last is null || record.Updated > last) last = record.Updated;
        }

        return last;
    }
}

/// <summary>What one daily clear did: each workspace's clear that took something, and each unit past its days that stayed, with its word.</summary>
public sealed record HistoryKeepingPass(IReadOnlyList<HistoryOutcome> Outcomes, IReadOnlyList<HistoryUnitPlan> Kept)
{
    public static HistoryKeepingPass None { get; } = new([], []);

    /// <summary>The units cleared, every workspace's.</summary>
    public int Cleared => Outcomes.Sum(outcome => outcome.Cleared.Count);

    /// <summary>What the home freed, in bytes.</summary>
    public long Bytes => Outcomes.Sum(outcome => outcome.Bytes);

    /// <summary>The units past their days that stayed, by the word that keeps each.</summary>
    public IReadOnlyDictionary<string, int> KeptBy =>
        Kept.GroupBy(unit => unit.Keep?.Word ?? HistoryWords.Unknown, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}
