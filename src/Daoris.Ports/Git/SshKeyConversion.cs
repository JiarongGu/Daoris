namespace Daoris.Ports.Git;

public sealed record SshKeyConversion(byte[] PrivateKey, byte[] PublicKey);

public delegate SshKeyConversion SshKeyConverter(byte[] source, string label, string? passphrase);

public sealed class SshKeyFormatException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
