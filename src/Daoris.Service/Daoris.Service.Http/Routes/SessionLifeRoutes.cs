using System.Globalization;
using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A session's life in shadow: the home lease holder's start begins its generation, the watch's tick audits the fold beside the stored
/// status (each difference and each unreadable journal row said once in the machine log, <c>session.fold-differs</c> with the session's
/// whole journal, <c>session.fact-unreadable</c> with its row), and the list reads the last audit. A local host's alone, since a lease is a
/// process of this machine's; nothing here moves a record.
/// </summary>
public sealed class SessionLifeRoutes(HostWiring host) : IRouteModule
{
    private SessionShadow? _shadow;

    private SessionShadow Shadow => LazyInitializer.EnsureInitialized(ref _shadow, () => new SessionShadow(host.Service.Sessions, TimeProvider.System)
    {
        Said = (difference, journal) => host.Log?.Info(
            "session.fold-differs",
            ("session", difference.Session), ("stored", Session.Spell(difference.Stored)), ("fold", Vocabulary<FoldedState>.Spell(difference.Fold.State)),
            ("outcome", difference.Fold.Outcome is { } outcome ? Vocabulary<SessionOutcome>.Spell(outcome) : null),
            ("holder", difference.Fold.Holder is { } holder ? Vocabulary<HolderKind>.Spell(holder.Kind) : null), ("subject", difference.Fold.Holder?.Subject),
            ("since", Moment(difference.Fold.Since)), ("breach", difference.Fold.Breach is { } breach ? Vocabulary<BreachCode>.Spell(breach) : null),
            ("facts", string.Join("; ", journal.Select(Said)))),
        Refused = row => host.Log?.Warn("session.fact-unreadable", ("session", row.Session), ("position", row.Position), ("said", SessionJournal.Said(row))),
    })!;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;
        routes.Map<BeginGeneration, GenerationResponse>(BeginAsync);
        routes.Map<AuditSessions, FoldReadingResponse>(AuditAsync);
        routes.Map<ReadFold, FoldReadingResponse>((_, _, _) => Task.FromResult<Answer<FoldReadingResponse>>(Reading(Shadow.Last)));
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

    private async Task<Answer<FoldReadingResponse>> AuditAsync(AuditSessions request, HttpContext http, CancellationToken ct)
    {
        var leases = new List<SessionLease>();
        foreach (var lease in request.Leases ?? [])
        {
            if (string.IsNullOrWhiteSpace(lease.Session) || lease.Generation < 0)
            {
                return Refusal.BadRequest($"A lease names a session and the generation its marker names; '{lease.Session}' in {lease.Generation} does not read.");
            }

            leases.Add(new SessionLease(lease.Session, lease.Generation, lease.OwnerRuns));
        }

        return Reading(await Shadow.AuditAsync(leases, FactWriter.Generation, ct));
    }

    private static FoldReadingResponse Reading(ShadowReading reading) => new(
        reading.Generation, reading.Audited is { } audited ? Moment(audited) : null, reading.Folded, reading.Took.TotalMilliseconds,
        [.. reading.Differences.Select(Difference)], [.. reading.Unreadable.Select(row => row.Session).Distinct(StringComparer.Ordinal)]);

    private static FoldDifferenceResponse Difference(ShadowDifference difference) => new(
        difference.Session, Session.Spell(difference.Stored), Vocabulary<FoldedState>.Spell(difference.Fold.State),
        difference.Fold.Outcome is { } outcome ? Vocabulary<SessionOutcome>.Spell(outcome) : null,
        difference.Fold.Holder is { } holder ? Vocabulary<HolderKind>.Spell(holder.Kind) : null, difference.Fold.Holder?.Subject,
        Moment(difference.Fold.Since), difference.Fold.Breach is { } breach ? Vocabulary<BreachCode>.Spell(breach) : null,
        Fact(difference.Last));

    private static SessionFactResponse Fact(SessionFact fact) => new(
        fact.Position, Vocabulary<SessionFactKind>.Spell(fact.Kind), Vocabulary<WitnessDoor>.Spell(fact.Witness.Door), fact.Witness.Reason,
        fact.Generation, Moment(fact.At), Vocabulary<HolderKind>.Spell(fact.Holder.Kind), fact.Holder.Subject, fact.Step, fact.Attempt,
        fact.Outcome is { } ended ? Vocabulary<SessionOutcome>.Spell(ended) : null);

    /// <summary>A fact as the log's line says it: its place, kind, generation, witness, holder, step and attempt.</summary>
    private static string Said(SessionFact fact) =>
        $"{fact.Position} {Vocabulary<SessionFactKind>.Spell(fact.Kind)} g{fact.Generation} {Vocabulary<WitnessDoor>.Spell(fact.Witness.Door)}"
        + (fact.Witness.Reason is { } reason ? $":{reason}" : "")
        + (fact.Holder.Kind == HolderKind.None ? "" : $" {Vocabulary<HolderKind>.Spell(fact.Holder.Kind)}{(fact.Holder.Subject is { } subject ? $" {subject}" : "")}")
        + (fact.Step is { } step ? $" step {step}{(fact.Attempt is { } attempt ? $"#{attempt}" : "")}" : "")
        + (fact.Outcome is { } outcome ? $" {Vocabulary<SessionOutcome>.Spell(outcome)}" : "")
        + $" {Moment(fact.At)}";

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Read(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}
