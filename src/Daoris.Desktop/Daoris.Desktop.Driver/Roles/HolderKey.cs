namespace Daoris.Driver;

/// <summary>
/// The holder key: 32 random bytes the window draws at an appointment and
/// puts in the environment of the one terminal it starts for the holder, as <see cref="Variable"/>, in hex. Daoris keeps only its
/// SHA-256, in memory alone; no file, page or pipe ever carries the key.
/// </summary>
/// <remarks>
/// <para><b>Stripped everywhere else</b>: every Daoris executable removes the variable from its own environment at its entry
/// (<see cref="Strip"/>), before anything else, so no start it builds can copy it; and every start's environment drops it
/// (<see cref="GitWiring.Removed"/>). The verb is the one reader: <c>daoris</c> takes it into memory first and then removes it
/// (<see cref="Take"/>), and only <c>sessions supervise</c> reads what it took.</para>
/// </remarks>
public static class HolderKey
{
    /// <summary>
    /// The variable the holder's terminal carries the key in, and the only place the verb reads it from: <c>DAORIS_ROLE_KEY</c>,
    /// since a shared holder's key is no one role's.
    /// </summary>
    public const string Variable = ChildMarks.RoleKeyVariable;

    /// <summary>The key's length in bytes, and so its hex text's in characters twice that.</summary>
    public const int Bytes = KeyedProof.KeyBytes;

    private static string? _taken;

    /// <summary>A new key, in hex: drawn at each appointment and never reused.</summary>
    public static string Draw() => KeyedProof.Draw();

    /// <summary>The key's SHA-256, which the instance keeps in memory and the proof is keyed by; null for a text that is no key.</summary>
    public static byte[]? Hash(string? key) => KeyedProof.Hash(key);

    /// <summary>The key's bytes, or null where <paramref name="key"/> is not 64 hex characters.</summary>
    public static byte[]? Read(string? key) => KeyedProof.Read(key);

    /// <summary>
    /// A Daoris executable's first act: the variable taken out of this process's environment, so no start it builds afterwards
    /// copies it, whatever that start does with its own block. A session's delegate key (<see cref="DelegateKey.Variable"/>) and the
    /// inbox's two messaging variables leave with it (<see cref="ChildMarks.MessagingVariables"/>).
    /// </summary>
    public static void Strip()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        Environment.SetEnvironmentVariable(DelegateKey.Variable, null);
        foreach (var name in ChildMarks.MessagingVariables) Environment.SetEnvironmentVariable(name, null);
    }

    /// <summary>
    /// The verb's first act: the variable read into memory, and a session's delegate key with it (<see cref="DelegateKey.Take"/>), then
    /// both removed from the environment as <see cref="Strip"/> removes them. Only <c>sessions supervise</c> asks for what is taken
    /// (<see cref="Taken"/>).
    /// </summary>
    public static void Take()
    {
        _taken = Environment.GetEnvironmentVariable(Variable);
        // Present at the entry, whatever its value and whether or not it is a key.
        TerminalRole.Held = _taken is not null;
        DelegateKey.Take();
        Strip();
    }

    /// <summary>
    /// What <see cref="Take"/> read, the delegate key's too, let go by a verb that holds no key for its life (the events relay); the entry's
    /// marker stays, so a role's terminal is still known as one.
    /// </summary>
    public static void Drop()
    {
        _taken = null;
        DelegateKey.Drop();
    }

    /// <summary>What <see cref="Take"/> read, or null where the variable is not set.</summary>
    public static string? Taken => _taken;

    /// <summary>
    /// The entry's marker: that the variable is present when <see cref="Take"/> runs, whatever its
    /// value, so this process started in a role's terminal. Every guard reads this, never the environment after the entry, which
    /// <see cref="Take"/> has already emptied.
    /// </summary>
    public static bool RoleTerminal => TerminalRole.Held;

    /// <summary>A test's own entry undone, so the marker does not outlive the case that took it.</summary>
    internal static void Forget()
    {
        _taken = null;
        TerminalRole.Held = false;
    }
}

/// <summary>
/// A role's terminal never acts as the owner nor alone: in a process whose entry found
/// <see cref="HolderKey.Variable"/> present, every verb that would send a person's press refuses <see cref="Code"/> before it connects,
/// and every verb that would act from its own process refuses it instead, exit 2.
/// </summary>
public static class RoleTerminal
{
    /// <summary>The refusal's code, which the instance's door says it by.</summary>
    public const string Code = TerminalRole.Code;

    /// <summary>What the refusal says.</summary>
    public const string Sentence = TerminalRole.Sentence;

    /// <summary>
    /// The verbs a role's terminal runs at all (each by its exact prefix): the roles' verbs and the open reads the
    /// holder needs. Any other verb is refused at the entry; these refuse their own alone paths (<see cref="InstanceDoor"/>).
    /// </summary>
    public static bool Runs(IReadOnlyList<string> args) => args switch
    {
        ["roles", "brief"] => true,
        ["sessions", "supervise", ..] => true,
        ["steward", ..] => true,
        ["sessions", "--json", ..] => true,
        ["notices", ..] => true,
        ["history", ..] => true,
        _ => false,
    };

    /// <summary>The refusal as a verb says it, naming its code.</summary>
    public static string Refusal(string verb) => TerminalRole.Refusal(verb);

    /// <summary>
    /// The host's entry, after <see cref="HolderKey.Take"/>: in a role's terminal, a verb it does not run is refused here, said on
    /// <paramref name="error"/>, exit 2; null lets the verb run.
    /// </summary>
    public static int? AtEntry(IReadOnlyList<string> args, TextWriter error)
    {
        if (!HolderKey.RoleTerminal || Runs(args)) return null;
        error.WriteLine(Refusal("daoris"));
        return 2;
    }
}
