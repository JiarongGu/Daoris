namespace Daoris.Driver;

/// <summary>What every <c>agent account</c> verb reads: its words, its terminal, the wiring file and its home, and the wiring as it stands.</summary>
internal sealed record AccountVerbContext(
    IReadOnlyList<string> Args, AgentsWorld World, string Path, string Home, HarnessSettings Settings, string Verb)
{
    public const string Prefix = "daoris";

    public void Say(string line) => World.Instance.Out.WriteLine(line);

    public bool Isolated => AgentsCommand.Isolated(Args, World.Instance.Environment);

    public string? Operand(int index) => AgentsCommand.Operand(Args, index);

    /// <summary>The operand at <paramref name="index"/>, refused where it is not there, as the CLI's <c>bare</c>.</summary>
    public string Bare(int index, string shape) =>
        Operand(index) ?? throw new AgentWords($"`agent {Verb}` needs {shape} — e.g. `daoris agent {Verb} claude-code`");

    /// <summary>
    /// The agent whose accounts this verb touches (AGT7): a door's are its owner's, and the verb says so rather than quietly landing
    /// somewhere else than was typed.
    /// </summary>
    public string Owner()
    {
        var given = Operand(2);
        var named = AgentsCommand.Named(World, Path);
        if (given is not { Length: > 0 }) throw new AgentWords($"`agent {Verb}` needs an agent — one of: {string.Join(", ", named)}");
        var (name, toolchain, _, _) = AgentsCommand.Agent(Args, Verb, "<agent>", World, Path, at: 2);
        var owner = toolchain.Owner(name);
        if (owner != name) Say($"{Prefix}: `{name}` runs as `{owner}`'s accounts — this is one of them.");
        return owner;
    }

    /// <summary>An account by its id or its name (ACCT2): its id, or what was given where it names none, for the verb to refuse.</summary>
    public string Account(string owner, string given) => AgentsCommand.AccountNamed(Home, owner, given);

    /// <summary>The set's order as written now: every account of the agent's, read again after a write on either path.</summary>
    public IReadOnlyList<string> Order(string owner) => HarnessSettings.LoadSet(Path, [owner]).Rotation.GetValueOrDefault(owner) ?? [];

    /// <summary>The agents this terminal's set knows, which every write here hands the wiring's change (POOLSTRAY1).</summary>
    public IReadOnlyList<string> Agents => AgentsCommand.Agents(World, Path);
}
