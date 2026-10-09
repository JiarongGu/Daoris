using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

/// <summary>
/// The person key (PERSONDOOR1a, D156 point 1; the person-door design §2.1, §2.3): a random 256-bit value minted for each
/// start of a local host by whoever starts it, handed on the first line of the host's standard input, and presented as
/// <see cref="Header"/> on the doors that are the person's or the driver's (<see cref="PersonDoors"/>).
/// </summary>
/// <remarks>
/// <para><b>Why the input.</b> The starter's standard input is the one channel no other process is handed: an environment
/// is copied into every child the host or its starter makes, and on Linux any process of the account reads
/// <c>/proc/&lt;pid&gt;/environ</c>. The shell already starts the host with its input redirected (LOG2a), so the key
/// travels first on it, and <see cref="InputEndStop"/> watches what follows for its end.</para>
///
/// <para><b>Asked, never assumed.</b> Only a host started with <see cref="InputVariable"/> set to <c>1</c> reads its input
/// for a key. A host handed none keeps today's trust (design §2.1; PERSONDOOR1i makes every host enforce), and its input is
/// never opened for this. A host that was asked and handed no key, or a line that is not one, does not start: a starter
/// that meant to gate the doors and failed must not leave them open by its mistake.</para>
///
/// <para><b>What a key is</b>: 32 bytes written in base64url without padding, 43 characters, the one canonical spelling
/// of those bytes. A caller presents the text, compared in constant time. The proof the shell asks before it trusts the
/// host it started (<see cref="Prove"/>) is HMAC-SHA-256 under the 32 bytes, of the nonce's UTF-8 bytes, written in
/// base64url without padding.</para>
///
/// <para><b>Never written.</b> The host keeps the key in this object's memory only: not in a file, an environment, the
/// machine log or an answer. A shared host reads none (design §6): its gate is minted keys.</para>
///
/// <para><b>A twin</b> (<c>.claude/knowledge/twins.md</c>): the shell that mints and hands the key (PERSONDOOR1g) spells
/// <see cref="InputVariable"/>, <see cref="Header"/>, the key's form and the proof again, on purpose, since the artefacts
/// share no code. <c>PersonKeyTests</c> holds the rows its tests are to match, the proof's vectors among them.</para>
/// </remarks>
public sealed class PersonKey
{
    /// <summary>The variable that asks a host to read its key from the first line of its standard input.</summary>
    public const string InputVariable = "DAORIS_PERSON_KEY_ON_INPUT";

    /// <summary>The header a caller presents the key in.</summary>
    public const string Header = "Daoris-Person";

    /// <summary>How many random bytes a key is.</summary>
    public const int Size = 32;

    /// <summary>How long a host asked for its key waits for the line before it refuses to start.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    private readonly byte[] _secret;
    private readonly byte[] _text;

    private PersonKey(byte[] secret, string text)
    {
        _secret = secret;
        _text = Encoding.ASCII.GetBytes(text);
    }

    /// <summary>Whether a value of <see cref="InputVariable"/> asks for the key: <c>1</c>, and nothing else, as LOG2a's.</summary>
    public static bool Asks(string? value) => value == "1";

    /// <summary>
    /// The key a line spells, or null when it spells none: 43 base64url characters that are the canonical spelling of 32
    /// bytes. The spaces around it, a Windows line's <c>\r</c> among them, are not part of it.
    /// </summary>
    public static PersonKey? Parse(string? line)
    {
        var text = line?.Trim();
        if (text is not { Length: 43 } || !text.All(IsBase64UrlCharacter)) return null;

        byte[] secret;
        try
        {
            secret = Base64Url.DecodeFromChars(text);
        }
        catch (FormatException)
        {
            return null;
        }

        // A last character whose spare bits are set decodes to the same bytes as the canonical one, and is a second
        // spelling of one key: refused, so a key has exactly one text a caller can present.
        return secret.Length == Size && string.Equals(Base64Url.EncodeToString(secret), text, StringComparison.Ordinal)
            ? new PersonKey(secret, text)
            : null;
    }

    /// <summary>Whether <paramref name="presented"/> is this key's text, compared in constant time for a text of its length.</summary>
    public bool Matches(string? presented) =>
        presented is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _text);

    /// <summary>
    /// The proof of possession for <paramref name="nonce"/> (design §2.3): HMAC-SHA-256 under the key's 32 bytes, of the
    /// nonce's UTF-8 bytes, in base64url without padding. Whoever holds the key can check it; nobody else can make it.
    /// </summary>
    public string Prove(string nonce) => Base64Url.EncodeToString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(nonce)));

    /// <summary>
    /// The key this start was handed, read once at the start (design §2.1): none and no refusal when
    /// <paramref name="asked"/> does not ask, and then <paramref name="input"/> is never called; otherwise the key on the
    /// input's first line, or the sentence the host refuses to start with.
    /// </summary>
    public static (PersonKey? Key, string? Refusal) FromInput(
        ServiceMode mode, string? asked, Func<TextReader> input, TimeSpan wait)
    {
        if (!Asks(asked)) return (null, null);

        if (mode == ServiceMode.Shared)
        {
            return (null,
                $"{InputVariable} asks a shared host for a person key, and a shared host reads none (D156 point 6): its gate "
                + "is the keys minted for each person's machine. Unset it, or start this host in local mode.");
        }

        string? line;
        try
        {
            // The read blocks until the starter writes the line; a starter that never does is waited for, then refused.
            var reading = Task.Run(() => input().ReadLine());
            if (!reading.Wait(wait))
            {
                return (null,
                    $"{InputVariable} is 1, and no key arrived on standard input within {wait.TotalSeconds:0} seconds. Whoever "
                    + "starts this host writes its key as the first line of its input.");
            }

            line = reading.Result;
        }
        catch (AggregateException failed) when (failed.InnerException is IOException or ObjectDisposedException)
        {
            line = null;
        }

        return Parse(line) is { } key
            ? (key, null)
            : (null,
                $"{InputVariable} is 1, and the first line of standard input is not a person key: a key is {Size} random bytes "
                + "in base64url without padding, 43 characters. The host did not start, so no door was left open.");
    }

    private static bool IsBase64UrlCharacter(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
}
