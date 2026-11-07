using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The spawn, prepared for either kind of session: the adapter's process, the toolchain's environment on it, and for a dsh home
/// Daoris made, its profile written. Shared by a quest's session and an ask's intake, so neither can forget a line the other carries.
/// </summary>
internal sealed class SessionEquipment(string home, DriverConfig config)
{
    /// <returns>The process to start, and what this harness is doing that Daoris could not govern.</returns>
    /// <param name="prepared">The process a resume prepared, given the same environment; null prepares a start.</param>
    public (ProcessStartInfo Info, string? Notice) Prepare(
        ProviderRegistry adapters, AgentDoor adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null)
    {
        target = TargetPrompt.ForStart(target, adapters, adapter);
        // A standing block past what the first prompt carries goes through its file, written before the spawn reads it.
        if (prepared is null) StandingBlocks.Hand(target);
        // The driver's own home, never the process's: a driver run on another home's configuration, or beside a test's, would hand its
        // sessions that other home's tools and git holds.
        var info = prepared ?? adapters.Start(adapter, Request(adapter, target with { Home = target.Home ?? home }));
        SpawnEquipment.Equip(adapters, adapter, info, selection);

        // Only a dsh home Daoris made is written to; anywhere else the notice says what happens, and a home holding somebody
        // else's patch layer is reported rather than overwritten.
        var harnessNotice = DshProfile.NoticeFor(adapter.Id.Value, selection.ProfileHome);
        if (harnessNotice is null && DshProfile.IsFor(adapter.Id.Value) && selection.ProfileHome is { Length: > 0 } dshHome)
        {
            try
            {
                DshProfile.Write(dshHome);
            }
            catch (DriverException refused)
            {
                harnessNotice = $"— {refused.Message}";
            }
        }

        return (info, harnessNotice);
    }

    /// <summary>What a start of <paramref name="target"/> asks of <paramref name="adapter"/>'s spawn, under the machine's command for it.</summary>
    public SpawnRequest Request(AgentDoor adapter, SessionTarget target) =>
        SpawnRequests.Of(target, config.Commands.GetValueOrDefault(adapter.Id.Value));

    /// <summary>
    /// Whether the rules this session would be handed let it call the connector's <paramref name="tool"/> unasked: the same
    /// composition <see cref="SessionRules.Hand"/> hands over. False for a harness that takes no rules, which then depends on the repository's own list.
    /// </summary>
    public bool HandedConnector(AgentDoor adapter, string? workspace, string? repository, string tool) =>
        adapter.Can.TakesSettings
        && PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.Load(home), workspace, repository), tool);
}
