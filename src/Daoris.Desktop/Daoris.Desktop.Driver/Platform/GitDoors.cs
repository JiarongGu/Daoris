using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Daoris's two doors at each git act (GITAUTH1b, D198 points 2 and 11; the git-auth design §4.2, §4.3 and §6.5): the ssh door, which
/// git starts as its ssh, and the remote helper, which git runs for every <c>daoris::</c> address the rewrite made. Each asks the judge
/// (<see cref="GitAccessRules"/>) for the act's class at its remote command, refuses with one line git prints, and otherwise starts what
/// git would have started without Daoris, carrying today's credentials: no git identity serves an act before GITAUTH1c and 1d.
/// </summary>
/// <remarks>
/// <para><b>Started only by git</b>, as <c>daoris-git-ssh.exe</c> and <c>git-remote-daoris.exe</c> in the install's <c>app/bin/</c>
/// (<c>Daoris.Desktop.GitDoor</c>, whose <c>Program</c> calls <see cref="Ssh"/> or <see cref="Helper"/> by its own file's name). Its
/// home is <see cref="GitRouting.HomeVariable"/>; where that is absent the door refuses, saying it was started outside Daoris's wiring.</para>
/// <para><b>Nothing prompts</b> (§4.6): the ssh door gives <c>-o BatchMode=yes</c> first, before any option of a command the
/// repository names, since OpenSSH takes the first value given; the helper's transport inherits the prompt variables
/// <see cref="GitWiring"/> handed.</para>
/// <para>The base library alone, as everything the door compiles in.</para>
/// </remarks>
public static class GitDoors
{
    /// <summary>The exit a refusal ends with: git's own for a remote that could not be read.</summary>
    public const int Refused = 128;

    private const string BatchMode = "BatchMode=yes";

    // ---- The ssh door (§4.2) ----

    /// <summary>
    /// What git handed the ssh door (§4.2's contract, OpenSSH's variant): its options, the <c>[user@]host</c>, and the remote command,
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
    /// its streams the door's own; the exit is that ssh's, or <see cref="Refused"/>.
    /// </summary>
    public static int Ssh(IReadOnlyList<string> arguments, Func<string, string?> variable, TextWriter error, string folder)
    {
        var (call, refusal) = ReadSsh(arguments);
        if (call is null) return Refuse(error, refusal!);
        if (variable(GitRouting.HomeVariable) is not { Length: > 0 } home)
            return Refuse(error, "daoris-git-ssh: was started outside Daoris's wiring, with no home to act for.");

        var act = GitAccessRules.ClassOf(call.Program, call.Operation)!.Value;
        var verdict = Judge(home, act, GitAddressKeys.Of(call.Host, call.Path), variable);
        if (!verdict.Allowed) return Refuse(error, verdict.Line!);

        var (file, prefix, problem) = SshCommand(folder, variable);
        if (problem is not null) return Refuse(error, problem);

        // Not the tools' environment (TOOLS5): the door is git's child, and its ssh takes git's environment as git would have handed it.
        var info = new ProcessStartInfo(file!) { UseShellExecute = false, WorkingDirectory = folder };
        foreach (var word in prefix) info.ArgumentList.Add(word);
        foreach (var word in call.Options) info.ArgumentList.Add(word);
        info.ArgumentList.Add(call.Host);
        info.ArgumentList.Add(call.Command);
        return Run(info, error);
    }

