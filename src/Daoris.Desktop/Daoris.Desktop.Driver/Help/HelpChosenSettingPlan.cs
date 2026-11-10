using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The edit a granted chosen setting makes (<see cref="ConfiguredAccess.SettingsBuilt"/>): notify, a session language, the session cap,
/// the cool-off and the quiet bound, each judged as its pressed route judges it and said in the terminal verb's words.
/// </summary>
internal static class HelpChosenSettingPlan
{
    public static HelpPlan Plan(string setting, string? repository, string? workspace, string value, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        static HelpPlan Planned(string describe, string terminal, Func<DriverConfig, DriverConfig> edit) => new(null, describe, terminal, edit);

        switch (setting)
        {
            case ConfiguredAccess.Notify when value is "on" or "off":
                return Planned(value == "on" ? "Say so when a session parks, or ends without being asked." : "Stop saying so when a session parks.",
                    $"daoris settings notify {value}", c => c.WithNotify(value == "on"));
            case ConfiguredAccess.Cap when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) && cap >= 1:
                return Planned(cap == 1 ? "Run one session at a time on this machine, across every repository."
                        : $"Run at most {cap} sessions at once on this machine, across every repository.",
                    $"daoris settings cap {cap}", c => c.WithCap(cap));
            case ConfiguredAccess.CoolOff when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cool) && cool >= 1:
                return Planned($"An account whose limit names no time cools for {cool} minute{(cool == 1 ? "" : "s")} before it is offered again.",
                    $"daoris settings cooloff {cool}", c => c.WithCoolOff(cool));
            case ConfiguredAccess.Quiet when long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var quiet) && QuietSetting.Takes(quiet):
                return Planned($"Look in on a session that says nothing and runs nothing for {quiet} minutes; none is ever stopped for running long.",
                    $"daoris settings quiet {quiet}", c => c.WithQuiet((int)quiet));
            case ConfiguredAccess.Language:
                return Language(repository?.Trim(), workspace?.Trim(), value, facts);
            default:
                return Refused(setting switch
                {
                    ConfiguredAccess.Notify => "`notify` is `on` or `off`.",
                    ConfiguredAccess.Cap => DriverConfig.CapRefusal,
                    ConfiguredAccess.CoolOff => DriverConfig.CoolOffRefusal,
                    ConfiguredAccess.Quiet => QuietSetting.Refusal,
                    _ => $"`{setting}` is not a setting a person grants.",
                });
        }
    }

    private static HelpPlan Language(string? repository, string? workspace, string value, HelpMachineFacts facts)
    {
        var circle = workspace is { Length: > 0 };
        if (circle == repository is { Length: > 0 }) return new("a session language is set for a repository or a workspace — name exactly one.", "", "", null);
        if (repository is { Length: > 0 } && !facts.Repositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
        {
            return new($"`{repository}` is not registered on this machine — use a repository's name as Repositories lists it.", "", "", null);
        }

        if (circle && !facts.Workspaces.Contains(workspace!, StringComparer.OrdinalIgnoreCase))
        {
            return new($"there is no workspace `{workspace}` on this machine.", "", "", null);
        }

        var code = value == "--clear" ? null : SessionLanguages.Code(value);
        if (value != "--clear" && code is null)
        {
            return new(value.Length == 0
                ? "`language` names a language, `en` or `zh`, or `--clear` — e.g. `daoris place language engine zh`."
                : SessionLanguages.Refusal(value), "", "", null);
        }

        var language = code is null ? null : SessionLanguages.NameOf(code);
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : repository!;
        return new(null,
            language is null
                ? circle
                    ? $"Clear workspace `{workspace}`'s session language: each repository there keeps its own, else none."
                    : $"Clear `{repository}`'s session language: it takes its workspace's again, else none."
                : (circle
                    ? $"Sessions in each repository of workspace `{workspace}` that sets none of its own write to you in {language}"
                    : $"Sessions in `{repository}` write to you in {language}")
                  + ": a question to you, a closing note, a decline's reason, their last words. The window's own language stays yours, in Settings → Appearance.",
            $"daoris place language {scope} {code ?? "--clear"}",
            c => circle ? c.WithWorkspaceLanguage(workspace!, code) : c.WithLanguage(repository!, code));
    }
}
