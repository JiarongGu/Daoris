using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A packet's reader held packet-only (<see cref="OpinionPosture.PacketOnly"/>): on a harness that takes Daoris's rules, its tools turned
/// off by them (<see cref="PacketOnlyPermissions"/>), handed the person's tier and its own file alone, no server and none of the person's
/// own tool home; on any other, <see cref="OpinionPosture.NotEnforced"/>, handed nothing, and its choice passed by the walk, so no
/// reader runs holding its tools.
/// </summary>
internal static class PacketOnlyRules
{
    /// <summary>Whether a reader on <paramref name="adapter"/> can be held packet-only, in the words its record keeps.</summary>
    public static string Posture(ProviderRegistry adapters, AgentDoor adapter) =>
        adapters.RuleLanguage(adapter) is not null && adapter.Can.TakesSettings ? OpinionPosture.PacketOnly : OpinionPosture.NotEnforced;

    /// <summary>
    /// The choices of <paramref name="order"/> whose harness holds a reader packet-only, in their order, and the agents passed as not
    /// enforced, an agent no door answers among them.
    /// </summary>
    public static (IReadOnlyList<ActorEffective> Held, IReadOnlyList<string> Passed) Readers(ProviderRegistry adapters, IReadOnlyList<ActorEffective> order)
    {
        var (held, passed) = (new List<ActorEffective>(), new List<string>());
        foreach (var choice in order)
        {
            AgentDoor door;
            try
            {
                door = adapters.Door(choice.Agent);
            }
            catch (DriverException)
            {
                passed.Add(choice.Agent);
                continue;
            }

            if (Posture(adapters, door) == OpinionPosture.PacketOnly) held.Add(choice);
            else passed.Add(choice.Agent);
        }

        return (held, passed);
    }

    /// <summary>
    /// The reader's rules file, what its protocol door carries and the account of what is handed; null where its harness cannot hold it
    /// packet-only, and then nothing is written or handed.
    /// </summary>
    /// <param name="folder">The reader's empty folder of its own, to which the tree guard holds it.</param>
    public static (string? File, object? Meta, HandedSection Handed)? Hand(
        string home, ProviderRegistry adapters, AgentDoor adapter, ProcessStartInfo info, string sessionId, string folder)
    {
        if (Posture(adapters, adapter) != OpinionPosture.PacketOnly || adapters.RuleLanguage(adapter) is not { } language) return null;
        GitSessionAccess.Begin(home, sessionId, repository: null, workspace: null);
        var rules = PermissionRules.Load(home);
        var shaped = PacketOnlyPermissions.Shape(PermissionRules.Compose(rules, null, null));
        shaped = shaped with { Deny = [.. shaped.Deny.Union([GitSessionHolds.ProfileDeny(home)], StringComparer.Ordinal)] };
        var file = language.Write(home, sessionId, new HandedRules(
            shaped, [.. PermissionRules.HardDeny(rules), PacketOnlyPermissions.HardDeny], TreeGuard.For(home, folder), ChildLineage.PathOf(home, sessionId))
        {
            Folder = folder,
            Shaped = true,
        });
        var handed = SessionRules.Handed(takes: true, rules, shaped, workspace: null, repository: null);
        if (adapter.Can.RulesOnWire) return (file, adapter.AcpSourcesAloneMeta(file), handed);
        adapter.Spawned().HandSettings(info, file);
        adapter.Spawned().HandSourcesAlone(info);
        return (file, null, handed);
    }
}
