namespace Daoris.Driver;

/// <summary>What fully landed means for a repository, the kind it declares.</summary>
public enum LandingKind
{
    /// <summary>The work is on the repository's own line in this checkout.</summary>
    Local,

    /// <summary>A pull request of the work is merged on its remote.</summary>
    PullRequestMerge,

    /// <summary>The work is in a release.</summary>
    Release,

    /// <summary>A family's pin moved onto the work.</summary>
    PinMove,
}

/// <summary>A landed proof's answer: <see cref="Unknown"/> is never landed.</summary>
public enum LandedVerdict
{
    Landed,
    Unlanded,
    Unknown,
}

/// <summary>What a landed proof is asked: whether the work at <paramref name="Work"/> (a save's commit or a branch) is on <paramref name="Line"/>, in the repository at <paramref name="Root"/>.</summary>
public sealed record LandedQuestion(string Root, string Work, string Line);

/// <summary>A landed proof's answer and the evidence it rests on, in a clause.</summary>
public sealed record LandedAnswer(LandedVerdict Verdict, string Evidence)
{
    public bool Landed => Verdict == LandedVerdict.Landed;

    public static LandedAnswer Unknown(string why) => new(LandedVerdict.Unknown, why);
}

/// <summary>
/// The landed proof, a port: a condition code checks, never an agent's word. Each proof answers for the landing kinds it names;
/// <see cref="LandedProofKinds"/> wires them and chooses one by the repository's declared kind, so another proof is its own module and a
/// line of that wiring. Anything a proof cannot read is <see cref="LandedVerdict.Unknown"/>, which no reader counts as landed.
/// </summary>
public interface ILandedProof
{
    /// <summary>The landing kinds this proof answers for.</summary>
    IReadOnlyCollection<LandingKind> Kinds { get; }

    Task<LandedAnswer> ProveAsync(LandedQuestion question, CancellationToken ct);
}
