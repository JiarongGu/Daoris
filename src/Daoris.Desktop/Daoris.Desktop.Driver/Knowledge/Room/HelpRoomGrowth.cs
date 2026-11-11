using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Which place a request calls for, said once and in order: the department that owns the work, a room in it, a new
/// repository, a folder department, or none; who is asked what; and where the press stops. The two presses that open a place are named
/// in <see cref="HelpRoomVerbs"/>, and what each mode does with them in <see cref="HelpRoomMayDo"/>.
/// </summary>
/// <remarks>
/// The question is asked of the registry and the records, never of a classifier, so the section says how it is asked and that the answer is
/// said in the turn. What the presses refuse is the routes' own words, which the room never restates.
/// </remarks>
internal sealed class HelpRoomGrowth : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("### Opening a department or a room\n\n");
        text.Append("Repositories and rooms are the company's departments, the structure its work is staffed through. Before you press\n");
        text.Append("anything that opens one, read the registry's departments (each one's `domain`: what it `owns` and `accepts`, in every\n");
        text.Append("workspace) and search the knowledge, never guess, and say in your answer which places you looked at and why none\n");
        text.Append("owns the work. The first row that holds is the answer:\n\n");
        text.Append("| The request | The answer |\n");
        text.Append("|---|---|\n");
        text.Append("| A registered department owns the work (its `owns` or `accepts` covers it, or its records show the same work done), in this workspace or another | That department: an ask, `daoris ask --workspace <name> \"…\"`, which the intake turns into quests |\n");
        text.Append("| The work is a new part of a department's code, built, tested and released with it, under the conventions it has | That department: a new folder in it is not a new place |\n");
        text.Append("| The same, but the part needs instructions, gates or a claim group of its own (an `AGENTS.md` of its own, its own checks) | A room in that department: `daoris place <repository> --room <path> --purpose \"…\" --apply` |\n");
        text.Append("| The work is its own deliverable: it builds, tests or releases apart, has its own history, audience or access rules, and no department owns it | A new repository: `daoris repository create <name> --purpose \"…\"` |\n");
        text.Append("| The work is files that want no history or branches (documents, assets) | A folder department: `daoris repository create <name> --kind folder --purpose \"…\"` |\n");
        text.Append("| A system reached only through a plugin's console | Not this: a system's adapter is the plugin's, which the person installs |\n");
        text.Append("| The request only names a thing, asks for a plan or an answer, or is a throwaway | Nothing is created: scratch is a repository's `_fixtures` or a gitignored folder, and a plan is words |\n\n");
        text.Append("Where two rows could hold, the earlier wins: an existing department over a room, a room over a new repository.\n");
        text.Append("The person's explicit words win over the table (\"make me a new repository for this\"), with the press's refusals\n");
        text.Append("still applying. Ask the person only what is theirs: a name where the request gives none that fits, a workspace\n");
        text.Append("where the work spans two, a folder where the workspace has no repository to infer one from.\n\n");
        text.Append("Any agent opens a place through these presses, you, a lead or a driven session, its tier deciding as for every press.\n");
        text.Append("The intake never creates one: where no department owns an ask it says so.\n\n");
        text.Append("`daoris repository create` stops before any content: the folder, an empty history with no remote, the registration,\n");
        text.Append("the drive and the quests. `--work \"…\"` carries the person's request as the quest that follows the set-up, asked of the\n");
        text.Append("new place's own session once the set-up is done. You write no file of the new department's, no `daoris.json`, no\n");
        text.Append("brief, no code: its own session does, reviewed and landed by its workspace's rule. A room is never made by a press:\n");
        text.Append("`--room` asks the repository's own session for one quest, with the reason in one line, and writes none of the room's\n");
        text.Append("text.\n\n");
        text.Append("A press the route would refuse (a name registered already, a folder that exists and is not empty, a workspace that\n");
        text.Append("does not exist, no git identity, a room's repository not adopted or held) is answered to you at once in the route's\n");
        text.Append("own words and never shown on a card: say them, never reword them into a promise, and work inside the departments\n");
        text.Append("and rooms that exist, saying in your answer that a place of its own was wanted.\n\n");
        return text.ToString();
    }
}
