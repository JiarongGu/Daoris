namespace Daoris.Driver;

/// <summary>
/// The names a provider answers for and the descriptor of a name no provider answers for (<see cref="ProviderRegistry"/>): a registry
/// is made over providers alone.
/// </summary>
internal static class ProviderRegistration
{
    /// <summary>Every name a provider answers for: its id, its doors', its account key and each door's declared one.</summary>
    internal static IEnumerable<string> Names(Provider provider) =>
        provider.Doors.Select(door => door.Id.Value)
            .Append(provider.Id.Value)
            .Append(provider.KeyOf().Value)
            .Concat(provider.Doors.Select(door => door.AccountOf?.Value).OfType<string>());

    /// <summary>A name kept for a plugin turned off: no door, so no provider of its own, but known with its id (POOLSTRAY1).</summary>
    internal static Provider Descriptor(string agent)
    {
        var id = new ProviderId(agent);
        return new Provider(
            id, new ProviderIdentity(null, null, null, false, []), [], Known<ToolSpec>.Absent,
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey(agent), null, PoolFacts.None), "a name a disabled plugin declares"),
            Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Known<ISettingsFile>.Absent, Known<IChildVocabulary>.Absent,
            Known<IRuleLanguage>.Absent, Known<ISkillLayout>.Absent, Known<IConversationHome>.Absent, Reach.Everywhere,
            Known<VocabularyPart>.Absent, Unavailable: true);
    }
}
