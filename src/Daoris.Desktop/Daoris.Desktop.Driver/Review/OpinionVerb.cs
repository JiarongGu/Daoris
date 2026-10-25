using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver opinion</c> (XAGENT1a; FOUNDATION1 mod-iii): the review's own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rule it edits (<see cref="OpinionRules"/>). The agents a reviewer is judged the
/// working agent's family over, plugins' harnesses among them, are handed it by Loop (<paramref name="adapters"/>, by the home).
/// </summary>
/// <param name="adapters">The machine's agents with the plugins beside the file's home as the driver reads them (XAGENT1b2).</param>
public sealed class OpinionVerb(Func<string, AdapterSet> adapters) : ChoiceVerb
{
    public override string Word => "opinion";

    /// <summary>
    /// <c>opinion</c> (XAGENT1a): the person's words made one edit, judged by the rules' table, and said in the driver's sentences, the
    /// working agent's own family named among the reviewers, then what the gate does with it (XAGENT1f4).
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--reviewers", "--on", "--minutes"];
        string[] switches = ["--required", "--not-required", "--verify", "--no-verify", "--recheck", "--no-recheck", "--clear"];
        const string usage = "`driver opinion` needs <repository>|--workspace <name>, then --reviewers <adapter,adapter> [--on landing,steps] "
            + "[--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck], or none, or --clear — "
            + "e.g. `daoris driver opinion web-app --reviewers codex-acp --required`.";
        Flags(args, valued, switches, "opinion", usage);

        bool? Both(string on, string off, string why)
        {
            if (args.Contains(on) && args.Contains(off)) throw new ChoiceWords($"{why} — say `{on}` or `{off}`, not both.");
            return args.Contains(on) ? true : args.Contains(off) ? false : null;
        }

        var required = Both("--required", "--not-required", "a rule is required or not");
        var verify = Both("--verify", "--no-verify", "a reviewer may build and run or it may not");
        var recheck = Both("--recheck", "--no-recheck", "the answers are read again or they are not");
        var workspace = FlagValue(args, "--workspace");
        var words = Operands(args, valued).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var none = words.ElementAtOrDefault(0) == "none";
        var clear = args.Contains("--clear");
        JsonArray? Listed(string flag) => FlagValue(args, flag) is { } given ? [.. given.Split(',').Select(each => (JsonNode?)each.Trim())] : null;
        var reviewers = Listed("--reviewers");
        var on = Listed("--on");
        var minutes = FlagValue(args, "--minutes");
        var set = new JsonObject();
        if (reviewers is not null) set["reviewers"] = reviewers;
        if (on is not null) set["on"] = on;
        if (required is not null) set["required"] = required;
        if (verify is not null) set["verify"] = verify;
        // A number only as digits: anything else is refused in the rule's own words.
        if (minutes is not null) set["minutes"] = minutes.All(char.IsAsciiDigit) && minutes.Length > 0 ? JsonValue.Create(double.Parse(minutes, CultureInfo.InvariantCulture)) : minutes;
        if (recheck is not null) set["recheck"] = recheck;
        if (string.IsNullOrEmpty(name) || words.Count > (none ? 1 : 0) || (!none && !clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (none) payload["none"] = true;
        if (clear) payload["clear"] = true;
        var edit = OpinionRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => OpinionRules.Apply(before, edit));
        return new("opinion", ChoiceRoutes.Opinion, payload, _ =>
        {
            StandingChoices.Write(path, config => OpinionRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceOpinions : after.Opinions;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceOpinions : before.Opinions).Keys, name) ?? name;
            var rule = map.GetValueOrDefault(key);
            if (clear)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own second-opinion rule, else none."
                    : $"daoris: `{key}` takes its workspace's second-opinion rule again, else none.");
            }
            else if (none)
            {
                Say($"daoris: `{key}` declares no second opinion.");
            }
            else if (rule is { IsNone: false })
            {
                var named = string.Join(", else ", rule.Reviewers.Select(each => $"`{each}`"));
                Say(workspace is not null
                    ? $"daoris: second opinions for repositories in the workspace `{key}` that set none of their own: {named}."
                    : $"daoris: second opinions for `{key}`: {named}.");
            }

            if (rule is not null)
            {
                // The working agent is the one this machine's work runs on: a reviewer of its family is no independent reading. A plugin's
                // agent is judged by what its plugin declares (XAGENT1b2), from the plugins beside the file, as the driver reads them.
                IReadOnlyList<string> same = rule.IsNone ? []
                    : OpinionRules.SameAgent(rule, before.Adapter, adapters(DriverConfig.HomeOf(path)));
                foreach (var sentence in OpinionRules.Says(rule, [.. same]).Concat(OpinionRules.Gate(rule))) Say($"  {sentence}");
            }

            if (rule is { IsNone: false } && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver opinion <repository> --clear` hands it back.");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, rule) in choices.Opinions) Say($"  opinion    {repository}  {OpinionListed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceOpinions) Say($"  opinion    workspace {workspace}  {OpinionListed(rule)}  (for each repository there that sets none)");
        if (choices.Opinions.Count == 0 && choices.WorkspaceOpinions.Count == 0)
        {
            Say("  opinion    none set — no other agent reads work before it lands; `daoris driver opinion <repository> "
                + "--reviewers <adapter>` declares one");
        }
    }

    /// <summary>A rule in a line of <c>list</c>: its reviewers in order, when they read, what they may run, and the bound of a pass.</summary>
    private static string OpinionListed(OpinionRule rule)
    {
        if (rule.IsNone) return "none here, whatever its workspace says";
        var parts = new List<string>
        {
            string.Join(", else ", rule.Reviewers),
            rule.On.Contains("landing") ? (rule.On.Contains("steps") ? "before landing and each next step" : "before landing") : "before each next step",
        };
        if (rule.Required) parts.Add("required");
        if (rule.Verify) parts.Add("may build and run what is declared safe");
        parts.Add($"at most {rule.Bound} minutes a pass");
        if (!rule.Recheck) parts.Add("no recheck");
        return string.Join("; ", parts);
    }
}
