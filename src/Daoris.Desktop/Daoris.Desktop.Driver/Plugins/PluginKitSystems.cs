using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The kit's word on the system points (<see cref="HookPoints.System"/>): their entries in <see cref="PluginKit.Points"/>, and what
/// <c>plugins new</c> adds for an adapter of a system reached only through its console: the manifest's <c>systems</c>, the kit's
/// <see cref="Library"/> answering both points over the author's read and write, and its README's section.
/// </summary>
public static class PluginKitSystems
{
    /// <summary>The kit's half of an adapter: both points answered over <c>read()</c> and <c>write()</c>, and the stand-in world.</summary>
    public const string Library = "system.mjs";

    /// <summary>The one system a new adapter declares, by id.</summary>
    public const string SystemId = "system";

    /// <summary>A plan's intended change, as the kit's samples say it.</summary>
    public static readonly SystemIntent SampleIntent = new("release", "2");

    /// <summary>The reviewed change set an apply is sent, as the kit's samples say it: the sample change, the resource absent when read.</summary>
    public static readonly SystemChangeSet SampleChangeSet = new(PluginKit.SampleSystem, 1,
        [new SystemOperation("release", "create", null, "2", new SystemPrecondition(SystemPreconditions.Etag, null))],
        new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The two points, in <see cref="HookPoints.System"/>'s order.</summary>
    public static IReadOnlyList<KitPoint> Points { get; } =
    [
        new(
            HookPoints.PlanRead,
            "query",
            "Once a session in a system department plans, its intended changes, each a resource given a value or removed. Read the system "
            + "as it stands and type each: `create`, `update` or `delete`, its value before and after, and the version it read under the "
            + "system's precondition. Nothing here changes the system; a change it already holds is left out.",
            """`{ "operations": [{ "resource", "op", "before", "after", "precondition": { "kind", "version" } }] }`, a version null where the resource must still be absent.""",
            "the session's plan would be refused, naming this plugin, and nothing kept",
            HookPeer.DefaultPatience,
            Frame(SystemWire.Plan(PluginKit.SampleSystem, [SampleIntent])),
            """
              // A system's plan, read and never written: the kit's system.mjs types each change from read() above.
              'plan/read': system.points['plan/read'],
            """),
        new(
            HookPoints.ApplyPerform,
            "act",
            "Once a person accepted a plan on review, its operations, applied one applier at a time: under each one's precondition, every "
            + "version checked before any write. A system declaring its precondition `none` is sent one operation at a time, and Daoris "
            + "reads each resource just before and just after it.",
            """`{ "state": "applied", "applied": [ … ] }`, `{ "state": "unchanged", "applied": [] }` where the system already holds it all, `{ "state": "stale", "applied", "refused", "present": [{ "resource", "exists", "value", "version" }] }`, or `{ "state": "failed", "applied", "message" }`.""",
            "the apply would fail in the driver's words, naming this plugin",
            HookPeer.DefaultPatience,
            Frame(SystemWire.Apply(PluginKit.SampleSystem, SampleChangeSet)),
            """
              // A reviewed plan applied: the kit's system.mjs checks each precondition and calls write() above for each operation.
              'apply/perform': system.points['apply/perform'],
            """),
    ];

    /// <summary>Whether a plugin of these points adapts a system, so the kit gives it <see cref="Library"/> and a system to declare.</summary>
    internal static bool Adapts(IReadOnlyList<KitPoint> points) => points.Any(point => HookPoints.System.Contains(point.Name, StringComparer.Ordinal));

    /// <summary>The manifest's <c>systems</c>: one, guarded by <c>etag</c>, which its author renames and guards as the system does.</summary>
    internal static string Declared(string name, IReadOnlyList<KitPoint> points) => !Adapts(points) ? "" : $$"""

          "systems": [
            { "id": {{JsonSerializer.Serialize(SystemId)}}, "name": {{JsonSerializer.Serialize(name)}}, "preconditions": "{{SystemPreconditions.Etag}}" }
          ],
        """.TrimEnd().Replace("\r\n", "\n");

    /// <summary>The two calls an adapter's author writes, handed to the kit's <c>adapter()</c>; each starts by saying it is not written yet.</summary>
    internal static string Calls(string id, IReadOnlyList<KitPoint> points) => !Adapts(points) ? "" : $$"""

        // The system points (README.md, "A system"): the kit's system.mjs answers both and calls these two, the only part to write.
        // Under the kit's stand-in world, which its tests and `daoris plugin try` set, neither is called.
        const system = adapter({
          // A resource as the system holds it now, read with the plugin's read identity: { value, version }, the version what a write
          // is guarded by (an etag, a serial), or null where the resource is absent.
          read: async (name, resource) => { throw new Error(`{{id}} does not read system ${name} yet.`); },
          // One operation written under its precondition: 'written', or 'stale' where the system refused the version it read.
          write: async (name, operation) => { throw new Error(`{{id}} does not write to system ${name} yet.`); },
        });

        """.Replace("\r\n", "\n");

    /// <summary>The README's section on an adapter, and its file row; nothing for a plugin that adapts no system.</summary>
    internal static (string Section, string File) Readme(IReadOnlyList<KitPoint> points) => !Adapts(points) ? ("", "") : (
        "## A system\n\n"
        + $"`plugin.json` declares one system, `{SystemId}`, guarded by `{SystemPreconditions.Etag}`: rename it, and say what its writes are "
        + "guarded by (`etag`, `serial`, `changeset`, or `none` where the system guards nothing, which the person sees as unguarded). "
        + $"Write `read()` and `write()` in `{PluginKit.Script}`; `{Library}` types each plan and applies each reviewed one over them.\n\n",
        $"| `{Library}` | the kit's half of an adapter: both system points over `read()` and `write()`, the stand-in world; written by the kit, not by hand |\n");

    private static JsonObject Frame(object frame) => JsonSerializer.SerializeToNode(frame)!.AsObject();
}
