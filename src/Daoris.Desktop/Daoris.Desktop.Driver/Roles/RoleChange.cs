namespace Daoris.Driver;

/// <summary>A refusal of <see cref="RoleCatalog"/>'s judge: one of its closed <see cref="RoleCatalog.Codes"/> and the sentence that says why.</summary>
public sealed record RoleRefusal(string Code, string Said);

/// <summary>
/// One change of the catalogue (§2.7): a role set, a seeded role removed (an added one deleted), or an id's record deleted so a seeded
/// id is the seed's again.
/// </summary>
public abstract record RoleChange
{
    /// <summary>The role of the record, new or changed.</summary>
    public static RoleChange Set(RoleRecord role) => new SetRole(role);

    /// <summary>The role of <paramref name="id"/> taken out: a seeded id's record says so, an added one's is deleted.</summary>
    public static RoleChange Remove(string id) => new RemoveRole(id);

    /// <summary><i>Back to the default</i>: <paramref name="id"/>'s record deleted, a seeded id the seed's again.</summary>
    public static RoleChange Default(string id) => new DefaultRole(id);

    /// <summary>The id the change is of.</summary>
    public abstract string Id { get; }

    public sealed record SetRole(RoleRecord Role) : RoleChange
    {
        public override string Id => Role.Id;
    }

    public sealed record RemoveRole(string RoleId) : RoleChange
    {
        public override string Id => RoleId;
    }

    public sealed record DefaultRole(string RoleId) : RoleChange
    {
        public override string Id => RoleId;
    }
}
