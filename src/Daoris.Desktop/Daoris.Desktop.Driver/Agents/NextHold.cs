namespace Daoris.Driver;

/// <summary>What keeps the next start off an account of the agent's (TOOL6e), or that nothing does.</summary>
public enum NextHold
{
    /// <summary>Ready, and ranked after the account the next start takes.</summary>
    Ready,

    /// <summary>Ready, and near its limit by what its agent said (<see cref="AccountReadings.Near"/>), so it goes last (D130 §6).</summary>
    Near,

    /// <summary>Its cool-off has not passed (D125 §2): it waits for a time.</summary>
    Cooling,

    /// <summary>Its provider refused its credential (AGT3b): it waits for a person.</summary>
    Refused,

    /// <summary>The agent last said nobody is signed in there: it waits for a person.</summary>
    SignedOut,

    /// <summary>Kept for conversations, which driven work never starts on (§4.6).</summary>
    Kept,

    /// <summary>The list names it, and this machine has no such account.</summary>
    Missing,

    /// <summary>The scope does not use it: never carries its starts (D130 §3.1).</summary>
    Outside,
}
