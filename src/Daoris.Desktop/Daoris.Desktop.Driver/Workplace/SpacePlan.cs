using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What the press lists (<see cref="SpaceFree.Plan"/>): what each rule takes now, what could go early in groups, and the doors of the kinds with presses of their own.</summary>
public sealed record SpacePlan(DateTimeOffset At, IReadOnlyList<SpaceUnit> Units, IReadOnlyDictionary<string, string> Unread)
{
    /// <summary>The early groups, in the card's order: each kind with something that could go early.</summary>
    public static IReadOnlyList<string> Groups { get; } = ["set-aside", "downloads", "browser-cache", "left-over", "update-failed"];

    /// <summary>The kinds with a press of their own (§3.3 (c)): Clear history… on a workspace's page, Clean up… on its Branches.</summary>
    public static IReadOnlyList<string> Doors { get; } = ["history", "trees"];
}
