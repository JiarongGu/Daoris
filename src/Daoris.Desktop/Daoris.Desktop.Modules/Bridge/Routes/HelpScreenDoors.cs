using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The doors Ask Daoris's Apply goes through: each the code a screen's own route runs, so an Apply is what the screen would have done.
/// <paramref name="service"/> is the loop's, or null before it is up, when an ask or a delete is the cold-start sentence.
/// </summary>
internal sealed class HelpScreenDoors(DriverContext context, ServiceClient? service) : IHelpDoors
{
    private readonly DriverLoop _loop = context.Loop;

    public void Change(Func<DriverConfig, DriverConfig> edit) => context.Change(edit);

    public Task UseGitAsync(HelpGitUse use, CancellationToken ct) =>
        ToolsRoutes.UseGitAsync(_loop.Home, _loop.Service, use.Scope, use.Repository, use.Workspace, use.Remote, use.Host, use.Identity, ct);

    // The ask composer's door, the local host's `POST /api/asks`, with its review choice and words. A named
    // environment the workspace does not declare is refused before anything is sent, as `daoris ask --review` refuses
    // it: the service holds no rule, and the proposal's plan holds no repositories per workspace.
    public async Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var asks = service ?? throw NotReady();
        if (review is not null
            && await AskReviewCommand.UndeclaredAsync(review, workspace, new AskReviewWorld(asks, () => DriverConfig.Load(_loop.ConfigPath)), ct)
                .ConfigureAwait(false) is { } undeclared)
        {
            return new AskAnswer(false, undeclared, null, null);
        }

