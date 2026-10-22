namespace Daoris.Driver;

/// <summary>Why a set-up was refused: a code a screen can key on, and the sentence a person reads, naming the door that answers it.</summary>
public sealed record SetupRefusal(string Code, string Sentence);

/// <summary>The codes of <see cref="SetupRefusal"/> (D124 §2.1), in the order the press asks.</summary>
public static class SetupRefusals
{
    public const string NotOnRegistry = "not-on-registry";
    public const string NoCheckout = "no-checkout";
    public const string NotDriven = "not-driven";
    public const string NoOwnTree = "no-own-tree";
    public const string NoHistory = "no-history";
    public const string NoNode = "no-node";
    public const string NoTool = "no-doctrine-tool";
    public const string AlreadySetUp = "already-set-up";

    /// <summary>Set up, naming no index, and built with a stack the press knows no generator for (ORIENT2c).</summary>
    public const string NoIndexForm = "no-index-form";

    public const string Open = "open";
}

/// <summary>
/// The programs a set-up's session runs, as the press found them on the <c>PATH</c> a child of Daoris starts with
/// (D124 §1.3, §2.1): <c>node</c>, which the doctrine tool runs on, and <c>daoris</c>, with what each answered for its
/// version. The files are this machine's and are never put in the quest.
/// </summary>
/// <param name="NodeProblem">Why no <c>node</c> could be named, in Tools' words, where none was.</param>
/// <param name="DaorisProblem">What was found in place of an answer, where <c>daoris</c> did not give one.</param>
public sealed record SetupTools(
    string? Node, string? NodeVersion, string? NodeProblem, string? Daoris, string? DaorisVersion, string? DaorisProblem);

/// <summary>What a press would do, or why it would not: plan, never apply (the CLI's convention, held here).</summary>
public sealed record SetupPlan(string Repository)
{
    /// <summary>Its circle, which the ask is made in; null where the registry holds no row for it.</summary>
    public string? Workspace { get; init; }

    /// <summary>What its line holds, where it could be read.</summary>
    public LayoutFacts? Facts { get; init; }

    /// <summary>The programs its session needs, as found.</summary>
    public SetupTools? Tools { get; init; }

    /// <summary>Which set-up the line calls for, where it could be read.</summary>
    public SetupCase? Case { get; init; }

    /// <summary>The generator an index quest names (ORIENT2c), whose two commands the press grants; null for any other case.</summary>
    public IndexForm? Index { get; init; }

    /// <summary>Every refusal that applies, in the press's order; none is a press that publishes.</summary>
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];

    /// <summary>The quest's title (<see cref="SetupQuests"/>), where there is one to ask.</summary>
    public string? Title { get; init; }

    /// <summary>The quest's body (<see cref="SetupBrief"/>).</summary>
    public string? Body { get; init; }

    /// <summary>How its work lands, said before the press (D117 §6.3).</summary>
    public Landing? Landing { get; init; }

    /// <summary>The agent this machine's driven sessions start on, which would carry it.</summary>
    public string? Adapter { get; init; }

    /// <summary>The ask's words: the title its first line, so the quest the service makes is titled by it, then the body.</summary>
    public string? Sentence => Title is null ? null : $"{Title}\n\n{Body}";

    /// <summary>Whether a press publishes: nothing refused, and something to ask.</summary>
    public bool Pressable => Refusals.Count == 0 && Title is not null;
}

/// <summary>What a press came to.</summary>
/// <param name="Message">The service's sentence, verbatim, or why nothing was asked.</param>
/// <param name="Added">The rules this press added to the repository's scope, and kept.</param>
public sealed record SetupOutcome(bool Published, string Message, string? AskId, string? QuestId, IReadOnlyList<string> Added);
