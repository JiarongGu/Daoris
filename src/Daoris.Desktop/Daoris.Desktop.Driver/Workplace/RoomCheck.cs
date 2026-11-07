namespace Daoris.Driver;

/// <summary>
/// The check of a repository's rooms, deterministic and stack-agnostic: it reads the lanes
/// file's rooms, the gates file's classes and git's tracked paths, nothing else. It refuses two rooms holding one path, a tracked path
/// no entry places, a unit that is not a tracked file of its room or whose folder holds another room's sources, a lockfile apart from
/// the unit beside it, a file written from two rooms, and a read or a write naming nothing tracked or declared. It joins into one claim
/// group the rooms whose build closures meet: a room's own paths, its units, and what its builds read and write, wherever another
/// room's closure holds the same path, so one session at a time works in place in a group. Every reference across rooms is listed,
/// so its lock is known. Nothing acts on the result yet.
/// </summary>
internal static class RoomCheck
{
    /// <summary>How many paths of one kind of problem are named before the rest are counted.</summary>
    private const int Named = 5;

    /// <summary>The check of a lanes file's and a gates file's texts (null for a file the repository lacks) over its tracked paths.</summary>
    internal static RoomCheckReport Of(string? lanes, string? gates, IReadOnlyList<string> tracked) =>
        Run(LaneRooms.Read(lanes), TreeClassesReader.Read(gates, tracked), tracked);

    internal static RoomCheckReport Run(LaneRoomsRead read, TreeClassesRead classes, IReadOnlyList<string> tracked)
    {
        var problems = read.Problems.Concat(classes.Problems).ToList();
        if (!read.Declares) return new RoomCheckReport([], [], [], read.InNone, classes.Classes, problems);

        var rooms = read.Rooms;
        var at = rooms.Select((room, i) => (room.Id, i)).ToDictionary(pair => pair.Id, pair => pair.i, StringComparer.Ordinal);
        var groups = new Joins(rooms.Count);
        var crossings = new List<RoomCrossing>();
        var owner = Placed(read, tracked, problems);

        var touching = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var writers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var untracked = new List<(int Room, string Verb, string Path)>();
        for (var i = 0; i < rooms.Count; i++)
        {
            foreach (var (verb, path) in rooms[i].Reads.Select(path => ("reads", path)).Concat(rooms[i].Writes.Select(path => ("writes", path))))
            {
                var test = RoomPaths.Declared(path);
                var matched = tracked.Where(each => test.IsMatch(each)).ToList();
                foreach (var each in matched)
                {
                    Add(touching, each, i);
                    if (verb == "writes") Add(writers, each, i);
                }

                List<string?> owners = matched.Count > 0
                    ? matched.Select(each => owner[each]).Distinct().ToList()
                    : [Room(read.RoomOf(RoomPaths.Probe(path)))];
                if (matched.Count == 0)
                {
                    untracked.Add((i, verb, path));
                    if (!classes.Classes.Paths.Any(declared => RoomPaths.Meet(declared.Path, path)))
                    {
                        problems.Add(verb == "reads"
                            ? $"room {rooms[i].Id} reads {path}, which no tracked file or declared output is"
                            : $"room {rooms[i].Id} writes {path}, which is neither tracked nor declared in trees");
                    }
                }

                foreach (var other in owners.Where(other => other != rooms[i].Id && (other is not null || matched.Count > 0)))
                {
                    crossings.Add(new(rooms[i].Id, verb, path, other ?? RoomCrossing.NoRoom));
                    if (other is not null) groups.Join(i, at[other]);
                }
            }
        }

        // A path two rooms' builds touch, a roomless input among them, is where their closures meet.
        foreach (var (_, each) in touching) each.ForEach(room => groups.Join(each[0], room));
        foreach (var (path, each) in writers.Where(pair => pair.Value.Distinct().Count() > 1))
        {
            problems.Add($"{path} is written from rooms {rooms[each[0]].Id} and {rooms[each.First(room => room != each[0])].Id}");
        }

        Untracked(rooms, untracked, groups, crossings, problems);
        var units = Units(rooms, tracked, owner, problems);
        Lockfiles(classes.Classes, units, owner, problems);

        var joined = Enumerable.Range(0, rooms.Count).GroupBy(groups.Root).OrderBy(group => group.Min())
            .Select(group => (IReadOnlyList<string>)[.. group.Order().Select(i => rooms[i].Id)]).ToList();
        return new RoomCheckReport([.. rooms.Select(room => room.Id)], joined, [.. crossings.Distinct()], read.InNone, classes.Classes, problems);
    }

