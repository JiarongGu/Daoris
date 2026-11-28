using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings trees [budget &lt;gb&gt;|none]</c>: the most a repository's trees may hold before the tree ledger says so
/// (<see cref="TreeBudgetSetting"/>). No route takes it yet, so it acts alone even beside a running Daoris; the ledger reads it at its
/// next reading. No word past <c>trees</c> says it.
/// </summary>
public sealed class TreesBudgetVerb : ChoiceVerb
{
    public override string Word => TreeBudgetSetting.Key;

    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        Listed(before, world);
        return 0;
    }

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        if (args.Count != 3 || args[1] != "budget") throw new ChoiceWords(TreeBudgetSetting.Refusal);
        int? gb = args[2] == TreeBudgetSetting.None ? null
            : int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && TreeBudgetSetting.Takes(number) ? number
            : throw new ChoiceWords(TreeBudgetSetting.Refusal);
        return new(Word, null, new JsonObject { ["gb"] = gb }, _ =>
        {
            StandingChoices.Write(path, config => config.WithTreeBudget(gb));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            world.Instance.Out.WriteLine(gb is { } set
                ? $"daoris: a repository's trees holding more than {set} GB are said once a day by the tree ledger's reading."
                : "daoris: a repository's trees have no budget; the tree ledger still says their bytes each day.");
            world.Instance.Out.WriteLine($"  Written to {path} — read at the ledger's next reading, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world) => world.Instance.Out.WriteLine(
        $"  trees      {(choices.TreeBudgetGb is { } gb ? $"{gb} GB at most in a repository's trees" : "no budget for a repository's trees")}  "
        + "(`daoris settings trees budget <gb>|none` sets it; the drive's floor is `daoris settings keep floor`)");
}
