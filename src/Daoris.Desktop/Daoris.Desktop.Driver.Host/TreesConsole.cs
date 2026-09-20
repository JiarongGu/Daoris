namespace Daoris.Driver.Host;

/// <summary>
/// The session trees, from a terminal: `daoris-driver trees [list | remove &lt;path&gt; [--force]]`.
/// </summary>
/// <remarks>
/// <para><b>Why here and not in the `daoris` CLI.</b> The tree lifecycle is git spawned against real
/// checkouts, and the CLI's discipline is that only `toolchain.ts` spawns anything — so the verbs live
/// on the binary that already owns git (D50's parity is between surfaces, not between packages). The
/// standing OPT-IN is the CLI's (`daoris driver trees &lt;repo&gt; on|off`), because that is a file
/// edit; the trees themselves are this side's, because they are processes and disk.</para>
///
/// <para>Exit codes keep the family contract: 0 clean · 1 refused (work would be lost) · 2 tool error.</para>
/// </remarks>
internal static class TreesConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        var configPath = DriverConfig.ResolvePath();
        var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var trees = new SessionTrees(home);

        switch (args)
        {
            case [] or ["list", ..]:
            {
                var listed = await trees.ListAsync().ConfigureAwait(false);
                if (listed.Count == 0)
                {
                    Console.WriteLine($"trees: none — nothing under {trees.TreesRoot}.");
                    Console.WriteLine("  `daoris driver trees <repository> on` opts a repository in;");
                    Console.WriteLine("  `daoris-driver chat --own-tree` opens one for a conversation.");
                    return 0;
                }

                foreach (var tree in listed)
                {
                    Console.WriteLine($"  {tree.Workspace}/{tree.Repository}  {tree.Branch}");
                    Console.WriteLine($"    {tree.Path}");
                }

                return 0;
            }

            case ["remove", var path, ..]:
            {
                var removal = await trees.RemoveAsync(path, force: args.Contains("--force"))
                    .ConfigureAwait(false);
                Console.WriteLine($"trees: {removal.Message}");
                // A refusal that preserves work is policy doing its job, not a tool error.
                return removal.Removed ? 0 : 1;
            }

            default:
                Console.Error.WriteLine("usage: daoris-driver trees [list | remove <path> [--force]]");
                Console.Error.WriteLine("  A session's worktree (D51). Removal refuses while the tree holds");
                Console.Error.WriteLine("  uncommitted changes or unmerged commits; --force means it.");
                return 2;
        }
    }
}
