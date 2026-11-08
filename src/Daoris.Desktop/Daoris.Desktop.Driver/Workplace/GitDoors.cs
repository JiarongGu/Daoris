using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Daoris's two doors at each git act (11): the ssh door, which
/// git starts as its ssh, and the remote helper, which git runs for every <c>daoris::</c> address the rewrite made. Each asks the judge
/// (<see cref="GitAccessRules"/>) for the act's class at its remote command, refuses with one line git prints, and otherwise starts what
/// git would have started without Daoris, carrying today's credentials.
/// </summary>
/// <remarks>
/// <para><b>Started only by git</b>, as <c>daoris-git-ssh.exe</c> and <c>git-remote-daoris.exe</c> in the install's <c>app/bin/</c>
/// (<c>Daoris.Desktop.GitDoor</c>, whose <c>Program</c> calls <see cref="Ssh"/> or <see cref="Helper"/> by its own file's name). Its
/// home is <see cref="GitRouting.HomeVariable"/>; where that is absent the door refuses, saying it is started outside Daoris's wiring.</para>
/// <para><b>Nothing prompts</b>: the ssh door gives <c>-o BatchMode=yes</c> first, before any option of a command the
/// repository names, since OpenSSH takes the first value given; the helper's transport inherits the prompt variables
/// <see cref="GitWiring"/> handed.</para>
/// <para>The base library alone, as everything the door compiles in.</para>
/// </remarks>
public static class GitDoors
{
    /// <summary>The exit a refusal ends with: git's own for a remote that could not be read.</summary>
    public const int Refused = 128;

    private const string BatchMode = "BatchMode=yes";

    // ---- The ssh door ----

    /// <summary>
    /// What git handed the ssh door (OpenSSH's variant): its options, the <c>[user@]host</c>, and the remote command,
    /// split into its program, its path and LFS's operation. Null with the refusal's line for anything else.
    /// </summary>
    public sealed record SshCall(IReadOnlyList<string> Options, string Host, string Command, string Program, string Path, string? Operation);

    /// <summary>Read git's arguments to the ssh door; the call, or the one line that refuses them.</summary>
    public static (SshCall? Call, string? Refusal) ReadSsh(IReadOnlyList<string> arguments)
    {
        var options = new List<string>();
        var index = 0;
        for (; index < arguments.Count && arguments[index].StartsWith('-'); index++)
        {
            var option = arguments[index];
            switch (option)
            {
                case "-4" or "-6":
                    options.Add(option);
                    break;
                case "-p" when index + 1 < arguments.Count:
                    options.Add(option);
                    options.Add(arguments[++index]);
                    break;
                case "-o" when index + 1 < arguments.Count && arguments[index + 1] == "SendEnv=GIT_PROTOCOL":
                    options.Add(option);
                    options.Add(arguments[++index]);
                    break;
                default:
                    return (null, $"daoris-git-ssh: refuses the option {option}: it takes only what git passes an OpenSSH ssh.");
            }
        }

        if (arguments.Count - index != 2) return (null, "daoris-git-ssh: takes one host and one remote command, as git passes them.");
        var host = arguments[index];
        var command = arguments[index + 1];
        var space = command.IndexOf(' ');
        var program = space < 0 ? command : command[..space];
        var (path, rest) = space < 0 ? ("", "") : Unquote(command[(space + 1)..]);
        var operation = rest.Trim() is { Length: > 0 } word ? word : null;
        if (GitAccessRules.ClassOf(program, operation) is null || path.Length == 0 || (operation is not null && program != "git-lfs-authenticate"))
            return (null, $"daoris-git-ssh: refuses the remote command {program}: it takes git-upload-pack, git-receive-pack and git-lfs-authenticate.");
        return (new SshCall(options, host, command, program, path, operation), null);
    }

