namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver work recovery drop</c> asks (RECOVERYDROP1): the refs named, or every one nothing holds, in the repository
/// named or in each repository with a checkout here; <see cref="Plan"/> only lists what a press would do.
/// </summary>
public sealed record RecoveryDropAsk(IReadOnlyList<string> Refs, bool Unheld, string? Repository, bool Plan);

/// <summary>The words of <c>daoris-driver work recovery drop</c>, read apart from the host so they are tested and the page's door shares them.</summary>
public static class RecoveryCommand
{
    /// <summary>The verb's usage, said with every refusal of its words.</summary>
    public const string Usage =
        "usage: daoris-driver work recovery drop <ref>... --repository <name> | --unheld [--repository <name>] [--plan]";

    /// <summary>
    /// Read the words after <c>work recovery drop</c>. Null with the problem said where they are not understood: it names refs or
    /// <c>--unheld</c> and not both, and refs are a repository's own, so it needs <c>--repository</c> for them.
    /// </summary>
    public static RecoveryDropAsk? Read(IReadOnlyList<string> args, out string problem)
    {
        var refs = new List<string>();
        string? repository = null;
        var unheld = false;
        var plan = false;
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--unheld":
                    unheld = true;
                    break;
                case "--plan":
                    plan = true;
                    break;
                case "--repository":
                    if (at + 1 >= args.Count || args[at + 1].StartsWith('-'))
                    {
                        problem = "`--repository` takes a repository's name.";
                        return null;
                    }

                    repository = args[++at];
                    break;
                case var flag when flag.StartsWith('-'):
                    problem = $"`{flag}` is not one of this verb's words.";
                    return null;
                case var named:
                    refs.Add(named);
                    break;
            }
        }

        if (unheld == (refs.Count > 0))
        {
            problem = unheld
                ? "name refs or `--unheld`, not both."
                : "name the recovery refs to delete, or `--unheld` for every one nothing holds.";
            return null;
        }

        if (refs.Count > 0 && repository is null)
        {
            problem = "a recovery ref is a repository's own: say which with `--repository <name>`.";
            return null;
        }

        problem = "";
        return new RecoveryDropAsk([.. refs.Distinct(StringComparer.Ordinal)], unheld, repository, plan);
    }
}
