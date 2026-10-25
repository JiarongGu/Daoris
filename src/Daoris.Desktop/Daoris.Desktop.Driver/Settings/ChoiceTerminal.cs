using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The terminal <c>daoris-driver driver</c> is said on and pressed from (FOUNDATION1 mod-iii, the design §14.2: each verb stays in its own
/// subdomain and reaches the instance through the instance's door): the settings declare this port, the instance's world
/// (<see cref="InstanceWorld"/>) implements it with the door, and every host and test that builds a <see cref="DriverChoicesWorld"/>
/// hands it one, so the settings' command names neither the world nor the door.
/// </summary>
public interface IChoiceTerminal
{
    /// <summary>The home the verb acts on, or null where it has none.</summary>
    string? Home { get; }

    /// <summary>The variables, read by name (never the process's, which a test does not share).</summary>
    Func<string, string?> Environment { get; }

    /// <summary>Where the answer and the progress are said.</summary>
    TextWriter Out { get; }

    /// <summary>Where a refusal is said.</summary>
    TextWriter Error { get; }

    /// <summary>
    /// A person's press sent to the running Daoris while it answers, confirmed in its window, and acted alone (<paramref name="alone"/>)
    /// where none runs (RPC1b, the terminal RPC design §7); the route's answer said in the verb's words (<paramref name="words"/>).
    /// </summary>
    Task<int> PressAsync(InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int> words, CancellationToken ct);
}
