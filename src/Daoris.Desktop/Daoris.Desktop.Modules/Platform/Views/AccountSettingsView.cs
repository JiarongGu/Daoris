using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris agent settings &lt;agent&gt; --account &lt;name&gt; model … effort …</c> as the card shows it: one of Daoris's accounts' own
/// model and effort, written to the tool's own settings file. Refused before any card where the agent is unknown here, keeps its settings
/// in files Daoris does not know, has no such account, or is handed a value the tool would not read; a key the press leaves out is
/// untouched, and a null clears it to the tool's own default.
/// </summary>
public sealed class AccountSettingsView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_AGENT_SETTINGS";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var agent = PressPayload.Text(payload, "door");
        var account = PressPayload.Text(payload, "profile");
        var roster = await world.ReadAsync(DriverModule.Name, "HARNESSES", null, cancellationToken).ConfigureAwait(false);
        var door = PressPayload.Rows(roster, "harnesses").FirstOrDefault(row => PressPayload.Field(row, "door") == agent);
        if (door.ValueKind != JsonValueKind.Object)
        {
            var names = string.Join(", ", PressPayload.Rows(roster, "harnesses").Select(row => PressPayload.Field(row, "door")));
            return PressViewed.Refused($"there is no agent `{agent}` on this machine — one of {names}.");
        }

        var owner = PressPayload.Field(door, "accountOf") is { Length: > 0 } of ? of : agent;
        if (!door.TryGetProperty("settingsChoices", out var choices) || choices.ValueKind != JsonValueKind.Object)
        {
            return PressViewed.Refused($"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, so Daoris "
                + "offers none — set its model with the tool itself.");
        }

        if (account.Length == 0)
        {
            return PressViewed.Refused($"name the account these settings are for — `{owner}`'s own configuration home is the tool's, and Daoris never touches it.");
        }

        var accounts = PressPayload.Rows(door, "profiles").Select(row => PressPayload.Field(row, "name")).ToList();
        if (!accounts.Contains(account, StringComparer.Ordinal))
        {
            return PressViewed.Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var (model, effort) = (Sent(payload, "model"), Sent(payload, "effort"));
        if (model is null && effort is null) return PressViewed.Refused("the change sets a model, an effort, or both.");
        try
        {
            if (model?.Value is { } setModel) AgentSettings.JudgeModel(setModel);
            if (effort?.Value is { } setEffort) AgentSettings.JudgeEffort(setEffort);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = owner, ["account"] = account };
        var command = $"daoris agent settings {owner} --account {ShellWord.Of(account, ShellWord.Account)}";
        if (model is not null)
        {
            facts["model"] = model.Value ?? "";
            command += $" model {model.Value ?? "unset"}";
        }

        if (effort is not null)
        {
            facts["effort"] = effort.Value ?? "";
            command += $" effort {effort.Value ?? "unset"}";
        }

        var kind = (model, effort) switch
        {
            (not null, not null) => "account-settings",
            (not null, null) => "account-settings-model",
            _ => "account-settings-effort",
        };
        return PressViewed.Shown(new PressView(kind, facts, command));
    }

    /// <summary>A key the press sent: its text, or none (the tool's own default) where it is null or the word <c>unset</c>; null where it is left out.</summary>
    private static AgentSettingEdit? Sent(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty(name, out var value)) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : null;
        return new AgentSettingEdit(text is null or "unset" ? null : text);
    }
}
