using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What a person's press means, as its route's own code reads it against the machine now: <see cref="Kind"/> the page's sentence
/// (<c>person.view.&lt;kind&gt;</c> in both catalogues), <see cref="Facts"/> the values that sentence names, and <see cref="Command"/>
/// the <c>daoris</c> command the press is, spelled for any shell. <see cref="Also"/> are further sentences the same press means, each worded
/// by its own kind. The card shows it above every value the press carries.
/// </summary>
public sealed record PressView(string Kind, IReadOnlyDictionary<string, string> Facts, string Command, IReadOnlyList<PressSaid>? Also = null)
{
    /// <summary>What only <c>SET_ROLE</c>'s card draws, as fields the page words: a place's rules, or a supervisor, before and after.</summary>
    public PressRules? Rules { get; init; }

    public PressSupervisor? Supervisor { get; init; }

    /// <summary>What only a plugin's card draws: what it runs, as its manifest writes it, or what an approval of its server names.</summary>
    public PressPlugin? Plugin { get; init; }

    public PressGrant? Grant { get; init; }
}

/// <summary>One more sentence of a press's meaning: <c>person.view.&lt;kind&gt;</c> with the values it names.</summary>
public sealed record PressSaid(string Kind, IReadOnlyDictionary<string, string> Facts)
{
    public PressSaid(string kind, params (string Name, string Value)[] facts)
        : this(kind, facts.ToDictionary(fact => fact.Name, fact => fact.Value, StringComparer.Ordinal))
    {
    }
}

public sealed record PressViewed(PressView? View, string? Refusal)
{
    public static PressViewed Refused(string why) => new(null, why);

    public static PressViewed Shown(PressView view) => new(view, null);
}

/// <summary>
/// One route's view of a press: judged at the ask with the route's own rules, so a press the route would refuse is answered to its
/// terminal in those words and never put to the person, and one it takes is shown on the card as what it means.
/// </summary>
/// <remarks>A view is a class of its own in this folder, registered in <see cref="PressViews.All"/> by one line.</remarks>
public interface IPressView
{
    /// <summary>The module the route is sent to.</summary>
    string Module { get; }

    /// <summary>The route, as the census names it.</summary>
    string Route { get; }

    /// <summary>The press judged against the machine as <paramref name="world"/> reads it now.</summary>
    Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken);
}

/// <summary>The routes whose presses the card shows as what they mean, one line each.</summary>
public static class PressViews
{
    public static readonly IReadOnlyList<IPressView> All =
    [
        new RegistryRewireView(),
        new FolderExcludesView(),
        new SessionArchiveView(),
        new SessionDeleteView(),
        new QuestDeleteView(),
        new AskDeleteView(),
        new WorkPauseView(),
        new WorkResumeView(),
        new AccountSettingsView(),
        new AccountUseView(),
        new AgentUpdateView(),
        PlaceSwitchView.Drivable(),
        PlaceSwitchView.Hold(),
        MachineSettingView.Notify(),
        MachineSettingView.Cap(),
        MachineSettingView.CoolOff(),
        MachineSettingView.Quiet(),
        MachineSettingView.Manifest(),
        MachineSettingView.Terminal(),
        new KeepRuleView(),
        new PlaceLineView(),
        new PlaceLanguageView(),
        new PlaceStandingView(),
        PlaceAcrossView.Read(),
        PlaceAcrossView.WriteTo(),
        new PlaceReviewView(),
        new PlaceOpinionView(),
        new PlaceHelpersView(),
        new PlaceCommitsView(),
        new RetryQuestView(),
        new GitUseView(),
        new BrowserFavoriteView(),
        new BrowserSettingView(),
        new RoleChangeView(),
        new PluginAddView(),
        new PluginInstallView(),
        new PluginUpdateView(),
        new PluginActionView(),
        new HistoryClearView(),
        new TreesSyncView(),
        new AskNewView(),
        new PlaceApplyView(),
    ];

    /// <summary>The view of <paramref name="module"/>'s <paramref name="route"/>; null where the route has none.</summary>
    public static IPressView? Find(string module, string route) => All.FirstOrDefault(each =>
        string.Equals(each.Module, module, StringComparison.OrdinalIgnoreCase) && string.Equals(each.Route, route, StringComparison.Ordinal));

    /// <summary>
    /// The press's view, read with its grounds at the ask: null where its route has none, or where the read failed, so the card shows
    /// its values alone rather than ask nothing. A cancel (a withdrawal or a close meanwhile) is let through, as a grounds read's is.
    /// </summary>
    public static async Task<PressViewed?> ReadAsync(
        IPipeGroundWorld world, string module, string route, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (Find(module, route) is not { } view) return null;
        try
        {
            return await view.ViewAsync(world, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
        {
            return null;
        }
    }
}

/// <summary>A registry row as a view reads it: the repository as the registry spells it, the workspace it is in, its kind and a folder's own excludes.</summary>
public sealed record PipeRegistered(string Repository, string? Workspace, string? Kind = null, IReadOnlyList<string>? Excludes = null);
