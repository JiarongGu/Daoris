using System.Globalization;
using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A session's life in shadow: the application's start begins its generation, and the fold is read beside the stored status, each
/// difference said once in the machine log (<c>session.fold-differs</c>). A local host's alone, since a lease is a process of this
/// machine's; nothing here moves a record.
/// </summary>
public sealed class SessionLifeRoutes(HostWiring host) : IRouteModule
{
    private SessionShadow? _shadow;

    private SessionShadow Shadow => LazyInitializer.EnsureInitialized(ref _shadow, () => new SessionShadow(host.Service.Sessions, TimeProvider.System)
    {
        Said = difference => host.Log?.Info(
            "session.fold-differs",
            ("session", difference.Session), ("stored", Session.Spell(difference.Stored)), ("fold", SessionFold.Spell(difference.Fold.State)),
            ("outcome", difference.Fold.Outcome is { } outcome ? SessionFold.Spell(outcome) : null), ("holder", difference.Fold.Holder),
            ("since", Moment(difference.Fold.Since)), ("breach", difference.Fold.Breach),
            ("facts", string.Join("; ", difference.Facts.Select(fact => $"{fact.Position} {fact.Kind} g{fact.Generation} {fact.Witness}")))),
    })!;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;
        routes.Map<BeginGeneration, GenerationResponse>(BeginAsync);
        routes.Map<FoldSessions, FoldSessionsResponse>(FoldAsync);
    }

    private async Task<Answer<GenerationResponse>> BeginAsync(BeginGeneration request, HttpContext http, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var started = request.Started is null ? now : Read(request.Started);
        if (started is null) return Refusal.BadRequest($"`started` is no moment: '{request.Started}'.");
        var begun = await host.Service.Sessions.Generations.BeginAsync(started.Value, now, ct);
        host.Log?.Info("session.generation", ("generation", begun.Number), ("started", Moment(begun.Started)));
        return new GenerationResponse(begun.Number, Moment(begun.Started));
    }

    private async Task<Answer<FoldSessionsResponse>> FoldAsync(FoldSessions request, HttpContext http, CancellationToken ct)
    {
        var leases = new List<ShadowLease>();
        foreach (var lease in request.Leases ?? [])
        {
            if (string.IsNullOrWhiteSpace(lease.Session) || Read(lease.Started) is not { } started)
            {
                return Refusal.BadRequest($"A lease names a session and its process's start; '{lease.Session}' at '{lease.Started}' does not read.");
            }

            leases.Add(new ShadowLease(lease.Session, started));
        }

        var reading = await Shadow.ReadAsync(leases, ct);
        return new FoldSessionsResponse(
            reading.Generation, reading.Folded, reading.Took.TotalMilliseconds,
            [.. reading.Differences.Select(Difference)]);
    }

    private static FoldDifferenceResponse Difference(ShadowDifference difference) => new(
        difference.Session, Session.Spell(difference.Stored), SessionFold.Spell(difference.Fold.State),
        difference.Fold.Outcome is { } outcome ? SessionFold.Spell(outcome) : null, difference.Fold.Holder, Moment(difference.Fold.Since),
        difference.Fold.Breach,
        [.. difference.Facts.Select(fact => new SessionFactResponse(
            fact.Position, fact.Kind.ToString(), fact.Witness, fact.Generation, Moment(fact.At), fact.Holder, fact.Step, fact.Attempt,
            fact.Outcome is { } ended ? SessionFold.Spell(ended) : null))]);

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Read(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}
