using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The one form of an agent account's id: <see cref="Prefix"/> and eight lowercase hex characters. A new account draws it
/// (<see cref="AccountNames.NewId"/>); an account an earlier build named by a counter (<c>account-N</c>) or by the person's word moves to
/// it once (<see cref="AccountIdMoves"/>), to the id <see cref="Moved"/> derives. The person reads the account's name, never this.
/// </summary>
public static class AccountIdForm
{
    /// <summary>What an id begins with, so a log line or a record says it is an account's.</summary>
    public const string Prefix = "acct-";

    private const int Digits = 8;

    /// <summary>Whether <paramref name="id"/> is the hash form, exactly: the prefix and eight lowercase hex characters.</summary>
    public static bool IsHash(string id) =>
        id.Length == Prefix.Length + Digits && id.StartsWith(Prefix, StringComparison.Ordinal)
        && id.AsSpan(Prefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;

    /// <summary>A fresh id: eight hex characters from <paramref name="draw"/>, else the system's random source.</summary>
    public static string Drawn(Func<string>? draw = null) =>
        Prefix + (draw?.Invoke() ?? RandomNumberGenerator.GetHexString(Digits, lowercase: true));

    /// <summary>
    /// The id a legacy id moves to: eight hex characters of the SHA-256 of the agent and the old id, the next eight where an id in
    /// <paramref name="taken"/> has them, so a plan and its apply name the same id and a second plan names none. Drawn where all are taken.
    /// </summary>
    public static string Moved(string agent, string legacy, IReadOnlySet<string> taken)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{agent.Trim().ToLowerInvariant()}/{legacy}")));
        for (var at = 0; at + Digits <= hash.Length; at += Digits)
        {
            var id = Prefix + hash.Substring(at, Digits);
            if (!taken.Contains(id)) return id;
        }

        while (true)
        {
            var id = Drawn();
            if (!taken.Contains(id)) return id;
        }
    }

    /// <summary>
    /// Whether a legacy id was the person's word, which the account keeps as its name where it has none: anything but a counter
    /// (<c>account-</c> and digits), which nobody chose.
    /// </summary>
    public static bool WasAWord(string legacy) =>
        !(legacy.StartsWith("account-", StringComparison.OrdinalIgnoreCase) && legacy.Length > 8 && legacy.AsSpan(8).IndexOfAnyExcept("0123456789") < 0);
}
