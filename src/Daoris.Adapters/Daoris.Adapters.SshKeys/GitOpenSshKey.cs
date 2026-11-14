using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;

namespace Daoris.Adapters.SshKeys;

internal static class GitOpenSshKey
{
    internal static byte[] Public(AsymmetricKeyParameter key) => OpenSshPublicKeyUtilities.EncodePublicKey(key switch
    {
        RsaPrivateCrtKeyParameters rsa => new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent),
        ECPrivateKeyParameters ec => new ECPublicKeyParameters(ec.Parameters.G.Multiply(ec.D), ec.Parameters),
        Ed25519PrivateKeyParameters ed => ed.GeneratePublicKey(),
        _ => throw new FormatException(),
    });

    internal static byte[] Encode(AsymmetricKeyParameter key, string label)
    {
        var publicBlob = Public(key);
        using var privatePart = new MemoryStream();
        var check = RandomNumberGenerator.GetBytes(4);
        privatePart.Write(check);
        privatePart.Write(check);
        switch (key)
        {
            case RsaPrivateCrtKeyParameters rsa:
                String(privatePart, "ssh-rsa");
                foreach (var value in new[] { rsa.Modulus, rsa.PublicExponent, rsa.Exponent, rsa.QInv, rsa.P, rsa.Q })
                    Block(privatePart, value.ToByteArray());
                break;
            case ECPrivateKeyParameters ec:
                privatePart.Write(publicBlob);
                Block(privatePart, ec.D.ToByteArray());
                break;
            case Ed25519PrivateKeyParameters ed:
                privatePart.Write(publicBlob);
                Block(privatePart, [.. ed.GetEncoded(), .. ed.GeneratePublicKey().GetEncoded()]);
                break;
            default:
                throw new FormatException();
        }
        String(privatePart, label);
        for (byte padding = 1; privatePart.Length % 8 != 0; padding++) privatePart.WriteByte(padding);
        using var envelope = new MemoryStream();
        envelope.Write("openssh-key-v1\0"u8);
        String(envelope, "none");
        String(envelope, "none");
        String(envelope, "");
        envelope.Write([0, 0, 0, 1]);
        Block(envelope, publicBlob);
        Block(envelope, privatePart.ToArray());
        var base64 = Convert.ToBase64String(envelope.ToArray());
        var lines = Enumerable.Range(0, (base64.Length + 69) / 70).Select(at => base64.Substring(at * 70, Math.Min(70, base64.Length - at * 70)));
        return Encoding.ASCII.GetBytes(string.Concat("-----", "BEGIN OPENSSH PRIVATE KEY-----\n") + string.Join('\n', lines)
            + string.Concat("\n-----", "END OPENSSH PRIVATE KEY-----\n"));
    }

    internal static void String(Stream stream, string value) => Block(stream, Encoding.UTF8.GetBytes(value));

    internal static void Block(Stream stream, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    internal sealed class Reader(byte[] bytes)
    {
        private int _at;

        internal byte[] Block()
        {
            if (bytes.Length - _at < 4) throw new FormatException();
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(_at, 4));
            _at += 4;
            if (length < 0 || length > bytes.Length - _at) throw new FormatException();
            var value = bytes.AsSpan(_at, length).ToArray();
            _at += length;
            return value;
        }

        internal string String() => Encoding.ASCII.GetString(Block());
        internal Org.BouncyCastle.Math.BigInteger Integer() => new(1, Block());
    }
}
