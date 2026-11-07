namespace Daoris.Driver;

/// <summary>One account the next start does not take, and what holds it.</summary>
/// <param name="Account">The profile's name, or null for the tool's own sign-in.</param>
/// <param name="Until">When it is offered again, where it is cooling.</param>
public sealed record AccountHeld(string? Account, NextHold Hold, DateTimeOffset? Until = null);
