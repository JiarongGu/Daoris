using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// When each account's sign-in state was last read, as this application saw it (UX6e, D150 §5.3): the Agents place says
/// each account's last known state with when it was read, and the roster's report carries the state alone.
/// </summary>
/// <remarks>
/// <para><b>Read from what the application asked and what it was answered, never by asking.</b> Nothing here starts a
/// process. A person's press, a sign-in's end, a key and an account's edit each ask the roster again, and say so
/// (<c>asked</c>): every account the report carries was read then, the tool's own sign-in with them, but for one a session
/// of Daoris's runs on, whose word the probe keeps (TOOL6g) and whose moment this keeps too. A report the driver made by
/// itself since (a start asking one account again, once per sign-out) is dated where its word changed, when this first
/// heard it, and an unchanged word keeps its moment: a reading is never claimed fresher than it was made.</para>
/// <para><b>The first answer since the application started</b> is the probe that just ran, so each word it gives is dated
/// then, and a word it could not give (unknown) is never read. The tool's own sign-in is asked only at a press, so before
/// one it is never read.</para>
/// <para>Each door is its own reading, by its adapter, as the roster reports it. In memory: a restart reads again.</para>
/// </remarks>
public sealed class RosterReads
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Seen> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The moments of one door's report: each account's, by its name, and the tool's own sign-in's; null where none was read.
    /// </summary>
    /// <param name="asked">The application has just asked this door's accounts again, the tool's own sign-in with them.</param>
    /// <param name="busy">Whether a session of Daoris's runs on an account, by its name: the probe kept its last word.</param>
    public Stamps Observe(HarnessReport report, DateTimeOffset now, bool asked, Func<string, bool>? busy = null)
    {
        lock (_gate)
        {
            _seen.TryGetValue(report.Adapter, out var before);
            var same = before is not null && ReferenceEquals(before.Report, report) && !asked;
            var accounts = new Dictionary<string, Word>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in report.Profiles)
            {
                Word? prior = null;
                before?.Accounts.TryGetValue(profile.Name, out prior);
                accounts[profile.Name] = Dated(prior, profile.Login, profile.Account, same, asked && !(busy?.Invoke(profile.Name) ?? false), now);
            }

            var own = Dated(before?.Own, report.OwnLogin, report.OwnAccount, same, asked, now);
            _seen[report.Adapter] = new Seen(report, accounts, own);
            return new Stamps(accounts.ToDictionary(entry => entry.Key, entry => entry.Value.Read, StringComparer.OrdinalIgnoreCase), own.Read);
        }
    }

    /// <summary>
    /// One word's moment: asked now, it was read now; otherwise an unchanged word keeps its moment, a changed one was first
    /// heard now, and a first unknown was never read.
    /// </summary>
    private static Word Dated(Word? prior, LoginState login, string? who, bool same, bool asked, DateTimeOffset now)
    {
        if (asked) return new Word(login, who, now);
        if (prior is not null && (same || (prior.Login == login && prior.Who == who))) return prior with { Login = login, Who = who };
        return new Word(login, who, login == LoginState.Unknown && prior is null ? null : now);
    }

    private sealed record Seen(HarnessReport Report, IReadOnlyDictionary<string, Word> Accounts, Word Own);

    private sealed record Word(LoginState Login, string? Who, DateTimeOffset? Read);
}

/// <summary>One door's moments: each account's by its name, and the tool's own sign-in's; null where none was read.</summary>
public sealed record Stamps(IReadOnlyDictionary<string, DateTimeOffset?> Accounts, DateTimeOffset? Own);
