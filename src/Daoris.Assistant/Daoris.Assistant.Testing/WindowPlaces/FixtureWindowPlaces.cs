using System.Text.Json;
using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The window's places read from a fixture's text, the shape the one places fixture both the core and the web read takes:
/// <c>{ "places": [{ "kind": "view", "name": "work" }], "doors": [{ "view": "work", "sentence": "…" }] }</c>.
/// </summary>
public sealed class FixtureWindowPlaces(IReadOnlyList<WindowPlace> places, IReadOnlyList<WindowDoor> doors) : IWindowPlaces
{
    public IReadOnlyList<WindowPlace> Places { get; } = [.. places];

    public IReadOnlyList<WindowDoor> Doors { get; } = [.. doors];

    /// <summary>The places and doors <paramref name="json"/> holds; a kind that is no <see cref="WindowPlaceKind"/> is refused.</summary>
    public static FixtureWindowPlaces Parse(string json)
    {
        using var fixture = JsonDocument.Parse(json);
        var root = fixture.RootElement;
        var places = Items(root, "places").Select(place => new WindowPlace(
            Enum.Parse<WindowPlaceKind>(Text(place, "kind"), ignoreCase: true), Text(place, "name")));
        var doors = Items(root, "doors").Select(door => new WindowDoor(Text(door, "view"), Text(door, "sentence")));
        return new FixtureWindowPlaces([.. places], [.. doors]);
    }

    private static IEnumerable<JsonElement> Items(JsonElement root, string name) =>
        root.TryGetProperty(name, out var items) ? items.EnumerateArray().ToList() : [];

    private static string Text(JsonElement item, string name) => item.GetProperty(name).GetString() ?? "";
}
