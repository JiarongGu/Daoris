namespace Daoris.Ports.Processes;

/// <summary>
/// The machine's seal of a secret kept at rest, for the user this runs as, so a kept file copied to another user or machine opens
/// nothing. The machine's adapter supplies it; the application keeps the sealed values and never names how they are sealed.
/// </summary>
/// <param name="Seal">The value to keep for a secret, a value already sealed coming back as it is; null where this machine cannot seal.</param>
/// <param name="Open">The secret a kept value is, a value kept before any seal coming back as written; null where this user cannot open it.</param>
public sealed record SecretSeal(Func<string, string?> Seal, Func<string, string?> Open);
