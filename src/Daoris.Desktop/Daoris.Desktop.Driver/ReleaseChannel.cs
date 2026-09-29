using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A detached OpenPGP signature, verified against one PINNED key (AGT2b) — what stands between a
/// vendor's release manifest and the binary Daoris installs from it. The twin of the CLI's
/// <c>openpgp.ts</c>; the two share no code, and their tests read the same vendor files.
/// </summary>
/// <remarks>
/// <para><b>Deliberately narrow.</b> It reads exactly what a vendor's release signing produces — an
/// armoured, detached, binary-document signature, version 4, RSA — and refuses anything else by name.
/// It is not a keyring: the trust root is a fingerprint written into this repository, and a key whose
/// primary fingerprint is not that one verifies nothing. Revocation and expiry are not read, because
/// the pin is the decision.</para>
///
/// <para>🔴 <b>The signer must be the pinned primary key itself.</b> A subkey would need its binding
/// signature checked too, and trusting any subkey in the block would trust whatever key the block's
/// author chose to add.</para>
/// </remarks>
public static class OpenPgp
{
    /// <summary>Whether it verified, the signer when it did, and why not when it did not.</summary>
    public sealed record Verdict(bool Ok, string? Fingerprint, string? Reason);

    /// <summary>A fingerprint as people write it — spaced, any case — as forty hex characters.</summary>
    public static string FingerprintOf(string text) =>
        new string(text.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToUpperInvariant();

    /// <summary>A fingerprint as people read it: ten groups of four.</summary>
    public static string Spaced(string fingerprint) =>
        string.Join(' ', fingerprint.Chunk(4).Select(chunk => new string(chunk)));

    /// <summary>The primary key's fingerprint in an armoured key block.</summary>
    public static string PrimaryFingerprint(string armouredKey) =>
        Keys(Dearmor(armouredKey, "PUBLIC KEY BLOCK")).First(key => key.Primary).Fingerprint;

    /// <summary>Verify a detached signature over <paramref name="data"/>, by the key <paramref name="pinned"/> names.</summary>
    public static Verdict VerifyDetached(byte[] data, string armouredSignature, string armouredKey, string pinned)
    {
        List<PublicKey> keys;
        Signature signature;
        try
        {
            keys = Keys(Dearmor(armouredKey, "PUBLIC KEY BLOCK"));
            signature = ReadSignature(Dearmor(armouredSignature, "SIGNATURE"));
        }
        catch (Exception error) when (error is FormatException or IndexOutOfRangeException or ArgumentException)
        {
            return new Verdict(false, null, error is FormatException ? error.Message : "the key or the signature is damaged — a packet is shorter than it says");
        }

        var want = FingerprintOf(pinned);
        var primary = keys.FirstOrDefault(key => key.Primary);
        if (primary is null || primary.Fingerprint != want)
        {
            return new Verdict(false, null,
                $"the key given is {(primary is null ? "no key at all" : Spaced(primary.Fingerprint))}, and the pinned "
                + $"release key is {Spaced(want)}");
        }

        var named = signature.IssuerFingerprint
            ?? (signature.IssuerKeyId is { } id
                ? keys.FirstOrDefault(key => key.Fingerprint.EndsWith(id, StringComparison.Ordinal))?.Fingerprint
                : null);
        if (named != primary.Fingerprint)
        {
            return new Verdict(false, null,
                $"the signature was made by {(named is null ? "a key it does not name" : Spaced(named))}, not by the "
                + $"pinned release key {Spaced(want)}");
        }

        if (signature.Type != 0x00)
        {
            return new Verdict(false, null,
                $"the signature is of type 0x{signature.Type:x2}, and a release manifest is signed as a binary document (0x00)");
        }
        if (signature.PublicKeyAlgorithm is not (1 or 3))
        {
            return new Verdict(false, null,
                $"the signature uses public-key algorithm {signature.PublicKeyAlgorithm}, and only RSA is read here");
        }
        var hash = signature.HashAlgorithm switch
        {
            8 => HashAlgorithmName.SHA256,
            9 => HashAlgorithmName.SHA384,
            10 => HashAlgorithmName.SHA512,
            _ => (HashAlgorithmName?)null,
        };
        if (hash is not { } name)
        {
            return new Verdict(false, null,
                $"the signature uses hash algorithm {signature.HashAlgorithm}, and only SHA-256, SHA-384 and SHA-512 are read here");
        }

        // What v4 hashes: the document, the hashed part, then a trailer naming the hashed part's length.
        var trailer = new byte[6];
        trailer[0] = 0x04;
        trailer[1] = 0xff;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(trailer.AsSpan(2), (uint)signature.HashedPart.Length);
        var signed = Concat(data, signature.HashedPart, trailer);

        using var digest = IncrementalHash.CreateHash(name);
        digest.AppendData(signed);
        if (!digest.GetHashAndReset().AsSpan(0, 2).SequenceEqual(signature.Left16))
        {
            return new Verdict(false, null, "the signature does not match these bytes — they are not what the release key signed");
        }

        using var rsa = RSA.Create(new RSAParameters { Modulus = primary.Modulus, Exponent = primary.Exponent });
        var value = signature.Value.Length >= primary.Modulus.Length
            ? signature.Value
            : Concat(new byte[primary.Modulus.Length - signature.Value.Length], signature.Value);
        return rsa.VerifyData(signed, value, name, RSASignaturePadding.Pkcs1)
            ? new Verdict(true, primary.Fingerprint, null)
            : new Verdict(false, null, "the signature does not match these bytes — they are not what the release key signed");
    }

    /// <summary>The bytes inside an ASCII armour of the named kind, with its checksum checked.</summary>
    /// <exception cref="FormatException">It is not that armour, or it fails its own checksum.</exception>
    public static byte[] Dearmor(string text, string kind)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var begin = Array.FindIndex(lines, line => line.Trim() == $"-----BEGIN PGP {kind}-----");
        var end = Array.FindIndex(lines, line => line.Trim() == $"-----END PGP {kind}-----");
        if (begin < 0 || end < begin) throw new FormatException($"not an armoured PGP {kind.ToLowerInvariant()}");

        // Headers (`Comment: …`) run to the first blank line; the body follows.
        var at = begin + 1;
        while (at < end && lines[at].Trim().Length > 0) at++;
        var body = lines[(at + 1)..end].Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        string? checksum = null;
        if (body.Count > 0 && body[^1].StartsWith('='))
        {
            checksum = body[^1];
            body.RemoveAt(body.Count - 1);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(string.Concat(body));
        }
        catch (FormatException)
        {
            throw new FormatException($"the armoured PGP {kind.ToLowerInvariant()} is not base64");
        }

        if (checksum is not null)
        {
            byte[] stated;
            try
            {
                stated = Convert.FromBase64String(checksum[1..]);
            }
            catch (FormatException)
            {
                stated = [];
            }
            if (stated.Length != 3 || (stated[0] << 16 | stated[1] << 8 | stated[2]) != Crc24(bytes))
            {
                throw new FormatException(
                    $"the armoured PGP {kind.ToLowerInvariant()} fails its own checksum — it was damaged on the way");
            }
        }

        return bytes;
    }

