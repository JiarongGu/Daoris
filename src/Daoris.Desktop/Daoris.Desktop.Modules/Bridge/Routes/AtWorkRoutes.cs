using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>Who is at work on a session, the page's `bridge/sessions/atWork.ts`.</summary>
public sealed class AtWorkRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// Who is at work on a session: its entry's parts and the attended part's subagents, composed from
    /// the groups, sessions and quests slices in memory and each part's lineage file, this window's tokens read where it issued them, and
    /// what its line waits on and its co-workers from the home's helper, piece and hand-over files. Numbered,
    /// so a failure is answered with its words and an older delivery never replaces a newer one.
    /// </summary>
    [DriverRoute("SESSION_AT_WORK", PipeClass.Open)]
    [ByEvent("SESSION_AT_WORK", Numbered = true, Untold = "each part's lineage file, this window's lineage tokens and the helper, piece and hand-over files, which the read's own judge watches")]
    private async Task<object?> SessionAtWorkAsync(SessionAtWorkRead press, CancellationToken cancellationToken)
    {
        var ask = press.Ask;
        var session = ask is { Length: > 0 } ? "" : ActField.Needs(press.Session, "session");
        await Task.CompletedTask;
        var slices = _loop.Slices;
        AtWorkHeld Compose() => ask is { Length: > 0 }
            ? AtWorkRead.ComposeAsk(ask, slices.Find(GroupsStateSlice.Name), slices.Find(SessionsStateSlice.Name), slices.Find(QuestsStateSlice.Name),
                slices.Find(AsksStateSlice.Name), record => ChildLineage.Reading(_loop.Home, record.Id, record.Kind, LineageTokens.Shared), id => ChildLineage.PathOf(_loop.Home, id))
            : AtWorkRead.Compose(session, slices.Find(GroupsStateSlice.Name), slices.Find(SessionsStateSlice.Name),
                slices.Find(QuestsStateSlice.Name), record => ChildLineage.Reading(_loop.Home, record.Id, record.Kind, LineageTokens.Shared), id => ChildLineage.PathOf(_loop.Home, id),
                _loop.HintStanding.Word, _loop.Home);

        // The source fence (rule 11): an input moved while it composed is composed again, at most three times; the last lands.
        var held = Compose();
        for (var again = 0; again < 3 && held.Dependencies is AtWorkDependencies read && !_loop.AtWork.Stands(read); again++) held = Compose();
        return held;
    }
}
