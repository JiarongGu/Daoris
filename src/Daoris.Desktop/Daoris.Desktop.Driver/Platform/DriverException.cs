namespace Daoris.Driver;

/// <summary>A driver error a person can act on. Exit code 2 territory: tool error, not policy.</summary>
/// <remarks>
/// Not sealed since TOOLS4: <see cref="ToolRefusal"/> is one that also names the check that failed, and travels every
/// road a driver error already does. It lives in Platform since PLATEXC1: the kernel throws it, so it must not sit in
/// Agents.
/// </remarks>
public class DriverException(string message) : Exception(message);
