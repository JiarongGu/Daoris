namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s sign-outs nobody at Daoris made: an account of Daoris's whose sign-in reading said signed in and then
/// signed out (<see cref="SignInState.NotByPerson"/>), one notice by agent, account and the sign-out's moment, read from
/// <c>reads.json</c> (<see cref="AccountReads"/>), which holds words and moments alone. Its fields: <see cref="Notice.Route"/> the
/// agent (the accounts' owner), <see cref="Notice.Code"/> the account's id, <see cref="Notice.Act"/> the person's name for it where
/// they gave one, <see cref="Notice.State"/> <c>out</c>, <see cref="Notice.At"/> since when.
/// </summary>
public static class SignOutNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "signed-out";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "sign-outs";

    /// <summary>How far back a sign-out is news: a week, as long as a notice is kept.</summary>
    public static readonly TimeSpan Reach = Notices.Week;

    /// <summary>Every sign-out within <see cref="Reach"/> nobody at Daoris made, of every agent's accounts on this machine.</summary>
    public static IReadOnlyList<NewsOccurrence> Occurrences(string home, DateTimeOffset now)
    {
        var occurrences = new List<NewsOccurrence>();
        foreach (var agent in AccountReads.Agents(home))
        {
            var names = AccountNames.Of(home, agent);
            foreach (var (account, read) in AccountReads.Of(home, agent).Accounts)
            {
                if (Of(agent, account, names.GetValueOrDefault(account), SignInStates.Of(read), now) is { } notice)
                {
                    occurrences.Add(new NewsOccurrence(notice.Id, notice.At, notice));
                }
            }
        }

        return occurrences;
    }

    /// <summary>Tell every sign-out within <see cref="Reach"/> not told yet, for the look and the credentials watch; how many.</summary>
    public static int Claim(string home, DateTimeOffset now)
    {
        var occurrences = Occurrences(home, now);
        return occurrences.Count == 0 ? 0 : new News(home).Claim(Teller, occurrences, null, now).Told.Count;
    }

    /// <summary>
    /// The notice a state makes, or null for one that is no sign-out the person did not make, or older than the reach. Its
    /// <see cref="Notice.Act"/> is the person's name for the account where they gave one, as the page names it.
    /// </summary>
    public static Notice? Of(string agent, string account, string? name, SignInState state, DateTimeOffset now)
    {
        if (state is not { Standing: SignInStanding.Out, NotByPerson: true, Since: { } since } || now - since > Reach) return null;
        var id = $"{Kind}:{agent}/{account}/{AccountCooling.Stamp(since)}";
        return new Notice(id, Kind, since, State: "out", Route: agent, Act: name, Code: account);
    }
}
