using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The transports' one shell (the agent-provider design §4.6): a door's <see cref="ProcessRecipe"/> completed into the process every
/// kind of session starts, in one order for every door. First the session's half (<see cref="Start"/>, <see cref="Chat"/>): the role
/// variables cleared, other providers' registered variables stripped, the repository and service set, the tools' environment, the
/// session's git, the identity variables, then the recipe's own. Then the account's half (<see cref="Equip"/>): the account's variables,
/// the binary and its pin, the program resolved on the tools' <c>PATH</c>, Claude Code's seam, the home variable last. A door's carriers
/// are appended after both, so the shim's check reads only the arguments the recipe built.
/// </summary>
/// <remarks>
/// The environment, not arguments, carries the target's pieces: any script shape can read it without parsing, and nothing quest-sized
/// ever hits a shell's quoting rules.
/// </remarks>
internal static class Shell
{
    /// <summary>A driven session's process: the recipe in the repository root, with whose work it serves on its environment.</summary>
    /// <param name="strip">Other providers' registered variables its environment must not inherit (s-vi); none for a door no registration made.</param>
    public static ProcessStartInfo Start(ProcessRecipe recipe, SessionTarget target, IReadOnlyCollection<string>? strip = null)
    {
        var info = Base(recipe, target.Root, target.Repository, target.ServiceUrl, target.Home ?? DaorisHome.Resolve(), strip);
        if (recipe.Input) info.RedirectStandardInput = true;
        Identity(info.Environment, target);
        return Own(info, recipe);
    }

    /// <summary>
    /// A conversation's process (D49 §3), its stdin open so the person's messages reach the agent. The quest variables are absent rather
    /// than empty: a conversation serves no quest, and a blank <c>DAORIS_QUEST_ID</c> would read as an id it failed to parse.
    /// </summary>
    public static ProcessStartInfo Chat(ProcessRecipe recipe, ChatTarget target, IReadOnlyCollection<string>? strip = null)
    {
        var info = Base(recipe, target.Root, target.Repository, target.ServiceUrl, DaorisHome.Resolve(), strip);
        info.RedirectStandardInput = true;
        // Its own record, as a driven session's is named (CHATTAKE1b): a connector its agent starts on the pipe door from the
        // repository's own server list inherits it, so a take there marks this conversation's record.
        if (target.Session is { } session) info.Environment[ChildMarks.SessionVariable] = session;
        return Own(info, recipe);
    }

    /// <summary>
    /// The account's half: an account's own variables, the binary a pin chose with the pin's environment, the program resolved as Windows
    /// can start it, Claude Code's seam and the account's home. <b>Creating the home is part of choosing it</b>: an agent may refuse to
    /// start when its home variable names a path that is not there, and Daoris owns that location by design.
    /// </summary>
    /// <param name="binary">The managed executable in place of what the recipe resolved (TOOL2/D57), or null for <c>PATH</c>.</param>
    /// <param name="claudeExecutable">The managed <c>claude</c> a door that runs Claude Code's program is pointed at, or null.</param>
    /// <remarks>
    /// 🔴 Every session's process, a probe's and a usage question's come through here, which is why the pin, the shim and the home are
    /// applied in this one place: a door that forgot one would spawn the wrong binary and record the pinned version beside it.
    /// </remarks>
    public static void Equip(
        ProcessStartInfo info, HarnessToolchain toolchain, string? profileHome, string? binary = null,
        string? claudeExecutable = null, IReadOnlyDictionary<string, string>? account = null)
    {
        // What the account itself carries — an API key in the tool's own variable (AGT3), so no spawn of a key account goes out without it.
        foreach (var (name, value) in account ?? new Dictionary<string, string>()) info.Environment[name] = value;

        // The arguments the recipe built stay exactly as they are: same tool, different location.
        if (binary is { Length: > 0 })
        {
            info.FileName = binary;

            // 🔴 And the version it was pinned at (AGT2): a pinned Claude Code reported its own auto-updates enabled, and would have moved
            // itself under the pin.
            foreach (var (name, value) in toolchain.PinnedEnvironment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        }

        // An agent npm installed starts as the node and script its `.cmd` shim would run, so no `cmd` stands before it and its arguments
        // are never read again by one (LAUNCHCHAIN1).
        PackageStarts.Direct(info, toolchain.Package);

        // 🔴 USE1f: what Windows can START, on the PATH the tools put first. A bare name from PATH starts only as an `.exe`, so an agent npm
        // installed globally (a `.cmd` shim) probed absent and every start on it was held, though the CLI found it.
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue("PATH", out var path) ? path : null);

        // A door running Claude Code's program finds its CLI through its own seam (ACP2, §1a); null leaves it unset.
        ClaudeAcp.PointAtClaude(info, claudeExecutable);

        if (profileHome is null) return;

        if (toolchain.ProfileVariable is not { Length: > 0 } variable)
        {
            throw new DriverException(
                "that agent has no configuration-home variable, so Daoris cannot run it as a named "
                + "account. Its accounts are managed with its own tooling.");
        }

        // 🔴 Created, not assumed to exist: `codex-acp` exits 1 before `initialize` completes when `CODEX_HOME` names a path that is not
        // there, while the Claude adapter creates its own, so every managed home is made here, for every door (ACP3).
        Directory.CreateDirectory(profileHome);
        info.Environment[variable] = profileHome;
    }