    private sealed record PublicKey(string Fingerprint, bool Primary, byte[] Modulus, byte[] Exponent);

    private sealed record Signature(
        int Type, int PublicKeyAlgorithm, int HashAlgorithm, string? IssuerFingerprint, string? IssuerKeyId,
        byte[] HashedPart, byte[] Left16, byte[] Value);

    private static IEnumerable<(int Tag, byte[] Body)> Packets(byte[] bytes)
    {
        var at = 0;
        while (at < bytes.Length)
        {
            var first = bytes[at];
            if ((first & 0x80) == 0) throw new FormatException("not an OpenPGP packet");

            int tag;
            long length;
            if ((first & 0x40) != 0)
            {
                tag = first & 0x3f;
                var octet = Byte(bytes, at + 1);
                if (octet < 192)
                {
                    length = octet;
                    at += 2;
                }
                else if (octet < 224)
                {
                    length = ((octet - 192) << 8) + Byte(bytes, at + 2) + 192;
                    at += 3;
                }
                else if (octet == 255)
                {
                    length = (long)Byte(bytes, at + 2) << 24 | (long)Byte(bytes, at + 3) << 16
                        | (long)Byte(bytes, at + 4) << 8 | (long)Byte(bytes, at + 5);
                    at += 6;
                }
                else
                {
                    throw new FormatException("a partial-length packet, which a key or a detached signature never uses");
                }
            }
            else
            {
                tag = (first >> 2) & 0x0f;
                switch (first & 0x03)
                {
                    case 0:
                        length = Byte(bytes, at + 1);
                        at += 2;
                        break;
                    case 1:
                        length = Byte(bytes, at + 1) << 8 | Byte(bytes, at + 2);
                        at += 3;
                        break;
                    case 2:
                        length = (long)Byte(bytes, at + 1) << 24 | (long)Byte(bytes, at + 2) << 16
                            | (long)Byte(bytes, at + 3) << 8 | (long)Byte(bytes, at + 4);
                        at += 5;
                        break;
                    default:
                        throw new FormatException("an indeterminate-length packet, which a key or a detached signature never uses");
                }
            }

            if (at + length > bytes.Length) throw new FormatException("a packet runs past the end of its data");
            yield return (tag, bytes[at..(int)(at + length)]);
            at += (int)length;
        }
    }

