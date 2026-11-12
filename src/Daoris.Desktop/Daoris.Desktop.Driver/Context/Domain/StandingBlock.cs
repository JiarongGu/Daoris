using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The standing block: the one section that tells a session where things are,
/// its repository's routing, the person's choices on this machine, its repository's and workspace's facts and the memory its line read.
/// Composed by the driver with no model, handed at a session's start and at a move through its brief, never on a resume of the same
/// conversation. It replaces the routing line, the standing answer's preface and the instruction's map, look and indexes.
/// </summary>
/// <remarks>
/// <b>Its bound.</b> The whole first prompt shares one value of at most 32,767 characters on Windows: the native door takes it on stdin,
/// and <c>DAORIS_TARGET</c> carries it as an environment variable, which Windows caps at that. Where the instruction and the block together would pass <see cref="Budget"/>, the block goes
/// through the door's file channel (<see cref="StandingFacts.File"/>), and the instruction carries <see cref="Pointer"/> in its place.
/// </remarks>
public static class StandingBlock
{
    /// <summary>The most one value weighs on Windows (an environment variable such as <c>DAORIS_TARGET</c>), which the whole first prompt shares.</summary>
    public const int ArgumentLimit = 32_767;

    /// <summary>The most a first prompt with its block inline weighs: the limit less a margin of 2,767 characters.</summary>
    public const int Budget = 30_000;

    /// <summary>The most indexes it names: a pointer to where a look starts, not a listing; past it the rest are counted.</summary>
    public const int IndexLimit = 8;

    /// <summary>The most memory entries the line read that it names, the newest; each clipped at <see cref="MemoryItemLimit"/>.</summary>
    public const int MemoryLimit = 5;

    /// <summary>The most one memory entry's line weighs.</summary>
    public const int MemoryItemLimit = 400;

    /// <summary>The block's opening line.</summary>
    internal const string Opening =
        "Daoris's standing block for this session: where things are, and what holds on this machine. Daoris composed it, with no model, "
        + "and hands it once, at the session's start.";

    /// <summary>The way to work that is not this repository's, told in every routing.</summary>
    internal const string OtherRepositories =
        "Work another repository owns goes to that repository as a quest (`quest_publish`, naming the repository from the registry or this "
        + "repository's `uses`), and a question about this repository goes to a helper (`helper_ask`).";

    /// <summary>What a session whose harness takes no rules file is told: no deny of the person's reaches it, and what still refuses.</summary>
    internal const string NoDenies =
        "Your harness takes no rules file, so none of the person's deny rules reaches you: hold to what they asked in their words. "
        + "Daoris's git doors still refuse a push, a force-push and a branch delete from here unless the person's git access allows them.";

    /// <summary>The block of <paramref name="facts"/>, with its account.</summary>
    public static StandingBlockTold Compose(StandingFacts facts)
    {
        var text = new StringBuilder(Opening);
        var said = new List<string>();
        var cuts = new List<HandedCut>();

        if (Where(facts, said, cuts) is { } where) text.Append("\n\n").Append(where);
        if (facts.Focus is { Length: > 0 } focus)
        {
            text.Append("\n\n").Append(focus.TrimEnd());
            said.Add("its focus");
        }

        if (facts is { Routing: { } routing, Repository: { } routed })
        {
            text.Append("\n\n").Append(Routing(routing, facts.Lanes, routed));
            said.Add($"the routing standing `{routing}`");
        }

        if (facts is { Choices: { } choices, Repository: { } repository })
        {
            text.Append("\n\n").Append(Choices(choices, repository));
            said.Add(choices.At is { } at ? $"the person's choices set {Moment(at)}" : "the person's choices");
            if (choices.Says.Length > StandingText.Limit)
            {
                var over = choices.Says.Length - StandingText.Limit;
                cuts.Add(new HandedCut(
                    HandedCuts.StandingLong,
                    $"{N(over)} characters of the person's choices left out past their bound of {N(StandingText.Limit)}")
                {
                    Count = over,
                    Limit = StandingText.Limit,
                });
            }
        }

        text.Append("\n\n").Append(Facts(facts));
        if (facts.NoDenies)
        {
            text.Append("\n\n").Append(NoDenies);
            said.Add("that none of the person's denies reaches its harness");
        }

        if (Memory(facts.Memory) is { } memory)
        {
            text.Append("\n\n").Append(memory);
            var named = Math.Min(facts.Memory.Count, MemoryLimit);
            said.Add(named == 1 ? "1 memory entry the line read" : $"{named} memory entries the line read");
            if (facts.Memory.Count > MemoryLimit)
            {
                var left = facts.Memory.Count - MemoryLimit;
                cuts.Add(new HandedCut(HandedCuts.BriefList, $"{left} memory entries the line read left out past its bound of {MemoryLimit}") { Count = left });
            }
        }

        var block = text.ToString();
        var whose = facts.Repository is { } itself ? $"for `{itself}`" + (facts.Workspace is { } of ? $" of `{of}`" : "")
            : facts.Workspace is { } workspace ? $"for the workspace `{workspace}`" : "for this machine's workspaces";
        return new StandingBlockTold(block, new HandedSection(
            HandedSections.Block, HandedSources.Machine,
            $"the standing block: {N(block.Length)} characters, {whose}" + (said.Count == 0 ? "" : ": " + InstructionAccounting.Joined(said)))
        {
            Chars = block.Length,
            From = facts.Repository ?? facts.Workspace,
            At = facts.Choices?.At,
            Cuts = cuts.Count == 0 ? null : cuts,
        });
    }

