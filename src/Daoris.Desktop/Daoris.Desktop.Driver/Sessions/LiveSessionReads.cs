using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The read of <see cref="LiveSessions"/> (FOUNDATION1 mod-v, the design §14.3: a member moved up to the subdomain whose types it names):
/// it asks the service's client and, with none, the home's process markers (<see cref="SessionProcesses"/>), so the shape the install names
/// holds neither. Every caller keeps <c>LiveSessions.ReadAsync</c> as spelled.
/// </summary>
public static class LiveSessionReads
{
    extension(LiveSessions)
    {
        /// <summary>
        /// The home's running sessions (§3.5): the service's active records, each with what its starts were handed. With no service to ask,
        /// or one that did not answer, the home's process markers say whether a session runs here at all (<see cref="SessionProcesses"/>):
        /// none is none, and one that runs keeps every version, since what it was handed cannot be read (§6.7).
        /// </summary>
        /// <param name="service">The service's client, or null for none.</param>
        /// <param name="markers">The home's process markers, <see cref="HomeEntry.SessionPids"/>; null where none are kept.</param>
        /// <param name="ct">Cancels the read.</param>
        public static async Task<LiveSessions> ReadAsync(ServiceClient? service, string? markers, CancellationToken ct)
        {
            string? why = null;
            if (service is not null)
            {
                try
                {
                    return new(await service.LiveSessionsAsync(ct).ConfigureAwait(false), null);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                                  || (error is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    why = error.Message;
                }
            }

            if (!SessionProcesses.AnyAlive(markers)) return LiveSessions.None;
            return new([], why is null
                ? "a session runs on this home, and no service is here to say what it was handed"
                : $"the service did not say which sessions run ({why.TrimEnd('.')})");
        }
    }
}
