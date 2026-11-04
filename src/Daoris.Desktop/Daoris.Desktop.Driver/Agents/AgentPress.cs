using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One agent change's press (PUSH1f2d, D194 points 4 and 6): the route the window's press sends and the payload the verb writes from its
/// words, the operation it calls alone, the press's own fields read from the route's answer, and the one function that says it from those
/// fields on both paths. <see cref="AgentsCommand.Plan"/> makes it; a test sends its payload through the route.
/// </summary>
/// <param name="Route">The route, or null where none takes the change and the verb acts alone even beside a running Daoris.</param>
/// <param name="Isolated">Whether the change is a named wiring file's home's, acted on alone, sending nothing (D194 point 2).</param>
/// <param name="Act">The operation, alone: it writes, and answers the press's own fields (or null where it has none).</param>
/// <param name="Fields">The press's own fields, read from the route's answer.</param>
/// <param name="Words">The verb's words from those fields, both paths', returning its exit code.</param>
public sealed record AgentPress(
    string Verb, InstanceRoute? Route, bool Isolated, JsonObject? Payload, Func<CancellationToken, Task<object?>> Act,
    Func<JsonElement, object?> Fields, Func<object?, int> Words);
