using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris ask</c>'s words, read before anything is sent: a new ask's words and what it carries, or one ask's act (the command tree
/// hands <c>ask publish &lt;id&gt;</c> on as <c>--publish &lt;id&gt;</c>, and so on). <c>--help</c> anywhere asks for the usage alone; a flag
/// this verb does not take is a problem, never the ask's words, and so is a flag where a value belongs. Each problem spells its verb as
/// <see cref="Usage"/> does.
/// </summary>
public sealed record AskArguments
{
    public bool Help { get; init; }

    public string? Workspace { get; init; }

    public string? To { get; init; }

    public string? Publish { get; init; }

    public string? Close { get; init; }

    public string? Reason { get; init; }

    public string? Delete { get; init; }

    public string? GoAhead { get; init; }

    public string? Short { get; init; }

    public string? Review { get; init; }

    public string? ReviewWords { get; init; }

    public string? SetReview { get; init; }

    public string? Kind { get; init; }

    public string? Workflow { get; init; }

    public string? WorkflowWords { get; init; }

    public string? SetWorkflow { get; init; }

    public bool ClearWorkflow { get; init; }

    /// <summary>A new ask's through words: the person's own, asking for its work to be delivered with no press of theirs (D277).</summary>
    public string? Through { get; init; }

    /// <summary><c>ask through &lt;id&gt;</c>: the ask whose through words are set from the words after it, or taken back with <c>--clear</c>.</summary>
    public string? SetThrough { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];

    public IReadOnlyList<string> Links { get; init; } = [];

    public IReadOnlyList<string> Words { get; init; } = [];

    public IReadOnlyList<QuestRequirementWire> Requirements { get; init; } = [];

    /// <summary>The words a new ask is made with, or an act's words after its id.</summary>
    public string Sentence => string.Join(' ', Words).Trim();

    /// <summary>The verb's forms, as a terminal spells them.</summary>
    public static IReadOnlyList<string> Usage { get; } =
    [
        "daoris ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… \"…\"",
        "daoris ask … --short \"…\"",
        "daoris ask … --review rule|on|<environment>|off [--review-words \"…\"]",
        "daoris ask … --kind <kind> [--workflow <id>|current] [--workflow-words \"…\"]",
        "daoris ask --to <repo> … [--require \"<the person's words>\" \"<the check>\"]… [--through \"<the person's words>\"]",
        "daoris ask publish <id> --to <repo> [--require \"<the person's words>\" \"<the check>\"]…",
        "daoris ask through <id> \"<the person's words>\"|--clear",
        "daoris ask close <id> --reason \"…\"",
        "daoris ask delete <id>",
        "daoris ask go-ahead <id> <n> approve|refuse [\"…\"]",
        "daoris ask review <id> on|<environment>|off [\"…\"]",
        "daoris ask workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]",
    ];

    private static readonly IReadOnlySet<string> Valued = new HashSet<string>(StringComparer.Ordinal)
    {
        "--workspace", "--to", "--file", "--url", "--publish", "--close", "--delete", "--go-ahead", "--reason", "--short", "--review",
        "--review-words", "--set-review", "--kind", "--workflow", "--workflow-words", "--set-workflow", "--through", "--set-through",
    };

    /// <summary>The words read, or null with the problem; a problem sends nothing.</summary>
    public static AskArguments? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Any(arg => arg is "--help" or "-h")) return new AskArguments { Help = true };

        var read = new AskArguments();
        var files = new List<string>();
        var links = new List<string>();
        var words = new List<string>();
        var requirements = new List<QuestRequirementWire>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg == "--clear")
            {
                read = read with { ClearWorkflow = true };
                continue;
            }

            if (arg == "--require")
            {
                if (i + 2 >= args.Count || IsFlag(args[i + 1]) || IsFlag(args[i + 2]))
                {
                    problem = "--require takes two values: the person's words, then the check.";
                    return null;
                }

                requirements.Add(new QuestRequirementWire(args[++i], args[++i]));
                continue;
            }

            if (!IsFlag(arg))
            {
                words.Add(arg);
                continue;
            }

            if (!Valued.Contains(arg))
            {
                problem = $"`{arg}` is not a flag of `daoris ask`.";
                return null;
            }

            if (i + 1 >= args.Count || IsFlag(args[i + 1]))
            {
                problem = $"{arg} needs a value.";
                return null;
            }

            var value = args[++i];
            read = arg switch
            {
                "--workspace" => read with { Workspace = value },
                "--to" => read with { To = value },
                "--publish" => read with { Publish = value },
                "--close" => read with { Close = value },
                "--delete" => read with { Delete = value },
                "--go-ahead" => read with { GoAhead = value },
                "--reason" => read with { Reason = value },
                "--short" => read with { Short = value },
                "--review" => read with { Review = value },
                "--review-words" => read with { ReviewWords = value },
                "--set-review" => read with { SetReview = value },
                "--kind" => read with { Kind = value },
                "--workflow" => read with { Workflow = value },
                "--workflow-words" => read with { WorkflowWords = value },
                "--set-workflow" => read with { SetWorkflow = value },
                "--through" => read with { Through = value },
                "--set-through" => read with { SetThrough = value },
                "--file" => Add(read, files, value),
                _ => Add(read, links, value),
            };
        }

        read = read with { Files = files, Links = links, Words = words, Requirements = requirements };
        problem = Combined(read);
        return problem is null ? read : null;
    }

    private static AskArguments Add(AskArguments read, List<string> list, string value)
    {
        list.Add(value);
        return read;
    }

    /// <summary>A flag: a dash and a letter, one word; an ask's words that open with a dash hold a space.</summary>
    private static bool IsFlag(string arg) =>
        arg.Length > 1 && arg[0] == '-' && !arg.Contains(' ', StringComparison.Ordinal) && char.IsLetter(arg.TrimStart('-').FirstOrDefault());

    /// <summary>What goes only with a new ask's words, or only with one act, said beside another.</summary>
    private static string? Combined(AskArguments read)
    {
        var act = read.Publish ?? read.Close ?? read.Delete ?? read.GoAhead ?? read.SetReview ?? read.SetThrough;
        if ((read.Review ?? read.ReviewWords) is not null && act is not null)
        {
            return "--review goes with a new ask's words; `ask review <id>` changes an ask's review choice.";
        }

        if (read.Short is not null && (act ?? read.SetWorkflow) is not null) return "--short goes with a new ask's words: an ask's title is given when it is asked.";
        if ((read.Kind ?? read.Workflow ?? read.WorkflowWords) is not null && act is not null)
        {
            return "--kind and --workflow go with a new ask's words; `ask workflow <id>` changes an ask's.";
        }

        if (read.Through is not null && (act ?? read.SetWorkflow) is not null) return "--through goes with a new ask's words; `ask through <id>` changes an ask's.";
        if (read.ClearWorkflow && (read.SetWorkflow ?? read.SetThrough) is null)
        {
            return "--clear goes with `ask workflow <id>` or `ask through <id>` here; `ask clear <id>` clears an ask's work from this machine.";
        }

        if (read.Publish is not null && read.To is null) return "`ask publish <id>` needs `--to <repo>`: the repository the ask becomes a quest for.";
        if (read.Requirements.Count > 0 && read.Publish is null && ((act ?? read.SetWorkflow) is not null || read.To is null))
        {
            return "--require goes with `--to <repo>` on a new ask, or with `ask publish <id>`: a requirement travels on the quest an ask becomes.";
        }

        return null;
    }
}