    /// <summary>
    /// The block as a first prompt carries it, after the <paramref name="before"/> characters composed ahead of it: inline where both fit
    /// under <see cref="Budget"/>, else through its file where <see cref="StandingFacts.File"/> names one (<see cref="Pointer"/>).
    /// </summary>
    public static StandingBlockTold Fitted(StandingFacts facts, int before)
    {
        var block = Compose(facts);
        return before + Joint.Length + block.Text.Length > Budget && facts.File is { } file ? Pointer(block, file) : block;
    }

    /// <summary>What joins the block to the words before it.</summary>
    public const string Joint = "\n\n";

    /// <summary>The line naming the file the block went through, read before anything else, with the account's section saying so.</summary>
    public static StandingBlockTold Pointer(StandingBlockTold block, string file)
    {
        var line = $"Daoris's standing block for this session is in `{file.Replace('\\', '/')}`, since this instruction with it would pass the "
                   + $"{N(Budget)} characters its door carries: read that file before anything else. It says where things are, and what "
                   + "holds on this machine.";
        return new StandingBlockTold(line, block.Account with
        {
            Said = $"{block.Account.Said}; handed in its own file, its {N(block.Text.Length)} characters past what the first prompt carries "
                   + $"with them, and this line of {N(line.Length)} in the instruction",
            Chars = line.Length,
            Cuts =
            [
                .. block.Account.Cuts ?? [],
                new HandedCut(HandedCuts.BlockFiled, $"handed in its own file past the {N(Budget)}-character bound on the first prompt") { Limit = Budget },
            ],
        })
        {
            Filed = true,
        };
    }

    /// <summary>
    /// The person's choices for <paramref name="repository"/> (the standing answer): their words, verbatim and
    /// quoted, with when they were set, cut at <see cref="StandingText.Limit"/> and said to be.
    /// </summary>
    public static string Choices(StandingAnswer choices, string repository)
    {
        var set = choices.At is { } at ? ", set " + Moment(at) : "";
        return $"The person's choices on this machine for `{repository}`, in their own words{set}. They answer what they cover, so do not "
               + "ask them for what they already say; the work's own words, and theirs on its ask, are newer and win where they differ.\n\n"
               + StandingText.Quoted(choices);
    }

