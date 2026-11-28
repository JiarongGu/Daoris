using System.Globalization;
using Daoris.Contracts.Sessions;
using Daoris.Ports.Processes;

namespace Daoris.Driver;

/// <summary>
/// What the fold in shadow says of a session where it differs from its stored status, as the wire spells it: its state, outcome, the
/// kind of what holds it and its subject, since when, and its breach's code (the window's catalogue words it).
/// </summary>
public sealed record FoldShown(string State, string? Outcome, string? Holder, string? Subject, string Since, string? Breach);

/// <summary>
/// The fold in shadow, the driver's half. The start of the application that holds the home's lease begins its generation
/// (<see cref="BeginAsync"/>); each tick of the watch audits the fold against the leases its session markers prove
/// (<see cref="AuditAsync"/>, <see cref="Leases"/>); and a look at the sessions reads the last audit (<see cref="ReadAsync"/>), which the
/// list's rows carry beside their state. Nothing acts on it.
/// </summary>
/// <remarks>
/// A driver beside the holder (<c>drive --share</c>) begins no generation: it writes in the holder's, which each audit answers, so
/// neither voids the other's leases; a lease is live while the driver that owns it runs (<see cref="ProvenLease.OwnerRuns"/>).
/// </remarks>
public static class SessionFolds
{
    /// <summary>
    /// The session processes <paramref name="markers"/> (the home's <c>run/sessions</c>) prove alive, each with its generation and
    /// whether its owner runs, through the one marker reader (<see cref="SessionMarker.Prove"/>) and this driver's look at a process.
    /// </summary>
    public static IReadOnlyList<LeaseRequest> Leases(string? markers) =>
        [.. SessionMarker.Prove(markers, SessionProcesses.IsRunning).Select(lease => new LeaseRequest(lease.Session, lease.Generation, lease.OwnerRuns))];

    /// <summary>
    /// Begin the generation of an application whose process started at <paramref name="started"/>, where this process holds the home's
    /// lease (<paramref name="holds"/>), and write in it from then on: true once nothing is left to begin (begun, or not the holder's to
    /// begin), false where the service did not answer it, said through <paramref name="said"/>, so the next look asks again.
    /// </summary>
    public static async Task<bool> BeginAsync(
        ServiceClient service, DateTimeOffset started, bool holds, Action<string>? said, CancellationToken ct)
    {
        if (!holds) return true;
        try
        {
            var sent = await service.SendAsync(new BeginGeneration(Moment(started)), ct).ConfigureAwait(false);
            if (sent.Answer is { } begun)
            {
                WritingGeneration.Set(begun.Generation);
                return true;
            }

            said?.Invoke(sent.Refusal);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            said?.Invoke(error.Message);
        }

        return false;
    }

    /// <summary>
    /// The tick's audit of the fold against the leases <paramref name="markers"/> prove; a driver that does not hold the home's lease
    /// writes in the generation it answers from then on. A refusal is thrown as a refused read's is, for the tick to say.
    /// </summary>
    /// <exception cref="DriverException">The service refused the audit.</exception>
    public static async Task<FoldReadingResponse> AuditAsync(ServiceClient service, string? markers, bool holds, CancellationToken ct)
    {
        var audit = (await service.SendAsync(new AuditSessions(Leases(markers)), ct).ConfigureAwait(false)).Answered();
        if (!holds && audit.Generation > 0) WritingGeneration.Set(audit.Generation);
        return audit;
    }

    /// <summary>
    /// Where the last audit found the fold differing from the stored status, by session; none where nothing has audited yet, and none
    /// where the service does not answer, which is said through <paramref name="said"/>, never passed over.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, FoldShown>> ReadAsync(ServiceClient service, Action<string>? said, CancellationToken ct)
    {
        try
        {
            var sent = await service.SendAsync(new ReadFold(), ct).ConfigureAwait(false);
            if (sent.Answer is { } reading) return Shown(reading);
            said?.Invoke(sent.Refusal);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            said?.Invoke(error.Message);
        }

        return None;
    }

    /// <summary>The rows an audit's differences lay on the list, by session.</summary>
    public static IReadOnlyDictionary<string, FoldShown> Shown(FoldReadingResponse reading) =>
        reading.Differences
            .DistinctBy(difference => difference.Session, StringComparer.Ordinal)
            .ToDictionary(
                difference => difference.Session,
                difference => new FoldShown(difference.State, difference.Outcome, difference.Holder, difference.Subject, difference.Since, difference.Breach),
                StringComparer.Ordinal);

    /// <summary>What a row's line says of a fold that differs: <i>in shadow: needs-you, held by person, since …</i>, and its breach's code.</summary>
    public static string Words(FoldShown fold) =>
        $"in shadow: {fold.State}{(fold.Outcome is { } outcome ? $" ({outcome})" : "")}"
        + $"{(fold.Holder is { } holder ? $", held by {holder}{(fold.Subject is { } subject ? $" {subject}" : "")}" : "")} since {fold.Since}"
        + $"{(fold.Breach is { } breach ? $": {breach}" : "")}";

    /// <summary>When this application's process started: what its generation is begun with.</summary>
    public static DateTimeOffset ApplicationStarted { get; } = StartOfThisProcess();

    private static DateTimeOffset StartOfThisProcess()
    {
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime();
    }

    private static readonly IReadOnlyDictionary<string, FoldShown> None = new Dictionary<string, FoldShown>(StringComparer.Ordinal);

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
