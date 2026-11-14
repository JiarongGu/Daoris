using System.Text;
using Daoris.Ports.Git;

namespace Daoris.Driver;

public static class GitKeyImport
{
    public sealed record ImportRead(byte[] Bytes, string PublicLine, string Type, string Fingerprint);

    public static ImportRead Read(string file, string label, string? passphrase = null)
    {
        if (!Path.IsPathFullyQualified(file)) throw new DriverException("git: name the key file by its whole path (import-path)");
        var info = new FileInfo(file);
        if (!info.Exists) throw new DriverException($"git: no file at {file} (import-missing)");
        if (info.Length > GitKeys.ImportLimit) throw new DriverException($"git: {file} is larger than a private key (import-format)");
        var bytes = File.ReadAllBytes(file); // read-shares: the person names this external file, never one AtomicFile replaces
        var comment = new string(label.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        var converted = Convert(bytes, comment, passphrase);
        var described = GitKeys.Describe(converted.PublicKey) ?? throw new DriverException("git: the key's type is not one ssh hosts take: ed25519, RSA or ECDSA (import-format)");
        var algorithm = described.Type == "ed25519" ? "ssh-ed25519" : described.Type.StartsWith("rsa-", StringComparison.Ordinal) ? "ssh-rsa"
            : "ecdsa-sha2-nistp" + described.Type[6..];
        var line = $"{algorithm} {System.Convert.ToBase64String(converted.PublicKey)}" + (comment.Length > 0 ? " " + comment : "");
        return new ImportRead(converted.PrivateKey, line, described.Type, described.Fingerprint);
    }

    private static SshKeyConversion Convert(byte[] bytes, string label, string? passphrase)
    {
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        if (text.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.Ordinal))
        {
            var (blob, problem) = GitKeys.ReadPrivate(text);
            if (problem is not null) throw new DriverException($"git: {problem}");
            return new SshKeyConversion(bytes, blob!);
        }
        try
        {
            return MachinePorts.SshKeys(bytes, label, passphrase);
        }
        catch (SshKeyFormatException error)
        {
            throw new DriverException(error.Code == "import-passphrase"
                ? "git: the key needs its passphrase, or the passphrase or key did not match; supply it once when importing (import-passphrase)"
                : "git: the private key is malformed, unsupported or its MAC does not match (import-format)");
        }
    }
}