    private static string? Where(StandingFacts facts, List<string> said, List<HandedCut> cuts)
    {
        if (facts.Repository is null)
        {
            if (!facts.Knowledge) return null;
            said.Add("where things are");
            return "Where things are. The workspace's knowledge and its memory, through your connector's `knowledge_search` and "
                   + "`memory_search`. This session works in no repository's tree.";
        }

        var text = new StringBuilder("Where things are. This repository's own documents, code and history: its log, and the commits that last ")
            .Append("changed what you are changing.");
        if (facts.Knowledge) text.Append(" The workspace's knowledge and its memory, through your connector's `knowledge_search` and `memory_search`.");
        if (facts.Checkouts) text.Append(" The other checkouts listed above, which you may read.");
        said.Add("where things are");
        if (facts.Indexes.Count > 0)
        {
            var named = facts.Indexes.Take(IndexLimit).Select(path => $"`{path}`").ToList();
            var more = facts.Indexes.Count - named.Count;
            text.Append($" This repository indexes its own documents in {(more > 0 ? $"{string.Join(", ", named)} and {more} more like them" : Listed(named))}: ")
                .Append("start a look there, and read every document whose entry matches the work.");
            said.Add(facts.Indexes.Count == 1 ? "1 index" : $"{facts.Indexes.Count} indexes");
            if (more > 0)
            {
                cuts.Add(new HandedCut(HandedCuts.IndexesBound, $"{more} indexes counted, not named, past the {IndexLimit} it names") { Count = more, Limit = IndexLimit });
            }
        }

        if (facts.CodeMap is { } map)
        {
            text.Append("\n\n")
                .Append($"It keeps a code map in `{map}`: its modules, a line on what each is for, and which depends on which. If your work ")
                .Append("adds, removes, moves or rewires a module, bring the map up to date in the same change: with the repository's own ")
                .Append("tool for it where it has one, otherwise by hand in the same shape — each module's `id` unique, its `path` relative ")
                .Append("to the repository, its `summary` one line, and each dependency naming two modules by `id`. A map that breaks that ")
                .Append("shape is shown as nothing at all, so leave it whole.");
            said.Add($"the code map `{map}`");
        }

        if (facts.Skills.Count > 0)
        {
            text.Append("\n\n")
                .Append($"Its skills are in {Listed([.. facts.Skills.Select(folder => $"`{folder}`")])}, and your harness does not load them ")
                .Append("itself: when a skill's description matches the work, read its `SKILL.md` there.");
            said.Add("its skills' folders");
        }

        if (facts.Rules is { Ids.Count: > 0 } rules)
        {
            text.Append("\n\n")
                .Append($"Your harness reads the first {N(rules.Limit)} bytes of this repository's `{rules.File}`, which is longer, so some ")
                .Append("of its rules are past what you were given. Each is this repository's own row in the workspace's knowledge, which ")
                .Append("`knowledge_get` reads by its id: ")
                .Append(string.Join(", ", rules.Ids.Select(id => $"`{id}`")))
                .Append('.');
            said.Add($"{(rules.Ids.Count == 1 ? "1 rule" : $"{rules.Ids.Count} rules")} past its cut `{rules.File}`");
        }

        return text.ToString();
    }

    /// <summary>
    /// The routing: whether a quest published to the repository's own lanes would start on this machine, and
    /// where work another repository owns, or a question about this one, goes.
    /// </summary>
    private static string Routing(string standing, IReadOnlyList<string> lanes, string repository)
    {
        var addresses = string.Join(", ", lanes.Select(lane => $"`{repository}:{lane}`"));
        var (word, sentences) = standing switch
        {
            RoutingStanding.NotDriven => ("not driven",
                "This repository is not driven on this machine, so a quest published to "
                + (lanes.Count > 0 ? $"its lanes ({addresses})" : "it") + " would not start here. Work this repository's own rows yourself."),
            RoutingStanding.NoLanes => ("no lanes",
                "This repository declares no lanes, so there is no lane of its own to publish a quest to. Work this repository's own rows yourself."),
            RoutingStanding.OneAtATime => ("one at a time",
                $"This repository is driven on this machine and declares the lanes {addresses}, but opens no trees of its own, "
                + "so a quest published to one of them starts only after your work ends."),
            _ => ("open",
                $"This repository is driven on this machine, declares the lanes {addresses} and opens trees of its own, "
                + "so a quest published to one of them can start beside your work."),
        };
        return $"Routing standing here: {word}. {sentences} {OtherRepositories}";
    }

    private static string Facts(StandingFacts facts) => facts.Repository is { } repository
        ? facts.Workspace is { } workspace
            ? $"The repository and its workspace: you work in `{repository}`, of the workspace `{workspace}`."
            : $"The repository: you work in `{repository}`."
        : facts.Workspace is { } named
            ? $"The workspace: this session works for the workspace `{named}`, in no repository."
            : "The workspace: this session works for this machine's workspaces, in no repository.";

    private static string? Memory(IReadOnlyList<string> memory)
    {
        if (memory.Count == 0) return null;
        var text = new StringBuilder("Memory this line read, as it stands now:");
        foreach (var entry in memory.Take(MemoryLimit)) text.Append("\n- ").Append(TextBounds.Clip(TextBounds.OneLine(entry), MemoryItemLimit));
        if (memory.Count > MemoryLimit) text.Append($"\n- … and {memory.Count - MemoryLimit} more memory entries this line read, which `memory_get` reads as they stand.");
        return text.ToString();
    }

    private static string Listed(IReadOnlyList<string> named) =>
        named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";

    private static string N(int count) => InstructionAccounting.N(count);

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
