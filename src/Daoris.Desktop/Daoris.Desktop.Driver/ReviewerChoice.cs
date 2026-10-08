namespace Daoris.Driver;

/// <summary>
/// One agent's family (XAGENT1b, D155 point 4, the second-agent design §3.1): whose accounts it runs as (AGT7's owner), and the
/// product and maker it declares. Two agents are one family when they run as one owner's accounts or declare one maker, so
/// Claude Code by either door is one family, and another account of an agent is that agent's family: an account is a
/// credential, not a second reader.
/// </summary>
/// <param name="Agent">The adapter it was read for, as the set spells it; trimmed as given where the set has none of that name.</param>
/// <param name="Owner">Whose accounts it runs as: its <c>accountOf</c>, else itself.</param>
/// <param name="Product">What a person calls the tool, as declared; null where it declares none.</param>
/// <param name="Maker">
/// Who makes it, as declared, trimmed; null where it declares none. A maker not declared is not known, so it is never taken as
/// another maker's (§3.1). Independence is by maker, never by model (D24).
/// </param>
/// <param name="Plugin">The plugin that declared the agent (D64), whose word its product and maker are; null for one this build carries.</param>
public sealed record AgentFamily(string Agent, string Owner, string? Product, string? Maker, string? Plugin = null)
{
    /// <summary>
    /// The family of <paramref name="name"/> as <paramref name="adapters"/> declares it: its toolchain where the set carries it,
    /// else the agent a door runs as where the set declares one (<see cref="AdapterSet.Holder"/>), else an owner of its own
    /// name with no maker.
    /// </summary>
    public static AgentFamily Of(AdapterSet adapters, string name)
    {
        var given = name.Trim();
        var carried = adapters.Names.FirstOrDefault(each => string.Equals(each, given, StringComparison.OrdinalIgnoreCase));
        var toolchain = carried is not null ? adapters.Resolve(carried).Toolchain : adapters.Holder(given);
        var agent = carried ?? given;
        return new AgentFamily(
            agent, toolchain?.Owner(agent) ?? agent, Declared(toolchain?.Product), Declared(toolchain?.Maker),
            carried is null ? null : adapters.DeclaredBy(carried));
    }

