using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A door's process for the <see cref="SpawnRequest"/> its executor composed: the door's spawn asked for its recipe, and the transports'
/// one shell completing it from the same request, for a driven start, a native resume and a conversation alike.
/// </summary>
internal static class DoorStarts
{
    /// <summary>A driven start's process.</summary>
    /// <param name="strip">Other providers' registered variables its environment must not inherit (s-vi).</param>
    public static ProcessStartInfo Started(ISpawn spawn, SpawnRequest request, IReadOnlyCollection<string>? strip = null) =>
        Shell.Start(spawn.Start(request), request, strip);

    /// <summary>A native resume's process on the conversation <see cref="SpawnRequest.Resume"/> names, or null where the door resumes none.</summary>
    public static ProcessStartInfo? Resumed(ISpawn spawn, SpawnRequest request, IReadOnlyCollection<string>? strip = null) =>
        spawn.Resume(request) is { } recipe ? Shell.Start(recipe, request, strip) : null;

    /// <summary>A conversation's process.</summary>
    public static ProcessStartInfo Chatted(ISpawn spawn, SpawnRequest request, IReadOnlyCollection<string>? strip = null) =>
        Shell.Chat(spawn.Chat(request), request, strip);
}
