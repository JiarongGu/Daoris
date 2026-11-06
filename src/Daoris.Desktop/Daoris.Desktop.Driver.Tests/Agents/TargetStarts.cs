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
    extension(ProviderRegistry adapters)
    {
        /// <summary>A driven start's process on <paramref name="door"/> for <paramref name="target"/> under the machine's <paramref name="command"/>.</summary>
        public ProcessStartInfo Start(string door, SessionTarget target, IReadOnlyList<string>? command) =>
            adapters.Start(adapters.Door(door), SpawnRequests.Of(target, command));

        /// <summary>A driven start's process on the registered <paramref name="door"/>.</summary>
        public ProcessStartInfo Start(AgentDoor door, SessionTarget target, IReadOnlyList<string>? command) =>
            adapters.Start(door, SpawnRequests.Of(target, command));

        /// <summary>A native resume's process on the registered <paramref name="door"/>, or null where it resumes none.</summary>
        public ProcessStartInfo? Resume(AgentDoor door, SessionTarget target, IReadOnlyList<string>? command, string conversation) =>
            adapters.Resume(door, SpawnRequests.Of(target, command) with { Resume = conversation });

        /// <summary>A conversation's process on the registered <paramref name="door"/>.</summary>
        public ProcessStartInfo Chat(AgentDoor door, SpawnRequest chat, IReadOnlyList<string>? command) =>
            adapters.Chat(door, chat with { Command = command });

        /// <summary>A native resume's process on <paramref name="conversation"/>, or null where the door resumes none.</summary>
        public ProcessStartInfo? Resume(string door, SessionTarget target, IReadOnlyList<string>? command, string conversation) =>
            adapters.Resume(adapters.Door(door), SpawnRequests.Of(target, command) with { Resume = conversation });

        /// <summary>A conversation's process on <paramref name="door"/> for <paramref name="chat"/> under the machine's <paramref name="command"/>.</summary>
        public ProcessStartInfo Chat(string door, SpawnRequest chat, IReadOnlyList<string>? command) =>
            adapters.Chat(adapters.Door(door), chat with { Command = command });
    }
}