    private static List<PublicKey> Keys(byte[] bytes)
    {
        var keys = new List<PublicKey>();
        foreach (var (tag, body) in Packets(bytes))
        {
            if (tag is not (6 or 14)) continue;
            if (body[0] != 4) throw new FormatException($"a version {body[0]} key, and only version 4 is read here");
            if (body[5] is not (1 or 2 or 3)) continue; // Not RSA: never a signer this can check.

            var (n, afterN) = Mpi(body, 6);
            var (e, _) = Mpi(body, afterN);
            var fingerprint = Convert.ToHexString(SHA1.HashData(
                Concat([0x99, (byte)(body.Length >> 8), (byte)body.Length], body)));
            keys.Add(new PublicKey(fingerprint, tag == 6, n, e));
        }

        if (!keys.Any(key => key.Primary)) throw new FormatException("the key block holds no RSA primary key");
        return keys;
    }

    private static Signature ReadSignature(byte[] bytes)
    {
        var signatures = Packets(bytes).Where(packet => packet.Tag == 2).ToList();
        if (signatures.Count != 1)
        {
            throw new FormatException($"a detached signature holds one signature packet, and this holds {signatures.Count}");
        }

        var body = signatures[0].Body;
        if (body[0] != 4) throw new FormatException($"a version {body[0]} signature, and only version 4 is read here");

        var hashedLength = body[4] << 8 | body[5];
        var hashedPart = body[..(6 + hashedLength)];
        var hashed = body[6..(6 + hashedLength)];
        var unhashedLength = body[6 + hashedLength] << 8 | body[7 + hashedLength];
        var unhashed = body[(8 + hashedLength)..(8 + hashedLength + unhashedLength)];
        var rest = 8 + hashedLength + unhashedLength;

        string? issuerFingerprint = null;
        string? issuerKeyId = null;
        foreach (var (area, trusted) in new[] { (hashed, true), (unhashed, false) })
        {
            foreach (var (type, data) in Subpackets(area))
            {
                // The fingerprint is believed only from the HASHED area, which the signature covers.
                if (type == 33 && trusted && data.Length > 0 && data[0] == 4) issuerFingerprint = Convert.ToHexString(data[1..]);
                if (type == 16) issuerKeyId = Convert.ToHexString(data);
            }
        }

        var (value, _) = Mpi(body, rest + 2);
        return new Signature(body[1], body[2], body[3], issuerFingerprint, issuerKeyId, hashedPart, body[rest..(rest + 2)], value);
    }

    private static IEnumerable<(int Type, byte[] Data)> Subpackets(byte[] area)
    {
        var at = 0;
        while (at < area.Length)
        {
            var octet = area[at];
            int length;
            if (octet < 192)
            {
                length = octet;
                at += 1;
            }
            else if (octet < 255)
            {
                length = ((octet - 192) << 8) + area[at + 1] + 192;
                at += 2;
            }
            else
            {
                length = area[at + 1] << 24 | area[at + 2] << 16 | area[at + 3] << 8 | area[at + 4];
                at += 5;
            }

            if (length < 1 || at + length > area.Length) throw new FormatException("a signature subpacket runs past its area");
            yield return (area[at] & 0x7f, area[(at + 1)..(at + length)]);
            at += length;
        }
    }

