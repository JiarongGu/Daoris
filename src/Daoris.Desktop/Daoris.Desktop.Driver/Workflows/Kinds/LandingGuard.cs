namespace Daoris.Driver;

/// <summary>Where a kind's node stands in the protected order before the landing (<see cref="ProtectedOrder"/>).</summary>
public enum LandingGuard
{
    /// <summary>Not a gate of the order.</summary>
    None,

    /// <summary>A gate before every landing, reached before it starts and wired straight into it.</summary>
    Before,

    /// <summary>A gate before every landing, as <see cref="Before"/>, and one every way to a landing passes.</summary>
    Passed,
}
