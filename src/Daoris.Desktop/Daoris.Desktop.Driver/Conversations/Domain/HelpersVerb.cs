using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver helpers</c> (MIXAGENT2a-i; FOUNDATION1 mod-iii): the participants' own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rule it edits (<see cref="HelperRules"/>).
/// </summary>
public sealed class HelpersVerb : ChoiceVerb
{
    public override string Word => "helpers";

    /// <summary><c>helpers</c> (MIXAGENT2a-i): what a helper an agent asks for does here now.</summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode", "--agents"];
        const string usage = "`driver helpers` needs <repository>|--workspace <name>, then --mode ask|on|off and/or --agents <adapter,adapter>, "
            + "or --clear — e.g. `daoris driver helpers web-app --mode on --agents codex-acp`.";
        Flags(args, valued, ["--clear"], "helpers", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        var agents = FlagValue(args, "--agents");
        var set = new JsonObject();
        if (mode is not null) set["mode"] = mode;
        if (agents is not null) set["agents"] = new JsonArray([.. agents.Split(',').Select(each => (JsonNode?)each.Trim())]);
        if (string.IsNullOrEmpty(name) || (!clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (clear) payload["clear"] = true;
        var edit = HelperRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => HelperRules.Apply(before, edit));
        return new("helpers", ChoiceRoutes.Helpers, payload, _ =>
        {
            StandingChoices.Write(path, config => HelperRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceHelpers : after.Helpers;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceHelpers : before.Helpers).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own helpers rule, else each helper waits for your yes."
                    : $"daoris: `{key}` takes its workspace's helpers rule again, else each helper waits for your yes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: helpers for repositories in the workspace `{key}` that set none of their own: {HelperRules.Listed(rule)}."
                    : $"daoris: helpers for `{key}`: {HelperRules.Listed(rule)}.");
                Say($"  {HelperRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, rule) in choices.Helpers) Say($"  helpers    {repository}  {HelperRules.Listed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceHelpers) Say($"  helpers    workspace {workspace}  {HelperRules.Listed(rule)}  (for each repository there that sets none)");
        if (choices.Helpers.Count == 0 && choices.WorkspaceHelpers.Count == 0)
        {
            Say("  helpers    none set — each helper an agent asks for waits for your yes; `daoris driver helpers <repository> "
                + "--mode on|off` sets one");
        }
    }
}
