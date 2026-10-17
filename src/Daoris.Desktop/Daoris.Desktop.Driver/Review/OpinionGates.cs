using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The gate's own record (XAGENT1f; the second-agent design §8.5–§8.6): <c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.json</c>, one per
/// chain, named by its first quest, with each repository's part apart. What the person said at the gate is kept here, on this
/// machine (D47 §4), and on the landing record when the work lands: the opinion itself is the local host's, which no door here
/// writes the person's answer into.
/// </summary>
/// <remarks>
/// What is kept is this use case's; the file and its format are <see cref="OpinionGateFiles"/>'.
/// </remarks>
public sealed class OpinionGates(string home)
{
    // One writer at a time in this process: the look, a press and the terminal's verbs may write at once.
    private static readonly object Gate = new();

    /// <summary>Where a chain's record is kept; null for a name that is no plain id.</summary>
    public string? PathOf(string chain) =>
        SafeNames.IsPlainId(chain) ? DaorisHome.Entry(home, HomeEntry.OpinionGates, $"{chain}.json") : null;

    /// <summary>What is kept for the chain's work in a repository; none where nothing reads.</summary>
    public OpinionGateKept Read(string chain, string repository) =>
        ReadAll(chain).TryGetValue(repository, out var kept) ? kept : OpinionGateKept.None;

    /// <summary>A pass asked, kept in the order asked.</summary>
    public void Asked(string chain, string repository, OpinionAskKept ask) =>
        Edit(chain, repository, kept => kept with { Asks = [.. kept.Asks, ask] });

    /// <summary>A pass the person asked for, kept until the next look starts it.</summary>
    public void Requested(string chain, string repository, OpinionRequest request) =>
        Edit(chain, repository, kept => kept with { Requests = [.. kept.Requests, request] });

    /// <summary>One session's requests, taken by the look, which starts the newest; the others stay for their own sessions.</summary>
    public IReadOnlyList<OpinionRequest> Take(string chain, string repository, string session)
    {
        IReadOnlyList<OpinionRequest> taken = [];
        Edit(chain, repository, kept =>
        {
            taken = [.. kept.Requests.Where(request => string.Equals(request.Session, session, StringComparison.OrdinalIgnoreCase))];
            return kept with { Requests = [.. kept.Requests.Where(request => !taken.Contains(request))] };
        });
        return taken;
    }

    /// <summary>An answer of the person's at the gate.</summary>
    public void Said(string chain, string repository, OpinionPersonWord word) =>
        Edit(chain, repository, kept => kept with { Person = [.. kept.Person, word] });

    /// <summary>Each repository's part of a chain's record, by name in any case; none where the name is no id or nothing reads.</summary>
    private IReadOnlyDictionary<string, OpinionGateKept> ReadAll(string chain) =>
        PathOf(chain) is { } path ? OpinionGateFiles.ReadGates(path) : new Dictionary<string, OpinionGateKept>(StringComparer.OrdinalIgnoreCase);

    private void Edit(string chain, string repository, Func<OpinionGateKept, OpinionGateKept> change)
    {
        if (PathOf(chain) is not { } path) throw new DriverException($"`{chain}` names no chain this machine keeps a gate for.");
        lock (Gate)
        {
            var all = ReadAll(chain).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var key = all.Keys.FirstOrDefault(name => string.Equals(name, repository, StringComparison.OrdinalIgnoreCase)) ?? repository;
            all[key] = change(all.GetValueOrDefault(key) ?? OpinionGateKept.None);
            OpinionGateFiles.WriteGates(path, chain, all);
            // This process's own write wakes no landing (FABRIC-END7, EVENT2's rule): another's, a terminal's answer, is heard.
            HomeWrites.Mine(path);
        }
    }
}
