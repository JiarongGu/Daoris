using System.Security.Cryptography;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

public sealed class GitKeyImportBridgeTests : DriverModuleBridge
{
    [Fact]
    public async Task An_import_uses_the_supplied_passphrase_and_returns_no_secret()
    {
        using var rsa = RSA.Create(2048);
        const string passphrase = " import passphrase ";
        var file = Path.Combine(Home, "import.pem");
        File.WriteAllText(file, rsa.ExportEncryptedPkcs8PrivateKeyPem(passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        var module = Module();
        using var convert = Daoris.Desktop.Driver.Tests.TestPorts.SshKeys.Using((source, label, secret) =>
        {
            Assert.Equal(File.ReadAllBytes(file), source);
            Assert.Equal(("Import", passphrase), (label, secret));
            return new Daoris.Ports.Git.SshKeyConversion("converted key"u8.ToArray(), Convert.FromBase64String("AAAAC3NzaC1lZDI1NTE5AAAAIMW8OpV8TxlaO8rBndwxdZUP6WFznyRHY9Callh5332I"));
        });

        var answer = await AnswerAsync(module, "GIT_IDENTITY", new { action = "add-ssh", host = "github.com", label = "Import", import = file, passphrase });

        var identity = Assert.Single(GitIdentities.Read(Home));
        Assert.Equal("ed25519", identity.KeyType);
        Assert.DoesNotContain(passphrase, answer.GetRawText());
        Assert.DoesNotContain(passphrase, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.GitIdentities)));
        Assert.Equal("converted key", File.ReadAllText(GitKeys.KeyPath(Home, identity.Id)));
    }
}