    /// <summary>A multiprecision integer: its bytes, and where the next field starts.</summary>
    private static (byte[] Value, int Next) Mpi(byte[] bytes, int at)
    {
        var bits = Byte(bytes, at) << 8 | Byte(bytes, at + 1);
        var length = (bits + 7) / 8;
        if (at + 2 + length > bytes.Length) throw new FormatException("a number runs past the end of its packet");
        return (bytes[(at + 2)..(at + 2 + length)], at + 2 + length);
    }

    private static int Byte(byte[] bytes, int at) =>
        at < bytes.Length ? bytes[at] : throw new FormatException("a packet header runs past the end of its data");

    /// <summary>The armour's own checksum (RFC 4880 §6.1).</summary>
    private static int Crc24(byte[] bytes)
    {
        var crc = 0xb704ce;
        foreach (var value in bytes)
        {
            crc ^= value << 16;
            for (var bit = 0; bit < 8; bit++)
            {
                crc <<= 1;
                if ((crc & 0x1000000) != 0) crc ^= 0x1864cfb;
            }
        }

        return crc & 0xffffff;
    }

    internal static byte[] Concat(params byte[][] parts)
    {
        var joined = new byte[parts.Sum(part => part.Length)];
        var at = 0;
        foreach (var part in parts)
        {
            part.CopyTo(joined, at);
            at += part.Length;
        }

        return joined;
    }
}

/// <summary>
/// Claude Code from Anthropic's own release bucket (AGT2b): the manifest, verified against its
/// detached signature under the PINNED release key, and only then its SHA-256 trusted for the binary,
/// fetched directly. Never the bootstrap and never <c>claude install</c> — the bootstrap always fetches
/// latest, and what the subcommand verifies is undocumented
/// (<c>docs/2026-09-24-agt2b-channel-evidence.md</c>). The twin of the CLI's <c>channels.ts</c>.
/// </summary>
/// <remarks>
/// 🔴 <b>The binary is installed and run as published</b> (the vendor's terms): the bytes that verified
/// are the bytes that land. Setting the executable bit is the only change, and it is the file
/// system's, not the binary's.
/// </remarks>
public static class ClaudeReleases
{
    /// <summary>What a toolchain declares to pin from this channel — the CLI's name for it too.</summary>
    public const string Channel = "claude-code-releases";

    public const string Base = "https://downloads.claude.ai/claude-code-releases";

    /// <summary>
    /// Where the bucket names its newest release, as plain text (USE1a, the channel evidence §1). The
    /// undocumented <c>stable</c> pointer beside it is not used. The CLI's <c>CLAUDE_LATEST</c> is the twin.
    /// </summary>
    public const string Latest = Base + "/latest";

    /// <summary>The release key's fingerprint, as the vendor publishes it — <b>the trust root</b>.</summary>
    public const string Fingerprint = "31DDDE24DDFAB679F42D7BD2BAA929FF1A7ECACE";

    /// <summary>The first version whose manifest is signed: 2.1.87's <c>.sig</c> answers 404.</summary>
    public const string FirstSigned = "2.1.89";

