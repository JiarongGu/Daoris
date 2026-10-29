using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver brainstorms</c> (BRAINSTORM1b, D176 point 9; FOUNDATION1 mod-iii): the work orders' own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the policy it edits (<see cref="BrainstormRules"/>). Which of its words change the policy is
/// the settings' grammar (<see cref="ChoiceGrammar"/>), whose minutes' flags are these.
/// </summary>
public sealed class BrainstormsVerb : ChoiceVerb
{
    public override string Word => "brainstorms";

    /// <summary>
    /// The brainstorm policy (BRAINSTORM1b, D176 point 9): this machine's, or with `--workspace` for the questions about a workspace's
    /// repositories. `--mode`, `--agents` and the minutes set what they name over the rule set there; `--clear`. No route sets it.
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var (workspace, clear, set) = BrainstormsAsked(args);
        var edit = new BrainstormEdit { Workspace = workspace, Set = set, Clear = clear };
        var after = Judged(() => BrainstormRules.Apply(before, edit));
        return Alone(verb, path, config => BrainstormRules.Apply(config, edit), () =>
        {
            if (workspace is null)
            {
                Say(after.Brainstorms is { } machine
                    ? $"daoris: brainstorms on this machine: {BrainstormRules.Listed(machine)}."
                    : $"daoris: this machine's brainstorms take the default again: {BrainstormRules.Listed(BrainstormRule.Default)}.");
            }
            else
            {
                var key = FindName(after.WorkspaceBrainstorms.Keys, workspace) ?? FindName(before.WorkspaceBrainstorms.Keys, workspace) ?? workspace;
                Say(AtName(after.WorkspaceBrainstorms, key) is { } rule
                    ? $"daoris: brainstorms for questions about repositories in the workspace `{key}`: {BrainstormRules.Listed(rule)}."
                    : $"daoris: questions about repositories in the workspace `{key}` take this machine's brainstorms rule again.");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary><c>brainstorms</c> naming no change: this machine's rule and each workspace's, said.</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        BrainstormsAsked(args);
        var said = world.Instance.Out;
        said.WriteLine($"daoris: brainstorms on this machine: {BrainstormRules.Listed(choices.Brainstorms ?? BrainstormRule.Default)}"
            + $"{(choices.Brainstorms is null ? " (the default)" : "")}.");
        foreach (var (name, rule) in choices.WorkspaceBrainstorms)
        {
            said.WriteLine($"  for questions about repositories in the workspace `{name}`: {BrainstormRules.Listed(rule)}.");
        }

        return 0;
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        Say(choices.Brainstorms is { } brainstorms
            ? $"  brainstorms  {BrainstormRules.Listed(brainstorms)}"
            : $"  brainstorms  none set — {BrainstormRules.Listed(BrainstormRule.Default)}; `daoris driver brainstorms --agents <a,b>` sets one");
        foreach (var (workspace, rule) in choices.WorkspaceBrainstorms) Say($"  brainstorms  workspace {workspace}  {BrainstormRules.Listed(rule)}  (for its repositories' questions)");
    }

    /// <summary>The brainstorm policy's minutes, each by its flag and its name in the rule.</summary>
    public static readonly (string Flag, string Name)[] MinuteFlags =
        [("--pass-minutes", "pass"), ("--synthesis-minutes", "synthesis"), ("--challenge-minutes", "challenge"), ("--request-minutes", "request")];


    /// <summary>
    /// <c>brainstorms</c>' words read (BRAINSTORM1b): its workspace, whether it clears, and the set it names, each part only where it was
    /// said, as the screen and Ask Daoris send it. A minute given as anything but digits is handed on as written, to be refused in the
    /// rule's words; a repository named is refused here.
    /// </summary>
    private static (string? Workspace, bool Clear, BrainstormSet? Set) BrainstormsAsked(IReadOnlyList<string> args)
    {
        const string usage = "`driver brainstorms` takes [--workspace <name>], then --mode ask|off, --agents <adapter,adapter>, --pass-minutes, "
            + "--synthesis-minutes, --challenge-minutes and --request-minutes <n>, or --clear — e.g. "
            + "`daoris driver brainstorms --agents claude-code,codex-acp --pass-minutes 10`.";
        string[] valued = ["--workspace", "--mode", "--agents", .. MinuteFlags.Select(each => each.Flag)];
        for (var at = 1; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (token.StartsWith("--", StringComparison.Ordinal) && token != "--clear")
            {
                throw new ChoiceWords($"`{token}` is not a flag `driver brainstorms` takes — {usage}");
            }
            else if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ChoiceWords($"a brainstorms rule is set for this machine or for a workspace — never for one repository. {usage}");
            }
        }

        var minutes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (flag, name) in MinuteFlags)
        {
            if (FlagValue(args, flag) is { } value)
            {
                minutes[name] = value.All(char.IsAsciiDigit) ? double.Parse(value, CultureInfo.InvariantCulture) : value;
            }
        }

        var set = new BrainstormSet
        {
            Mode = FlagValue(args, "--mode"),
            Agents = FlagValue(args, "--agents")?.Split(',').Select(each => (string?)each.Trim()).ToList(),
            Minutes = minutes.Count > 0 ? minutes : null,
        };
        return (FlagValue(args, "--workspace"), args.Contains("--clear"), set.Names ? set : null);
    }
}
