using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris agent settings &lt;agent&gt; --account &lt;name&gt; model … effort …</c> as the card shows it: one of Daoris's accounts' own
/// model and effort, written to the tool's own settings file. Refused before any card where the agent is unknown here, keeps its settings
/// in files Daoris does not know, has no such account, or is handed a value the tool would not read; an effort for one model (`--for`) is
/// shown as a sentence of its own beside the rest; a key the press leaves out is
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

        var (model, effort, perModel) = (Sent(payload, "model"), Sent(payload, "effort"), PerModel(payload));
        if (model is null && effort is null && perModel.Count == 0) return PressViewed.Refused("the change sets a model, an effort, or both.");
        if (payload is { ValueKind: JsonValueKind.Object } sent && sent.TryGetProperty("perModel", out var models) && models.ValueKind == JsonValueKind.Object
            && models.EnumerateObject().Any(entry => entry.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
        {
            return PressViewed.Refused("the effort for a model is set with a name, or cleared with null.");
        }

        try
        {
            if (model?.Value is { } setModel) AgentSettings.JudgeModel(setModel);
            if (effort?.Value is { } setEffort) AgentSettings.JudgeEffort(setEffort);
            foreach (var (name, each) in perModel)
            {
                AgentSettings.JudgeModel(name);
                if (each.Value is { } setFor) AgentSettings.JudgeEffort(setFor);
            }
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

        var said = new List<PressSaid>();
        foreach (var (name, each) in perModel)
        {
            said.Add(new PressSaid("account-settings-for", ("agent", owner), ("account", account), ("model", name.Trim()), ("effort", each.Value ?? "")));
            command += $" effort {each.Value ?? "unset"} --for {ShellWord.Of(name.Trim(), "<model>")}";
        }

        string? kind = (model, effort) switch
        {
            (not null, not null) => "account-settings",
            (not null, null) => "account-settings-model",
            (null, not null) => "account-settings-effort",
            _ => null,
        };
        if (kind is null)
        {
            kind = said[0].Kind;
            facts = new Dictionary<string, string>(said[0].Facts, StringComparer.Ordinal);
            said.RemoveAt(0);
        }

        return PressViewed.Shown(new PressView(kind, facts, command, said.Count > 0 ? said : null));
    }

    /// <summary>The efforts the press sets for one model each, a null clearing it, as the route reads them: the model's name trimmed once judged.</summary>
    private static List<KeyValuePair<string, AgentSettingEdit>> PerModel(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("perModel", out var models) && models.ValueKind == JsonValueKind.Object
            ? [.. models.EnumerateObject().Where(entry => entry.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null).Select(entry =>
                KeyValuePair.Create(entry.Name, new AgentSettingEdit(entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString()!.Trim() : null)))]
            : [];

    /// <summary>A key the press sent: its text, or none (the tool's own default) where it is null or the word <c>unset</c>; null where it is left out.</summary>
    private static AgentSettingEdit? Sent(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty(name, out var value)) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : null;
        return new AgentSettingEdit(text is null or "unset" ? null : text);
    }
}
