using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's chosen settings (HELPMODE1c3; the Ask Daoris modes design §4.1): five typed tools over the ordinary settings, each a
/// request the driver judges, applies through the setting's own checks and writer, and answers (<see cref="SettingsBox"/>). None
/// takes a path, and none reaches the mode, a grant, an account or a safeguard. Each acts only as the person granted it, which the
/// driver reads at every call.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the request is written and its answer read, under the home.</param>
[McpServerToolType]
public sealed class ChosenSettingsTools(IntakeScope? intake = null, SessionLedger? ledger = null, SettingsBox? box = null)
{
    private const string Granted =
        "Ask Daoris only, and only where the person granted this setting under Ask Daoris's configured access (chosen settings): "
        + "otherwise it is refused, saying so, and `setting_propose` proposes it for their Apply instead. ";

    private const string Applies =
        " It changes the setting at once, with no Apply, through the same checks and writer as Settings, and the change is recorded. "
        + "Say what you changed, and to what.";

    [McpServerTool(Name = "settings_notify")]
    [Description(Granted + "Choose whether Daoris says so when a session parks, or ends without being asked." + Applies)]
    public Task<string> NotifyAsync(
        [Description("True to say so when a session parks or ends without being asked; false to stop saying so.")] bool on,
        CancellationToken ct = default) =>
        SetAsync(SettingsBox.Notify, on ? "on" : "off", null, null, ct);

    [McpServerTool(Name = "settings_language")]
    [Description(
        Granted + "Set the language a repository's or a workspace's sessions write to the person in: a question to them, a closing "
        + "note, a decline's reason, their last words. The work's language, never the window's, which stays the viewer's own. Name a "
        + "repository or a workspace, exactly one." + Applies)]
    public Task<string> LanguageAsync(
        [Description("`en` or `zh`, or `clear` to take the repository's or the workspace's own away.")] string language,
        [Description("The repository, by its registered name. Name this or workspace, not both.")] string? repository = null,
        [Description("The workspace, for every repository in it that sets none of its own.")] string? workspace = null,
        CancellationToken ct = default) =>
        SetAsync(SettingsBox.Language, language, repository, workspace, ct);

    [McpServerTool(Name = "settings_timeout")]
    [Description(Granted + "Set how many minutes one session may run on this machine, at least one." + Applies)]
    public Task<string> TimeoutAsync(
        [Description("The minutes, a whole number of at least 1.")] int minutes,
        CancellationToken ct = default) =>
        SetAsync(SettingsBox.Timeout, Number(minutes), null, null, ct);

    [McpServerTool(Name = "settings_cap")]
    [Description(Granted + "Set how many sessions run at once on this machine, across every repository, at least one." + Applies)]
    public Task<string> CapAsync(
        [Description("The sessions at once, a whole number of at least 1.")] int sessions,
        CancellationToken ct = default) =>
        SetAsync(SettingsBox.Cap, Number(sessions), null, null, ct);

    [McpServerTool(Name = "settings_cooloff")]
    [Description(
        Granted + "Set how many minutes an account whose limit names no time cools before it is offered again, at least one."
        + Applies)]
    public Task<string> CoolOffAsync(
        [Description("The minutes, a whole number of at least 1.")] int minutes,
        CancellationToken ct = default) =>
        SetAsync(SettingsBox.CoolOff, Number(minutes), null, null, ct);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<string> SetAsync(string setting, string value, string? repository, string? workspace, CancellationToken ct)
    {
        var settings = box ?? SettingsBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return await settings.SetAsync(record, session, setting, value, repository, workspace, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }
}
