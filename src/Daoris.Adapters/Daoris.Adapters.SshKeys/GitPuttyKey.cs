using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Math;

namespace Daoris.Adapters.SshKeys;

internal static class GitPuttyKey
{
    internal static AsymmetricKeyParameter Read(string text, string? passphrase)
    {
        using var lines = new StringReader(text);
        var first = lines.ReadLine() ?? throw new FormatException();
        var version = first.StartsWith("PuTTY-User-Key-File-2: ", StringComparison.Ordinal) ? 2
            : first.StartsWith("PuTTY-User-Key-File-3: ", StringComparison.Ordinal) ? 3 : throw new FormatException();
        var algorithm = first[(first.IndexOf(':') + 2)..];
        var encryption = Field(lines, "Encryption");
        if (encryption is not ("none" or "aes256-cbc")) throw new FormatException();
        var comment = Field(lines, "Comment");
        var publicBlob = Blob(lines, "Public-Lines");
        var password = Encoding.UTF8.GetBytes(encryption == "none" ? "" : passphrase ?? "");
        byte[] cipherKey = [], iv = new byte[16], macKey;
        if (version == 2)
        {
            cipherKey = [.. SHA1.HashData([0, 0, 0, 0, .. password]), .. SHA1.HashData([0, 0, 0, 1, .. password])[..12]];
            macKey = SHA1.HashData([.. Encoding.ASCII.GetBytes("putty-private-key-file-mac-key"), .. password]);
        }
        else if (encryption != "none")
        {
            var flavour = Field(lines, "Key-Derivation") switch
            {
                "Argon2d" => Argon2Parameters.Argon2d,
                "Argon2i" => Argon2Parameters.Argon2i,
                "Argon2id" => Argon2Parameters.Argon2id,
                _ => throw new FormatException(),
            };
            var memory = Number(Field(lines, "Argon2-Memory"), 256 * 1024);
            var passes = Number(Field(lines, "Argon2-Passes"), 100);
            var parallelism = Number(Field(lines, "Argon2-Parallelism"), 32);
            if ((long)memory * passes > 8 * 1024 * 1024 || memory < 8 * parallelism) throw new FormatException();
            var salt = Convert.FromHexString(Field(lines, "Argon2-Salt"));
            if (salt.Length is < 8 or > 64) throw new FormatException();
            var generator = new Argon2BytesGenerator();
            generator.Init(new Argon2Parameters.Builder(flavour).WithSalt(salt).WithMemoryAsKB(memory).WithIterations(passes).WithParallelism(parallelism).Build());
            var material = new byte[80];
            generator.GenerateBytes(password, material);
            cipherKey = material[..32];
            iv = material[32..48];
            macKey = material[48..];
            CryptographicOperations.ZeroMemory(material);
        }
        else macKey = [];
        var privateBlob = Blob(lines, "Private-Lines");
        var expected = Convert.FromHexString(Field(lines, "Private-MAC"));
        if (lines.ReadToEnd().Trim().Length != 0) throw new FormatException();
        try
        {
            if (encryption != "none")
            {
                using var aes = Aes.Create();
                aes.Key = cipherKey;
                privateBlob = aes.DecryptCbc(privateBlob, iv, PaddingMode.None);
            }
            using var signed = new MemoryStream();
            GitOpenSshKey.String(signed, algorithm);
            GitOpenSshKey.String(signed, encryption);
            GitOpenSshKey.String(signed, comment);
            GitOpenSshKey.Block(signed, publicBlob);
            GitOpenSshKey.Block(signed, privateBlob);
            var actual = version == 2 ? HMACSHA1.HashData(macKey, signed.ToArray()) : HMACSHA256.HashData(macKey, signed.ToArray());
            if (!CryptographicOperations.FixedTimeEquals(expected, actual)) throw new FormatException();
            var key = Private(algorithm, publicBlob, privateBlob);
            if (!GitOpenSshKey.Public(key).AsSpan().SequenceEqual(publicBlob)) throw new FormatException();
            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            CryptographicOperations.ZeroMemory(cipherKey);
            CryptographicOperations.ZeroMemory(macKey);
            CryptographicOperations.ZeroMemory(privateBlob);
        }
    }

    private static AsymmetricKeyParameter Private(string algorithm, byte[] publicBlob, byte[] privateBlob)
    {
        var publicKey = OpenSshPublicKeyUtilities.ParsePublicKey(publicBlob);
        if (new GitOpenSshKey.Reader(publicBlob).String() != algorithm) throw new FormatException();
        var reader = new GitOpenSshKey.Reader(privateBlob);
        switch (publicKey)
        {
            case RsaKeyParameters rsa:
                var d = reader.Integer();
                var p = reader.Integer();
                var q = reader.Integer();
                var inverse = reader.Integer();
                if (!p.Multiply(q).Equals(rsa.Modulus) || !q.ModInverse(p).Equals(inverse)) throw new FormatException();
                return new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.Exponent, d, p, q, d.Mod(p.Subtract(BigInteger.One)), d.Mod(q.Subtract(BigInteger.One)), inverse);
            case ECPublicKeyParameters ec:
                return new ECPrivateKeyParameters(reader.Integer(), ec.Parameters);
            case Ed25519PublicKeyParameters:
                var seed = reader.Integer().ToByteArrayUnsigned();
                if (seed.Length > 32) throw new FormatException();
                var padded = new byte[32];
                seed.CopyTo(padded, 32 - seed.Length);
                return new Ed25519PrivateKeyParameters(padded, 0);
            default:
                throw new FormatException();
        }
    }

    private static string Field(StringReader lines, string name)
    {
        var line = lines.ReadLine();
        var prefix = name + ": ";
        return line is not null && line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : throw new FormatException();
    }

    private static int Number(string text, int maximum) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && number > 0 && number <= maximum ? number : throw new FormatException();

    private static byte[] Blob(StringReader lines, string name)
    {
        var count = Number(Field(lines, name), 32768);
        var value = new StringBuilder();
        for (var at = 0; at < count; at++) value.Append(lines.ReadLine() ?? throw new FormatException());
        return Convert.FromBase64String(value.ToString());
    }
}
