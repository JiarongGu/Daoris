using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;

namespace Daoris.Adapters.SshKeys.Tests;

internal static class PuttyKeyFixture
{
    internal const string Passphrase = "one passphrase";

    internal static (string Text, byte[] Seed) Ed25519(int version, bool encrypted)
    {
        var seed = Enumerable.Range(1, 32).Select(each => (byte)each).ToArray();
        var key = new Ed25519PrivateKeyParameters(seed, 0);
        var publicBlob = OpenSshPublicKeyUtilities.EncodePublicKey(key.GeneratePublicKey());
        var privateBlob = Strings(seed);
        return (Encode(version, encrypted, "ssh-ed25519", publicBlob, privateBlob), seed);
    }

    internal static string Rsa(int version, RSA key)
    {
        var values = key.ExportParameters(true);
        return Encode(version, true, "ssh-rsa", Strings(Encoding.ASCII.GetBytes("ssh-rsa"), Positive(values.Exponent!), Positive(values.Modulus!)),
            Strings(Positive(values.D!), Positive(values.P!), Positive(values.Q!), Positive(values.InverseQ!)));
    }

    internal static string Ec(int version, ECDsa key, string curve)
    {
        var values = key.ExportParameters(true);
        var algorithm = "ecdsa-sha2-" + curve;
        return Encode(version, true, algorithm, Strings(Encoding.ASCII.GetBytes(algorithm), Encoding.ASCII.GetBytes(curve), [4, .. values.Q.X!, .. values.Q.Y!]), Strings(Positive(values.D!)));
    }

    private static byte[] Positive(byte[] bytes) => bytes[0] >= 128 ? [0, .. bytes] : bytes;

    private static string Encode(int version, bool encrypted, string algorithm, byte[] publicBlob, byte[] privateBlob)
    {
        if (encrypted) Array.Resize(ref privateBlob, (privateBlob.Length + 15) / 16 * 16);
        var encryption = encrypted ? "aes256-cbc" : "none";
        var password = Encoding.UTF8.GetBytes(encrypted ? Passphrase : "");
        byte[] aesKey = [], iv = new byte[16], macKey;
        var headers = "";
        if (version == 2)
        {
            macKey = SHA1.HashData([.. Encoding.ASCII.GetBytes("putty-private-key-file-mac-key"), .. password]);
            aesKey = [.. SHA1.HashData([0, 0, 0, 0, .. password]), .. SHA1.HashData([0, 0, 0, 1, .. password])[..12]];
        }
        else if (encrypted)
        {
            var salt = Enumerable.Range(0, 16).Select(each => (byte)each).ToArray();
            var generator = new Argon2BytesGenerator();
            generator.Init(new Argon2Parameters.Builder(Argon2Parameters.Argon2id).WithSalt(salt).WithMemoryAsKB(32).WithIterations(2).WithParallelism(1).Build());
            var material = new byte[80];
            generator.GenerateBytes(password, material);
            aesKey = material[..32];
            iv = material[32..48];
            macKey = material[48..];
            headers = $"Key-Derivation: Argon2id\nArgon2-Memory: 32\nArgon2-Passes: 2\nArgon2-Parallelism: 1\nArgon2-Salt: {Convert.ToHexString(salt)}\n";
        }
        else macKey = [];
        var signed = Strings(Encoding.ASCII.GetBytes(algorithm), Encoding.ASCII.GetBytes(encryption), Encoding.UTF8.GetBytes("fixture"), publicBlob, privateBlob);
        var mac = version == 2 ? HMACSHA1.HashData(macKey, signed) : HMACSHA256.HashData(macKey, signed);
        if (encrypted)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            privateBlob = aes.EncryptCbc(privateBlob, iv, PaddingMode.None);
        }
        return $"PuTTY-User-Key-File-{version}: {algorithm}\nEncryption: {encryption}\nComment: fixture\nPublic-Lines: 1\n{Convert.ToBase64String(publicBlob)}\n{headers}Private-Lines: 1\n{Convert.ToBase64String(privateBlob)}\nPrivate-MAC: {Convert.ToHexString(mac)}\n";
    }

    private static byte[] Strings(params byte[][] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(size, value.Length);
            stream.Write(size);
            stream.Write(value);
        }
        return stream.ToArray();
    }
}
