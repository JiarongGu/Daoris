using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The `agents trust` verb: the folders a harness is told to trust.
/// </summary>
internal static class AgentTrustVerb
{
    /// <summary>
    /// <c>trust &lt;agent&gt; &lt;folder&gt; [--account &lt;account&gt;] --yes</c>: the agent's trust in a folder, asked, then written on
    /// the person's word. Without <c>--yes</c> the question is the command: it names the folder, each account's file and what
    /// trusting means, grants nothing, and exits 1. The account is the one named; with none named, every account of Daoris's for the
    /// agent, each in its own file (one set serves every workspace, so a folder trusted for Daoris is trusted
    /// for each account that may run its work); with none at all, refused. 🔴 Never the tool's own trust file, which is the person's.
    /// </summary>
    internal static AgentPress? Trust(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, _, roster) = AgentsCommand.Agent(args, "trust", "<agent>", world, path);
        if (toolchain.Home.TrustFile is not { } trustFile)
        {
            throw new AgentWords($"`{name}` has no trust question Daoris knows of, so there is nothing to grant: its own permissions apply as they are.");
        }

        var given = AgentsCommand.Operand(args, 2) ?? throw new AgentWords("`agent trust` needs <agent> <folder> — e.g. `daoris agent trust claude-code`");
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(world.Folder, given)));
        if (!Directory.Exists(folder)) throw new AgentWords($"no folder at `{folder}` — trust is granted to a folder a session would run in.");

        var owner = roster.Adapters.OwnerOfAgent(name).Value;
        if (owner != name) Say($"{AgentsCommand.Prefix}: `{name}` runs as `{owner}`'s accounts — this is one of them.");
        var profile = AgentsCommand.FlagValue(args, "--account") is { } named ? AgentsCommand.AccountNamed(home, owner, named) : null;
        // With no account named, the set in its order: the order's accounts that are here, then the rest.
        IReadOnlyList<string> accounts = profile is not null ? [profile] : SetOf(path, home, owner);
        if (accounts.Count == 0)
        {
            throw new AgentWords(
                $"`{owner}` has no account of Daoris's to trust the folder for — Daoris never writes your own sign-in's file. Sign in to "
                + $"one with `daoris agent login {owner} --new`, or trust the folder with the tool itself, at your terminal (run `claude` "
                + "there once and accept its trust prompt).");
        }

        var files = accounts.Select(account => Path.Combine(HarnessSettings.ProfileHome(home, owner, account), trustFile)).ToList();
        Say(profile is not null
            ? $"{AgentsCommand.Prefix}: trusting `{folder}` for `{owner}`, as the account `{profile}` — in its own file:"
            : $"{AgentsCommand.Prefix}: trusting `{folder}` for `{owner}`, for each of Daoris's accounts ({string.Join(", ", accounts.Select(each => $"`{each}`"))}) — each in its own file:");
        foreach (var file in files) Say($"  {file}");
        Say("  This is what the agent asks the first time it runs in a folder. Once it is granted, the");
        Say("  folder's own settings and `permissions.allow` apply there, and the agent stops asking.");
        Say($"  Daoris writes that one flag and nothing else in {(files.Count == 1 ? "the file" : "each file")}.");

        if (args.Contains("--dry-run")) return null;
        if (!args.Contains("--yes"))
        {
            Say("  Not granted: this is the question. Run it again with --yes to grant it.");
            exit = 1;
            return null;
        }

        return new AgentPress("trust", null, false, null, _ => Task.FromResult<object?>(Granted(files, folder)), _ => null, done =>
        {
            var grant = (TrustGrant)done!;
            if (!grant.Changed)
            {
                Say("  Already trusted, so nothing was written.");
            }
            else if (grant.Verified)
            {
                Say("  Granted.");
            }
            else
            {
                Say("  Written, but reading the file back does not show it — a running Claude Code may have saved");
                Say("  over it. Run this again once it has exited.");
                return 1;
            }

            return 0;
        });
    }

    /// <summary>
    /// <paramref name="owner"/>'s accounts of Daoris's in the set's order: the order's that are on
    /// this machine, then the rest by id. None where it has none.
    /// </summary>
    internal static List<string> SetOf(string path, string home, string owner)
    {
        var here = HarnessSettings.Profiles(home, owner);
        var order = HarnessSettings.LoadSet(path, [owner]).Rotation.GetValueOrDefault(owner) ?? [];
        return [.. order.Where(account => here.Contains(account, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal),
            .. here.Where(account => !order.Contains(account, StringComparer.Ordinal))];
    }

    /// <summary>
    /// The folder granted in each file, one after another: changed where any took the write, verified where every re-read shows it.
    /// A file this build cannot read refuses as <see cref="ClaudeTrust.Grant"/> does, the ones before it written.
    /// </summary>
    internal static TrustGrant Granted(IReadOnlyList<string> files, string folder)
    {
        var grants = files.Select(file => ClaudeTrust.Grant(file, folder)).ToList();
        return new TrustGrant(grants[0].Key, grants.Any(grant => grant.Changed), grants.All(grant => grant.Verified));
    }
}
