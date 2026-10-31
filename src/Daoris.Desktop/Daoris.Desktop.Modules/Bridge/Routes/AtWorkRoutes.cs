using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>Who is at work on a session, the page's `bridge/sessions/atWork.ts` (COWORKVIEW1a).</summary>
public sealed class AtWorkRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// Who is at work on a session (COWORKVIEW1a, the design §4.1): its entry's parts and the attended part's subagents, composed from
    /// the groups and sessions slices in memory and each part's lineage file, this window's tokens read where it issued them. Numbered,
    /// so a failure is answered with its words and an older delivery never replaces a newer one.
    /// </summary>
    [DriverRoute("SESSION_AT_WORK")]
    [ByEvent("SESSION_AT_WORK", Numbered = true, Untold = "each part's lineage file and this window's lineage tokens, which the read's own judge watches")]
    private async Task<object?> SessionAtWorkAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        await Task.CompletedTask;
        var slices = _loop.Slices;
        AtWorkHeld Compose() => AtWorkRead.Compose(session, slices.Find(GroupsStateSlice.Name), slices.Find(SessionsStateSlice.Name),
            record => ChildLineage.Reading(_loop.Home, record.Id, record.Kind, LineageTokens.Shared), id => ChildLineage.PathOf(_loop.Home, id));

        // The source fence (rule 11): an input moved while it composed is composed again, at most three times; the last lands.
        var held = Compose();
        for (var again = 0; again < 3 && held.Dependencies is AtWorkDependencies read && !_loop.AtWork.Stands(read); again++) held = Compose();
        return held;
    }
}
