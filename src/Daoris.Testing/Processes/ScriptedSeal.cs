using System.Text;
using Daoris.Ports.Processes;

namespace Daoris.Testing;

/// <summary>
/// The machine's seal as a test keeps a secret: reversible, marked by <see cref="Prefix"/>, and readable on any machine, so a unit test
/// proves what the application keeps sealed without the user's real seal. A marked value it did not seal opens nothing.
/// </summary>
public static class ScriptedSeal
{
    /// <summary>What marks a value this seal kept.</summary>
    public const string Prefix = "scripted1:";

    // Inside the encoding, so a marked value this seal did not make (another user's, in a test) opens to null.
    private const string Mark = "sealed:";

    /// <summary>The seal as a test's root hands it.</summary>
    public static SecretSeal Port { get; } = new(Seal, Open);

    private static string? Seal(string secret) =>
        secret.StartsWith(Prefix, StringComparison.Ordinal) ? secret : Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(Mark + secret));

    private static string? Open(string kept)
    {
        if (!kept.StartsWith(Prefix, StringComparison.Ordinal)) return kept;
        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(kept[Prefix.Length..]));
            return text.StartsWith(Mark, StringComparison.Ordinal) ? text[Mark.Length..] : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