    /// <summary>
    /// The release key, carried rather than fetched — and still checked against <see cref="Fingerprint"/>,
    /// so an edit here that is not the vendor's key verifies nothing. The published copy is
    /// <c>https://downloads.claude.ai/keys/claude-code.asc</c>; a test holds the two equal.
    /// </summary>
    public const string ReleaseKey = """
        -----BEGIN PGP PUBLIC KEY BLOCK-----

        mQINBGnK73ABEACnbytJXkjweYrwIr0aLEFRlH+C0nF44KxFc7gQmJ6PjSPMGZAD
        dxZcaixU7zZl8WxEpVO0wLmIH8cf2zGOdyuZg1Yaugk1vHb2b8WBhAGCQJdPgB8W
        XquedepEYtk56uP/gCoTjJDUZluEGBHnlnuujSJ4orxEdhSykEoAUfJZGEILPpMd
        bphFt/Sn+Eb/TxM5jpKPdwnv8AShNF/1mZU1fWTQq9tRKJUakZj04gdaDFElQXak
        CtTij+GT6yoYCARSHwGO+PC/Pr6q4tc+D7LRjxSBvUWDoFSmlqb/PJ1hj9D/7I2O
        e4XXniAPWMR56KvxHlzOzrNQdJujbJdSkCwh1ZijkSd3y8ayW5WYUTGdRab99NUw
        agzlabe/VVF6kzJ0Scn5q3PihB2Y9Bwo0CKnkYk7a7KT77EWv0Kkq+VHmOtqX3a2
        hhX+b6a6ve9rzJ1qZYGj+obv/C3Sx1LzUjAfqVy7RJDf2uAoP5t2g8u/TkSpUxhM
        VEjZBkSxYZhMyzQM6t8IgkUfnSrIPTHixbDWARZ4beMOBjxyPZK1nP7OOrNR3TkK
        JtwLMQAabURCDnL0PjS0iwBTU4jtumBD1XSULyWuoTvMljrpQr1nV1oDyOt0OLqa
        KA2McWtd9PdXhC8y2EIg7TmrTlJLfHYbdmkiCYj4J49Q8HWkN/6WE+RTUwARAQAB
        tD5BbnRocm9waWMgQ2xhdWRlIENvZGUgUmVsZWFzZSBTaWduaW5nIDxzZWN1cml0
        eUBhbnRocm9waWMuY29tPokCUQQTAQoAOxYhBDHd3iTd+rZ59C170rqpKf8afsrO
        BQJpyu9wAhsPBQsJCAcCAiICBhUKCQgLAgQWAgMBAh4HAheAAAoJELqpKf8afsrO
        l5IP/2I8X1dFy5xYczWB/coIxGjuzS/V6ByZGZZEJsbr04pmuHiFUykJqPGWGQ6q
        U0YF5iEwvEkaagS5m7DzhSEf3FM3Cgafax/6d70tar9Vr1D+w6uPfxetu7u/WYJp
        aolIsdh5fTrBh9zSM1Njl8FM8wG8CwZQjS33Oa7d8cwRkgdUWbt6LXgz+cTQNuBn
        BgW6Ks7oZFI25dfu0ojDR+aDFJg4+4wZoyDLPvJz1SIrJ5WFGs67zsx9SfS3yZnf
        XKmBe+f0dUy+GJ2nFZrXFf99+c0dPEHYO8DCeAHZizjkFrdYtUHdDU0YDYEGkLJa
        bE+pgcpkHf5EvsZzHsyDbl95W/eh7pcXMbwkN+W4CBYUE9X4uHhqzWaC5yAVRWUA
        1BJ9V4LjZfHPLEJt0I3TxzXiEg9/BVeaTYq9RjaxIFo9Nfk158HqJY6SA5jslBlx
        Gv/No8u+xVcze2UJyGVfEIUfm92+0UAIkny3+5cuVV0ICzJxXlXj0CnLM9Lt50wE
        p3suVwuBEviCbZ08eAH1Ht8gbBdSsiOkIU8CX3v/scwHHx5q0+NBL6xLrQObg13a
        tRXBlKObfElkPN3lTUbUnJOW4U8uSjH8VRP+AujKWMDFe7x0zCs+iYY1mTOvbrTS
        9n3CmZUmbynZ+E/QWNENpW/pDNZdWFy43PASmML5FHu4m9Sn
        =oqMI
        -----END PGP PUBLIC KEY BLOCK-----
        """;

    /// <summary>The build for a machine, in the bucket's own names.</summary>
    public static string Platform(string os, string arch, bool musl)
    {
        var known = os is "win32" or "darwin" or "linux" && arch is "x64" or "arm64";
        if (!known)
        {
            throw new DriverException(
                $"Claude Code publishes no build for {os}-{arch}, so there is nothing to pin here. Install it with "
                + "its own tooling if it runs on this machine; unpinned, Daoris runs whatever is on PATH.");
        }

        return $"{os}-{arch}{(os == "linux" && musl ? "-musl" : "")}";
    }

