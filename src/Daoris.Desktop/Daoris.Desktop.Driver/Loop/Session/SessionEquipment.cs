using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The spawn, prepared for either kind of session: the adapter's process, the toolchain's environment on it, and for a dsh home
/// Daoris made, its profile written. Shared by a quest's session and an ask's intake, so neither can forget a line the other carries.
/// </summary>
internal sealed class SessionEquipment(string home, DriverConfig config, Func<string, string?> variable)
{
    /// <returns>The process to start, and what this harness is doing that Daoris could not govern.</returns>
    /// <param name="prepared">The process a resume prepared, given the same environment; null prepares a start.</param>
    public (ProcessStartInfo Info, string? Notice) Prepare(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null)
    {
        target = TargetPrompt.ForStart(target, adapter);
        // A standing block past what the first prompt carries goes through its file, written before the spawn reads it.
        if (prepared is null) StandingBlocks.Hand(target);
        var info = prepared ?? adapter.Prepare(target with { Home = target.Home ?? DaorisHome.Resolve(variable) }, config.Commands.GetValueOrDefault(adapter.Name));
        SpawnEquipment.Equip(adapter, info, selection);

        // Only a dsh home Daoris made is written to; anywhere else the notice says what happens, and a home holding somebody
        // else's patch layer is reported rather than overwritten.
        var harnessNotice = DshProfile.NoticeFor(adapter.Name, selection.ProfileHome);
        if (harnessNotice is null && adapter is DshAdapter && selection.ProfileHome is { Length: > 0 } dshHome)
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

    /// <summary>
    /// Whether the rules this session would be handed let it call the connector's <paramref name="tool"/> unasked (D73): the same
    /// composition <see cref="SessionRules.Hand"/> hands over. False for a harness that takes no rules, which then depends on the repository's own list.
    /// </summary>
    public bool HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        adapter.TakesSettings
        && PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.Load(home), workspace, repository), tool);
}
