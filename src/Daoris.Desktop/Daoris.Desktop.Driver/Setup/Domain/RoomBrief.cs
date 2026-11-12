using System.Text;

namespace Daoris.Driver;

/// <summary>What a room's quest is composed from: the repository, the folder, the person's reason, and the declaration its line holds today.</summary>
/// <param name="Repository">The repository it is for, by its registered name.</param>
/// <param name="Path">The folder, as the repository keeps a path: forward slashes, no leading or trailing one (<see cref="PlaceRoomPath"/>).</param>
/// <param name="Purpose">The person's reason, one line.</param>
/// <param name="Line">The line the declaration is read on, and its tip <paramref name="Commit"/>.</param>
/// <param name="Declared"><c>daoris.json</c>'s <c>rooms</c>, as declared.</param>
/// <param name="Lanes">The rooms the lanes file declares: a lane's <c>room</c>, or its own id where it carries <c>builds</c>.</param>
/// <param name="Folders">The folders holding an <c>AGENTS.md</c> of their own, outside the doctrine's folders.</param>
public sealed record RoomBriefInput(
    string Repository, string Path, string Purpose, string Line, string Commit, IReadOnlyList<string> Declared, IReadOnlyList<string> Lanes,
    IReadOnlyList<string> Folders);

/// <summary>
/// The room quest's words: a short title and a body that carries the canon's room convention and the repository's own declaration, for a session
/// that creates the folder's own <c>AGENTS.md</c>, declares it and runs the doctrine tool on its own branch. Daoris writes none of it.
/// </summary>
/// <remarks>
/// <para>🔴 <b>In the canon's words</b>, as <see cref="SetupBrief"/>'s are: no decision number, no path of Daoris's own, no machine path. The
/// convention is the canon's <c>rooms-and-classes</c> knowledge, named by <see cref="RoomsProposal.Knowledge"/> (the one place the set-up's brief and
/// Ask Daoris's room name it) and quoted only at <see cref="Quoted"/>'s four lines, which <c>PlaceRoomTests</c> holds to the canon's text.</para>
///
/// <para><b>Every verb it asks for is one the press allows</b> (<see cref="SetupPressRules.RoomVerbs"/>), exactly, since a session on the protocol
/// door cannot ask for another. The body is the whole of the request: a room has no chain.</para>
/// </remarks>
public static class RoomBrief
{
    /// <summary>The longest path a title carries before it is cut.</summary>
    private const int TitleLength = 80;

    /// <summary>The verbs the body asks for, which its press allows.</summary>
    public static IReadOnlyList<string> Verbs => SetupPressRules.RoomVerbs;

    /// <summary>The lines of the canon's convention the body quotes, each a sentence of its text as the canon writes it.</summary>
    public static IReadOnlyList<string> Quoted { get; } =
    [
        "A room is a lane that one session at a time claims and works in, in place in the repository's checkout.",
        "A lane that carries `builds` is a room.",
        "Lanes that are one package, one lockfile or one build name the same `room` and keep their own names.",
        "Paths are repository-relative with `/`; a folder ends in `/`; `*` stays in a folder and `**` crosses.",
    ];

    /// <summary>The title of the quest that asks for a room at <paramref name="path"/>.</summary>
    public static string TitleOf(string path) => $"Add a room for {TextBounds.Clip(path, TitleLength)}";

    /// <summary>
    /// The body's opening: what is asked, the person's reason quoted, and whose the change is. The one source of the words, composed into the body
    /// and shown on a card before the quest is asked.
    /// </summary>
    public static string Opening(string path, string purpose) =>
        $"**What is asked, and whose it is.** Give `{path}` a room of its own in this repository: a folder with an "
        + "`AGENTS.md` of its own, declared as a room, so that a session working there reads the conventions of that area and not "
        + $"only the root brief's. The person's reason:\n\n{TextBounds.Quote(purpose)}\n\n"
        + "This is this repository's own change, made by its own session on its own branch and landed by the workspace's rule. "
        + "Daoris never writes a room's text, and nothing was written here from outside: the folder's `AGENTS.md` and its declaration "
        + "are this repository's own files, and nothing outside this tree is this session's to change.";

