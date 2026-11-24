using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// The person's switch on an account, on or off: part of the account record every agent shares (<c>accounts.json</c>, beside the
/// name), read by the one account walk and so the same for Claude Code, Codex and any other harness. An account switched off stays
/// signed in and is read as before; Daoris starts no session on it and moves none to it (<see cref="AccountReadiness.Off"/>), and a
/// session already on it goes on to its turn's end and is moved at its next resume by the walk. Written only by the person's press:
/// the window's switch and <c>daoris agent account on|off</c>, as a sign-in is the person's.
/// </summary>
/// <remarks>
/// The file's shape: <c>{ "&lt;agent&gt;": { "&lt;id&gt;": { "name": "…", "off": true } } }</c>. A switched-on account has no <c>off</c>
/// key, and an entry holding nothing is removed, so the file of a person who never switches holds no trace of the feature. The
/// file is written whole under its <see cref="KeepLock"/> by <see cref="AccountNames"/>' writers and this one alike, each keeping
/// the keys it has no field for.
/// </remarks>
public static class AccountSwitch
{
    private const string OffKey = "off";

    /// <summary>Whether the person switched <paramref name="account"/> of <paramref name="agent"/> off; false for any other, and for a file that does not read.</summary>
    public static bool IsOff(string home, string agent, string account) =>
        Child(Child(AccountFileNodes.Load(AccountNames.PathOf(home)), agent), account) is { } entry && Off(entry);

    /// <summary>Every account of <paramref name="agent"/> the person switched off, compared without case.</summary>
    public static IReadOnlySet<string> OffOf(string home, string agent)
    {
        var off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Child(AccountFileNodes.Load(AccountNames.PathOf(home)), agent) is not { } held) return off;
        foreach (var (id, node) in held)
        {
            if (!HarnessAccountFolders.NotAnAccount(id) && node is JsonObject entry && Off(entry)) off.Add(id);
        }

        return off;
    }

    /// <summary>
    /// Switch an account off or on: the person's press, both doors'. Refused for an account that is not here, writing nothing.
    /// </summary>
    /// <param name="accounts">The agent's accounts on this machine, <see cref="HarnessSettings.Profiles"/>.</param>
    /// <returns>Whether the file changed: false where the account already stood so.</returns>
    /// <exception cref="AccountNameException">The account is not here, or the file does not read.</exception>
    /// <remarks>🔴 The file's keep lock is held from the read to the write, as a rename holds it.</remarks>
    public static bool Set(string home, string agent, IReadOnlyCollection<string> accounts, string account, bool off)
    {
        lock (AccountNames.Gate)
        {
            using var locked = KeepLock.Take(AccountNames.PathOf(home));
            var (root, unreadable) = AccountFileNodes.Read(AccountNames.PathOf(home));
            if (AccountNames.Problem(accounts, AccountNames.Of(home, agent), account, null) is { } problem)
            {
                throw new AccountNameException(problem, AccountNames.Sentence(agent, problem, account, null, accounts, AccountNames.Of(home, agent)));
            }

            if (unreadable)
            {
                throw new AccountNameException(
                    new AccountNameProblem(AccountNameProblemKind.Unreadable),
                    $"{AccountNames.FileName} could not be read, so `{account}` was not switched {(off ? "off" : "on")} — writing it would lose every "
                    + "other account's name. Fix the file or remove it, then switch it again.");
            }

            var held = Child(root, agent);
            var entry = held is null ? null : Child(held, account);
            if (off == (entry is not null && Off(entry))) return false;

            if (off)
            {
                held ??= (JsonObject)(root[agent] = new JsonObject());
                if (entry is null) held[account] = entry = new JsonObject();
                entry[OffKey] = true;
            }
            else
            {
                entry!.Remove(OffKey);
                if (entry.Count == 0) held!.Remove(Key(held, account)!);
                if (held!.Count == 0) root.Remove(Key(root, agent)!);
            }

            JsonFile.Write(AccountNames.PathOf(home), root);
            return true;
        }
    }

    private static bool Off(JsonObject entry) => entry[OffKey] is JsonValue value && value.TryGetValue<bool>(out var off) && off;
}
