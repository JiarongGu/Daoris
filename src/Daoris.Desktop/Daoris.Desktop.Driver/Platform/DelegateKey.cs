namespace Daoris.Driver;

/// <summary>
/// The delegate key as the verb holds it: 32 random bytes in hex that Daoris draws at each start of a session it runs and puts in that
/// start's environment as <see cref="Variable"/>, beside the session's id. Daoris keeps only its SHA-256, in memory
/// (the session delegates); the verb proves it over a press's bytes (the delegate proof), and no file, page or pipe carries
/// the key itself.
/// </summary>
/// <remarks>
/// <para><b>Stripped everywhere else</b>, as the holder key is: every Daoris executable's entry removes it (the holder key's strip),
/// every start drops what it inherited and a session's start alone adds its own back. The verb takes it into memory first
/// (<see cref="Take"/>, called by the holder key's take), with the session's id read at the same time.</para>
/// </remarks>
public static class DelegateKey
{
    /// <summary>The variable a session's start carries the key in, and the only place the verb reads it from.</summary>
    public const string Variable = ChildMarks.DelegateKeyVariable;

    private static DelegateHeld? _taken;

    /// <summary>
    /// The verb's first act, inside <see cref="HolderKey.Take"/>: the key and the session's id read into memory, then the key removed from
    /// the environment. A key with no session's id beside it is no delegate's, so nothing is taken.
    /// </summary>
    public static void Take()
    {
        _taken = Read(Environment.GetEnvironmentVariable);
        Environment.SetEnvironmentVariable(Variable, null);
    }

    /// <summary>The key and the session's id as <paramref name="environment"/> holds them; null where either is missing or the key is no key.</summary>
    public static DelegateHeld? Read(Func<string, string?> environment) =>
        environment(Variable) is { } key && KeyedProof.Read(key) is not null && environment(ChildMarks.SessionVariable) is { Length: > 0 } session
            ? new DelegateHeld(session, key)
            : null;

    /// <summary>What <see cref="Take"/> read, let go by a verb that holds no key for its life (the events relay).</summary>
    public static void Drop() => _taken = null;

    /// <summary>What <see cref="Take"/> read, or null where this terminal is no session's Daoris handed a key.</summary>
    public static DelegateHeld? Taken => _taken;
}

/// <summary>A session's delegate key as its verb took it: the session it is handed to, and the key, in hex.</summary>
public sealed record DelegateHeld(string Session, string Key);
