namespace Daoris.Driver;

/// <summary>
/// The factory of the doors a plugin declares (the agent-provider design §4.8, row q): a <see cref="DeclaredDoor"/> made a contract door
/// riding the protocol transport, what the manifest may never claim being what the declaration has no field for. The door's identity and
/// its home are its own, from the declaration; what the transport gives every protocol door is stated from the transport, and that the
/// door holds a conversation is stated here.
/// </summary>
public static class DeclaredDoors
{
    /// <summary>The contract door a declaration makes.</summary>
    public static AgentDoor Door(DeclaredDoor declared) => new(
        new DoorId(declared.Name),
        Transport.Protocol,
        new DoorIdentity(declared.DeclaredBy, declared.Product, declared.Maker),
        Known<ProgramFacts>.Declared(
            new ProgramFacts(
                [declared.Command[0]], declared.VersionArguments ?? [], declared.Install, null, declared.Package, null, null,
                // No version question declared: presence is asked of the file or PATH, never by running it.
                ProbeByPresence: declared.VersionArguments is null),
            $"the {declared.DeclaredBy} plugin's declaration"),
        new DoorHome(declared.ProfileVariable, declared.OwnHome, null),
        LentPart.None,
        Known<IDoorOffers>.Absent,
        new PosturesPart(declared.Posture, null, null, null),
        Known<ISpawn>.Declared(new DeclaredSpawn(declared.Command), "the plugin's declared command"),
        Known<IDialect>.Absent,
        new AbilitiesPart(
            Known<bool>.Declared(true, "a declared door holds a conversation"),
            Known<bool>.Absent,
            Known<bool>.Absent),
        new DoorTablesPart(null, null),
        string.IsNullOrEmpty(declared.AccountOf) ? null : new AccountKey(declared.AccountOf),
        InstructionsPart.None);

    /// <summary>The door a declaration makes, as today's readers ask it of an adapter, until row t takes the seam away.</summary>
    public static ISessionAdapter Adapter(DeclaredDoor declared)
    {
        var door = Door(declared);
        return new LegacyDoor(Provider(door), door);
    }

    /// <summary>
    /// The provider of a door a declaration made, made from the declaration alone: its id the door's, no program of its own, and the
    /// account pool the door's own key names. The registry groups it with the provider its door's account key names.
    /// </summary>
    private static Provider Provider(AgentDoor door) => new(
        new ProviderId(door.Id.Value),
        new ProviderIdentity(null, door.Identity.Maker, null, false, [door.Identity.DeclaredBy!]),
        [door],
        Known<ProgramFacts>.Absent,
        Known<AccountsPart>.Declared(new AccountsPart(door.AccountOf ?? new AccountKey(door.Id.Value), null, PoolFacts.None), "a plugin's declaration"),
        Known<TablesPart>.Absent,
        Known<UsagePart>.Absent,
        Known<LentPart>.Absent,
        Known<ISettingsFile>.Absent,
        Known<IChildVocabulary>.Absent,
        Known<IRuleLanguage>.Absent,
        Known<ISkillLayout>.Absent,
        Known<IConversationHome>.Absent,
        Reach.Everywhere,
        Known<VocabularyPart>.Absent);
}