        return await asks.AskAsync(workspace, sentence, [], [], null, ct, review, reviewWords, shortTitle: shortTitle).ConfigureAwait(false);
    }

    // HISTORY_CLEAR's own clear of exactly the units the card listed: the driver library's one owner, each unit judged
    // again as it goes; the outcome comes back whole, and a service that does not answer throws before anything is removed.
    public Task<HistoryOutcome> ClearHistoryAsync(HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, CancellationToken ct) =>
        HistoryClearing.ClearAsync(HistoryRoutes.WorldOf(_loop), scope, id, units, PluginEvents.Screen, ct);

    public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        var toolchain = ManagedAgents.Of(_loop.Harnesses, harness);
        return context.Actions.StartProcessActionAsync(
            harness, action, profile: null, version, toolchain, config.Commands.GetValueOrDefault(harness),
            context.Actions.Relay(harness, action), config, ended);
    }

    public Task SetAccountUseAsync(string harness, UseChange change, CancellationToken ct)
    {
        var owner = AccountsRoutes.OwnerOf(_loop.Harnesses, harness);
        try
        {
            HarnessSettings.Change(
                _loop.Harnesses.SettingsPath, read => AccountsRoutes.UseEdited(read, owner, change, workspace: null), agents: _loop.Harnesses.Agents);
        }
        catch (ShenoraException refused)
        {
            throw new DriverException(refused.Message);
        }

        _loop.Nudge();
        return Task.CompletedTask;
    }

    // ACCOUNT_USE's own `order`: the set's order through the screen's own edit, the accounts named first,
    // or the ids' order again where cleared, written whole by the change and clearing no setting; then the loop asked to look.
    public Task SetAccountOrderAsync(string harness, IReadOnlyList<string> accounts, bool clear, CancellationToken ct)
    {
        var owner = AccountsRoutes.OwnerOf(_loop.Harnesses, harness);
        try
        {
            HarnessSettings.Change(_loop.Harnesses.SettingsPath, clear
                ? read => read.WithRotation(owner, null)
                : read => AccountsRoutes.OrderEdited(read, HarnessSettings.Profiles(_loop.Harnesses.Home, owner), owner, accounts, workspace: null),
                agents: _loop.Harnesses.Agents);
        }
        catch (ShenoraException refused)
        {
            throw new DriverException(refused.Message);
        }

        _loop.Nudge();
        return Task.CompletedTask;
    }

    public Task<bool> ReadyAccountAsync(string harness, string? account, CancellationToken ct)
    {
        var ended = _loop.Harnesses.Ready(harness, account);
        _loop.Nudge();
        return Task.FromResult(ended);
    }

    // TREES_SYNC_PLAN's own list: the same checkouts and sessions, and each line fetched as the person,
    // whose press this is — the look on Ask Daoris's card, never its proposal.
    // It takes what the screen's look takes: the repositories holding Daoris's branches, and the one a card names.
    public async Task<SyncPlan> SyncPlanAsync(string? repository, string? workspace, CancellationToken ct)
    {
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, repository, ct, workspace).ConfigureAwait(false);
        return await new SessionTrees(_loop.Home).SyncPlanAsync(repositories, inUse, fetch: true, ct, Named(repository))
            .ConfigureAwait(false);
    }

    // TREES_SYNC's own press: only the rows the look listed, fetching nothing, then the loop asked to look. The
    // sessions in use are asked again inside each repository's hold, as the screen's press asks them.
    public async Task<SyncDone> SyncAsync(string? repository, string? workspace, IReadOnlySet<string> only, CancellationToken ct)
    {
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, repository, ct, workspace).ConfigureAwait(false);
        var ledger = _loop.Service ?? throw NotReady();
        var done = await new SessionTrees(_loop.Home).SyncAsync(repositories, inUse, only, fetch: false, ct,
            inUseNow: async token => await LinesRoutes.InUseAsync(_loop.Home, ledger, token).ConfigureAwait(false), scope: Named(repository),
            beside: token => BranchTidying.BesideAsync(_loop.Home, ledger, token)).ConfigureAwait(false);
        _loop.Nudge();
        return done;
    }

    /// <summary>A card naming a repository includes it, as `--repository` does; none, and the default.</summary>
    private static SyncScope? Named(string? repository) => repository is null ? null : SyncScope.Named([repository]);

    // BrowserModule's own edits: the same check, the same refusal for a file it could not read, the
    // same write. A route's refusal is its words as the driver's, so the card settles refused with them.
    public void ChangeBrowser(string setting, string value, string? address, string? title)
    {
        var home = BrowserModule.Home;
        try
        {
            switch (setting, value)
            {
                case ("use", _): BrowserModule.SetBrowser(home, value); break;
                case ("links", _): BrowserModule.SetLinks(home, value); break;
                case ("extensions", _): BrowserModule.SetExtensions(home, value); break;
                case ("favorite", "add"): BrowserModule.AddFavorite(home, address ?? "", title); break;
                case ("favorite", "remove"): BrowserModule.RemoveFavorite(home, address ?? ""); break;
                default: throw new DriverException($"`{setting} {value}` is not a change the Browser screen makes.");
            }
        }
        catch (ShenoraException refused)
        {
            throw new DriverException(refused.Message);
        }
    }

    // `daoris plugin add`'s copy, the driver's twin; the loop is asked to look, and it starts
    // what the plugin runs at that look, as it does any plugin. Nothing runs here.
    public void AddPlugin(string folder)
    {
        PluginInstall.Add(_loop.Home, folder, ProviderRegistry.Built().Names);
        _loop.Nudge();
    }

    // PLUGIN_ACTION's own enable|disable: the same lookup, the same row, the same nudge.
    public void SwitchPlugin(string id, bool on)
    {
        PluginsRoutes.SwitchPlugin(_loop, PluginsRoutes.InstalledPlugin(_loop, id), on);
        _loop.Nudge();
    }

    // PLUGIN_INSTALL's own copy of one of the install's offers; nothing runs here.
    public void AddOffer(string id)
    {
        PluginInstall.AddOffer(_loop.Home, context.OffersFolder, id, ProviderRegistry.Built().Names);
        _loop.Nudge();
    }

    // PLUGIN_UPDATE's own apply: the hook stopped, the folder swapped, the loop asked to look.
    public async Task UpdatePluginAsync(string id, CancellationToken ct) =>
        await PluginsRoutes.UpdatePluginAsync(context, id).ConfigureAwait(false);

    // PLUGIN_ACTION's own help-approve and help-withdraw: the person's approval, written as the page writes it.
    public void ApproveHelp(string plugin, string server, IReadOnlyList<string> writes) =>
        HelpGrants.Approve(_loop.Home, PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names), plugin, server, writes);

    public void WithdrawHelp(string plugin, string server) => HelpGrants.Withdraw(_loop.Home, plugin, server);
}
