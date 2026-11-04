using Daoris.Contracts.Platform;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// What a local host tells the processes beside it that tell from its ledger: which families moved (FABRIC-EVENT3b, the fabric events
/// design §A4.2) and the event journal's rows (FOUNDATION1 l-i). A read every caller may make, as the connector reads the same store.
/// </summary>
public sealed class PlatformRoutes(HostWiring host) : IRouteModule
{
    public void Map(RouteMap routes)
    {
        if (host.Changes is not { } changes) return;

        // A missing, malformed, stale or future cursor is answered at once with every family and the current cursor; a ninth wait at once
        // with the cursor. The host's stop answers a held wait at once, so a long-poll never holds the shell's wait. A reader that never
        // asks for the journal, every client from before the family, is answered what it always was and woken by nothing a journal-only
        // append does; the host's `journal` protocol mark says a host answers it.
        routes.Map<ReadChanges, ChangesResponse>(async (request, _, ct) =>
        {
            using var ends = CancellationTokenSource.CreateLinkedTokenSource(ct, host.Stopping);
            var asked = request.Journal is "1" || string.Equals(request.Journal, "true", StringComparison.OrdinalIgnoreCase);
            var answer = await changes.WaitAsync(request.Since, TimeSpan.FromSeconds(Math.Clamp(request.Wait ?? 0, 0, 30)), ends.Token, asked);
            return new ChangesResponse(
                answer.Since, answer.Moved, answer.Journal is { } place ? new ChangesJournalResponse(place.Store, place.Floor, place.Newest) : null);
        });
        routes.Map<ReadJournal, JournalPage>(async (request, _, ct) =>
            await host.Service.Journal(request.Since ?? 0, request.Limit ?? JournalRows.Most, ct));
    }
}
