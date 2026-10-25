using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver handover</c> (MIXAGENT1c; FOUNDATION1 mod-iii): the course's own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rule it edits (<see cref="HandOverRules"/>).
/// </summary>
public sealed class HandOverVerb : ChoiceVerb
{
    public override string Word => "handover";

    /// <summary><c>handover</c> (MIXAGENT1c): what an agent's ask to hand its session over does here now.</summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode"];
        const string usage = "`driver handover` needs <repository>|--workspace <name>, then --mode ask|on|off, or --clear — e.g. "
            + "`daoris driver handover web-app --mode on`.";
        Flags(args, valued, ["--clear"], "handover", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        if (string.IsNullOrEmpty(name) || (!clear && mode is null)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (mode is not null) payload["set"] = new JsonObject { ["mode"] = mode };
        if (clear) payload["clear"] = true;
        var edit = HandOverRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => HandOverRules.Apply(before, edit));
        return new("handover", ChoiceRoutes.HandOver, payload, _ =>
        {
            StandingChoices.Write(path, config => HandOverRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceHandOver : after.HandOver;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceHandOver : before.HandOver).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own hand-over rule, else an agent's ask waits for your yes."
                    : $"daoris: `{key}` takes its workspace's hand-over rule again, else an agent's ask waits for your yes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: hand-over for repositories in the workspace `{key}` that set none of their own: {rule.Mode}."
                    : $"daoris: hand-over for `{key}`: {rule.Mode}.");
                Say($"  {HandOverRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, rule) in choices.HandOver) Say($"  hand-over  {repository}  {rule.Mode}");
        foreach (var (workspace, rule) in choices.WorkspaceHandOver) Say($"  hand-over  workspace {workspace}  {rule.Mode}  (for each repository there that sets none)");
        if (choices.HandOver.Count == 0 && choices.WorkspaceHandOver.Count == 0)
        {
            Say("  hand-over  none set — an agent's ask to hand its session over waits for your yes; `daoris driver handover "
                + "<repository> --mode on|off` sets one");
        }
    }
}
