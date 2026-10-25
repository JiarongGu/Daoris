using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver commits</c> (PROCESS1a; FOUNDATION1 mod-iii): the workplace's own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rule it edits (<see cref="CommitRules"/>).
/// </summary>
public sealed class CommitsVerb : ChoiceVerb
{
    public override string Word => "commits";

    /// <summary><c>commits</c> (PROCESS1a): how work is committed here now.</summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode", "--convention"];
        const string usage = "`driver commits` needs <repository>|--workspace <name>, then --mode as-you-go|ask|at-end and/or "
            + "--convention \"<your words>\" (one paragraph, at most 400 characters) or --no-convention, or --clear — "
            + "e.g. `daoris driver commits web-app --mode ask --convention \"Imperative subject line.\"`.";
        Flags(args, valued, ["--clear", "--no-convention"], "commits", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        // `--no-convention` is the empty convention, which takes the standing one away (a flag cannot carry an empty value).
        if (args.Contains("--no-convention") && args.Contains("--convention")) throw new ChoiceWords(usage);
        var convention = args.Contains("--no-convention") ? "" : FlagValue(args, "--convention");
        var set = new JsonObject();
        if (mode is not null) set["mode"] = mode;
        if (convention is not null) set["convention"] = convention;
        if (string.IsNullOrEmpty(name) || (!clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (clear) payload["clear"] = true;
        var edit = CommitRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => CommitRules.Apply(before, edit));
        return new("commits", ChoiceRoutes.Commits, payload, _ =>
        {
            StandingChoices.Write(path, config => CommitRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceCommits : after.Commits;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceCommits : before.Commits).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own commit rule, else work is committed as it goes."
                    : $"daoris: `{key}` takes its workspace's commit rule again, else work is committed as it goes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: commits for repositories in the workspace `{key}` that set none of their own: {CommitRules.Listed(rule)}"
                    : $"daoris: commits for `{key}`: {CommitRules.Listed(rule)}");
                Say($"  {CommitRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, rule) in choices.Commits) Say($"  commits    {repository}  {CommitRules.Listed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceCommits) Say($"  commits    workspace {workspace}  {CommitRules.Listed(rule)}  (for each repository there that sets none)");
        if (choices.Commits.Count == 0 && choices.WorkspaceCommits.Count == 0)
        {
            Say("  commits    none set — work is committed as it goes; `daoris driver commits <repository> "
                + "--mode ask|at-end` sets one");
        }
    }
}