    /// <summary>
    /// The ssh git would have started without Daoris (§4.2 step 4, today's credentials): the repository's <c>core.sshCommand</c> at
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
        // Not the tools' environment (TOOLS5): git's own git, on git's PATH, asked one setting.
        var info = new ProcessStartInfo(git)
        {
            UseShellExecute = false,
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var word in new[] { "config", "--get", "core.sshCommand" }) info.ArgumentList.Add(word);
        try
        {
            using var process = Process.Start(info)!;
            process.StandardInput.Close();
            var said = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? said.Trim() : null;
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

    // ---- The remote helper (§4.3) ----

    /// <summary>
    /// The remote helper: start git's own <c>git-remote-https</c> (or <c>-http</c>) for the address, without Daoris's routing, and relay
    /// git's conversation with it, judging the act's class at its command: <c>list</c>, <c>fetch</c> and <c>stateless-connect</c> a read,
    /// <c>list for-push</c> and <c>push</c> a write. A refusal ends the conversation before the command reaches the transport.
    /// </summary>
    public static int Helper(
        IReadOnlyList<string> arguments, Func<string, string?> variable, Stream input, Stream output, TextWriter error,
        Func<IDictionary<string, string?>, IDictionary<string, string?>>? environment = null)
    {
        if (arguments.Count != 2) return Refuse(error, "git-remote-daoris: takes a remote and an address, as git passes them.");
        var address = arguments[1];
        var transport = address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "git-remote-https"
            : address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "git-remote-http"
            : null;
        if (transport is null) return Refuse(error, "git-remote-daoris: carries only http:// and https:// addresses.");
        if (variable(GitRouting.HomeVariable) is not { Length: > 0 } home)
            return Refuse(error, "git-remote-daoris: was started outside Daoris's wiring, with no home to act for.");
        if (Find(transport, variable("PATH")) is not { } file) return Refuse(error, $"git-remote-daoris: finds no {transport} on git's PATH.");

        // Not the tools' environment (TOOLS5): git's own transport, with git's environment less Daoris's routing (§4.3).
        var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
        info.ArgumentList.Add(arguments[0]);
        info.ArgumentList.Add(address);
        GitRouting.Unroute(info.Environment);
        environment?.Invoke(info.Environment);

        var key = GitAddressKeys.Of(address);
        using var process = Process.Start(info)!;
        string? refused = null;
        // git's side, on a thread of its own: the conversation ends when the transport's answers end, which may be before git closes
        // its side (an empty repository's clone), and a door still reading git then would hold git waiting on its answers.
        _ = Task.Run(() =>
        {
            var judged = new Dictionary<GitAct, GitVerdict>();
            var to = process.StandardInput.BaseStream;
            try
            {
                while (ReadLine(input) is { } line)
                {
                    var text = Encoding.UTF8.GetString(line).TrimEnd('\n', '\r');
                    if (ClassOfHelperCommand(text) is { } act)
                    {
                        if (!judged.TryGetValue(act, out var verdict)) judged[act] = verdict = Judge(home, act, key, variable);
                        if (!verdict.Allowed)
                        {
                            Volatile.Write(ref refused, verdict.Line);
                            Stop(process);
                            return;
                        }
                    }

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

        Relay(process.StandardOutput.BaseStream, output);
        process.WaitForExit();
        if (Volatile.Read(ref refused) is { } line) return Refuse(error, line);
        output.Flush();
        return process.ExitCode;
    }

    /// <summary>The class of a remote-helper command git sends (§4.3 step 4); null for one that moves nothing (<c>capabilities</c>, <c>option</c>).</summary>
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

    private static GitVerdict Judge(string home, GitAct act, string? key, Func<string, string?> variable)
    {
        if (act == GitAct.Read) return GitAccessRules.Judge(act, pushesHere: false, granted: false);
        var granted = GitGrants.Take(home, variable(GitRouting.GrantVariable), key, DateTimeOffset.UtcNow);
        return GitAccessRules.Judge(act, granted || GitAccessRules.PushesHere(home), granted);
    }

    private static int Refuse(TextWriter error, string line)
    {
        error.WriteLine(line);
        error.Flush();
        return Refused;
    }

    private static int Run(ProcessStartInfo info, TextWriter error)
    {
        try
        {
            using var process = Process.Start(info)!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception failure)
        {
            return Refuse(error, $"daoris-git-ssh: could not start {Path.GetFileName(info.FileName)} ({failure.Message}).");
        }
    }

    private static void Stop(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }

    /// <summary>Copy until <paramref name="from"/> ends, flushing each read on: either side may wait on what the last read held.</summary>
    private static void Relay(Stream from, Stream to)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
            {
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
