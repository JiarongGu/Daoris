using System.Globalization;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>What the fold says of a session where it differs from its stored status: its state, outcome, holder, since and breach, as the wire spells them.</summary>
public sealed record FoldShown(string State, string? Outcome, string? Holder, string Since, string? Breach);

/// <summary>
/// The fold in shadow, the driver's half: each start of the application begins its generation (<see cref="BeginAsync"/>), and a look
/// at the sessions reads where the fold differs from the stored status (<see cref="ReadAsync"/>) against the leases its session markers
/// prove (<see cref="Leases"/>), which the list's rows carry beside their state. Nothing acts on it; a service that does not answer
/// (a shared host, an older one) leaves every row as it stands.
/// </summary>
/// <remarks>
/// The watch begins the generation at each look until the service has answered once, before the look's sweep moves a record, so the
/// sweep's facts and every later one are stamped with it and a process older than the application's start holds a void lease.
/// </remarks>
public static class SessionFolds
{
    /// <summary>
    /// The live leases <paramref name="markers"/> (the home's <c>run/sessions</c>) prove: each marker whose process is alive as the one
    /// it marked (<see cref="SessionProcesses.IsRunning"/>), with its session and its start. An opener's mark is no process of the
    /// session's, and a marker that cannot be read proves nothing.
    /// </summary>
    public static IReadOnlyList<LeaseRequest> Leases(string? markers)
    {
        List<string> files;
        try
        {
            if (markers is null || !Directory.Exists(markers)) return [];
            files = [.. Directory.EnumerateFiles(markers, "*.pid")];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var leases = new List<LeaseRequest>();
        foreach (var marker in files)
        {
            string[] fields;
            try
            {
                fields = AtomicFile.ReadText(marker).Trim().Split(' ');
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (fields.Length != 2 || !int.TryParse(fields[0], CultureInfo.InvariantCulture, out var pid)
                || !long.TryParse(fields[1], CultureInfo.InvariantCulture, out var started) || !SessionProcesses.IsRunning(pid, started))
            {
                continue;
            }

            leases.Add(new LeaseRequest(Path.GetFileNameWithoutExtension(marker), Moment(new DateTimeOffset(started, TimeSpan.Zero))));
        }

        return leases;
    }

    /// <summary>
    /// Begin the generation of an application whose process started at <paramref name="started"/>: true once the service answered,
    /// whatever it said (a host that serves no generations answers too), false where it could not be reached, so the next look asks again.
    /// </summary>
    public static async Task<bool> BeginAsync(ServiceClient service, DateTimeOffset started, CancellationToken ct)
    {
        try
        {
            await service.SendAsync(new BeginGeneration(Moment(started)), ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Where the fold differs from the stored status, by session, against the leases <paramref name="markers"/> prove; none where the service does not answer.</summary>
    public static async Task<IReadOnlyDictionary<string, FoldShown>> ReadAsync(ServiceClient service, string? markers, CancellationToken ct)
    {
        Sent<FoldSessionsResponse> sent;
        try
        {
            sent = await service.SendAsync(new FoldSessions(Leases(markers)), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return None;
        }

        return sent.Answer is not { } answer
            ? None
            : answer.Differences
                .GroupBy(difference => difference.Session, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new FoldShown(group.First().State, group.First().Outcome, group.First().Holder, group.First().Since, group.First().Breach),
                    StringComparer.Ordinal);
    }

    /// <summary>What a row's line says of a fold that differs: <i>the fold says needs-you, held by person, since …</i>, and its breach.</summary>
    public static string Words(FoldShown fold) =>
        $"the fold says {fold.State}{(fold.Outcome is { } outcome ? $" ({outcome})" : "")}"
        + $"{(fold.Holder is { } holder ? $", held by {holder}" : "")} since {fold.Since}{(fold.Breach is { } breach ? $": {breach}" : "")}";

    /// <summary>When this application's process started: what its generation is begun with, since every process it starts starts after it.</summary>
    public static DateTimeOffset ApplicationStarted { get; } = StartOfThisProcess();

    private static DateTimeOffset StartOfThisProcess()
    {
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime();
    }

    private static readonly IReadOnlyDictionary<string, FoldShown> None = new Dictionary<string, FoldShown>(StringComparer.Ordinal);

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
