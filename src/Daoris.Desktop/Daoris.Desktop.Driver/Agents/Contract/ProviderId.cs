namespace Daoris.Driver;

/// <summary>
/// An agent's id among providers (AGENTPROVIDER1, the agent-provider design §2.2): unique among providers, compared without case as
/// the adapters' own table compares a name. Typed, so one passed where a <see cref="DoorId"/> or an <see cref="AccountKey"/> is
/// wanted does not compile; opaque outside the providers, carried, stored, compared for identity and shown, never read for behaviour.
/// </summary>
public readonly record struct ProviderId
{
    public ProviderId(string value) => Value = Ids.Named(value, nameof(value));

    public string Value { get; }

    public bool Equals(ProviderId other) => Ids.Same(Value, other.Value);

    public override int GetHashCode() => Ids.Hash(Value);

    public override string ToString() => Value ?? "";
}

/// <summary>
/// A way into an agent (an adapter's name, today) among doors (the design §2.2): unique among doors. It may equal its own provider's
/// id (<c>claude-code</c>, <c>dsh</c>, <c>stub</c>), never another provider's.
/// </summary>
public readonly record struct DoorId
{
    public DoorId(string value) => Value = Ids.Named(value, nameof(value));

    public string Value { get; }

    public bool Equals(DoorId other) => Ids.Same(Value, other.Value);

    public override int GetHashCode() => Ids.Hash(Value);

    public override string ToString() => Value ?? "";
}

/// <summary>
/// The string every account record holds (the design §2.2, §5): today's <c>HarnessToolchain.Owner(name)</c>, an id of a kind of its
/// own. A door's is its harness's <c>accountOf</c> verbatim, where it declares one, else its provider's id; never followed past that
/// one level. It resolves to a pool (<see cref="AccountPool"/>), never to one provider.
/// </summary>
public readonly record struct AccountKey
{
    public AccountKey(string value) => Value = Ids.Named(value, nameof(value));

    public string Value { get; }

    public bool Equals(AccountKey other) => Ids.Same(Value, other.Value);

    public override int GetHashCode() => Ids.Hash(Value);

    public override string ToString() => Value ?? "";
}

/// <summary>The one rule the three kinds of id share: a name, never blank, the same name in any case.</summary>
internal static class Ids
{
    public static string Named(string? value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("an id is a name, never blank", parameter) : value;

    public static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public static int Hash(string? value) => value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(value);
}
