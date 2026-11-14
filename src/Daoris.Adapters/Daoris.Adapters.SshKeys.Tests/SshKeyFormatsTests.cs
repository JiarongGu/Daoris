using System.Security.Cryptography;
using System.Text;
using Daoris.Ports.Git;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Security;
using Xunit;

namespace Daoris.Adapters.SshKeys.Tests;

public sealed class SshKeyFormatsTests
{
    [Theory]
    [InlineData("rsa")]
    [InlineData("ec")]
    [InlineData("pkcs8-rsa")]
    [InlineData("pkcs8-ec")]
    [InlineData("encrypted-pkcs8")]
    public void PEM_keys_become_OpenSSH_and_still_sign_as_the_source(string format)
    {
        using var rsa = RSA.Create(2048);
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = format switch
        {
            "rsa" => rsa.ExportRSAPrivateKeyPem(),
            "ec" => ec.ExportECPrivateKeyPem(),
            "pkcs8-rsa" => rsa.ExportPkcs8PrivateKeyPem(),
            "pkcs8-ec" => ec.ExportPkcs8PrivateKeyPem(),
            _ => rsa.ExportEncryptedPkcs8PrivateKeyPem("one passphrase", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)),
        };
        var source = Encoding.UTF8.GetBytes(pem);
        var original = source.ToArray();
        var read = SshKeyFormats.Convert(source, "Work", format == "encrypted-pkcs8" ? "one passphrase" : null);
        Assert.Equal(original, source);
        Verify(read, rsa, ec, format is "ec" or "pkcs8-ec");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PuTTY_RSA_and_EC_keys_still_sign_as_the_source(int version)
    {
        using var rsa = RSA.Create(2048);
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        foreach (var (text, elliptic) in new[] { (PuttyKeyFixture.Rsa(version, rsa), false), (PuttyKeyFixture.Ec(version, ec, "nistp384"), true) })
            Verify(SshKeyFormats.Convert(Encoding.UTF8.GetBytes(text), "Work", PuttyKeyFixture.Passphrase), rsa, ec, elliptic);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void PuTTY_versions_preserve_the_Ed25519_seed_and_public_key(int version, bool encrypted)
    {
        var (text, seed) = PuttyKeyFixture.Ed25519(version, encrypted);
        var source = Encoding.UTF8.GetBytes(text);
        var read = SshKeyFormats.Convert(source, "Work", encrypted ? PuttyKeyFixture.Passphrase : null);
        var key = Assert.IsType<Ed25519PrivateKeyParameters>(Decode(read));
        Assert.Equal(seed, key.GetEncoded());
        Assert.Equal(OpenSshPublicKeyUtilities.EncodePublicKey(key.GeneratePublicKey()), read.PublicKey);
        Assert.Equal(text, Encoding.UTF8.GetString(source));
        if (encrypted)
        {
            Assert.Equal("import-passphrase", Assert.Throws<SshKeyFormatException>(() => SshKeyFormats.Convert(source, "Work", null)).Code);
            Assert.Equal("import-passphrase", Assert.Throws<SshKeyFormatException>(() => SshKeyFormats.Convert(source, "Work", "wrong")).Code);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void A_modified_PuTTY_comment_fails_MAC_verification(int version)
    {
        var (text, _) = PuttyKeyFixture.Ed25519(version, false);
        var source = Encoding.UTF8.GetBytes(text.Replace("Comment: fixture", "Comment: tampered", StringComparison.Ordinal));
        Assert.Equal("import-format", Assert.Throws<SshKeyFormatException>(() => SshKeyFormats.Convert(source, "Work", null)).Code);
    }

    [Fact]
    public void An_encrypted_PEM_refuses_missing_and_wrong_passphrases_without_repeating_them()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportEncryptedPkcs8PrivateKeyPem("secret passphrase", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));
        foreach (var password in new string?[] { null, "wrong secret" })
        {
            var error = Assert.Throws<SshKeyFormatException>(() => SshKeyFormats.Convert(Encoding.UTF8.GetBytes(pem), "Work", password));
            Assert.Equal("import-passphrase", error.Code);
            Assert.DoesNotContain("secret", error.ToString());
        }
    }

    private static AsymmetricKeyParameter Decode(SshKeyConversion read)
    {
        var text = Encoding.ASCII.GetString(read.PrivateKey);
        Assert.Contains("BEGIN OPENSSH PRIVATE KEY", text);
        var body = string.Concat(text.Split('\n').Where(line => !line.StartsWith('-')));
        return OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(Convert.FromBase64String(body));
    }

    private static void Verify(SshKeyConversion read, RSA rsa, ECDsa ec, bool elliptic)
    {
        var key = Decode(read);
        var signer = SignerUtilities.GetSigner(elliptic ? "SHA256withECDSA" : "SHA256withRSA");
        signer.Init(true, key);
        var message = "key import proof"u8.ToArray();
        signer.BlockUpdate(message, 0, message.Length);
        var signature = signer.GenerateSignature();
        Assert.True(elliptic
            ? ec.VerifyData(message, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
            : rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
}
