using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The rooms a repository's lanes file declares. A lane that carries <c>builds</c> or a
/// <c>room</c> is a room: its own, by its id, or the one its <c>room</c> names, so lanes keep their names and are one room. A laneless
/// group that names a room is part of it. A lane or a group that is neither is said to be in none, and a repository whose entries are
/// all neither declares no rooms. Read from the lanes file's text; the lanes' own rules are the registration's
/// (<see cref="LineRegistration.LanesProblemsOf"/>), and a file they refuse declares no rooms.
/// </summary>
/// <remarks>
/// <c>builds</c> is an object of three lists in the gates file's path grammar (<see cref="RoomPaths"/>): <c>units</c>, the build unit
/// files whose sources lie in the room (a project, a package, a module: one file each, the folder it sits in its sources);
/// <c>reads</c>, what its builds read outside its units' folders (another room's unit, a props file, a central package file, a
/// version file, a generator's sources); and <c>writes</c>, what its builds or generators write outside them. The conventions a
/// proposal reads these by are canon knowledge (<c>rooms-and-classes</c>), never code.
/// </remarks>
internal static partial class LaneRooms
{
    private static readonly string[] BuildsKeys = ["units", "reads", "writes"];

    /// <summary>The rooms <paramref name="text"/> declares, or why it cannot be read; null text is a repository with no lanes file.</summary>
    internal static LaneRoomsRead Read(string? text)
    {
        if (text is null) return LaneRoomsRead.None;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text.TrimStart('﻿'));
        }
        catch (JsonException)
        {
            return LaneRoomsRead.None with { Problems = [$"{DeclarationNames.Lanes} is not valid JSON"] };
        }

        var problems = LineRegistration.LanesProblemsOf(parsed).ToList();
        if (problems.Count > 0) return LaneRoomsRead.None with { Problems = problems };
        var file = (JsonObject)parsed!;
        if (file["laneless"] is { } groups && groups is not JsonArray) return LaneRoomsRead.None with { Problems = ["'laneless' is not a list"] };

        var entries = new List<Entry>();
        var lanes = file["lanes"] as JsonArray ?? [];
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = (JsonObject)lanes[i]!;
            var id = lane["id"]!.GetValue<string>();
            entries.Add(EntryOf(lane, $"lane '{id}'", id, lane["steward"]?.GetValueKind() == JsonValueKind.True, problems));
        }

        var laneless = file["laneless"] as JsonArray ?? [];
        for (var i = 0; i < laneless.Count; i++)
        {
            if (laneless[i] is not JsonObject group)
            {
                problems.Add($"laneless group {i + 1}: is not an object");
                continue;
            }

            var name = $"laneless group {i + 1}";
            var entry = EntryOf(group, name, null, false, problems);
            if (entry.Builds is not null && entry.Room is null) problems.Add($"{name}: it has builds and names no room");
            entries.Add(entry);
        }

        if (problems.Count > 0) return LaneRoomsRead.None with { Problems = problems };

        // A path is the first entry's that holds it, the steward's before the rest, as the merge tool places it.
        var placing = entries.Where(entry => entry.Steward).Concat(entries.Where(entry => !entry.Steward)).ToList();
        var rooms = entries.Where(entry => entry.Room is not null).GroupBy(entry => entry.Room!, StringComparer.Ordinal)
            .Select(room => new DeclaredRoom(
                room.Key,
                [.. room.Select(entry => entry.Member)],
                Any([.. room.Select(entry => entry.Owns)]),
                [.. room.SelectMany(entry => entry.Builds?.Units ?? []).Distinct(StringComparer.Ordinal)],
                [.. room.SelectMany(entry => entry.Builds?.Reads ?? []).Distinct(StringComparer.Ordinal)],
                [.. room.SelectMany(entry => entry.Builds?.Writes ?? []).Distinct(StringComparer.Ordinal)]))
            .ToList();
        return new LaneRoomsRead(
            rooms,
            [.. entries.Where(entry => entry.Room is null).Select(entry => entry.Said)],
            path => placing.Find(entry => entry.Owns(path)) is { } owner ? owner.Room ?? "" : null,
            []);
    }

    private static Func<string, bool> Any(IReadOnlyList<Func<string, bool>> tests) => path => tests.Any(test => test(path));

    private static Entry EntryOf(JsonObject entry, string name, string? id, bool steward, List<string> problems)
    {
        var paths = (entry["paths"] as JsonArray ?? []).Where(path => path?.GetValueKind() == JsonValueKind.String)
            .Select(path => path!.GetValue<string>()).Where(path => path.Length > 0).ToList();
        string? room = null;
        if (entry["room"] is { } named)
        {
            room = named.GetValueKind() == JsonValueKind.String ? named.GetValue<string>() : "";
            if (!RoomId().IsMatch(room)) problems.Add($"{name}: its room must be lower-case letters, digits and dashes, starting with a letter");
        }

        Builds? builds = null;
        if (entry["builds"] is { } declared)
        {
            builds = BuildsOf(declared, name, problems);
            room ??= id;
        }

        return new Entry(id ?? name, steward, room, builds, RoomPaths.Lane(paths), id ?? string.Join(", ", paths.Where(path => !path.StartsWith('!'))));
    }

    private static Builds BuildsOf(JsonNode declared, string name, List<string> problems)
    {
        if (declared is not JsonObject builds)
        {
            problems.Add($"{name}: 'builds' is not an object");
            return new([], [], []);
        }

        foreach (var (key, _) in builds.Where(pair => !pair.Key.StartsWith('_') && !BuildsKeys.Contains(pair.Key, StringComparer.Ordinal)))
        {
            problems.Add($"{name}: 'builds.{key}' is not units, reads or writes");
        }

        var units = Paths(builds, "units", name, problems);
        foreach (var unit in units.Where(unit => !RoomPaths.IsFile(unit))) problems.Add($"{name}: the unit '{unit}' is one file, never a pattern");
        return new(units, Paths(builds, "reads", name, problems), Paths(builds, "writes", name, problems));
    }

    private static IReadOnlyList<string> Paths(JsonObject builds, string key, string name, List<string> problems)
    {
        if (builds[key] is not { } listed) return [];
        if (listed is not JsonArray list || list.Any(each => each?.GetValueKind() != JsonValueKind.String))
        {
            problems.Add($"{name}: 'builds.{key}' is not a list of paths");
            return [];
        }

        var paths = list.Select(each => each!.GetValue<string>()).ToList();
        foreach (var path in paths)
        {
            if (RoomPaths.Unfit(path) is { } why) problems.Add($"{name}: 'builds.{key}' path '{path}' {why}");
        }

        return paths;
    }

    /// <summary>A room is addressed as a lane is: the CLI's <c>LANE_ID</c>.</summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex RoomId();

    private sealed record Builds(IReadOnlyList<string> Units, IReadOnlyList<string> Reads, IReadOnlyList<string> Writes);

    /// <summary>
    /// A lane or a laneless group: how its room names it (a lane by its id), its room or null for none, and how the report names it
    /// where it is in none.
    /// </summary>
    private sealed record Entry(string Member, bool Steward, string? Room, Builds? Builds, Func<string, bool> Owns, string Said);
}

/// <summary>One room: its id, the lanes and groups it is made of, the paths they hold, and its builds' units, reads and writes.</summary>
internal sealed record DeclaredRoom(
    string Id, IReadOnlyList<string> Members, Func<string, bool> Owns, IReadOnlyList<string> Units, IReadOnlyList<string> Reads,
    IReadOnlyList<string> Writes);

/// <summary>
/// A lanes file's rooms, in the order it declares them; what it says is in none (a lane by its id, a group by its paths); the room a
/// tracked path is placed in (<c>""</c> for an entry in none, null for no entry at all); and why it cannot be read.
/// </summary>
internal sealed record LaneRoomsRead(
    IReadOnlyList<DeclaredRoom> Rooms, IReadOnlyList<string> InNone, Func<string, string?> RoomOf, IReadOnlyList<string> Problems)
{
    internal static readonly LaneRoomsRead None = new([], [], _ => null, []);

    /// <summary>Whether any room is declared: a repository with none works on the fallback (R5.4).</summary>
    internal bool Declares => Rooms.Count > 0;

    /// <summary>Whether some lane or laneless group holds <paramref name="path"/>.</summary>
    internal bool Placed(string path) => RoomOf(path) is not null;
}
