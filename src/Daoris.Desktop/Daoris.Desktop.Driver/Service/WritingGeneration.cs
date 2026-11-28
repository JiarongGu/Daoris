using System.Globalization;
using Daoris.Contracts.Sessions;
using Daoris.Ports.Sessions;

namespace Daoris.Driver;

/// <summary>
/// The application's generation this driver process writes in, carried on every request to the service
/// (<see cref="SessionLifeHeaders.Generation"/>), so each fact the service appends names its writer's generation and never one the store
/// stamps: the generation its start began where it holds the home's lease (<see cref="SessionFolds.BeginAsync"/>), the newest the service
/// answered where it runs beside the holder (<see cref="SessionFolds.AuditAsync"/>), or, in a session's terminal, the one its session's
/// marker names (<see cref="FromMarker"/>); 0 where none is known, which a fact reads as written by no start of the application.
/// </summary>
/// <remarks>
/// A process that outlived a restart writes in the generation it started in, so its writes after the restart are told apart from the
/// current start's. Each session process this driver starts is marked with it (<see cref="SessionMarker"/>).
/// </remarks>
public static class WritingGeneration
{
    private static long _current;

    /// <summary>The generation this process writes in now; 0 for none.</summary>
    public static long Current => Interlocked.Read(ref _current);

    /// <summary>Write in <paramref name="generation"/> from now on.</summary>
    public static void Set(long generation) => Interlocked.Exchange(ref _current, generation);

    /// <summary>
    /// A session's terminal writes in the generation its session's marker names: the start its process began under. A marker that
    /// is not there or does not read leaves it as it is.
    /// </summary>
    /// <param name="markers">The home's session markers' folder.</param>
    /// <param name="session">The terminal's session (<see cref="ChildMarks.SessionVariable"/>).</param>
    public static void FromMarker(string markers, string session)
    {
        try
        {
            var file = Path.Combine(markers, session + ".pid");
            if (SafeNames.IsFolderId(session) && File.Exists(file) && SessionMarker.Parse(AtomicFile.ReadText(file)) is { Opening: false } marker)
            {
                Set(marker.Generation);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unread, the terminal writes in none: its facts say no start of the application wrote them.
        }
    }

    /// <summary>Set the generation on one request to the service, where one is known.</summary>
    public static void Dress(HttpRequestMessage request)
    {
        if (Current is > 0 and var generation)
        {
            request.Headers.TryAddWithoutValidation(SessionLifeHeaders.Generation, generation.ToString(CultureInfo.InvariantCulture));
        }
    }
}