    /// <summary>This machine's build.</summary>
    public static string Current()
    {
        var os = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
        return Platform(os, arch, musl: RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Refuse a version this channel cannot install verifiably, before anything is fetched.
    /// </summary>
    /// <remarks>
    /// 🔴 A version before 2.1.89 is refused, not installed unverified — its manifest has no signature,
    /// so the one file the signature exists to vouch for would be taken on trust, and a fall back to
    /// npm would be the same trust by another road.
    /// </remarks>
    public static void RefuseVersion(string version)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
        {
            throw new DriverException(
                $"`{version}` is not a Claude Code version — a pin names one exact release, like 2.1.281, never a "
                + "pointer such as latest.");
        }

        if (new Version(version) < new Version(FirstSigned))
        {
            throw new DriverException(
                $"Claude Code {version} was published before its release manifests were signed (the first is "
                + $"{FirstSigned}), so Daoris cannot verify it and does not install it. Pin {FirstSigned} or later — or "
                + "install that version with its own tooling, and unpinned, Daoris runs it from PATH.");
        }
    }

    /// <summary>
    /// The exact version the bucket names as its newest release (USE1a) — what Update pins.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The pointer only chooses a version; it vouches for nothing.</b> It is not signed, so it is
    /// read as one exact version or refused, and the version it names is then installed by
    /// <see cref="InstallAsync"/>, verified exactly as a typed one. A pin never names the pointer
    /// itself: a pin meaning "whatever is newest today" would change under a running arrangement.
    /// </remarks>
    public static async Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport = null)
    {
        using var http = transport is null ? new HttpClient() : new HttpClient(transport, disposeHandler: false);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");

        var body = await BytesAsync(http, Latest, ct).ConfigureAwait(false)
            ?? throw new DriverException(
                $"nothing answered at {Latest}, where the release channel names its newest Claude Code — nothing was "
                + "fetched or pinned.");
        var said = Encoding.UTF8.GetString(body).Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(said, @"^\d+\.\d+\.\d+$"))
        {
            var shown = said.Length > 60 ? said[..60] + "…" : said;
            throw new DriverException(
                $"the release channel's newest-release pointer at {Latest} answered `{shown}`, which is not a version — "
                + "nothing was fetched or pinned.");
        }

