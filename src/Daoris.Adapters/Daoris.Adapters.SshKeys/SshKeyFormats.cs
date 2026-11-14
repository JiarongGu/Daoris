using System.Security.Cryptography;
using System.Text;
using Daoris.Ports.Git;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Daoris.Adapters.SshKeys;

public static class SshKeyFormats
{
    public static SshKeyConversion Convert(byte[] bytes, string label, string? passphrase)
    {
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var encrypted = text.Contains("ENCRYPTED", StringComparison.Ordinal) || text.Contains("Encryption: aes256-cbc", StringComparison.Ordinal);
        if (encrypted && passphrase is null) throw new SshKeyFormatException("import-passphrase");
        try
        {
            var key = text.StartsWith("PuTTY-User-Key-File-", StringComparison.Ordinal)
                ? GitPuttyKey.Read(text, passphrase)
                : Pem(text, passphrase);
            return new SshKeyConversion(GitOpenSshKey.Encode(key, label), GitOpenSshKey.Public(key));
        }
        catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or FormatException or CryptoException or PkcsException or CryptographicException or ArithmeticException)
        {
            throw new SshKeyFormatException(encrypted ? "import-passphrase" : "import-format");
        }
    }

    private static AsymmetricKeyParameter Pem(string text, string? passphrase)
    {
        using var input = new StringReader(text);
        var read = new PemReader(input, new Password(passphrase)).ReadObject();
        return read switch
        {
            AsymmetricCipherKeyPair pair => pair.Private,
            AsymmetricKeyParameter { IsPrivate: true } key => key,
            _ => throw new FormatException(),
        };
    }

    private sealed class Password(string? value) : IPasswordFinder
    {
        public char[] GetPassword() => (value ?? "").ToCharArray();
    }
}