    /// <summary>
    /// The ssh door: judge the remote command, then start the ssh git would have started without Daoris with <c>BatchMode</c> first,
    /// its streams the door's own; the exit is that ssh's, or <see cref="Refused"/>. A push's commands are read from git's side and judged
    /// before they reach the remote (<see cref="GitPushUpdates"/>).
    /// </summary>
    /// <param name="input">git's side of the conversation, read only for a push; null is the door's standard input.</param>
    public static int Ssh(IReadOnlyList<string> arguments, Func<string, string?> variable, TextWriter error, string folder, Stream? input = null)
    {
        var (call, refusal) = ReadSsh(arguments);
        if (call is null) return Refuse(error, refusal!);
        if (variable(GitRouting.HomeVariable) is not { Length: > 0 } home)
            return Refuse(error, "daoris-git-ssh: was started outside Daoris's wiring, with no home to act for.");

        var act = GitAccessRules.ClassOf(call.Program, call.Operation)!.Value;
        var host = call.Host[(call.Host.LastIndexOf('@') + 1)..];
        var key = GitAddressKeys.Of(call.Host, call.Path);
        // Who serves the act is known before the judge, which refuses a Write through an identity read only on its host.
        var serves = Serving(home, folder, call, variable);
        var verdict = Judge(home, act, key, variable, remote: null, host, folder, serves.Identity);
        if (!verdict.Allowed) return Refuse(error, verdict.Line!);

        // Each update of a push is judged by its kind for every agent, whatever its harness's rules, and the
        // grant by the refs it carries, before the remote hears any of them.
        var pushes = call.Program == "git-receive-pack"
            ? new SshPush(input ?? Console.OpenStandardInput(), updates =>
                GitPushDoor.Refusal(home, verdict, updates, KeepsHistory(variable, folder), key, variable, remote: null, host, folder))
            : null;

        if (serves.Kind == GitServeKind.Identity) return IdentitySsh(home, serves.Identity!, call, variable, error, folder, act, host, pushes);

        // Nothing serving a host means no key to reach it with.
        if (serves.Kind == GitServeKind.None)
        {
            GitLog.Refused(home, variable, "no-identity", act, null, host);
            return Refuse(error, GitMoved.SshLine(host));
        }

        // Under own: what git would have run.
        var (file, prefix, problem) = SshCommand(folder, variable);
        if (problem is not null)
        {
            // A command that is not OpenSSH is the repository's own input the door cannot carry; no ssh at all, an act not reached.
            GitLog.Refused(home, variable, file is null && problem.Contains("plink", StringComparison.Ordinal) ? "own-input" : "unreached", act, null, host);
            return Refuse(error, problem);
        }

        return Run(SshStart(file!, folder, [.. prefix, .. call.Options, call.Host, call.Command]), error, pushes);
    }

    /// <summary>A push's side of the ssh door: git's stream, and the judge of its commands, which answers the refusal's line or null.</summary>
    private sealed record SshPush(Stream Input, Func<IReadOnlyList<GitPushUpdate>, string?> Refusal);

