using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A stand-in for the person's account's variables (WIRING1e): a dictionary that notes what was written and can be made to fail, so
/// no test of a set-up press reads or writes the machine's own account, registry or <c>PATH</c>.
/// </summary>
public sealed class AccountStandIn : IAccountEnvironment
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every write, in order: the name and the value stored, null for a removal.</summary>
    public List<(string Name, string? Value)> Writes { get; } = [];

    /// <summary>Whether a read throws, as an account whose store cannot be read does.</summary>
    public bool Unreadable { get; set; }

    /// <summary>Whether a write throws, as an account whose store cannot be written does.</summary>
    public bool Unwritable { get; set; }

    public AccountStandIn With(string name, string value)
    {
        _values[name] = value;
        return this;
    }

    public string? Read(string name) =>
        Unreadable ? throw new IOException("the stand-in's store is unreadable") : _values.GetValueOrDefault(name);

    public void Write(string name, string? value)
    {
        if (Unwritable) throw new IOException("the stand-in's store is unwritable");
        Writes.Add((name, value));
        if (value is null) _values.Remove(name);
        else _values[name] = value;
    }
}