    private static ProcessStartInfo Base(
        ProcessRecipe recipe, string root, string repository, string serviceUrl, string? home, IReadOnlyCollection<string>? strip)
    {
        var info = new ProcessStartInfo
        {
            FileName = recipe.Program,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // 🔴 The shell is a window, not a console. A console child of a GUI process is given a console of its own unless this says
            // otherwise, so every git, every probe and every session flashed a terminal onto the desktop. Held by a source scan over every
            // spawn the desktop makes, because the next spawn site will forget it too.
            CreateNoWindow = true,

            // 🔴 A transcript is read as UTF-8 or it is not the transcript. .NET defaults a redirected stream to the console's code page
            // (CP936 here), and the first real deployment recorded an em-dash decoded as GBK and re-encoded: still valid UTF-8, so nothing
            // downstream could tell, and a session in 简体中文 lost every Chinese character. The protocol door parses JSON-RPC off it too.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in recipe.Arguments) info.ArgumentList.Add(argument);

        Spawning.Unrole(info.Environment);
        // Other providers' credentials and homes the driver inherited (s-vi), taken before anything Daoris sets and the account's bound.
        foreach (var name in strip ?? []) info.Environment.Remove(name);
        info.Environment["DAORIS_REPOSITORY"] = repository;
        info.Environment["DAORIS_SERVICE_URL"] = serviceUrl;
        // TOOLS5 (D121 §2.4): every session finds the tools the person chose first on its PATH, so its own `git push` is the git the
        // driver's fetch ran, and an agent npm put under a managed node is found the same way when the program is resolved (Equip). The
        // tools are the home's the start was handed (ENVSEAM1), this process's where it was handed none.
        Tools.Hand(info, home);
        SessionGit.Current.Hand(info.Environment, home, root);
        return info;
    }

    /// <summary>Whose work a driven start serves (ENVLEAK1), each set by the start for its role and the rest absent.</summary>
    private static void Identity(IDictionary<string, string?> environment, SessionTarget target)
    {
        // An intake (D65 §1b) serves an ask, not a quest: the ask and its own session, the quest variables absent rather than blank.
        if (target.Ask is { } ask)
        {
            environment[AgentWorkVariables.Ask] = ask;
            if (target.Session is { } session) environment[ChildMarks.SessionVariable] = session;
            environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return;
        }

        // A reviewer (XAGENT1d), a helper or a piece serves another session's work and no quest: its own record, so its connector says
        // its opinion as that session, and its instruction.
        if (target.Opinion is not null || target.Helper is not null || target.Piece is not null)
        {
            if (target.Session is { } reviewer) environment[ChildMarks.SessionVariable] = reviewer;
            environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return;
        }

        // A brainstorm's pass (BRAINSTORM1a, D176 point 2) serves no quest and no repository: the brainstorm, its turn and where its
        // result tool writes, and its instruction; no repository named.
        if (target.Brainstorm is { } brainstorm)
        {
            environment[AgentWorkVariables.Brainstorm] = brainstorm;
            if (target.BrainstormTurn is { } turn) environment[AgentWorkVariables.Turn] = turn;
            if (target.BrainstormAnswer is { } answer) environment[AgentWorkVariables.Answer] = answer;
            if (target.Session is { } pass) environment[ChildMarks.SessionVariable] = pass;
            environment.Remove("DAORIS_REPOSITORY");
            environment.Remove("DAORIS_SERVICE_URL");
            environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return;
        }

        environment["DAORIS_QUEST_ID"] = target.QuestId;
        environment["DAORIS_QUEST_TITLE"] = target.Title;
        environment["DAORIS_QUEST_BODY"] = target.Body;
        environment["DAORIS_QUEST_ASKER"] = target.Asker;
        environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
        // Its own record, as an intake's is named (SESS1): a connector the agent starts itself on the pipe door inherits it.
        if (target.Session is { } own) environment[ChildMarks.SessionVariable] = own;

        // The quest's files (D65 §2), absent rather than empty where this machine holds none: a blank directory reads as one emptied.
        if (target.AttachmentsDirectory is { } attachments) environment["DAORIS_QUEST_ATTACHMENTS"] = attachments;
    }

    /// <summary>The recipe's own variables and its input's encoding, after Daoris's and before the account's.</summary>
    private static ProcessStartInfo Own(ProcessStartInfo info, ProcessRecipe recipe)
    {
        foreach (var (name, value) in recipe.Variables) info.Environment[name] = value;
        if (recipe.Utf8Input) info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        return info;
    }
}
