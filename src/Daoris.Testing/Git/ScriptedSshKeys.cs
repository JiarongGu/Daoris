using Daoris.Ports.Git;

namespace Daoris.Testing;

public sealed class ScriptedSshKeys
{
    private readonly AsyncLocal<SshKeyConverter?> _current = new();

    public IDisposable Using(SshKeyConverter convert)
    {
        var previous = _current.Value;
        _current.Value = convert;
        return new Scope(() => _current.Value = previous);
    }

    public SshKeyConversion Convert(byte[] source, string label, string? passphrase) =>
        _current.Value?.Invoke(source, label, passphrase) ?? throw new SshKeyFormatException("import-format");

    private sealed class Scope(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}
