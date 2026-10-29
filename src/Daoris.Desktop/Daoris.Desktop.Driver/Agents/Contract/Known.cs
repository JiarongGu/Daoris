namespace Daoris.Driver;

/// <summary>Where a known fact comes from (the agent-provider design §2.4): declared, negotiated at an opening, or measured.</summary>
public enum KnownBy
{
    Declared,
    Negotiated,
    Measured,
}

/// <summary>
/// A fact about another program, naming its source, its evidence and where it applies, or stated absent (the agent-provider design
/// §2.4). Absent is a statement and reads as today's behaviour for an agent that says nothing (D53); it is never a value to read.
/// </summary>
/// <remarks>
/// <see cref="Applies"/> is a plain description here; COAGENT1e builds the effective configuration it names. Declared, negotiated and
/// measured stay separate evidence, never a score.
/// </remarks>
public readonly record struct Known<T>(T? Value, KnownBy By, string Evidence, string Applies, bool IsKnown)
{
    /// <summary>Applies to every start: the default of a declared fact.</summary>
    public const string Anywhere = "any";

    /// <summary>Stated absent.</summary>
    public static Known<T> Absent => default;

    public static Known<T> Declared(T value, string evidence, string applies = Anywhere) =>
        new(value, KnownBy.Declared, evidence, applies, true);

    public static Known<T> Negotiated(T value, string evidence, string applies) =>
        new(value, KnownBy.Negotiated, evidence, applies, true);

    public static Known<T> Measured(T value, string evidence, string applies) =>
        new(value, KnownBy.Measured, evidence, applies, true);

    /// <summary>The value, where it is known; asking an absent fact for it is a defect in the asker.</summary>
    public T Required => IsKnown
        ? Value!
        : throw new InvalidOperationException("this fact is stated absent: ask whether it is known before reading it");

    public bool TryGet(out T value)
    {
        value = IsKnown ? Value! : default!;
        return IsKnown;
    }
}