    /// <summary>Whether this and <paramref name="other"/> are one family: one owner, or one maker both declare, in any case.</summary>
    public bool Same(AgentFamily other) =>
        string.Equals(Owner, other.Owner, StringComparison.OrdinalIgnoreCase)
        || (Maker is not null && other.Maker is not null && string.Equals(Maker, other.Maker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The family as a person reads it: its product, else its name, with its maker; a plugin's maker said as its plugin's word
    /// (§3.1), and one not declared said so.
    /// </summary>
    public string Said()
    {
        var maker = Maker is null ? "maker not declared" : Plugin is null ? Maker : $"{Maker}, as its plugin `{Plugin}` declares";
        return $"{Product ?? $"`{Agent}`"} ({maker})";
    }

    private static string? Declared(string? text) => text?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}

/// <summary>How a reviewer stands to the work's families (§3.4): what every opinion carries on its face.</summary>
public static class ReviewerLabels
{
    /// <summary>Its maker is declared, and is not the maker of any family that wrote the work: tried first.</summary>
    public const string AnotherMaker = "another-maker";

    /// <summary>Not of the work's families by owner, and its maker not declared: never taken as another maker's agent.</summary>
    public const string MakerNotDeclared = "maker-not-declared";

    /// <summary>One of the work's families: the same agent, fresh, taken only when listed and no other can run.</summary>
    public const string SameAgent = "same-agent";
}

/// <summary>
/// Why no other maker's agent could read the work (§3.3), the code a choice carries wherever another maker's agent did not
/// read it: when it is unavailable, and when a reviewer of another standing read it in that one's place.
/// </summary>
public static class ReviewerUnavailable
{
    /// <summary>No listed reviewer of another maker is installed, or one is named that this machine has no agent of.</summary>
    public const string NoReviewer = "no-reviewer";

    /// <summary>Each listed reviewer of another maker that is installed waits for an account's cool-off.</summary>
    public const string Cooling = "cooling";

    /// <summary>Each listed reviewer of another maker that is installed has no account signed in.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>No listed reviewer is another maker's agent: only the work's own family, or a maker not declared.</summary>
    public const string NotIndependent = "not-independent";

    /// <summary>The listed reviewers of another maker were refused otherwise, or for different reasons: each named.</summary>
    public const string Refused = "refused";
}

/// <summary>Why one reviewer could not run, as the account walk answered for it (<see cref="ReviewerTried.Held"/>).</summary>
public static class ReviewerHeld
{
    /// <summary>Nothing of it is installed, or this machine has no agent of that name.</summary>
    public const string NotInstalled = "not-installed";

    /// <summary>Pinned to a version nothing is installed at (D57).</summary>
    public const string PinNotInstalled = "pin-not-installed";

    /// <summary>Its accounts wait for a cool-off (D125).</summary>
    public const string Cooling = "cooling";

    /// <summary>The accounts it may use are not signed in (ROSTER1).</summary>
    public const string SignedOut = "signed-out";

    /// <summary>Refused for anything else, its provider's refusal among them (AGT3b).</summary>
    public const string Refused = "refused";
}

/// <summary>One reviewer the walk tried and passed, with what held it.</summary>
/// <param name="Reviewer">The adapter, as the rule names it.</param>
/// <param name="Label">How it stands to the work's families (<see cref="ReviewerLabels"/>).</param>
/// <param name="Held">What held it (<see cref="ReviewerHeld"/>).</param>
/// <param name="Refusal">The account walk's own sentence, which names the fix. Machine-local: it may name an account.</param>
public sealed record ReviewerTried(string Reviewer, string Label, string Held, string Refusal)
{
    /// <summary>The cool-off that holds it, when that is why.</summary>
    public CoolingEntry? Cooling { get; init; }
}

/// <summary>
/// The reviewer chosen for a second opinion (XAGENT1b, D155 point 4, the second-agent design §3), or why none could be: the walk
/// over agents that comes before the account walk and calls it, unchanged, for each.
/// </summary>
/// <remarks>
/// <para><b>The walk</b> (§3.2): the rule's reviewers, in the person's order, never one it does not name (D23). Another maker's
/// agent first; then one whose maker is not declared, which is never taken as independent; then one of the work's own families,
/// taken only because the person listed it, and labelled. Each is asked of <see cref="HarnessRoster.SelectAsync"/> as a driven
/// start, so its account comes from its agent's scope for the workspace (D130), past its cool-offs (D125), its sign-ins
/// (ROSTER1), the account kept for conversations (D130 §4.6) and its pin (D57). The first allowed is the reviewer, its start
/// counted as chosen; nothing is counted for an entry that was refused.</para>
/// <para><b>Never downgraded silently</b> (§3.3): wherever another maker's agent did not read the work, the choice carries a
/// code (<see cref="ReviewerUnavailable"/>) and says why, each entry passed with its own sentence.</para>
/// <para>Nothing asks for one yet: the pass (XAGENT1d) and the gate (XAGENT1f) do, and they read the work's adapters from the
/// session records whose work is on the candidate.</para>
/// </remarks>
/// <param name="Reviewer">The adapter that reads the work, or null when none can.</param>
/// <param name="Label">How it stands to the work's families (<see cref="ReviewerLabels"/>), or null when none can.</param>
/// <param name="Code">Why no other maker's agent read it (<see cref="ReviewerUnavailable"/>); null when one did.</param>
/// <param name="Sentence">What a person reads: who reads it and how it stands, or that none can, and why. Machine-local.</param>
public sealed record ReviewerChoice(string? Reviewer, string? Label, string? Code, string Sentence)
{
    /// <summary>Whether a reviewer was chosen.</summary>
    public bool Chosen => Reviewer is not null;

    /// <summary>The account walk's answer for the reviewer, which its start runs on; null when none can.</summary>
    public HarnessSelection? Selection { get; init; }

    /// <summary>The reviewer's family: its product and maker, as every opinion carries them (§3.4); null when none can.</summary>
    public AgentFamily? Family { get; init; }

    /// <summary>The families that wrote the work, each once, in the order first named (§3.4).</summary>
    public IReadOnlyList<AgentFamily> Working { get; init; } = [];

    /// <summary>Each reviewer tried and passed, in the order tried.</summary>
    public IReadOnlyList<ReviewerTried> Tried { get; init; } = [];

    /// <summary>
    /// The first reset among the reviewers passed for a cool-off, wherever another maker's agent did not read the work: when
    /// trying again may find one. Null otherwise.
    /// </summary>
    public CoolingEntry? Cooling { get; init; }

    /// <summary>A listed reviewer: its family, how it stands to the work's, and whether this machine has an agent of its name.</summary>
    private sealed record Entry(AgentFamily Family, string Label, bool Known)
    {
        /// <summary>
        /// Whether it may be another maker's agent: one that declares another maker, or a name nothing on this machine declares,
        /// whose maker nobody can read; never one of the work's families.
        /// </summary>
        public bool MayBeAnother => Label == ReviewerLabels.AnotherMaker || (!Known && Label == ReviewerLabels.MakerNotDeclared);
    }

    /// <summary>
    /// The walk's passes, each in the rule's order (§3.2, §3.3): another maker's agent, with each name this machine has no agent
    /// of, which is said and asks nothing; then an agent whose maker is not declared; then the work's own family.
    /// </summary>
    private static readonly Func<Entry, bool>[] Passes =
    [
        entry => entry.MayBeAnother,
        entry => entry.Known && entry.Label == ReviewerLabels.MakerNotDeclared,
        entry => entry.Label == ReviewerLabels.SameAgent,
    ];

    /// <summary>
    /// Choose the reviewer of work the <paramref name="working"/> agents wrote, from <paramref name="rule"/>'s reviewers, for a
    /// repository in <paramref name="workspace"/>.
    /// </summary>
    /// <param name="roster">The machine's agents, the build's and its plugins', and the account walk.</param>
    /// <param name="working">The adapters of the sessions whose work is read: their families are the work's.</param>
    public static async Task<ReviewerChoice> ChooseAsync(
        HarnessRoster roster, OpinionRule rule, IReadOnlyCollection<string> working, DriverConfig config, string? workspace,
        CancellationToken ct = default)
    {
        var adapters = roster.Adapters;
        var families = new List<AgentFamily>();
        foreach (var name in working.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var family = AgentFamily.Of(adapters, name);
            if (!families.Any(known => known.Same(family))) families.Add(family);
        }

        var entries = rule.Reviewers
            .Select(name => AgentFamily.Of(adapters, name))
            .Select(family => new Entry(family, LabelOf(family, families), adapters.Names.Contains(family.Agent, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var tried = new List<ReviewerTried>();
        foreach (var pass in Passes)
        {
            foreach (var entry in entries.Where(pass))
            {
                var (selection, held) = await TryAsync(roster, entry, config, workspace, ct).ConfigureAwait(false);
                if (selection is not null) return Chose(entry, selection, entries, tried, families);
                tried.Add(held!);
            }
        }

        var code = Why(entries, tried);
        return new ReviewerChoice(null, null, code, $"No second opinion: {Lead(code)}.{Passed(tried)}")
        {
            Working = families,
            Tried = tried,
            Cooling = FirstReset(tried),
        };
    }

    private static string LabelOf(AgentFamily reviewer, IReadOnlyList<AgentFamily> working) =>
        working.Any(reviewer.Same) ? ReviewerLabels.SameAgent
        : reviewer.Maker is null ? ReviewerLabels.MakerNotDeclared
        : ReviewerLabels.AnotherMaker;

    /// <summary>One reviewer asked of the account walk: its selection where it may run, else what held it.</summary>
    private static async Task<(HarnessSelection? Selection, ReviewerTried? Held)> TryAsync(
        HarnessRoster roster, Entry entry, DriverConfig config, string? workspace, CancellationToken ct)
    {
        var (name, label) = (entry.Family.Agent, entry.Label);
        // Named, never guessed (D23): a name this machine has no agent of is said, and nothing is asked.
        if (!entry.Known)
        {
            return (null, new ReviewerTried(
                name, label, ReviewerHeld.NotInstalled,
                $"`{name}` is no agent on this machine: neither this build nor an installed plugin declares it."));
        }

        HarnessSelection selection;
        try
        {
            selection = await roster.SelectAsync(name, config, workspace, chosen: null, StartKind.Driven, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return (null, new ReviewerTried(name, label, ReviewerHeld.Refused, error.Message));
        }

        if (selection.Allowed) return (selection, null);

        // What held it, from the walk's facts and never its words: a cool-off waits for a time, so it is named first; a list
        // with none cooling and an account not signed in waits for a sign-in, which its sentence names.
        var held = selection switch
        {
            { Absent: AgentAbsence.NotInstalled } => ReviewerHeld.NotInstalled,
            { Absent: AgentAbsence.PinNotInstalled } => ReviewerHeld.PinNotInstalled,
            { Cooling: not null } => ReviewerHeld.Cooling,
            { NotReady: AccountReadiness.SignedOut } or { SignedOut: not null } => ReviewerHeld.SignedOut,
            _ => ReviewerHeld.Refused,
        };
        return (null, new ReviewerTried(name, label, held, selection.Refusal!) { Cooling = selection.Cooling });
    }

    private static ReviewerChoice Chose(
        Entry entry, HarnessSelection selection, IReadOnlyList<Entry> entries, IReadOnlyList<ReviewerTried> tried,
        IReadOnlyList<AgentFamily> working)
    {
        var (family, label) = (entry.Family, entry.Label);
        var reads = $"`{family.Agent}` reads it: {family.Said()}";
        var code = label == ReviewerLabels.AnotherMaker ? null : Why(entries, tried);
        var sentence = label switch
        {
            ReviewerLabels.AnotherMaker => $"{reads}, another maker's agent than the one that did the work{Work(working)}.",
            ReviewerLabels.MakerNotDeclared => $"{reads}, whose reading is not counted as another maker's.",
            _ => $"{reads}, the same agent as the one that did the work, in a fresh conversation: not an independent reading.",
        };
        if (code is not null) sentence += $" No other maker's agent could read it: {Lead(code)}.{Passed(tried)}";

        return new ReviewerChoice(family.Agent, label, code, sentence)
        {
            Selection = selection,
            Family = family,
            Working = working,
            Tried = tried,
            Cooling = code is null ? null : FirstReset(tried),
        };
    }

    /// <summary>
    /// Why no other maker's agent read the work (§3.3), over the listed reviewers that may be another maker's, each of which was
    /// tried in the first pass: none listed is <c>not-independent</c>; none installed is <c>no-reviewer</c>; then the one hold
    /// every installed one shares, cooling or signed out; else <c>refused</c>, each named.
    /// </summary>
    private static string Why(IReadOnlyList<Entry> entries, IReadOnlyList<ReviewerTried> tried)
    {
        if (!entries.Any(entry => entry.MayBeAnother)) return ReviewerUnavailable.NotIndependent;

        var installed = tried
            .Where(each => each.Label == ReviewerLabels.AnotherMaker && each.Held != ReviewerHeld.NotInstalled)
            .ToList();
        if (installed.Count == 0) return ReviewerUnavailable.NoReviewer;
        if (installed.All(each => each.Held == ReviewerHeld.Cooling)) return ReviewerUnavailable.Cooling;
        return installed.All(each => each.Held == ReviewerHeld.SignedOut) ? ReviewerUnavailable.SignedOut : ReviewerUnavailable.Refused;
    }

    private static string Lead(string code) => code switch
    {
        ReviewerUnavailable.NotIndependent => "no listed reviewer is another maker's agent",
        ReviewerUnavailable.NoReviewer => "no listed reviewer of another maker is installed",
        ReviewerUnavailable.Cooling => "every listed reviewer of another maker is cooling",
        ReviewerUnavailable.SignedOut => "no listed reviewer of another maker has an account signed in",
        _ => "every listed reviewer of another maker was refused",
    };

    /// <summary>Each reviewer passed, with the walk's own sentence: <c> `name`: sentence.</c></summary>
    private static string Passed(IReadOnlyList<ReviewerTried> tried) =>
        string.Concat(tried.Select(each => $" `{each.Reviewer}`: {(each.Refusal.TrimEnd().EndsWith('.') ? each.Refusal.TrimEnd() : each.Refusal.TrimEnd() + ".")}"));

    /// <summary>The work's families, after a comma; nothing where no family of the work is known.</summary>
    private static string Work(IReadOnlyList<AgentFamily> working)
    {
        if (working.Count == 0) return "";
        var said = working.Select(family => family.Said()).ToList();
        return ", " + (said.Count == 1 ? said[0] : $"{string.Join(", ", said.Take(said.Count - 1))} and {said[^1]}");
    }

    private static CoolingEntry? FirstReset(IEnumerable<ReviewerTried> tried) =>
        tried.Select(each => each.Cooling).OfType<CoolingEntry>().MinBy(cooling => cooling.Until);
}
