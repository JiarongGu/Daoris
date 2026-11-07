namespace Daoris.Driver;

/// <summary>The cool-offs a record's words wait on, read for the listing: which account, until when, and what the chat runner said holds them.</summary>
internal static class SessionCooling
{
    /// <summary>A hold on an account's cool-off: when it ends and which account, by its id and the name the person gave it.</summary>
    internal static WordsHold Hold(string reason, CoolingEntry cooling, string home) => new(WordsHold.Cooling, reason)
    {
        Until = cooling.Until,
        Account = cooling.Account,
        AccountName = cooling.Account is { } id && AccountNames.Of(home, cooling.Agent).TryGetValue(id, out var name) ? name : null,
    };

    /// <summary>
    /// The earliest cool-off among the agent's accounts on this machine, the own account's included; null where one of them is not
    /// cooling, since the words then go on there (a sign-in or a refusal is the walk's to find, as before the first look).
    /// </summary>
    internal static CoolingEntry? Soonest(Func<string?, string?, string?, CoolingEntry?> coolingOf, string home, string adapter, CoolingEntry own, string? model)
    {
        IReadOnlyList<string> profiles;
        try
        {
            profiles = HarnessSettings.Profiles(home, own.Agent);
        }
        catch (DriverException)
        {
            return own;
        }

        var cooling = new List<CoolingEntry> { own };
        foreach (var profile in profiles)
        {
            if (string.Equals(profile, own.Account, StringComparison.OrdinalIgnoreCase)) continue;
            if (coolingOf(adapter, profile, model) is not { } other) return null;
            cooling.Add(other);
        }

        return cooling.MinBy(entry => entry.Until);
    }

    /// <summary>The home's cool-offs as a start reads them: by the account's owner, over the build's harnesses and the plugins'.</summary>
    internal static Func<string?, string?, string?, CoolingEntry?> On(string home)
    {
        var built = ProviderRegistry.Built();
        var adapters = built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names));
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(home, HomeEntry.Harnesses));
        return (adapter, profile, model) => roster.CoolingOf(adapter ?? "", profile, model);
    }

    /// <summary>The cool-off on the account a record ran on, or null: none, or an adapter this build no longer has.</summary>
    internal static CoolingEntry? Of(Func<string?, string?, string?, CoolingEntry?> coolingOf, PriorSession record)
    {
        try
        {
            return coolingOf(record.Adapter, record.Profile, record.ModelOn(record.Adapter ?? ""));
        }
        catch (DriverException)
        {
            // An adapter this build no longer has: nothing cools on it, and its runner says why it cannot go on.
            return null;
        }
    }

    /// <summary>
    /// What the chat runner said holds a conversation's words, where its line is still the conversation's last: the words after
    /// <see cref="ResumeWords.NotYet"/>. Null where a word said since, or anything else, came after it.
    /// </summary>
    internal static string? NotYet(SessionEvents events, string session)
    {
        try
        {
            return events.Page(session, limit: 1).Events is [{ Kind: SessionEventKind.Note, Text: { } line }]
                   && line.StartsWith(ResumeWords.NotYet, StringComparison.Ordinal)
                ? line[ResumeWords.NotYet.Length..].Trim()
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A conversation that does not read says nothing holds it: it is listed going on, which the runner then says.
            return null;
        }
    }
}
