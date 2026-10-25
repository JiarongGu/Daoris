using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver work-cycle</c> (FABRIC-GRAPH1c, the fabric events design §D7; FOUNDATION1 mod-iii): the workflows' own verb, the
/// settings' port (<see cref="ChoiceVerb"/>) implemented where a graph is read, a quest's or an ask's cycle of work.
/// </summary>
public sealed class WorkCycleVerb : ChoiceVerb
{
    public override string Word => "work-cycle";

    /// <summary>
    /// A quest's or an ask's cycle of work (FABRIC-GRAPH1c; the fabric events design §D7's `workCycles`): a whole graph for one kind,
    /// for a repository or with `--workspace` for each repository there naming none of that kind, judged as `WithWorkCycle` judges
    /// it. A branch's cycle is its landing rule's (`landing … --cycle`). No route sets it, so it acts alone.
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var clear = args.Contains("--clear");
        var words = Operands(args, ["--workspace"]).Skip(1).ToList();
        var owner = workspace;
        if (owner is null && words.Count > 0)
        {
            owner = words[0];
            words.RemoveAt(0);
        }

        var kind = words.ElementAtOrDefault(0);
        var file = words.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(kind) || clear == (file is not null))
        {
            throw new ChoiceWords("`driver work-cycle` needs <repository>|--workspace <name>, then quest|ask, then <file>|--clear — e.g. "
                + "`daoris driver work-cycle aurora-engine quest cycle.json`, a whole graph its work of that kind follows to its end.");
        }

        if (kind == WorkUnitKind.Branch)
        {
            throw new ChoiceWords("a branch's cycle is its landing rule's `cycle`, so that a repository's landing and its end move together (D164); "
                + "no kind has two homes. `daoris driver landing <repository> … --cycle <file>` sets it.");
        }

        if (!NodeGraphHomes.WorkCycleKinds.Contains(kind))
        {
            throw new ChoiceWords($"`{kind}` is no kind of work with a cycle of its own here — "
                + $"{string.Join(" or ", NodeGraphHomes.WorkCycleKinds.Select(each => $"`{each}`"))}.");
        }

        string? cycle = null;
        if (file is not null)
        {
            cycle = ChoiceCycles.File(world, file);
            if (NodeGraphs.Read(cycle).Problems.FirstOrDefault() is { } first)
            {
                throw new ChoiceWords($"the cycle in `{file}` does not hold: {first.Words} Nothing was written.");
            }
        }

        return Alone(verb, path,
            config => workspace is not null ? config.WithWorkspaceWorkCycle(workspace, kind, cycle) : config.WithWorkCycle(owner, kind, cycle), () =>
            {
                var unit = kind == WorkUnitKind.Ask ? "an ask" : $"a {kind}";
                var where = workspace is not null ? $"each repository of the workspace `{workspace}` that names none of its own" : $"`{owner}`";
                Say(cycle is not null
                    ? $"daoris: {unit} in {where} follows its cycle: {ChoiceCycles.Said(cycle, "")}."
                    : $"daoris: {unit} in {where} takes {(workspace is not null ? "none" : "its workspace's cycle")} again{(workspace is not null ? "" : ", else none")}.");
                Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                return 0;
            });
    }
}
