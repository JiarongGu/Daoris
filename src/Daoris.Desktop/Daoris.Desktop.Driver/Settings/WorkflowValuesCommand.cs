namespace Daoris.Driver;

/// <summary>What <c>daoris workflow values</c> asks: a node of a repository's workflow, and its values to set or a clear, with the reason.</summary>
public sealed record WorkflowValuesAsk(string Repository, string Workflow, string Node)
{
    /// <summary>Each option's JSON text to set, or null to say or to clear.</summary>
    public IReadOnlyDictionary<string, string>? Values { get; init; }

    /// <summary>Whether the node's values are taken away.</summary>
    public bool Clear { get; init; }

    /// <summary>Why: recorded with the values.</summary>
    public string? Why { get; init; }

    /// <summary>Whether the ask writes the driver's file.</summary>
    public bool Changes => Clear || Values is not null;
}

/// <summary>
/// <c>daoris workflow values</c>: a repository's own values over a node of a workflow it follows (a release step's target, say), said, set
/// or cleared, with the reason recorded. The window's <c>SET_WORKFLOW_VALUES</c> twin: both go through <see cref="WorkflowValuesKey.Apply"/>,
/// so they refuse alike, and the release manager's holder sets a target here, from a terminal or Ask Daoris, never by editing the file.
/// </summary>
/// <remarks>Exit codes are the family's: 0 said or set · 1 refused, nothing written · 2 could not, the usage among them.</remarks>
public static class WorkflowValuesCommand
{
    public const string Usage = "usage: daoris workflow values <repository> <workflow> <node> [<name>=<json>… | --clear] --why <reason>";

    /// <summary>Whether <paramref name="args"/> ask this verb.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args.Count > 0 && args[0] == "values";

    /// <summary>What the words after <c>values</c> ask, or null with what is wrong with them.</summary>
    public static WorkflowValuesAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var words = args.Skip(Asks(args) ? 1 : 0).ToList();
        if (words.Count < 3 || words.Take(3).Any(word => word.StartsWith("--", StringComparison.Ordinal)))
        {
            problem = "`values` takes a repository, a workflow's id and a node's id first.";
            return null;
        }

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string? why = null;
        var clear = false;
        for (var at = 3; at < words.Count; at++)
        {
            var word = words[at];
            if (word == "--why" && at + 1 < words.Count) why = words[++at];
            else if (word == "--clear") clear = true;
            else if (word.IndexOf('=') is > 0 and var equals) values[word[..equals]] = word[(equals + 1)..];
            else
            {
                problem = $"`{word}` is no `<name>=<json>`, `--clear` or `--why <reason>`.";
                return null;
            }
        }

        if (clear && values.Count > 0)
        {
            problem = "one change at a time: values or `--clear`.";
            return null;
        }

        return new WorkflowValuesAsk(words[0], words[1], words[2]) { Values = values.Count > 0 ? values : null, Clear = clear, Why = why };
    }

    /// <summary>Say the node's values, or make the change, written to the driver's file at <paramref name="configPath"/>.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing is written.</exception>
    public static int Run(WorkflowValuesAsk ask, string configPath, TextWriter output)
    {
        var config = DriverConfig.Load(configPath);
        if (ask.Changes)
        {
            try
            {
                config = WorkflowValuesKey.Apply(config, ask.Repository, ask.Workflow, ask.Node, ask.Clear ? null : ask.Values, ask.Why);
            }
            catch (DriverException refused)
            {
                output.WriteLine($"values: {refused.Message}");
                return 1;
            }

            config.Save(configPath);
        }

        output.WriteLine(Said(config, ask));
        return 0;
    }

    /// <summary>A node's values in one line, and the reason kept for them.</summary>
    public static string Said(DriverConfig config, WorkflowValuesAsk ask)
    {
        var values = WorkflowValuesKey.Of(config.WorkflowValues, ask.Repository, ask.Workflow, ask.Node);
        var why = config.WorkflowValuesWhy.GetValueOrDefault(WorkflowValuesKey.WhyOf(ask.Repository, ask.Workflow, ask.Node));
        var where = $"`{ask.Repository}` {ask.Workflow}/{ask.Node}";
        var said = values is null ? $"{where}: no values of its own." : $"{where}: {string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}"))}.";
        return why is null ? said : $"{said} Why: {why}";
    }
}