        return said;
    }

    /// <summary>
    /// Install one version into <paramref name="where"/>, verified end to end. Staged at
    /// <c>&lt;where&gt;.part</c> and moved into place only once it verified, so the managed directory
    /// holds a verified install or nothing — <see cref="HarnessSettings.ManagedBinary"/> finding one is
    /// the proof.
    /// </summary>
    /// <returns>The executable, inside <paramref name="where"/>.</returns>
    public static async Task<string> InstallAsync(
        string where, string version, Action<string> write, CancellationToken ct,
        HttpMessageHandler? transport = null, string? platform = null, (string Key, string Fingerprint)? trust = null)
    {
        RefuseVersion(version);
        platform ??= Current();
        var binary = platform.StartsWith("win32", StringComparison.Ordinal) ? "claude.exe" : "claude";
        var (key, pinned) = trust ?? (ReleaseKey, Fingerprint);

        // A 200 MB download outlives any fixed timeout; the person's stop is the token.
        using var http = transport is null
            ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");

        var staging = where + ".part";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        var into = Path.Combine(staging, "package", "bin");
        Directory.CreateDirectory(into);

        try
        {
            var manifestUrl = $"{Base}/{version}/manifest.json";
            write($"fetching Claude Code {version}'s release manifest for {platform}, and its signature");
            var manifest = await BytesAsync(http, manifestUrl, ct).ConfigureAwait(false)
                ?? throw new DriverException(
                    $"the release bucket holds no Claude Code {version} — nothing answered at {manifestUrl}. Versions "
                    + "skip numbers; the release notes name the ones that exist.");
            var signature = await BytesAsync(http, manifestUrl + ".sig", ct).ConfigureAwait(false)
                ?? throw new DriverException(
                    $"Claude Code {version}'s manifest has no signature beside it, so Daoris cannot verify it and did "
                    + "not install it. Nothing was downloaded beyond the manifest.");

            var verdict = OpenPgp.VerifyDetached(manifest, Encoding.UTF8.GetString(signature), key, pinned);
            if (!verdict.Ok)
            {
                throw new DriverException(
                    $"Claude Code {version}'s manifest did not verify: {verdict.Reason}. Nothing was installed — a "
                    + "manifest that does not verify vouches for no binary.");
            }
            write($"the signature verifies: signed by the release key {OpenPgp.Spaced(verdict.Fingerprint!)}");

            var (checksum, size) = Entry(manifest, manifestUrl, version, platform, binary);

            var url = $"{Base}/{version}/{platform}/{binary}";
            write($"downloading {url} ({size / 1024.0 / 1024.0:0.0} MB)");
            var saved = await SaveAsync(http, url, Path.Combine(into, binary), ct).ConfigureAwait(false)
                ?? throw new DriverException($"nothing answered at {url}, which Claude Code {version}'s signed manifest names.");
            if (saved.Sha256 != checksum || saved.Size != size)
            {
                throw new DriverException(
                    $"the binary downloaded from {url} is not the one the signed manifest names — its SHA-256 is "
                    + $"{saved.Sha256} ({saved.Size} bytes), and the manifest says {checksum} ({size} bytes). It was "
                    + "deleted, and nothing was installed.");
            }
            write($"the binary's SHA-256 matches the signed manifest: {saved.Sha256}");

            if (!OperatingSystem.IsWindows() && !platform.StartsWith("win32", StringComparison.Ordinal))
            {
                File.SetUnixFileMode(Path.Combine(into, binary),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (Directory.Exists(where))
            {
                // Daoris's own directory, holding nothing that runs (the caller asked ManagedBinary first).
                write($"replacing {where}, which held nothing that runs");
                Directory.Delete(where, recursive: true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(where)!);
            Directory.Move(Path.Combine(staging, "package"), where);
            return Path.Combine(where, "bin", binary);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    /// <summary>The platform's entry in a manifest that verified — and the version it signed.</summary>
    private static (string Checksum, long Size) Entry(byte[] manifest, string url, string version, string platform, string binary)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(manifest).RootElement;
        }
        catch (JsonException)
        {
            throw new DriverException($"Claude Code {version}'s manifest verified and is not JSON — nothing was installed.");
        }

        // 🔴 A genuine manifest served for another version verifies perfectly. Only the version it
        // SIGNED stops an old release being installed under a new release's name.
        var signed = root.TryGetProperty("version", out var said) && said.ValueKind == JsonValueKind.String ? said.GetString() : null;
        if (signed != version)
        {
            throw new DriverException(
                $"the signed manifest at {url} is for Claude Code {signed}, not {version} — a genuine manifest "
                + "served for the wrong version. Nothing was installed.");
        }

        if (!root.TryGetProperty("platforms", out var platforms) || platforms.ValueKind != JsonValueKind.Object
            || !platforms.TryGetProperty(platform, out var entry) || entry.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException($"Claude Code {version}'s signed manifest lists no build for {platform}, so there is nothing to install.");
        }

        var named = entry.TryGetProperty("binary", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
        if (named != binary)
        {
            throw new DriverException(
                $"Claude Code {version}'s signed manifest names `{named}` for {platform}, where the tool's own binary "
                + $"is `{binary}` — refused rather than followed.");
        }

        var checksum = entry.TryGetProperty("checksum", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        if (checksum is null || !System.Text.RegularExpressions.Regex.IsMatch(checksum, "^[0-9a-fA-F]{64}$")
            || !entry.TryGetProperty("size", out var s) || !s.TryGetInt64(out var size) || size <= 0)
        {
            throw new DriverException($"Claude Code {version}'s signed manifest carries no usable SHA-256 and size for {platform}.");
        }

        return (checksum.ToLowerInvariant(), size);
    }

    private static async Task<byte[]?> BytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        return response is null ? null : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Stream a download to disk, hashing on the way — never held whole.</summary>
    private static async Task<(string Sha256, long Size)?> SaveAsync(HttpClient http, string url, string to, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        if (response is null) return null;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(to))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                size += read;
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }

    /// <summary>The response, or null for a 404 — "that version is not there" is an answer the caller words.</summary>
    private static async Task<HttpResponseMessage?> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            throw new DriverException($"could not reach {new Uri(url).Host} — {error.Message}. Nothing was installed.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new DriverException($"{url} answered {status} — nothing was installed.");
        }

        return response;
    }
}
