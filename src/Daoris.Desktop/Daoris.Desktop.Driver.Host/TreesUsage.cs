namespace Daoris.Driver.Host;

/// <summary>The usage `daoris tree` says where a word was not understood.</summary>
internal static class TreesUsage
{
    /// <summary>The verb's usage, after what was not understood where something was said; exit 2.</summary>
    public static int Say(string? problem)
    {
        if (problem is not null) Console.Error.WriteLine($"trees: {problem}");
        Console.Error.WriteLine("usage: daoris tree [list | remove <path|session|branch> [--repository <name>] [--force]");
        Console.Error.WriteLine("                           | remove <landing branch> [--repository <name>] --why \"…\" [--plan]");
        Console.Error.WriteLine("                           | clean [--workspace <name>] [--yes] | land <session> [--plan] [--branch <name>]");
        Console.Error.WriteLine("                           | land --entry <id> | --ask <id> [--leave-out <session>]... [--plan]");
        Console.Error.WriteLine("                           | hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]");
        Console.Error.WriteLine("                           | state [--entries]");
        Console.Error.WriteLine("                           | state <session|branch> [--repository <name>]");
        Console.Error.WriteLine("                           | sync [--repository <name>] [--workspace <name>] [--all] [--yes]]");
        Console.Error.WriteLine("  A session's worktree (D51). Removal refuses while the tree holds");
        Console.Error.WriteLine("  uncommitted changes or work no branch of yours holds; --force means it.");
        Console.Error.WriteLine("  A session or its branch removes a failed or superseded attempt's branch,");
        Console.Error.WriteLine("  with its tree where it is still here (LAND3); one whose tree a session");
        Console.Error.WriteLine("  still running or waiting holds is kept, even with --force (LAND3c).");
        Console.Error.WriteLine("  A branch a landing made whose work the line will never hold is withdrawn with --why \"…\": its");
        Console.Error.WriteLine("  commits stay on a recovery ref and its record says why (LANDRETIRE1). Only one whose files still");
        Console.Error.WriteLine("  differ on the line goes, and never one checked out, a pull request may still carry, or the line holds;");
        Console.Error.WriteLine("  --plan says what it would do. Only the process holding the home acts: the desktop's window, or this alone.");
        Console.Error.WriteLine("  clean lists every session branch with what it holds; --yes removes those");
        Console.Error.WriteLine("  whose work is on a branch of yours, or that hold nothing (D88), and every");
        Console.Error.WriteLine("  branch a landing made whose files read on the line as it left them (WSR5).");
        Console.Error.WriteLine("  land accepts a session's work as the review's Accept does, by the workspace's");
        Console.Error.WriteLine("  rule; --plan says where it would go and does nothing (D87). A rule that tidies");
        Console.Error.WriteLine("  also removes every other session branch the landed work holds (LAND3). --branch names the");
        Console.Error.WriteLine("  branch itself, in place of the rule's name, where the rule makes a branch (LANDNAME1a). A rule naming a");
        Console.Error.WriteLine("  plugin hands the branch to it to push and open the pull request (D100). A session");
        Console.Error.WriteLine("  that already landed, while its branch stands or once its tree is gone, is said");
        Console.Error.WriteLine("  as its review says it, and lands nothing again (D113).");
        Console.Error.WriteLine("  A conversation's work is gated by its chat run: its press asks the second opinion its rule names and");
        Console.Error.WriteLine("  lands once every part settles. A look it waits on is passed over with `workflow skip <run> <node> \"…\"`");
        Console.Error.WriteLine("  first (CHATFLOW1d).");
        Console.Error.WriteLine("  land --entry or --ask is the one Accept of a line of work or an ask, as Sessions' list has it: every part");
        Console.Error.WriteLine("  lands in the order it ran, each by its own gate and the rule; --leave-out names a part a later one");
        Console.Error.WriteLine("  redid, which --plan says (D167's 2026-10-15 amendment). An ask lands each repository's line of work.");
        Console.Error.WriteLine("  hand gives a branch a landing made to a landing plugin afterwards — the one");
        Console.Error.WriteLine("  --plugin names, else the rule's — to push and open the pull request (WSR5).");
        Console.Error.WriteLine("  state alone says each unit of work's milestone: whether its end was reached, the result kept, the");
        Console.Error.WriteLine("  cleanup pending (what waits and why) or completed, and the recovery retained; --entries lists the");
        Console.Error.WriteLine("  inventory it is read from. It changes nothing (FABRIC-MILESTONE1a); `work state` says the same.");
        Console.Error.WriteLine("  state asks the plugin that pushed a landed branch, else the rule's, again whether");
        Console.Error.WriteLine("  its pull request completed, keeps the answer, and says what it proves here; the");
        Console.Error.WriteLine("  clean-up and sync act on what it kept, and ask too where git cannot tell (D148).");
        Console.Error.WriteLine("  sync brings each repository up to date after a pull request merged: it fetches");
        Console.Error.WriteLine("  the line and fast-forwards it, replays the branches still at work onto it (only");
        Console.Error.WriteLine("  their own commits), and deletes the landed branches whose work reached it. It");
        Console.Error.WriteLine("  lists first; --yes does it. Daoris fetches, and never pushes (WSR6). It takes");
        Console.Error.WriteLine("  the repositories holding Daoris's branches and names the rest; --all takes");
        Console.Error.WriteLine("  every one, and --repository the one named (D112).");
        Console.Error.WriteLine("  For clean and sync, --workspace takes one workspace's checkouts alone, as its");
        Console.Error.WriteLine("  Branches tab does; a workspace the registry does not name is refused (BRSCOPE1a).");
        return 2;
    }
}
