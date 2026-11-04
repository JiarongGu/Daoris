using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>What every adapter shares: the process shell, and the target riding in the environment.</summary>
internal static class Spawning
{
    /// <summary>
    /// A redirected process in the repository root. The environment, not arguments, carries the
    /// target's pieces: any script shape can read it without parsing, and nothing quest-sized ever
    /// hits a shell's quoting rules.
    /// </summary>
    /// <param name="redirectInput">
    /// Open stdin. A pipe-door session is given its whole target at once and has nobody to take turns
    /// with, so it gets none; a protocol-door session needs one, because the driver writes frames
    /// into it (D53).
    /// </param>
    public static ProcessStartInfo InRoot(
        SessionTarget target, string fileName, IEnumerable<string> arguments, bool redirectInput = false)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl, target.Home ?? DaorisHome.Resolve());
        if (redirectInput) info.RedirectStandardInput = true;

        // An intake (D65 §1b) serves an ask, not a quest: the ask and its own session, and the quest
        // variables absent rather than blank — the chat rule, for the same reason.
        if (target.Ask is { } ask)
        {
            info.Environment[AgentWorkVariables.Ask] = ask;
            if (target.Session is { } session) info.Environment[ChildMarks.SessionVariable] = session;
            info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return info;
        }

        // A reviewer (XAGENT1d) serves another session's work and no quest: its own record, so its connector says its opinion as
        // that session, and its instruction; the quest variables absent, as an intake's are.
        if (target.Opinion is not null || target.Helper is not null || target.Piece is not null)
        {
            if (target.Session is { } reviewer) info.Environment[ChildMarks.SessionVariable] = reviewer;
            info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return info;
        }

        // A brainstorm's pass (BRAINSTORM1a, D176 point 2) serves no quest and no repository: the brainstorm, its turn and where its
        // result tool writes, and its instruction; the quest variables absent, and no repository named.
        if (target.Brainstorm is { } brainstorm)
        {
            info.Environment[AgentWorkVariables.Brainstorm] = brainstorm;
            if (target.BrainstormTurn is { } turn) info.Environment[AgentWorkVariables.Turn] = turn;
            if (target.BrainstormAnswer is { } answer) info.Environment[AgentWorkVariables.Answer] = answer;
            if (target.Session is { } pass) info.Environment[ChildMarks.SessionVariable] = pass;
            info.Environment.Remove("DAORIS_REPOSITORY");
            info.Environment.Remove("DAORIS_SERVICE_URL");
            info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return info;
        }

        info.Environment["DAORIS_QUEST_ID"] = target.QuestId;
        info.Environment["DAORIS_QUEST_TITLE"] = target.Title;
        info.Environment["DAORIS_QUEST_BODY"] = target.Body;
        info.Environment["DAORIS_QUEST_ASKER"] = target.Asker;
        info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
        // Its own record, as an intake's is named (SESS1): a connector the harness starts itself on the
        // pipe door inherits it, and says which session published a quest.
        if (target.Session is { } own) info.Environment[ChildMarks.SessionVariable] = own;

        // The quest's files (D65 §2), when this machine holds any — absent rather than empty when it
        // does not, because a blank directory would read to a session as one that was emptied.
        if (target.AttachmentsDirectory is { } attachments)
        {
            info.Environment["DAORIS_QUEST_ATTACHMENTS"] = attachments;
        }

        return info;
    }

    /// <summary>
    /// The same shell for a CHAT (D49 §3), with stdin open so the person's messages reach the harness.
    /// </summary>
    /// <remarks>
    /// The quest variables are absent rather than empty: a conversation serves no quest, and a blank
    /// `DAORIS_QUEST_ID` would read to a session as an id it failed to parse. What it gets is what is
    /// true — which repository it is the agent for, where to reach the service if the conversation
    /// turns into a quest worth taking or publishing, and which record it is.
    /// </remarks>
    public static ProcessStartInfo ChatInRoot(
        ChatTarget target, string fileName, IEnumerable<string> arguments)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl, DaorisHome.Resolve());
        info.RedirectStandardInput = true;
        // Its own record, as a driven session's is named (CHATTAKE1b): a connector its harness starts on the pipe door from the
        // repository's own server list inherits it, so a take there marks this conversation's record.
        if (target.Session is { } session) info.Environment[ChildMarks.SessionVariable] = session;
        return info;
    }

    private static ProcessStartInfo Shell(
        string root, string fileName, IEnumerable<string> arguments, string repository, string serviceUrl, string? home)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // 🔴 The shell is a window, not a console. A console child of a GUI process is given a
            // console of its own unless this says otherwise — and nothing said otherwise, so every
            // git, every probe and every session flashed a terminal onto the desktop. Held by a source scan over every
            // spawn the desktop makes, because the next spawn site will forget it too.
            CreateNoWindow = true,

            // 🔴 A transcript is read as UTF-8 or it is not the transcript. .NET defaults a
            // redirected stream to the CONSOLE's codepage; on this machine that is CP936, and the
            // first real deployment recorded an em-dash (`e2 80 94`) as `e9 88 a5 3f` — decoded as
            // GBK and re-encoded. The result is still valid UTF-8, so nothing downstream can tell it
            // was ever wrong, and a platform that speaks 简体中文 loses every Chinese character in a
            // session record. Set here, once, because every adapter goes through this shell and the
            // protocol door parses JSON-RPC off the same stream.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        Unrole(info.Environment);
        info.Environment["DAORIS_REPOSITORY"] = repository;
        info.Environment["DAORIS_SERVICE_URL"] = serviceUrl;
        // TOOLS5 (D121 §2.4): every session, chat, intake and helper finds the tools the person chose first on its
        // PATH, so its own `git push` is the git the driver's fetch ran. Before the harness's binary is resolved on
        // that PATH (HarnessProbe.Apply), so an agent npm put under a managed node is found the same way. The tools are the
        // home's the start was handed (ENVSEAM1), this process's where it was handed none.
        Tools.Hand(info, home);
        AgentWorkplace.Current.HandGit(info.Environment, home, root);

        return info;
    }

    /// <summary>
    /// The variables that say which work a start serves, each set by the start itself for its role (ENVLEAK1): a quest's, an
    /// ask's, a brainstorm's, its own record's, and the repository and service it is the agent for.
    /// </summary>
    internal static readonly IReadOnlyList<string> Roles =
    [
        "DAORIS_QUEST_ID", "DAORIS_QUEST_TITLE", "DAORIS_QUEST_BODY", "DAORIS_QUEST_ASKER", "DAORIS_QUEST_ATTACHMENTS",
        "DAORIS_TARGET", AgentWorkVariables.Ask, ChildMarks.SessionVariable, ChildMarks.LineageProcVariable,
        AgentWorkVariables.Brainstorm, AgentWorkVariables.Turn, AgentWorkVariables.Answer, AgentWorkVariables.Room,
        "DAORIS_REPOSITORY", "DAORIS_SERVICE_URL",
    ];

    /// <summary>
    /// A start begins from the driver's own environment, and drops every role variable it inherits (ENVLEAK1): a driver run
    /// from inside a session Daoris drives — a test, a rehearsal — carries that session's quest, target and record, and a chat
    /// that inherited them would serve a quest it was never given, absent being the rule. Its own role is set after.
    /// </summary>
    internal static void Unrole(IDictionary<string, string?> environment)
    {
        foreach (var name in Roles) environment.Remove(name);
    }
}
