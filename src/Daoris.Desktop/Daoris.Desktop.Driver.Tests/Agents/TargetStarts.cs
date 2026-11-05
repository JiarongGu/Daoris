using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A test's way from the target it composed to a door's process: the <see cref="SpawnRequest"/> composed from it exactly as a driven start's
/// executor composes it (<see cref="SpawnRequests.Of(SessionTarget, IReadOnlyList{string})"/>), then asked of the door. So a test that
/// starts a door from a target reads what the executor would hand it, and no test composes a request its own way.
/// </summary>
internal static class TargetStarts
{
    extension(ISessionAdapter adapter)
    {
        /// <summary>A driven start's process for <paramref name="target"/> under the machine's <paramref name="command"/>.</summary>
        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            adapter.Prepare(SpawnRequests.Of(target, command));

        /// <summary>A native resume's process on <paramref name="conversation"/>, or null where the door resumes none.</summary>
        public ProcessStartInfo? PrepareResume(SessionTarget target, IReadOnlyList<string>? command, string conversation) =>
            adapter.PrepareResume(SpawnRequests.Of(target, command) with { Resume = conversation });

        /// <summary>A conversation's process for <paramref name="chat"/> under the machine's <paramref name="command"/>.</summary>
        public ProcessStartInfo PrepareChat(SpawnRequest chat, IReadOnlyList<string>? command) =>
            adapter.PrepareChat(chat with { Command = command });
    }
}
