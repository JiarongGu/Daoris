using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row a (D213; <c>docs/2026-10-26-agent-provider-design.md</c> §2, §5, §11): the provider contract named as records
/// and the registry over today's adapters answering every question the contract asks, with no behaviour changed. Nothing reads
/// either but these tests, which hold the registry equal to every copy of "own, else owner" the code makes today.
/// </summary>
public sealed class ProviderRegistryTests
{
    private static readonly string[] ProviderParts =
    [
        "Id", "Identity", "Doors", "Program", "Accounts", "Tables", "Usage", "Lent", "Models", "Children", "Rules", "Skills",
        "Conversations", "Reach", "Vocabulary", "Unavailable",
    ];

    private static readonly string[] DoorParts =
    [
        "Id", "Transport", "Identity", "Program", "Home", "Lent", "Offers", "Postures", "Spawn", "Dialect", "Abilities", "Tables",
    ];

    /// <summary>§2.3: every part is required, so each is a member of the record, and the records are sealed.</summary>
    [Fact]
    public void The_provider_and_the_door_are_sealed_records_holding_every_part_of_the_contract()
    {
        Assert.True(typeof(Provider).IsSealed);
        Assert.True(typeof(AgentDoor).IsSealed);
        Assert.Equal(ProviderParts.Order(), Members(typeof(Provider)).Order());
        Assert.Equal(DoorParts.Order(), Members(typeof(AgentDoor)).Order());
    }

    private static IEnumerable<string> Members(Type record) => record
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name)
        .Where(name => name != "EqualityContract");

    /// <summary>§2.2: a provider's id and a door's are typed, so one passed where the other is wanted does not compile.</summary>
    [Fact]
    public void The_ids_are_typed_and_convert_to_no_other_kind()
    {
        Type[] kinds = [typeof(ProviderId), typeof(DoorId), typeof(AccountKey)];
        foreach (var kind in kinds)
        {
            var conversions = kind.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name is "op_Implicit" or "op_Explicit");
            Assert.Empty(conversions);
        }

        Assert.NotEqual<object>(new ProviderId("claude-code"), new DoorId("claude-code"));
        Assert.NotEqual<object>(new DoorId("codex"), new AccountKey("codex"));
    }

    /// <summary>A name is the same name in any case, as the adapters' own table treats it, and says what it is in the one way.</summary>
    [Fact]
    public void An_id_compares_without_case_and_is_never_blank()
    {
        Assert.Equal(new ProviderId("Claude-Code"), new ProviderId("claude-code"));
        Assert.Equal(new DoorId("Claude-Code").GetHashCode(), new DoorId("claude-code").GetHashCode());
        Assert.Equal(new AccountKey("Codex"), new AccountKey("codex"));
        Assert.Equal("claude-code", new DoorId("claude-code").ToString());
        Assert.Throws<ArgumentException>(() => new ProviderId(" "));
        Assert.Throws<ArgumentException>(() => new DoorId(""));
        Assert.Throws<ArgumentException>(() => new AccountKey(null!));
    }

    /// <summary>§2.4: a fact is known with its source and where it applies, or it is stated absent; absent is never a value read.</summary>
    [Fact]
    public void A_fact_names_its_source_its_evidence_and_where_it_applies_or_is_stated_absent()
    {
        var declared = Known<bool>.Declared(true, "the door's declaration");
        Assert.True(declared.IsKnown);
        Assert.True(declared.Value);
        Assert.Equal(KnownBy.Declared, declared.By);
        Assert.Equal("the door's declaration", declared.Evidence);
        Assert.Equal(Known<bool>.Anywhere, declared.Applies);

        var negotiated = Known<string>.Negotiated("plan", "initialize answered", "claude-code-acp 0.79.0");
        Assert.Equal(KnownBy.Negotiated, negotiated.By);
        Assert.Equal("claude-code-acp 0.79.0", negotiated.Applies);
        var measured = Known<int>.Measured(3, "a probe on the install", "posture asking");
        Assert.Equal(KnownBy.Measured, measured.By);

        var absent = Known<string>.Absent;
        Assert.False(absent.IsKnown);
        Assert.False(absent.TryGet(out _));
        Assert.Throws<InvalidOperationException>(() => absent.Required);
        Assert.NotEqual(Known<bool>.Absent, Known<bool>.Declared(false, "declared false"));
    }

    [Fact]
    public void A_transport_is_the_wire_the_adapter_declares()
    {
        Assert.Equal(Transport.Native, TransportOf.Wire(SessionWire.Pipe));
        Assert.Equal(Transport.Protocol, TransportOf.Wire(SessionWire.Acp));
    }

    /// <summary>§5: a key resolves to its pool, the key and the providers holding it, and a pool that holds none is no pool.</summary>
    [Fact]
    public void An_account_pool_holds_its_key_and_every_provider_holding_it()
    {
        var pool = new AccountPool(new AccountKey("claude-code-acp"), [new ProviderId("foo"), new ProviderId("baz")], [new DoorId("foo")]);

        Assert.Equal(new AccountKey("claude-code-acp"), pool.Key);
        Assert.Equal(2, pool.Providers.Count);
        Assert.Throws<ArgumentException>(() => new AccountPool(new AccountKey("x"), [], []));
    }

    /// <summary>§5's table: each field a record keeps, with the kind of id it holds, so no reader guesses.</summary>
    [Fact]
    public void The_records_table_names_the_kind_of_id_each_field_holds()
    {
        Assert.NotEmpty(RecordIds.Fields);
        Assert.All(RecordIds.Fields, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Record));
            Assert.False(string.IsNullOrWhiteSpace(row.Field));
        });
        Assert.Equal(RecordIds.Fields.Count, RecordIds.Fields.Select(row => (row.Record, row.Field)).Distinct().Count());
        foreach (var kind in Enum.GetValues<IdKind>())
        {
            Assert.Contains(RecordIds.Fields, row => row.Kind == kind);
        }

        Assert.Equal(IdKind.Door, RecordIds.KindOf("The session record", "Adapter"));
        Assert.Equal(IdKind.AccountKey, RecordIds.KindOf("The account homes, cooling, windows", "profiles/agents/<agent>/"));
        Assert.Equal(IdKind.AccountKey, RecordIds.KindOf("SessionEnded, the usage history", "TurnBinding.Agent"));
        Assert.Equal(IdKind.Provider, RecordIds.KindOf("The lineage", "harness"));
        Assert.Throws<DriverException>(() => RecordIds.KindOf("The session record", "NoSuchField"));
    }
}