    /// <summary>Each tracked path's room, or null for one in none; two rooms holding a path and a path no entry places, refused.</summary>
    private static Dictionary<string, string?> Placed(LaneRoomsRead read, IReadOnlyList<string> tracked, List<string> problems)
    {
        var owner = new Dictionary<string, string?>(tracked.Count, StringComparer.Ordinal);
        var overlaps = new Dictionary<(string, string), List<string>>();
        var unplaced = new List<string>();
        foreach (var path in tracked)
        {
            var holding = read.Rooms.Where(room => room.Owns(path)).Take(2).ToList();
            if (holding.Count > 1) Add(overlaps, (holding[0].Id, holding[1].Id), path);
            var placed = read.RoomOf(path);
            if (placed is null) unplaced.Add(path);
            owner[path] = Room(placed);
        }

        foreach (var ((one, other), paths) in overlaps) problems.Add($"{paths[0]} is in rooms {one} and {other}{More(paths.Count)}");
        problems.AddRange(unplaced.Take(Named).Select(path => $"{path} is in no room and no lane or laneless group says so"));
        if (unplaced.Count > Named) problems.Add($"{unplaced.Count - Named} more paths are in no room and no lane or laneless group says so");
        return owner;
    }

    /// <summary>Reads and writes of untracked, declared paths: where one room's meets another's, their closures meet.</summary>
    private static void Untracked(
        IReadOnlyList<DeclaredRoom> rooms, List<(int Room, string Verb, string Path)> untracked, Joins groups, List<RoomCrossing> crossings,
        List<string> problems)
    {
        for (var a = 0; a < untracked.Count; a++)
        {
            for (var b = a + 1; b < untracked.Count; b++)
            {
                var (one, other) = (untracked[a], untracked[b]);
                if (one.Room == other.Room || !RoomPaths.Meet(one.Path, other.Path)) continue;
                groups.Join(one.Room, other.Room);
                if (one.Verb == "writes" && other.Verb == "writes") problems.Add($"{other.Path} is written from rooms {rooms[one.Room].Id} and {rooms[other.Room].Id}");
                else if (one.Verb == "reads" && other.Verb == "writes") crossings.Add(new(rooms[one.Room].Id, "reads", one.Path, rooms[other.Room].Id));
                else if (one.Verb == "writes" && other.Verb == "reads") crossings.Add(new(rooms[other.Room].Id, "reads", other.Path, rooms[one.Room].Id));
            }
        }
    }

    /// <summary>Each unit's room: a tracked file of its own room, declared once, its folder holding no other room's sources.</summary>
    private static Dictionary<string, string> Units(
        IReadOnlyList<DeclaredRoom> rooms, IReadOnlyList<string> tracked, Dictionary<string, string?> owner, List<string> problems)
    {
        var units = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var room in rooms)
        {
            foreach (var unit in room.Units)
            {
                if (!owner.TryGetValue(unit, out var lies))
                {
                    problems.Add($"the unit {unit} of room {room.Id} is not a tracked file");
                    continue;
                }

                if (units.TryGetValue(unit, out var first))
                {
                    problems.Add($"the unit {unit} is declared by rooms {first} and {room.Id}");
                    continue;
                }

                units[unit] = room.Id;
                if (lies != room.Id)
                {
                    problems.Add($"the unit {unit} of room {room.Id} lies in {Said(lies)}");
                    continue;
                }

                var folder = Folder(unit);
                var stray = tracked.Where(path => path.StartsWith(folder, StringComparison.Ordinal) && owner[path] != room.Id).ToList();
                if (stray.Count > 0) problems.Add($"the unit {unit} of room {room.Id} has sources in {Said(owner[stray[0]])}: {stray[0]}{More(stray.Count)}");
            }
        }

        return units;
    }

    /// <summary>A lockfile belongs to the room of the unit beside it (R5.3 break 4): one in another room splits it.</summary>
    private static void Lockfiles(TreeClasses classes, Dictionary<string, string> units, Dictionary<string, string?> owner, List<string> problems)
    {
        foreach (var key in classes.Dependencies.SelectMany(each => each.Key).Distinct(StringComparer.Ordinal).Where(owner.ContainsKey))
        {
            foreach (var (unit, room) in units.Where(pair => Folder(pair.Key) == Folder(key) && pair.Value != owner[key]))
            {
                problems.Add($"the lockfile {key} is in {Said(owner[key])} and the unit beside it, {unit}, in room {room}");
            }
        }
    }

    private static string Folder(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..(slash + 1)] : "";

    private static string? Room(string? placed) => string.IsNullOrEmpty(placed) ? null : placed;

    private static string Said(string? room) => room is null ? "no room" : $"room {room}";

    private static string More(int count) => count > 1 ? $", and {count - 1} more" : "";

    private static void Add<TKey>(Dictionary<TKey, List<int>> map, TKey key, int value)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(value);
    }

    private static void Add<TKey>(Dictionary<TKey, List<string>> map, TKey key, string value)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(value);
    }

    /// <summary>Rooms joined into groups: each group's root is its first room joined.</summary>
    private sealed class Joins(int count)
    {
        private readonly int[] _parent = [.. Enumerable.Range(0, count)];

        internal int Root(int room)
        {
            while (_parent[room] != room) room = _parent[room] = _parent[_parent[room]];
            return room;
        }

        internal void Join(int one, int other)
        {
            var (a, b) = (Root(one), Root(other));
            if (a != b) _parent[Math.Max(a, b)] = Math.Min(a, b);
        }
    }
}