    /// <summary>The title and the body for a room asked of <paramref name="input"/>'s repository.</summary>
    public static (string Title, string Body) Compose(RoomBriefInput input)
    {
        var path = input.Path;
        var body = new StringBuilder();
        body.Append(Opening(path, input.Purpose)).Append("\n\n");

        body.Append("**What was read here**, from its line `").Append(input.Line).Append("` at commit `").Append(SetupBrief.Short(input.Commit)).Append("`:\n\n")
            .Append("- `daoris.json`'s `rooms`: ").Append(input.Declared.Count == 0 ? "none." : SetupBrief.Names(input.Declared) + ".").Append('\n')
            .Append("- `daoris.lanes.json`'s lanes ").Append(input.Lanes.Count == 0 ? "name no room." : "name these rooms: " + SetupBrief.Names(input.Lanes) + ".").Append('\n')
            .Append("- Folders with an `AGENTS.md` of their own: ").Append(input.Folders.Count == 0 ? "none." : SetupBrief.Names(input.Folders) + ".").Append('\n')
            .Append("- No declaration names `").Append(path).Append("` and no `AGENTS.md` is there: it holds no room today.\n\n");

        body.Append("**The convention** is the doctrine's `").Append(RoomsProposal.Knowledge).Append("` knowledge: read it first, since the steps after ")
            .Append("this follow it. It says, among other things:\n\n")
            .Append(string.Join("\n", Quoted.Select(line => TextBounds.Quote(line)))).Append("\n\n");

        string[] steps =
        [
            "**The tool.** Run `daoris --version`. If it prints nothing, or `daoris` is not found, stop and decline: *the doctrine command could not run "
            + "here*, with what it printed.",
            "**The checks first.** Run `daoris check` and this repository's own checks, where they are a session's to run, before the first change, and "
            + "note which were green.",
            $"**Write the room's own `AGENTS.md`** at `{path}/AGENTS.md`, making the folder where it is not there, from the room template beside the "
            + "`set-up-documents` skill. It holds only what differs in that area from the root brief: its conventions, its traps and its checks, found "
            + "by reading its code and its build files, never its folder names. A line goes in only if nearly every task there needs it and nothing "
            + "in the code would tell a reader; nothing is copied from the root brief, and no fact is written that the code did not confirm.",
            $"**Declare it.** Add `{path}` to `daoris.json`'s `rooms`. Where this repository's lanes file has a lane that owns this folder, declare "
            + "that lane's `room`, and its `builds` where its builds are its own, by the convention above; leave every other lane, and a "
            + "repository that has no lanes file, as they are. If the line already holds a room there by the time you read this, close `done` "
            + "saying so, and write no second.",
            "**Verify.** Run `daoris sync`, which keeps the room's `CLAUDE.md` pointer and never writes its text, then `daoris check`, which fails a "
            + "declared room that holds no text, then this repository's own checks: green where they were green before. One green before and red "
            + "after means this does not close `done`.",
            SetupBrief.Commit(),
        ];
        body.Append("**The steps**, in order.\n\n");
        for (var i = 0; i < steps.Length; i++) body.Append(i + 1).Append(". ").Append(steps[i]).Append('\n');
        body.Append('\n');

        body.Append("**What this writes, and what it never does.** Write only in this tree, on this branch: `").Append(path).Append("/AGENTS.md` and ")
            .Append("`").Append(path).Append("/CLAUDE.md` (the pointer `daoris sync` writes), `daoris.json`'s `rooms`, the lanes file's declaration ")
            .Append("for the lane that owns the folder, and `daoris.lock` with what `daoris sync` writes. Never push, merge or open a pull request; ")
            .Append("never write outside this tree (another repository's checkout you may read is read only); never publish a request to another ")
            .Append("repository; never run `daoris connect` or `daoris upstream`; never set `remote.join` or `remote.knowledge` in `daoris.json`; never ")
            .Append("edit another room's `AGENTS.md`, or move or rename a room that exists; and never change a source, build or CI file: anything ")
            .Append("that needs one is named in your close, as work for a request the person may publish.\n\n");

        body.Append("**How to close it.** Close it `done`, saying: the tool's version; the checks before and after; the room's path, and in one line what ")
            .Append("its `AGENTS.md` holds; each line declared and where; and what you found that needs a request elsewhere. Or decline, with the ")
            .Append("reason, *the doctrine command could not run here* among them.\n");

        return (TitleOf(path), body.ToString().TrimEnd() + "\n");
    }
}
