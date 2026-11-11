using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings growth &lt;workspace&gt; [--repository &lt;name&gt;] mode|asked|off</c>: the person's own press of the growth switch
/// (<see cref="GrowthRules"/>) and its read form, which says the value and where it comes from. The settings' port (<see cref="ChoiceVerb"/>)
/// implemented beside the rule it edits. It is never an agent's: a change from a session's terminal or a process in an agent's job is refused
/// before anything is pressed or written, and a press that proves a session's key is refused by the pipe in every mode.
/// </summary>
public sealed class GrowthVerb : ChoiceVerb
{
    private const string Usage = "usage: daoris settings growth <workspace> [--repository <name>] mode|asked|off — e.g. `daoris settings growth aurora off`; "
        + "with no value it says the value and where it comes from.";

    /// <summary>The refusal of a change from where an agent is: the switch is the person's, or an agent could turn its own refusal back off.</summary>
    internal const string Alone =
        "the growth switch is the person's alone: an agent that could turn its own refusal back off would be held by nothing. Change it in Daoris's window (Settings) or from your own terminal; nothing was written.";

    private static readonly string[] Valued = ["--repository"];

    public override string Word => "growth";

    /// <summary>The read form: the value and where it comes from, nothing written.</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var (workspace, repository, _) = Read(args);
        var resolved = GrowthRules.Resolve(before, repository, workspace);
        world.Instance.Out.WriteLine(repository is null
            ? $"daoris: new repositories and rooms in the workspace `{workspace}`: {resolved.Value} ({Source(resolved, workspace, repository)})."
            : $"daoris: new rooms in `{repository}`: {resolved.Value} ({Source(resolved, workspace, repository)}).");
        world.Instance.Out.WriteLine($"  {GrowthRules.Says(resolved.Value)}");
        return 0;
    }

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var (workspace, repository, value) = Read(args);
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 } || world.InAgentJob() == true) throw new ChoiceWords(Alone, 1);

        var payload = new JsonObject { ["workspace"] = workspace };
        if (repository is not null) payload["repository"] = repository;
        payload["value"] = value;
        var edit = GrowthRules.EditOf(JsonSerializer.SerializeToElement(payload));
        DriverConfig after;
        try
        {
            after = Judged(() => GrowthRules.Apply(before, edit));
        }
        catch (ChoiceWords refused)
        {
            throw new ChoiceWords($"{refused.Message} — {Usage}", refused.Exit);
        }

        return new("growth", ChoiceRoutes.Growth, payload, _ =>
        {
            StandingChoices.Write(path, config => GrowthRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var resolved = GrowthRules.Resolve(after, repository, workspace);
            Say(repository is null
                ? $"daoris: new repositories and rooms in the workspace `{workspace}`: {resolved.Value}."
                : $"daoris: new rooms in `{repository}`: {resolved.Value} ({Source(resolved, workspace, repository)}).");
            Say($"  {GrowthRules.Says(resolved.Value)}");
            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>: what is set, and none where nothing is.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, value) in choices.Growth) Say($"  growth     {repository}  {value}");
        foreach (var (workspace, value) in choices.WorkspaceGrowth) Say($"  growth     workspace {workspace}  {value}  (for each repository there that sets none)");
    }

    /// <summary>The words read: the workspace, the repository where one is named and the value where one is given; a refusal carries the usage.</summary>
    private static (string Workspace, string? Repository, string Value) Read(IReadOnlyList<string> args)
    {
        try
        {
            Flags(args, Valued, [], "settings growth", Usage);
            var repository = FlagValue(args, "--repository");
            var operands = Operands(args, Valued);
            if (operands.Count < 2) throw new ChoiceWords("`settings growth` needs the workspace the switch is for.");
            if (operands.Count > 3) throw new ChoiceWords("`settings growth` takes one value: `mode`, `asked` or `off`.");
            return (operands[1], repository, operands.Count > 2 ? operands[2] : "");
        }
        catch (ChoiceWords refused) when (!refused.Message.EndsWith(Usage, StringComparison.Ordinal))
        {
            throw new ChoiceWords($"{refused.Message} — {Usage}", refused.Exit);
        }
    }

    /// <summary>Where the value is set, as the words say it: the place's own, its workspace's, or the default.</summary>
    private static string Source(ResolvedGrowth resolved, string workspace, string? repository) => resolved.Source switch
    {
        GrowthRules.RepositorySource => "its own",
        GrowthRules.WorkspaceSource when repository is null => "its own",
        GrowthRules.WorkspaceSource => $"from the workspace `{workspace}`",
        _ => "the default",
    };
}
