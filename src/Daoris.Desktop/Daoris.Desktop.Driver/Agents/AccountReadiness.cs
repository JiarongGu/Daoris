namespace Daoris.Driver;

/// <summary>Why an account a start may use is or is not ready: every way of not being ready is walked past alike.</summary>
public enum AccountReadiness
{
    /// <summary>Not cooling, not refused, and not signed out: it may run.</summary>
    Ready,

    /// <summary>Its cool-off has not passed: it waits for a time.</summary>
    Cooling,

    /// <summary>Its provider refused its credential: it waits for a person.</summary>
    Refused,

    /// <summary>The agent says nobody is signed in there, or its key is gone: it waits for a person.</summary>
    SignedOut,
}
