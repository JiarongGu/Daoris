using System.Text;
using Daoris.Driver;
using Daoris.Ports.Git;

namespace Daoris.Desktop.Driver.Tests;

public sealed class GitKeyImportStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "import-storage-" + Guid.NewGuid().ToString("N"));
    public GitKeyImportStorageTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Conversion_happens_before_a_private_folder_and_never_changes_the_source()
    {
        var path = Path.Combine(_root, "source.ppk");
        File.WriteAllText(path, "encrypted key fixture");
        const string passphrase = "one private passphrase";
        using (TestPorts.SshKeys.Using((_, _, _) => throw new SshKeyFormatException("import-passphrase")))
            await Assert.ThrowsAsync<DriverException>(() => GitIdentityActs.AddSshAsync(_root, "github.com", "Work", null, null, path, false, CancellationToken.None, "wrong"));
        Assert.False(Directory.Exists(DaorisHome.Entry(_root, HomeEntry.GitKeys)));
        var calls = 0;
        using var converted = TestPorts.SshKeys.Using((source, label, secret) =>
        {
            calls++;
            Assert.False(Directory.Exists(DaorisHome.Entry(_root, HomeEntry.GitKeys)));
            Assert.Equal(("encrypted key fixture", "Work", passphrase), (Encoding.UTF8.GetString(source), label, secret));
            return new SshKeyConversion("converted private key"u8.ToArray(), Convert.FromBase64String("AAAAC3NzaC1lZDI1NTE5AAAAIMW8OpV8TxlaO8rBndwxdZUP6WFznyRHY9Callh5332I"));
        });

        var identity = await GitIdentityActs.AddSshAsync(_root, "github.com", "Work", null, null, path, false, CancellationToken.None, passphrase);

        Assert.Equal(1, calls);
        Assert.True(GitPrivateFolder.IsPrivate(GitKeys.Folder(_root, identity.Id)));
        Assert.Equal("converted private key", File.ReadAllText(GitKeys.KeyPath(_root, identity.Id)));
        Assert.Equal("encrypted key fixture", File.ReadAllText(path));
        Assert.DoesNotContain(passphrase, File.ReadAllText(DaorisHome.Entry(_root, HomeEntry.GitIdentities)));
    }
}
