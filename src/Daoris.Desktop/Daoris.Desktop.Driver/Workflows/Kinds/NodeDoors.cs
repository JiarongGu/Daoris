using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// The doors a runtime starts its nodes on, by kind (D247 §3.5): built from the kit and the loop's wiring, each starting kind building its own.
/// None by default, so a runtime nobody wired starts nothing on a door and each such node waits. A test replaces one kind's door with
/// <see cref="With"/>.
/// </summary>
public sealed class NodeDoors
{
    private readonly ImmutableDictionary<string, INodeDoor> _doors;

    private NodeDoors(ImmutableDictionary<string, INodeDoor> doors) => _doors = doors;

    /// <summary>No door: nothing is started on one.</summary>
    public static NodeDoors None { get; } = new(ImmutableDictionary.Create<string, INodeDoor>(StringComparer.Ordinal));

    /// <summary>Each starting kind's door, as its unit builds it from <paramref name="wiring"/>.</summary>
    internal static NodeDoors Wired(NodeDoorWiring wiring)
    {
        var doors = None._doors;
        foreach (var unit in NodeKit.Built.Units)
        {
            if (unit is IStarts starts && starts.Door(wiring) is { } door) doors = doors.SetItem(unit.Row.Kind, door);
        }

        return new NodeDoors(doors);
    }

    /// <summary>These doors with <paramref name="kind"/>'s replaced by <paramref name="door"/>, or taken away where it is null.</summary>
    public NodeDoors With(string kind, INodeDoor? door) => new(door is null ? _doors.Remove(kind) : _doors.SetItem(kind, door));

    /// <summary>The door of the shape <typeparamref name="T"/>, or null where none is wired.</summary>
    public T? Door<T>() where T : class, INodeDoor => _doors.Values.OfType<T>().FirstOrDefault();
}
