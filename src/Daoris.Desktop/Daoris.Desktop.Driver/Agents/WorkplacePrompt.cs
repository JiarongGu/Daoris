using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The instruction's words about the working tree: what it reads across, what it must not write, how it is set up and where its commits land.
/// </summary>
internal static class WorkplacePrompt
{
    /// <summary>
    /// What to ask another repository for (D79). Where the session may read across (D107), reading is no
    /// longer forbidden: what stays asked is a change, and what its code cannot tell.
    /// </summary>
    internal static string Needs(SessionTarget target) => target.ReadsAcross.Count == 0
        ? """
          If the work needs something only another repository knows or can change — its contract, its data,
          a change in its code — do not read into it and do not guess: ask it.
          """
        : """
          If the work needs something another repository's code cannot tell you, or a change in it — what it
          promises, why it is the way it is — do not guess: ask it.
          """;

    /// <summary>
    /// The other checkouts this session may read and not write (D107), each by name and path, and how: their
    /// files where they lie, and two read-only git commands. Empty when there are none, so the target reads
    /// exactly as it did before.
    /// </summary>
    internal static string Reading(SessionTarget target)
    {
        var readOnly = target.ReadsAcross
            .Where(read => !target.WritesAcross.Any(write => string.Equals(write.Repository, read.Repository, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return readOnly.Count == 0
            ? ""
            : $"""

              You may read these other repositories' checkouts on this machine, and change nothing in them:

              {Listed(readOnly)}

              Read their files where they lie, and see how each stands with `git -C <path> status` and
              `git -C <path> branch --list`, the path written as above.

              """;
    }

    /// <summary>
    /// Never write outside this repository — except, where the person declared it (D107), in the checkouts
    /// this repository may also change, which the boundary names with how to commit there.
    /// </summary>
    internal static string Boundary(SessionTarget target) => target.WritesAcross.Count == 0
        ? """
          Never write outside this repository. Work another repository needs is a quest published to it,
          never an edit — that is the rule the whole arrangement rests on. Anything that cannot be taken
          back or that leaves the repository — a push, a publish, a release — is not yours to do; surface
          it and finish.
          """
        : $"""
          Never write outside this repository, except in these, which the person has declared this one may change:

          {Listed(target.WritesAcross)}

          There, change only what this quest needs, follow that repository's own doctrine, and commit what you
          change in it with `git -C <path> add` and `git -C <path> commit`.
          Any other work another repository needs is a quest published to it, never an edit — that is the rule
          the whole arrangement rests on. Anything that cannot be taken back or that leaves the repository — a
          push, a publish, a release — is not yours to do; surface it and finish.
          """;

    /// <summary>Each checkout on a line of its own, its path as git and its rule spell it.</summary>
    internal static string Listed(IEnumerable<AcrossCheckout> checkouts) =>
        string.Join("\n", checkouts.Select(checkout => $"- `{checkout.Repository}` — `{AcrossRules.GitPath(checkout.Path)}`"));

    /// <summary>
    /// A set-up step's section (REVIEWENV1c, D154 points 4–6; the review environment design §2.2–§2.6, §3.4): the environment, its
    /// kind and its address; the repository's procedure, read first and never gone past; that the tree holds the chain's work and
    /// the step adds none; never a process or a port of the person's; what needs a go-ahead and what never happens; proof; the
    /// set-up said through <c>review_ready</c> before the done; and how the person's <i>not yet</i> comes back. Nothing for every
    /// other quest, so its instruction reads as it did.
    /// </summary>
    internal static string SetUp(SessionTarget target)
    {
        if (target.SetUp is not { } environment) return "";
        var name = environment.Name;
        var local = environment.Kind == "local";
        var where = environment.Address is { } address ? $" at `{address}`" : "";
        var text = new StringBuilder("\n");
        text.Append($"This quest is a set-up step. It shows the work of its chain to the person in `{name}`, for them to review ")
            .Append("before it lands: ")
            .Append(local
                ? $"`{name}` is a local environment, and the work is shown in Daoris's browser{where}, where the app normally runs.\n"
                : $"`{name}` is a deployed environment{where}, reached by the route this repository documents.\n")
            .Append('\n')
            .Append($"- Read `{environment.Procedure}` first. It is this repository's own procedure for reaching `{name}`: follow it, and ")
            .Append("never go past it. Where it does not cover something you need, say so under **Needs you** rather than improvise.\n")
            .Append("- This tree holds the chain's work. Add none of your own, and make no commit for the set-up itself; what the procedure ")
            .Append("makes, such as a build's output, stays where the procedure keeps it.\n")
            .Append("- Never stop, restart or take over a process or a port of the person's. That needs their go-ahead at the least, and ")
            .Append("showing the work needs none.\n")
            // REVIEWENV1d (design §2.3 steps 1–4): the tab the driver opened before this session, by the title it gave it, and
            // what Daoris does with the folder once the session ends. The service's `review_serve` answers for the moment itself.
            .Append(local
                ? $"- Daoris opened a tab of its browser for you, titled `{ReviewTabNames.Title(target.QuestId)}` until you navigate it. "
                  + "Find it among the browser's tabs and work in it: the person sees it, and a tab you open yourself has no window.\n"
                  + "- Build the work as the procedure says, then ask Daoris to serve that build to that tab with your connector's "
                  + $"`review_serve`: the build's folder in this tree, and `{environment.Address}`. Where it answers that nothing is "
                  + "served yet, show it in that tab the way the procedure gives. Once you end, Daoris serves that folder to that tab at "
                  + $"`{environment.Address}` until the person's review, so a reload there shows your build and never the person's own "
                  + "server. Go to the change, do what it is about, and leave the tab there. An interaction that would save to a shared "
                  + "environment's data needs the person's go-ahead; without it, stop just before it and say so.\n"
                : $"- Reach `{name}` as the procedure says. A deploy, a pipeline run, a configuration write or a sign-in there needs the "
                  + "person's go-ahead, asked once on the ask. A step the procedure leaves to a push is the person's: say so under "
                  + "**Needs you**, naming the procedure's line. Then show the work there in Daoris's browser, go to the change, and "
                  + "leave it there.\n")
            .Append("- Never production, a push, or a merge into a line, and write nothing outside this tree but your proof. Capture ")
            .Append("proof of what you showed.\n")
            .Append("- Then say the set-up with your connector's `review_ready`: where the tab is (`look`), what it shows and what to ")
            .Append("look at (`shows`), and how to show it again by hand (`again`). Close this quest `done` only after that; it then ")
            .Append("waits for the person's review, and Daoris reads the commit your tree holds when you end.\n")
            .Append("- The person may say *not yet*, with their words, or ask to see it again: either comes back to you as a new turn. ")
            .Append("Put right what their words name: in the showing or the environment, there; in the work, in this tree, committed, ")
            .Append("with your closing note saying it was a correction under their words. Then show it again and say a new set-up.\n");
        return text.ToString();
    }

    /// <summary>
    /// How the work lands and is committed (D87, PROCESS1d-i): the branch the pull request comes from and that the session makes
    /// no move of its own on it, then the commit rule standing for its repository, said only for a tree of its own
    /// (<see cref="ProcessWords"/>). Under a rule that accepts automatically (LAND2b), that its quest's done lands it, so its work
    /// is committed by then.
    /// </summary>
    internal static string Landing(SessionTarget target) => AgentWorkplace.Current.ProcessWords(target.Commits, target.LandsOn) ?? "";
}
