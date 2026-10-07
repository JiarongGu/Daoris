using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// An account's windows asked of its agent's own server, where its door carries none (CODEXUSE1, D125's CODEXUSE1 note):
/// kept in <c>windows.json</c> as a door's frame is (<see cref="AccountWindows.Said"/>), so near, pace, a start's line of what
/// each account said, <c>daoris agent list</c> and the page read a Codex account as they read a Claude Code one.
/// </summary>
/// <remarks>
/// <para><b>When, as ROSTER1 reads an account</b>: at a person's press, the agent's or the account's; and at a start's walk
/// over a list, where switching before the limit and pace read it, for each account of the list the start could take whose
/// last reading is older than <see cref="CodexUsage.Fresh"/>. Never at a look, a panel, a pick or the page's next start, and
/// never the tool's own sign-in, which is in no list (D125 §3.7) and has no place in <c>windows.json</c>.</para>
/// <para><b>How, as a status question is asked</b> (TOOL6g): under the account's <see cref="ProbeLock"/>, and not at all
/// while a session of Daoris's runs on it, whose own process may refresh its token; never an account cooling, refused, read
/// signed out, or that is a key, whose plan has no windows. A reading not had is unknown and keeps nothing (D57).</para>
/// </remarks>
public sealed partial class HarnessRoster
{
    /// <summary>
    /// How an account's windows are asked in a test (CODEXUSE1): by owner and account, in place of the agent's server, asked
    /// under the same lock and the same holds, since every real question spawns the agent, which the fast half may not.
    /// </summary>
    internal Func<string, string, CancellationToken, Task<IReadOnlyList<WindowReading>?>>? AskingUsage { get; init; }

    // When each account's windows were last asked, by owner and account, answered or not: one that could not be had waits
    // as long as a reading stands before it is asked again.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _usageAsked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How this adapter's accounts' windows are asked (CODEXUSE1): its own question, else its owner's, since a door's readings
    /// are its owner's as its accounts are (AGT7). Null asks nothing.
    /// </summary>
    public UsageQuestion? UsageOf(string adapter)
    {
        var resolved = Adapters.Resolve(adapter);
        if (resolved.Toolchain?.Usage is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && Adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? Adapters.Resolve(owner).Toolchain?.Usage
            : null;
    }

    /// <summary>
    /// Whether this adapter's accounts say what they have left (D130 §6): its door carries the agent's frame
    /// (<see cref="WindowsOf"/>), or its agent's server is asked (<see cref="UsageOf"/>). What switching before the limit waits
    /// for.
    /// </summary>
    public bool Speaks(string adapter) => WindowsOf(adapter) is not null || UsageOf(adapter) is not null;

    /// <summary>
    /// A start's walk over a list (CODEXUSE1): each account it could take whose last reading is older than
    /// <see cref="CodexUsage.Fresh"/>, asked at once, each under its own lock, before the walk reads what each said.
    /// </summary>
    private async Task FreshenAsync(
        string adapter, string owner, IReadOnlyList<string> list, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is null) return;

        var now = Clock();
        IReadOnlyList<string> present;
        AgentReads reads;
        try
        {
            present = HarnessSettings.Profiles(Home, owner);
            reads = AccountReads.Of(Home, owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        var asking = list
            .Where(account => present.Contains(account, StringComparer.OrdinalIgnoreCase))
            .Where(account => Before(owner, account, now).IsReady && !SaidOut(reads, account))
            .Where(account => !Recent(owner, account, now))
            .Select(account => AskUsageAsync(adapter, owner, account, config, ct))
            .ToList();
        await Task.WhenAll(asking).ConfigureAwait(false);
    }

    /// <summary>Whether an account was asked, or said, within <see cref="CodexUsage.Fresh"/>: kept from a restart too.</summary>
    private bool Recent(string owner, string account, DateTimeOffset now)
    {
        if (_usageAsked.TryGetValue(AccountKey(owner, account), out var asked) && now - asked < CodexUsage.Fresh) return true;
        try
        {
            return AccountWindows.SaidOf(Home, owner, account, now) is { } said && now - said.Seen < CodexUsage.Fresh;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ask one account's windows of its agent's server, and keep what it answered (CODEXUSE1): null where nothing was asked or
    /// nothing could be had, and then nothing is kept. Never throws but for the caller's own stop.
    /// </summary>
    private async Task<IReadOnlyList<WindowReading>?> AskUsageAsync(
        string adapter, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is not { } question) return null;
        try
        {
            // An account that is a key is billed by the key, not a plan's windows (AGT3): nothing to read.
            if (HarnessKeys.Of(Home, owner, account) is not null) return null;
            _usageAsked[AccountKey(owner, account)] = Clock();

            // One question per account at a time, and none while a session of Daoris's runs on it (TOOL6g).
            await using var held = await ProbeLock.TakeAsync(ProbeLock.PathOf(Home, owner, account), ct: ct).ConfigureAwait(false);
            if (held is null || Busy(owner, account)) return null;

            IReadOnlyList<WindowReading>? readings;
            if (AskingUsage is { } standIn)
            {
                readings = await standIn(owner, account, ct).ConfigureAwait(false);
            }
            else
            {
                if (UsageCommand(question, config) is not { } command) return null;
                var toolchain = AccountToolchain(adapter) ?? Toolchain(adapter)!;
                var info = CodexUsage.Prepare(command.Run, toolchain, HarnessSettings.ProfileHome(Home, owner, account), command.Managed);
                readings = await CodexUsage.ReadAsync(info, CodexUsage.Patience, ct).ConfigureAwait(false);
            }

            if (readings is not { Count: > 0 }) return null;
            AccountWindows.Said(Home, owner, account, readings, Clock(), session: null);
            return readings;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // A reading not had, or not kept, costs a start's ranking, never the start: the account reads as unknown.
            return null;
        }
    }

    /// <summary>
    /// The server's command, as every spawn resolves its binary (D57): <c>driver.json</c>'s command for the question's harness,
    /// then its managed pin, then <c>PATH</c>. Null where it is pinned with nothing installed at the pin: nobody to ask.
    /// </summary>
    private (IReadOnlyList<string> Run, string? Managed)? UsageCommand(UsageQuestion question, DriverConfig config)
    {
        var arguments = question.Command.Skip(1).ToList();
        if (config.Commands.GetValueOrDefault(question.Harness) is { Count: > 0 } named) return ([.. named, .. arguments], null);
        if (Settings.ResolveVersion(question.Harness, workspace: null, chosen: null) is not { } pinned) return (question.Command, null);
        return HarnessSettings.ManagedBinary(Home, question.Harness, pinned, [question.Command[0]]) is { } managed
            ? ([managed, .. arguments], managed)
            : null;
    }

    /// <summary>A press (ROSTER1): each of the agent's accounts asked, one at a time per account, whatever its last reading's age.</summary>
    private async Task PressUsageAsync(string adapter, string owner, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is null) return;
        IReadOnlyList<string> accounts;
        try
        {
            accounts = HarnessSettings.Profiles(Home, owner);
        }
        catch (DriverException)
        {
            return;
        }

        await Task.WhenAll(accounts.Select(account => AskUsageAsync(adapter, owner, account, config, ct))).ConfigureAwait(false);
    }
}
