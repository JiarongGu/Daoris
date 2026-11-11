namespace Daoris.Driver;

/// <summary>How a removal's <c>forget</c> ended: the plugin declares none, it said it signed out, or it did not confirm.</summary>
public enum ForgetOutcome
{
    None,
    Confirmed,
    NotConfirmed,
}

/// <summary>
/// The sign-out a plugin asks for at its removal: the command its <c>storage.forget</c> names, run once as a round <c>from: removal</c>
/// at the machine before the plugin's files go, bound to <see cref="Bound"/>. Best effort: a plugin that fails, asks, or does not end in
/// time is <see cref="ForgetOutcome.NotConfirmed"/> and its removal goes on, since a removal is the person's and never waits on a plugin.
/// </summary>
public static class PluginForget
{
    /// <summary>How long the command is given.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>The plugin's forget run, if it declares a sound one.</summary>
    public static ForgetOutcome Run(string home, PluginEntry plugin, PluginCommandRun.Starter? start = null)
    {
        var name = plugin.Manifest.Interfaces.Storage?.Forget;
        if (name is null || plugin.Problem is not null || plugin.Manifest.Hooks is null) return ForgetOutcome.None;
        if (plugin.Manifest.Interfaces.Commands.FirstOrDefault(each => each.Name == name) is not { Interactive: false } command) return ForgetOutcome.None;
        using var bound = new CancellationTokenSource(Bound);
        try
        {
            var call = new CommandCall(command.Name, CommandCall.NewRun(), new Dictionary<string, object?>(), new Dictionary<string, string>(),
                CommandWire.Removal, PluginPlace.Machine, "en");
            var round = Task.Run(() => PluginCommandRun.RoundAsync(home, plugin, call, bound.Token, start), bound.Token).GetAwaiter().GetResult();
            return round.Ask is null && round.Exit == 0 ? ForgetOutcome.Confirmed : ForgetOutcome.NotConfirmed;
        }
        catch (Exception failed) when (failed is DriverException or OperationCanceledException or IOException)
        {
            return ForgetOutcome.NotConfirmed;
        }
    }
}