    /// <summary>The ssh the door starts, under an identity or as git would have: its file, its folder and its words.</summary>
    private static ProcessStartInfo SshStart(string file, string folder, IReadOnlyList<string> words)
    {
        // Not the tools' environment: the door is git's child, and its ssh takes git's environment as git would have handed it.
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            WorkingDirectory = folder,
            // inherits its console: the door is git's console child, and its ssh speaks git's protocol over the stdio it hands down. A
            // spawn with no stream redirected and no window is given a console of its own, which cuts that stdio.
            CreateNoWindow = false,
        };
        foreach (var word in words) info.ArgumentList.Add(word);
        return info;
    }

    /// <summary>
    /// What serves an ssh act: the repository its working folder belongs to (<see cref="GitFolders"/>), the
    /// remotes whose address is the act's, and the act's host with git's port, resolved from the files alone, taking no lease.
    /// </summary>
    public static GitServes Serving(string home, string folder, SshCall call, Func<string, string?> variable)
    {
        var identities = GitIdentities.Read(home);
        var pointers = GitIdentityUse.Read(home);
        if (identities.Count == 0 && pointers.Count == 0) return GitServes.Nothing([]);

        var port = PortOf(call.Options);
        var host = GitHosts.Of(call.Host, port);
        var (repository, workspace) = GitFolders.RepositoryOf(home, folder);
        IReadOnlyList<string> remotes = repository is not null && pointers.Repository.ContainsKey(repository)
            ? RemotesAt(folder, GitAddressKeys.Of(call.Host, call.Path), variable)
            : [];
        return GitIdentityUse.Resolve(identities, pointers, repository, workspace, remotes, host, GitIdentity.Ssh);
    }

    /// <summary>
    /// What serves an HTTP or HTTPS act: as <see cref="Serving"/>, by the act's address, its host with a port where
    /// not the scheme's default. From the files alone, taking no lease.
    /// </summary>
    public static GitServes ServingHttps(string home, string folder, string address, Func<string, string?> variable)
    {
        var identities = GitIdentities.Read(home);
        var pointers = GitIdentityUse.Read(home);
        if (identities.Count == 0 && pointers.Count == 0) return GitServes.Nothing([]);
        if (GitHttps.HostOf(address) is not { } host) return GitServes.Nothing([]);

        var (repository, workspace) = GitFolders.RepositoryOf(home, folder);
        IReadOnlyList<string> remotes = repository is not null && pointers.Repository.ContainsKey(repository)
            ? RemotesAt(folder, GitAddressKeys.Of(address), variable)
            : [];
        return GitIdentityUse.Resolve(identities, pointers, repository, workspace, remotes, host, GitIdentity.Https);
    }

    /// <summary>The port git handed the door by <c>-p</c>, or null for the default.</summary>
    internal static string? PortOf(IReadOnlyList<string> options)
    {
        for (var index = 0; index + 1 < options.Count; index++)
        {
            if (options[index] == "-p") return options[index + 1];
        }

        return null;
    }

    /// <summary>
    /// The repository's remotes whose fetch or push address has <paramref name="key"/> (<see cref="GitAddressKeys"/>), read from git's
    /// configuration in the act's folder (the door's one git start under a repository's pointer).
    /// </summary>
    internal static IReadOnlyList<string> RemotesAt(string folder, string? key, Func<string, string?> variable)
    {
        if (key is null || Find("git", variable("PATH")) is not { } git) return [];
        var said = Ask(git, folder, ["config", "--get-regexp", @"^remote\..*\.(url|pushurl)$"]);
        if (said is null) return [];
        var names = new List<string>();
        foreach (var line in said.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ');
            if (space < 0) continue;
            var name = line[..space];
            var remote = name["remote.".Length..name.LastIndexOf('.')];
            if (string.Equals(GitAddressKeys.Of(line[(space + 1)..].Trim()), key, StringComparison.OrdinalIgnoreCase) && !names.Contains(remote))
                names.Add(remote);
        }

        return names;
    }

    /// <summary>An identity's act: <see cref="GitSshProgram"/>'s ssh by its full path, Daoris's options first, then git's own.</summary>
    private static int IdentitySsh(
        string home, GitIdentity identity, SshCall call, Func<string, string?> variable, TextWriter error, string folder, GitAct act, string host,
        SshPush? pushes)
    {
        var (ssh, problem) = GitSshProgram.Of(home, variable);
        if (ssh is null)
        {
            GitLog.Refused(home, variable, problem!.Contains("OpenSSH", StringComparison.Ordinal) ? "own-input" : "unreached", act, null, host);
            return Refuse(error, problem);
        }

        if (!File.Exists(GitKeys.KeyPath(home, identity.Id)))
        {
            GitLog.Refused(home, variable, "no-identity", act, null, host);
            return Refuse(error, $"daoris-git-ssh: the git identity {identity.Label} has no key on this machine: add it again in Daoris.");
        }

        GitSshFiles.Used(home, identity.Id);
        return Run(SshStart(ssh, folder, IdentityArguments(home, identity.Id, call)), error, pushes);
    }

    /// <summary>Every argument an identity's ssh is started with, in order: the door's argument table holds it.</summary>
    public static IReadOnlyList<string> IdentityArguments(string home, string id, SshCall call) =>
        [.. GitSshFiles.IdentityOptions(home, id), .. call.Options, call.Host, call.Command];

    /// <summary>
    /// The ssh git would have started without Daoris (today's credentials): the repository's <c>core.sshCommand</c> at
    /// whatever scope git reads it, through <c>sh</c> where it holds git's shell characters; else the <c>ssh</c> git's own path finds.
    /// Answered as the file to start and the words before git's own: <c>-o BatchMode=yes</c> first of all. PuTTY's <c>plink</c>,
    /// which takes other arguments, is refused.
    /// </summary>
    public static (string? File, IReadOnlyList<string> Prefix, string? Problem) SshCommand(string folder, Func<string, string?> variable)
    {
        var configured = ConfiguredSsh(folder, variable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            var ssh = Find("ssh", variable("PATH"));
            return ssh is null
                ? (null, [], "daoris-git-ssh: finds no ssh on git's PATH.")
                : (ssh, ["-o", BatchMode], null);
        }

        var (first, rest) = FirstWord(configured);
        var name = Path.GetFileNameWithoutExtension(first.Replace('\\', '/').Split('/')[^1]).ToLowerInvariant();
        if (name is "plink" or "tortoiseplink")
            return (null, [], $"daoris-git-ssh: refuses this repository's ssh command ({name}): it does not take OpenSSH's arguments.");

        if (configured.IndexOfAny(GitRouting.ShellCharacters.ToCharArray()) < 0) return (first, ["-o", BatchMode], null);

        var sh = Find("sh", variable("PATH"));
        if (sh is null) return (null, [], "daoris-git-ssh: this repository's ssh command needs a shell, and git's PATH has no sh.");
        var spliced = $"{configured[..^rest.Length].TrimEnd()} -o {BatchMode} {rest}".TrimEnd();
        return (sh, ["-c", spliced + " \"$@\"", spliced], null);
    }

    /// <summary>The repository's own <c>core.sshCommand</c>, as git reads it in <paramref name="folder"/>; null where none is set.</summary>
    private static string? ConfiguredSsh(string folder, Func<string, string?> variable)
    {
        if (Find("git", variable("PATH")) is not { } git) return null;
        return Ask(git, folder, ["config", "--get", "core.sshCommand"])?.Trim();
    }

    /// <summary>Whether the first commit is an ancestor of the second where the push runs; false where git cannot say (an object the repository lacks).</summary>
    private static Func<string, string, bool> KeepsHistory(Func<string, string?> variable, string folder) =>
        (old, now) => Find("git", variable("PATH")) is { } git && Ask(git, folder, ["merge-base", "--is-ancestor", old, now]) is not null;

    /// <summary>What git answers in <paramref name="folder"/> on its standard output; null where it fails or cannot start.</summary>
    private static string? Ask(string git, string folder, IReadOnlyList<string> arguments)
    {
        // Not the tools' environment: git's own git, on git's PATH, asked its configuration.
        var info = new ProcessStartInfo(git)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var word in arguments) info.ArgumentList.Add(word);
        try
        {
            using var process = Process.Start(info)!;
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEndAsync();
            var said = process.StandardOutput.ReadToEnd();
            error.Wait();
            process.WaitForExit();
            return process.ExitCode == 0 ? said : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>A shell command's first word, unquoted, and what follows it as written.</summary>
    internal static (string First, string After) FirstWord(string command)
    {
        var text = command.TrimStart();
        if (text.Length > 0 && text[0] is '"' or '\'')
        {
            var close = text.IndexOf(text[0], 1);
            if (close > 0) return (text[1..close], text[(close + 1)..].TrimStart());
        }

        var space = text.IndexOfAny([' ', '\t']);
        return space < 0 ? (text, "") : (text[..space], text[(space + 1)..].TrimStart());
    }

    /// <summary>A path git quoted for the shell (<c>sq_quote</c>: <c>'…'</c>, with <c>'\''</c> for a quote), and what follows it.</summary>
    internal static (string Path, string After) Unquote(string text)
    {
        if (!text.StartsWith('\''))
        {
            var space = text.IndexOf(' ');
            return space < 0 ? (text, "") : (text[..space], text[(space + 1)..]);
        }

        var built = new StringBuilder();
        var index = 1;
        while (index < text.Length)
        {
            var close = text.IndexOf('\'', index);
            if (close < 0) return (built.Append(text[index..]).ToString(), "");
            built.Append(text, index, close - index);
            if (text.AsSpan(close).StartsWith("'\\''"))
            {
                built.Append('\'');
                index = close + 4;
                continue;
            }

            return (built.ToString(), text[(close + 1)..]);
        }

        return (built.ToString(), "");
    }

    // ---- The remote helper ----

    /// <summary>
    /// The remote helper: start git's own <c>git-remote-https</c> (or <c>-http</c>) for the address, without Daoris's routing, and relay
    /// git's conversation with it, judging the act's class at its command: <c>list</c>, <c>fetch</c> and <c>stateless-connect</c> a read,
    /// <c>list for-push</c> and <c>push</c> a write. A refusal ends the conversation before the command reaches the transport.
    /// </summary>
    public static int Helper(
        IReadOnlyList<string> arguments, Func<string, string?> variable, Stream input, Stream output, TextWriter error,
        Func<IDictionary<string, string?>, IDictionary<string, string?>>? environment = null, string? folder = null)
    {
        if (arguments.Count != 2) return Refuse(error, "git-remote-daoris: takes a remote and an address, as git passes them.");
        var address = arguments[1];
        var transport = address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "git-remote-https"
            : address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "git-remote-http"
            : null;
        if (transport is null) return Refuse(error, "git-remote-daoris: carries only http:// and https:// addresses.");
        if (variable(GitRouting.HomeVariable) is not { Length: > 0 } home)
            return Refuse(error, "git-remote-daoris: was started outside Daoris's wiring, with no home to act for.");
        var remote = RemoteName(arguments[0]);
        var host = Uri.TryCreate(address, UriKind.Absolute, out var parsed) ? parsed.Authority[(parsed.Authority.LastIndexOf('@') + 1)..] : null;
        if (Find(transport, variable("PATH")) is not { } file)
        {
            GitLog.Refused(home, variable, "unreached", act: null, remote, host);
            return Refuse(error, $"git-remote-daoris: finds no {transport} on git's PATH.");
        }

        var serves = ServingHttps(home, folder ?? Environment.CurrentDirectory, address, variable);
        var identity = serves.Kind == GitServeKind.Identity ? serves.Identity : null;
        // A remote nothing serves reads anonymously, as an identity's with no sign-in would.
        var anonymous = serves.Kind == GitServeKind.None;
        if ((identity is not null || anonymous) && GitHttps.HasPassword(address))
            return Refuse(error, "git-remote-daoris: refuses an address carrying a password: a git identity's token is kept in Daoris's tokens folder, not written into an address.");

        // Not the tools' environment: git's own transport, with git's environment less Daoris's routing (§4.3).
        // The relay reads and writes the streams' bytes (BaseStream); the encodings are named for the census, and change no byte.
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(arguments[0]);
        // Under an identity the transport is given no user-info; the credential door answers the identity's user and token.
        info.ArgumentList.Add(identity is null && !anonymous ? address : GitHttps.Bare(address));
        GitRouting.Unroute(info.Environment);
        if (identity is not null)
        {
            GitHttps.Apply(info.Environment, home, identity.Id, GitCredentialDoor.FolderOf(variable));
            GitSshFiles.Used(home, identity.Id);
        }
        else if (anonymous)
        {
            GitHttps.Apply(info.Environment, home, identityId: null, doorFolder: null);
        }

        environment?.Invoke(info.Environment);

        var key = GitAddressKeys.Of(address);
        var pushes = new GitHelperPush();
        using var process = Process.Start(info)!;
        string? refused = null;
        // git's side, on a thread of its own: the conversation ends when the transport's answers end, which may be before git closes
        // its side (an empty repository's clone), and a door still reading git then would hold git waiting on its answers.
        _ = Task.Run(() =>
        {
            var judged = new Dictionary<GitAct, GitVerdict>();
            var pending = new List<string>();
            var where = folder ?? Environment.CurrentDirectory;
            var to = process.StandardInput.BaseStream;
            try
            {
                while (ReadLine(input) is { } line)
                {
                    var text = Encoding.UTF8.GetString(line).TrimEnd('\n', '\r');
                    // A push's lines are held to the batch's end, judged together with the remote's refs and their objects, and
                    // forwarded as the objects judged: a refused update stops the transport before it has been told of any.
                    if (text.StartsWith("push ", StringComparison.Ordinal))
                    {
                        pending.Add(text);
                        continue;
                    }

                    if (text.Length == 0 && pending.Count > 0)
                    {
                        if (!judged.TryGetValue(GitAct.Write, out var write)) judged[GitAct.Write] = write = Judge(home, GitAct.Write, key, variable, remote, host, where, identity);
                        var (forward, said) = write.Allowed ? PushBatch(home, write, pending, pushes, key, variable, remote, host, where) : ([], write.Line);
                        if (said is not null)
                        {
                            Volatile.Write(ref refused, said);
                            Stop(process);
                            return;
                        }

                        foreach (var each in forward) to.Write(Encoding.UTF8.GetBytes(each + "\n"));
                        pending.Clear();
                        to.Write(line);
                        to.Flush();
                        continue;
                    }

                    if (ClassOfHelperCommand(text) is { } act)
                    {
                        if (!judged.TryGetValue(act, out var verdict)) judged[act] = verdict = Judge(home, act, key, variable, remote, host, where, identity);
                        if (!verdict.Allowed)
                        {
                            Volatile.Write(ref refused, verdict.Line);
                            Stop(process);
                            return;
                        }
                    }

                    if (text == "list for-push") pushes.Listing();
                    to.Write(line);
                    to.Flush();
                    if (text.StartsWith("stateless-connect ", StringComparison.Ordinal) || text.StartsWith("connect ", StringComparison.Ordinal))
                    {
                        // From here the conversation is the protocol's packets, not lines, each awaited by the other side: relayed as
                        // they come, flushed each time, since a pipe's buffered write would hold a request its answer waits on.
                        Relay(input, to);
                        break;
                    }
                }
            }
            catch (IOException)
            {
                // The transport went first; its exit says why.
            }

            try { to.Close(); } catch (IOException) { }
        });

        // The transport's answers pass through the tap first, so the remote's refs are known before git's push lines come.
        Relay(process.StandardOutput.BaseStream, output, pushes.Feed);
        process.WaitForExit();
        if (Volatile.Read(ref refused) is { } line) return Refuse(error, line);
        output.Flush();
        return process.ExitCode;
    }

    /// <summary>
    /// The batch of push lines judged: the lines to forward, the objects judged in place of git's source names, or the line
    /// that refuses the whole batch. Refused too where the remote's refs or a source could not be read, since an update unread is unjudged.
    /// </summary>
    private static (IReadOnlyList<string> Forward, string? Refusal) PushBatch(
        string home, GitVerdict write, IReadOnlyList<string> lines, GitHelperPush pushes, string? key, Func<string, string?> variable,
        string? remote, string? host, string folder)
    {
        const string Unread = "daoris: Daoris's door could not read what this push moves, so nothing was pushed.";
        var git = Find("git", variable("PATH"));
        if (git is null || pushes.Refs is not { } refs) return ([], Unread);
        var batch = GitHelperPush.Build(lines, refs, source =>
            source.StartsWith('-') ? null
                : Ask(git, folder, ["rev-parse", "--verify", "--quiet", source])?.Trim() is { } oid && GitPushUpdates.IsObject(oid) ? oid : null);
        if (batch is null) return ([], Unread);
        var refusal = GitPushDoor.Refusal(home, write, batch.Updates, KeepsHistory(variable, folder), key, variable, remote, host, folder);
        return refusal is null ? (batch.Lines, null) : ([], refusal);
    }

    /// <summary>The class of a remote-helper command git sends; null for one that moves nothing (<c>capabilities</c>, <c>option</c>).</summary>
    public static GitAct? ClassOfHelperCommand(string line) => line switch
    {
        "list for-push" => GitAct.Write,
        "list" => GitAct.Read,
        _ when line.StartsWith("push ", StringComparison.Ordinal) => GitAct.Write,
        _ when line.StartsWith("fetch ", StringComparison.Ordinal) => GitAct.Read,
        _ when line.StartsWith("stateless-connect ", StringComparison.Ordinal) => GitAct.Read,
        _ when line.StartsWith("connect ", StringComparison.Ordinal) => line.Contains("receive-pack", StringComparison.Ordinal) ? GitAct.Write : GitAct.Read,
        _ => null,
    };

    // ---- Shared ----

    /// <summary>
    /// The judge asked for one act, its grant's use and its refusal each a coded line. <paramref name="identity"/> is the one serving
    /// the act, or null; one read only on its host refuses a Write.
    /// </summary>
    private static GitVerdict Judge(
        string home, GitAct act, string? key, Func<string, string?> variable, string? remote, string? host, string folder, GitIdentity? identity)
    {
        // A grant standing for this address lets the Write through to its updates, which it is taken for once, where it carries them (GitPushDoor).
        var granted = act == GitAct.Write && GitGrants.Read(home, variable(GitRouting.GrantVariable), key, DateTimeOffset.UtcNow) is not null;
        var (repository, workspace) = GitFolders.RepositoryOf(home, folder);
        var value = GitSessionAccess.Held(home, variable(GitLog.SessionVariable), repository, workspace).Of(GitLog.ClassName(act)).Value;
        var verdict = GitAccessRules.Judge(act, value, granted, identity is { ReadOnly: true } readOnly ? readOnly.Label : null);
        // Asked at the door while the act waits, where Daoris runs to ask: the person's yes lets this one act through.
        if (verdict.Ask) verdict = GitDoorAsks.Hold(home, repository, workspace, remote, host, GitLog.ClassName(act), act, key, variable);
        if (!verdict.Allowed) GitLog.Refused(home, variable, verdict.Code, act, remote, host);
        return verdict;
    }

    /// <summary>
    /// The remote's name git handed the helper, for the log: git hands the address itself for a push or fetch to an address with no
    /// remote, which is never logged, so such an act's remote is none.
    /// </summary>
    internal static string? RemoteName(string name) =>
        name.Length == 0 || name.Contains("://", StringComparison.Ordinal) || name.Contains('@') || name.Contains(':') ? null : name;

    private static int Refuse(TextWriter error, string line)
    {
        error.WriteLine(line);
        error.Flush();
        return Refused;
    }

    private static int Run(ProcessStartInfo info, TextWriter error, SshPush? pushes)
    {
        // A push's commands come from git through the door, so its judge reads them before the remote does; its other streams stay git's.
        if (pushes is not null) info.RedirectStandardInput = true;
        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (System.ComponentModel.Win32Exception failure)
        {
            return Refuse(error, $"daoris-git-ssh: could not start {Path.GetFileName(info.FileName)} ({failure.Message}).");
        }

        using (process)
        {
            string? refused = null;
            if (pushes is not null)
            {
                // git's side, on a thread of its own: git sends its commands only once the remote's advertisement reached it, and an ssh
                // that ends first (a key refused) must end the door rather than leave it reading git.
                _ = Task.Run(() =>
                {
                    var to = process.StandardInput.BaseStream;
                    try
                    {
                        if (GitPushUpdates.ReadCommands(pushes.Input) is { } commands)
                        {
                            if (pushes.Refusal(commands.Updates) is { } line)
                            {
                                Volatile.Write(ref refused, line);
                                Stop(process);
                                return;
                            }

                            to.Write(commands.Read);
                            to.Flush();
                            Relay(pushes.Input, to);
                        }
                    }
                    catch (IOException)
                    {
                        // The ssh went first; its exit says why.
                    }

                    try { to.Close(); } catch (IOException) { }
                });
            }

            process.WaitForExit();
            return Volatile.Read(ref refused) is { } said ? Refuse(error, said) : process.ExitCode;
        }
    }

    private static void Stop(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }

    /// <summary>Copy until <paramref name="from"/> ends, flushing each read on: either side may wait on what the last read held.</summary>
    private static void Relay(Stream from, Stream to, Action<byte[], int>? tap = null)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
            {
                tap?.Invoke(buffer, read);
                to.Write(buffer, 0, read);
                to.Flush();
            }
        }
        catch (IOException)
        {
            // The other side went; its process's exit says why.
        }
    }

    /// <summary>One line of git's conversation, with its line feed; null at its end.</summary>
    private static byte[]? ReadLine(Stream input)
    {
        var line = new MemoryStream();
        while (true)
        {
            var next = input.ReadByte();
            if (next < 0) return line.Length == 0 ? null : line.ToArray();
            line.WriteByte((byte)next);
            if (next == '\n') return line.ToArray();
        }
    }

    /// <summary>A program on <paramref name="path"/>, by the extensions Windows starts; null where none is there.</summary>
    internal static string? Find(string name, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat", ""] : [""];
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                try
                {
                    var file = Path.Combine(folder.Trim('"'), name + extension);
                    if (File.Exists(file)) return file;
                }
                catch (ArgumentException)
                {
                    // A malformed folder on PATH is passed over, as Windows passes it.
                }
            }
        }

        return null;
    }
}
