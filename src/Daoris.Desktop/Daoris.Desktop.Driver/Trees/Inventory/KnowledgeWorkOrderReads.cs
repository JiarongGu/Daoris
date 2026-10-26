using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class KnowledgeWorkOrderReads : IKnowledgeWorkOrders
{
    [ModuleInitializer]
    internal static void Install() => KnowledgeWorkOrders.Current = new KnowledgeWorkOrderReads();

    public IReadOnlyList<string> TreesToJudge(IKnowledgeSessionLook look, Func<string, bool> held, IReadOnlyCollection<string>? only = null) =>
        SessionGroups.TreesToJudge((SessionLook)look, held, only);

    public string Normal(string tree) => SessionGroups.Normal(tree);

    public async Task<IKnowledgeSessionLook> JudgeAsync(IKnowledgeSessionLook look, Func<string, bool> held,
        Func<string, CancellationToken, Task<TreeWork?>> judge, IReadOnlyCollection<string>? only, CancellationToken ct, int atOnce) =>
        await SessionGroups.JudgeAsync((SessionLook)look, held, judge, only, ct, atOnce).ConfigureAwait(false);

    public IReadOnlyList<RecordedTree> RecordedOf(IEnumerable<SessionRecord> records) => WorkInventories.RecordedOf(records);

    public async Task<HelpWork> ReadWorkAsync(string home, HelpWorkPart part, CancellationToken ct)
    {
        var (units, recovery, unread) = await WorkInventories.ReadPartAsync(home, part.Roots, part.InUse, part.Holds, ct, part.Recorded).ConfigureAwait(false);
        return new HelpWork(units, unread, recovery);
    }

    public bool WaitsOnYou(string id, string state, string? answer) => SessionGroups.WaitsOnYou(id, state, answer);

    public IEnumerable<string> Milestone(IKnowledgeMilestone unit) => WorkWords.Milestone(unit);

    public HelpSessions Placed(IKnowledgeSessionLook source)
    {
        var look = (SessionLook)source;
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = SessionGroups.Read(look)
            .Where(row => !row.Teammate && records.TryGetValue(row.Session, out var record) && record.Repository != KnowledgeRoomIdentity.Repository)
            .ToList();
        int In(string group) => rows.Count(row => row.Group == group);
        var ended = rows.Where(row => row.Group == SessionGroup.Ended).ToList();
        return new HelpSessions(
            In(SessionGroup.You), In(SessionGroup.Review), In(SessionGroup.Working), In(SessionGroup.Later), ended.Count,
            [.. ended.Take(HelpRoomMachineNow.EndedListed).Select(row => new HelpEndedSession(row.Session, records[row.Session].Repository))]);
    }
}
