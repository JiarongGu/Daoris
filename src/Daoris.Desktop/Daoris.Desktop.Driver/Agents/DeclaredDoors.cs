namespace Daoris.Driver;

/// <summary>
/// The factory of the doors a plugin declares (the agent-provider design §4.8, row q): a <see cref="DeclaredDoor"/> made a contract door
/// riding the protocol transport, what the manifest may never claim being what the declaration has no field for. The door's identity and
/// its home are its own, from the declaration; what the transport gives every protocol door is stated from the transport (its resume,
/// eligibility to negotiate at the open), and that the door holds a conversation and is handed no settings file is stated here. The
/// registry places the door by its account key when it joins (<see cref="JoinedProviders"/>).
/// </summary>
public static class DeclaredDoors
{
    /// <summary>The contract door a declaration makes.</summary>
    public static AgentDoor Door(DeclaredDoor declared) => new(
        new DoorId(declared.Name),
        Transport.Protocol,
        new DoorIdentity(declared.DeclaredBy, declared.Product, declared.Maker),
        Known<ToolSpec>.Declared(
            new ToolSpec(
                [declared.Command[0]], declared.VersionArguments ?? [], declared.Install, null, declared.Package, null, null,
                // No version question declared: presence is asked of the file or PATH, never by running it.
                ProbeByPresence: declared.VersionArguments is null),
            $"the {declared.DeclaredBy} plugin's declaration"),
        new DoorHome(declared.ProfileVariable, declared.OwnHome, null),
        LentPart.None,
        Known<IDoorOffers>.Absent,
        new PosturesPart(declared.Posture, declared.AskingPosture, declared.FullPosture),
        Known<ISpawn>.Declared(new DeclaredSpawn(declared.Command), "the plugin's declared command"),
        Known<IDialect>.Absent,
        new AbilitiesPart(
            Known<bool>.Declared(true, "a declared door holds a conversation"),
            Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
            Known<bool>.Declared(false, "a declared door is handed no settings file")),
        new DoorTablesPart(null, null),
        string.IsNullOrEmpty(declared.AccountOf) ? null : new AccountKey(declared.AccountOf),
        InstructionsPart.None);
}
