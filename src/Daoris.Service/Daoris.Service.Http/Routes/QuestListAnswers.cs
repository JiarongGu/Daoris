using Daoris.Contracts.Quests;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The quest list as the door answers it: each quest with whether this host would delete it and how its takes stand. The driver's
/// readers and the window read the whole list again and again while nothing in it moves, so an answer is kept with the store's mark
/// and answered again until a write moves it.
/// </summary>
/// <remarks>
/// A kept file's path is read from the disk, which the mark does not see; its bytes arrive only with the publish that writes the
/// quest and go with the delete that removes it, so a path moves with the mark.
/// </remarks>
public sealed class QuestListAnswers(HostWiring host, QuestAnswers answers)
{
    private ComposedService Service => host.Service;

    private MarkedAnswers<(string? Repository, bool Closed, string? Workspace, bool Local), QuestResponse[]>? _kept;

    private MarkedAnswers<(string? Repository, bool Closed, string? Workspace, bool Local), QuestResponse[]> Kept =>
        LazyInitializer.EnsureInitialized(ref _kept, () => new(Service.Quests.MarkAsync))!;

    /// <param name="local">Whether the caller is on this machine, which a kept file's path and this machine's id answer only to.</param>
    public Task<QuestResponse[]> ListAsync(string? repository, bool includeClosed, string? workspace, bool local, CancellationToken ct) =>
        // Made apart from the asker's cancellation: readers at one mark share the making, and one that leaves stops none of the others.
        Kept.AnswerAsync((repository, includeClosed, workspace, local), () => MakeAsync(repository, includeClosed, workspace, local, CancellationToken.None), ct);

    private async Task<QuestResponse[]> MakeAsync(string? repository, bool includeClosed, string? workspace, bool local, CancellationToken ct)
    {
        var quests = await Service.Quests.ListAsync(repository, includeClosed, workspace, ct);
        var deletable = await answers.DeletableAsync(quests, ct);
        var takes = await Service.Quests.ReadTakesAsync(quests, ct);
        var here = local ? Service.Quests.Machine : null;
        return [.. quests.Select(q => QuestAnswers.ToQuest(q, Service.Files, local, deletable.Contains(q.Id), takes.GetValueOrDefault(q.Id), here))];
    }
}
