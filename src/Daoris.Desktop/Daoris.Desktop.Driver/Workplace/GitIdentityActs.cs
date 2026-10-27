using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A program's run, for the acts that start ssh's tools: its exit, its standard output and its standard error.</summary>
public sealed record GitToolRun(int Code, string Output, string Error);

/// <summary>A host's key as <c>ssh-keyscan</c> offered it: its line (<c>type base64</c>), its fingerprint, and whether it is the host's published one.</summary>
public sealed record GitHostKey(string Key, string Fingerprint, bool? Published);

/// <summary>A test's answer (§7.2 step 6): its result, the user the host named, the host's keys where it is not yet trusted, and the host's words.</summary>
public sealed record GitTestAnswer(string Result, string? User, IReadOnlyList<GitHostKey> HostKeys, string? Words);

/// <summary>
/// The typed operations on git identities and their pointers (GITAUTH1c, D198 points 1, 4 and 5; the git-auth design §3, §7.2 and
/// §8), each called by its terminal verb (D194 point 4): add an SSH identity by generating a key or importing the one file the
/// person names, test it (trusting a host's key only where the person names its fingerprint, §3.5), rename it, set what it commits
/// as, mark it read only on the host, remove it, make it its host's default, and point a repository's remote, a workspace's host or
/// this machine's host at it or at the person's own sign-ins.
/// </summary>
/// <remarks>
/// <para><b>Nothing of the person's is read</b> but the one key file an import names: ssh's tools run with Daoris's own configuration
/// and known hosts, never the person's <c>~/.ssh</c>, agent or credential store.</para>
/// <para>Refusals are <see cref="DriverException"/>s whose text is the verb's one line, its code in brackets.</para>
/// </remarks>
public static partial class GitIdentityActs
{
    /// <summary>How long a test's connection may take.</summary>
    public static readonly TimeSpan TestBound = TimeSpan.FromSeconds(30);

    /// <summary>Starts a program for these acts; a test hands a stand-in.</summary>
    public static Func<string, IReadOnlyList<string>, string, Task<GitToolRun>> Runner { get; set; } = RunAsync;

    // ---- The ssh tools (D121 §2.5's choice) ----

    /// <summary>
    /// The folder the OpenSSH tools an identity uses are in: beside the ssh the person chose in <c>tools.json</c>; else git's own,
    /// in its install's <c>usr/bin</c>; else Windows'; else the first <c>ssh</c> on <c>PATH</c>.
    /// </summary>
    /// <exception cref="DriverException">No OpenSSH is found, or the chosen ssh is not OpenSSH.</exception>
    public static string SshTools(string home)
    {
        var (chosen, problem) = GitDoors.OpenSsh(home, name => name == "PATH" ? Environment.GetEnvironmentVariable("PATH") : null);
        var read = Tools.Read(home);
        if (read.Git.TryGetValue("core.sshCommand", out _))
        {
            if (chosen is null) throw new DriverException($"git: {problem} (ssh-not-openssh)");
            return Path.GetDirectoryName(chosen)!;
        }

        if (Tools.Resolve(read, home, "git").File is { } git)
        {
            for (var folder = Path.GetDirectoryName(git); folder is not null; folder = Path.GetDirectoryName(folder))
            {
                if (File.Exists(Path.Combine(folder, "usr", "bin", Exe("ssh-keygen")))) return Path.Combine(folder, "usr", "bin");
                if (Path.GetPathRoot(folder) == folder) break;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH");
            if (File.Exists(Path.Combine(windows, Exe("ssh-keygen")))) return windows;
        }

        if (chosen is not null) return Path.GetDirectoryName(chosen)!;
        throw new DriverException("git: finds no OpenSSH on this machine: Git for Windows carries one (no-ssh)");
    }

    private static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    // ---- Add ----

    /// <summary>
    /// Add an SSH identity (§7.2 steps 1-3, §8 <c>git identity add-ssh</c>): its folder made private first, then its key generated
    /// (ed25519, or RSA 4096 where the host takes RSA alone or the person asks) or imported from the one file the person named.
    /// </summary>
    /// <exception cref="DriverException">A label in use, a host that does not read, an import refused, or ssh-keygen failed.</exception>
    public static async Task<GitIdentity> AddSshAsync(
        string home, string host, string label, string? name, string? email, string? import, bool rsa, CancellationToken ct)
    {
        var canonical = HostOf(host);
        label = LabelOf(label);
        var identities = GitIdentities.Read(home);
        if (GitIdentities.Problem(home) is { } problem) throw new DriverException($"git: {problem} (identities-unread)");
        if (identities.Any(each => string.Equals(each.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new DriverException($"git: an identity is already named {label} (label-taken)");
        var (commitsName, commitsEmail) = CommitsOf(name, email);

        // Read the person's file before anything is made, so a refused import leaves nothing behind.
        var imported = import is null ? null : GitKeys.ReadImport(import, label);
        var id = GitIdentities.NewId();
        var folder = GitKeys.MakeFolder(home, id);
        try
        {
            string publicLine;
            if (imported is not null)
            {
                await File.WriteAllBytesAsync(Path.Combine(folder, GitKeys.KeyFile), imported.Bytes, ct).ConfigureAwait(false);
                publicLine = imported.PublicLine;
                await File.WriteAllTextAsync(Path.Combine(folder, GitKeys.PublicFile), publicLine + "\n", ct).ConfigureAwait(false);
            }
            else
            {
                var keygen = Path.Combine(SshTools(home), Exe("ssh-keygen"));
                string[] type = rsa || GitHosts.RsaAlone(canonical) ? ["-t", "rsa", "-b", "4096"] : ["-t", "ed25519"];
                var run = await Runner(keygen, [.. type, "-q", "-N", "", "-C", label, "-f", Path.Combine(folder, GitKeys.KeyFile)], folder).ConfigureAwait(false);
                if (run.Code != 0) throw new DriverException($"git: ssh-keygen could not make the key ({LastLine(run.Error)}) (keygen-failed)");
                publicLine = GitKeys.PublicKey(home, id) ?? throw new DriverException("git: ssh-keygen wrote no public key (keygen-failed)");
            }

            var described = GitKeys.Describe(publicLine) ?? throw new DriverException("git: the public key does not read (keygen-failed)");
            GitSshFiles.Ensure(home);
            var identity = new GitIdentity(id, label, canonical, GitIdentity.Ssh, null, commitsName, commitsEmail, described.Type,
                described.Fingerprint, ReadOnly: false, Test: null, Default: !identities.Any(each => each.Host == canonical && each.Default));
            GitIdentities.Change(home, all => [.. all, identity]);
            return identity;
        }
        catch
        {
            GitKeys.Remove(home, id);
            throw;
        }
    }

    /// <summary>A host as the person typed it, canonical; refused where it is no host.</summary>
    public static string HostOf(string host)
    {
        var canonical = GitHosts.Canonical(host);
        if (!Regex.IsMatch(canonical, @"^[a-z0-9]([a-z0-9.\-]*[a-z0-9])?(:[0-9]{1,5})?\z", RegexOptions.CultureInvariant))
            throw new DriverException($"git: {host} is not a host: name it as an address does, github.com or git.example.com:2222 (host)");
        return canonical;
    }

    private static string LabelOf(string label)
    {
        var trimmed = label.Trim();
        if (trimmed.Length is 0 or > 80 || trimmed.Any(char.IsControl)) throw new DriverException("git: a label is one line of up to 80 characters (label)");
        return trimmed;
    }

    private static (string? Name, string? Email) CommitsOf(string? name, string? email)
    {
        if (name is null && email is null) return (null, null);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || !email.Contains('@') || (name + email).Any(char.IsControl))
            throw new DriverException("git: commits as needs a name and an email, both (commits-as)");
        return (name.Trim(), email.Trim());
    }

    // ---- Test (§7.2 step 6, §3.5) ----

    /// <summary>
    /// Test an SSH identity: where its host is not yet in Daoris's known hosts, answer <c>host-unknown</c> with the keys the host
    /// offers, each compared with the host's published fingerprint; where <paramref name="trust"/> names one of them, trust that key
    /// (the person's press) and go on. Then connect as the identity and read the host's greeting.
    /// </summary>
    /// <exception cref="DriverException">No such identity, not an SSH one, or a fingerprint the host does not offer.</exception>
    public static async Task<GitTestAnswer> TestAsync(string home, string name, string? trust, CancellationToken ct)
    {
        var identity = Find(home, name);
        if (identity.Way != GitIdentity.Ssh) throw new DriverException($"git: {identity.Label} is not an SSH identity (way)");
        GitSshFiles.Ensure(home);
        var tools = SshTools(home);
        var (host, port) = GitHosts.SshTarget(identity.Host);
        var known = GitSshFiles.KnownName(host, port);
        var held = GitSshFiles.Known(home, known);

        if (trust is not null)
        {
            var offered = await ScanAsync(tools, identity.Host, host, port).ConfigureAwait(false);
            var chosen = offered.FirstOrDefault(each => each.Fingerprint == trust.Trim())
                ?? throw new DriverException($"git: {host} offers no key with the fingerprint {trust.Trim()} (trust-mismatch)");
            GitSshFiles.Trust(home, known, chosen.Key);
            held = [chosen.Key];
        }

        if (held.Count == 0)
        {
            var offered = await ScanAsync(tools, identity.Host, host, port).ConfigureAwait(false);
            return Record(home, identity, new GitTestAnswer("host-unknown", null, offered, null));
        }

        List<string> arguments = [.. GitSshFiles.IdentityOptions(home, identity.Id), "-o", "ConnectTimeout=15", "-T"];
        if (port is not null) arguments.AddRange(["-p", port]);
        arguments.Add("git@" + host);
        var run = await Runner(Path.Combine(tools, Exe("ssh")), arguments, home).WaitAsync(TestBound, ct).ConfigureAwait(false);
        var answer = Greeting(run);
        if (answer.Result == "host-changed")
        {
            var offered = await ScanAsync(tools, identity.Host, host, port).ConfigureAwait(false);
            answer = answer with { HostKeys = offered };
        }

        GitSshFiles.Used(home, identity.Id);
        return Record(home, identity, answer);
    }

    /// <summary>
    /// A host's greeting read (§7.2 step 6): github.com and gitlab.com name the account; Azure DevOps says it takes no shell, which is
    /// reached; ssh's own refusals are classed as a git act's are (§5.1).
    /// </summary>
    public static GitTestAnswer Greeting(GitToolRun run)
    {
        var words = GitWords.Redact(run.Error + "\n" + run.Output) ?? "";
        var failure = GitFailures.Class(words, GitAct.Read);
        var result = failure switch
        {
            GitFailureClass.HostChanged => "host-changed",
            GitFailureClass.HostUnknown => "host-unknown",
            GitFailureClass.Refused => "refused",
            GitFailureClass.Unreachable => "unreachable",
            _ => null,
        };
        if (result is not null) return new GitTestAnswer(result, null, [], LastLine(words));

        var user = Named().Match(words) is { Success: true } match ? match.Groups["user"].Value : null;
        var reached = user is not null || words.Contains("Shell access is not supported", StringComparison.Ordinal)
            || words.Contains("successfully authenticated", StringComparison.OrdinalIgnoreCase) || run.Code is 0 or 1;
        return new GitTestAnswer(reached ? "reached" : "unreachable", user, [], LastLine(words));
    }

    [GeneratedRegex(@"(?:Hi (?<user>[A-Za-z0-9_.\-/]+)! You've successfully authenticated|Welcome to GitLab, @(?<user>[A-Za-z0-9_.\-]+)!)", RegexOptions.CultureInvariant)]
    private static partial Regex Named();

    private static GitTestAnswer Record(string home, GitIdentity identity, GitTestAnswer answer)
    {
        GitIdentities.Change(home, all => [.. all.Select(each => each.Id != identity.Id ? each : each with
        {
            Test = new GitIdentityTest(answer.Result, answer.User, DateTimeOffset.UtcNow),
            User = answer.User ?? each.User,
        })]);
        return answer;
    }

    /// <summary>The keys a host offers (<c>ssh-keyscan</c>), each with its fingerprint and whether it is the host's published one.</summary>
    private static async Task<IReadOnlyList<GitHostKey>> ScanAsync(string tools, string canonical, string host, string? port)
    {
        List<string> arguments = ["-T", "15"];
        if (port is not null) arguments.AddRange(["-p", port]);
        arguments.Add(host);
        var run = await Runner(Path.Combine(tools, Exe("ssh-keyscan")), arguments, tools).WaitAsync(TestBound).ConfigureAwait(false);
        return HostKeys(run.Output, Published(canonical));
    }

    /// <summary>The keys of <c>ssh-keyscan</c>'s output, each described and compared with <paramref name="published"/> (null: none published).</summary>
    public static IReadOnlyList<GitHostKey> HostKeys(string scanned, IReadOnlyList<string>? published)
    {
        var keys = new List<GitHostKey>();
        foreach (var line in scanned.Split('\n'))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0].StartsWith('#')) continue;
            if (GitKeys.Describe(parts[1] + " " + parts[2]) is not { } described && !parts[1].StartsWith("ssh-", StringComparison.Ordinal)) continue;
            string fingerprint;
            try
            {
                fingerprint = GitKeys.Fingerprint(Convert.FromBase64String(parts[2]));
            }
            catch (FormatException)
            {
                continue;
            }

            var key = parts[1] + " " + parts[2];
            if (keys.Any(each => each.Key == key)) continue;
            keys.Add(new GitHostKey(key, fingerprint, published is null ? null : published.Contains(fingerprint, StringComparer.Ordinal)));
        }

        return keys;
    }

    /// <summary>
    /// The fingerprints a public host publishes for its SSH keys (§3.5), from the tool resources' list (<c>resources.json</c>'s
    /// <c>hostKeys</c>), so it updates without a release; null for a host that publishes none Daoris knows.
    /// </summary>
    public static IReadOnlyList<string>? Published(string host) => GitHostKeys.Published(host);

    // ---- The rest of the family (§8) ----

    /// <summary>Rename an identity; its label stays unique.</summary>
    public static GitIdentity Rename(string home, string name, string label)
    {
        var identity = Find(home, name);
        label = LabelOf(label);
        if (GitIdentities.Read(home).Any(each => each.Id != identity.Id && string.Equals(each.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new DriverException($"git: an identity is already named {label} (label-taken)");
        return Replace(home, identity with { Label = label });
    }

    /// <summary>Set or clear what an identity commits as (§3.6).</summary>
    public static GitIdentity CommitsAs(string home, string name, string? author, string? email)
    {
        var identity = Find(home, name);
        var (commitsName, commitsEmail) = CommitsOf(author, email);
        return Replace(home, identity with { CommitsName = commitsName, CommitsEmail = commitsEmail });
    }

    /// <summary>Mark an identity read only on the host, or not (§6.3). Turning it off is a widening.</summary>
    public static GitIdentity ReadOnly(string home, string name, bool on) => Replace(home, Find(home, name) with { ReadOnly = on });

    /// <summary>
    /// Remove an identity (§7.1 <i>Remove…</i>): every pointer at it is cleared, each scope falling to the next step of §3.2, and its
    /// key folder deleted. Answers the scopes that pointed at it.
    /// </summary>
    public static IReadOnlyList<string> Remove(string home, string name)
    {
        var identity = Find(home, name);
        var pointing = GitIdentityUse.PointingAt(GitIdentityUse.Read(home), identity.Id);
        ChangeDriverConfig(home, root =>
        {
            var pointers = GitIdentityUse.Of(root);
            foreach (var (repository, remotes) in pointers.Repository)
            {
                foreach (var (remote, pointer) in remotes)
                {
                    if (pointer == identity.Id) GitIdentityUse.Set(root, GitIdentityUse.RepositoryKey, repository, remote, null);
                }
            }

            foreach (var (workspace, hosts) in pointers.Workspace)
            {
                foreach (var (host, pointer) in hosts)
                {
                    if (pointer == identity.Id) GitIdentityUse.Set(root, GitIdentityUse.WorkspaceKey, workspace, host, null);
                }
            }
        });
        GitIdentities.Change(home, all => [.. all.Where(each => each.Id != identity.Id)]);
        GitKeys.Remove(home, identity.Id);
        return pointing;
    }

    /// <summary>Make an identity its host's default, or clear the host's default (§8 <c>git default</c>).</summary>
    public static GitIdentity? Default(string home, string host, string? name)
    {
        var canonical = HostOf(host);
        var identity = name is null ? null : Find(home, name);
        if (identity is not null && identity.Host != canonical)
            throw new DriverException($"git: {identity.Label} is for {identity.Host}, not {canonical} (host-mismatch)");
        GitIdentities.Change(home, all => [.. all.Select(each => each.Host != canonical ? each : each with { Default = each.Id == identity?.Id })]);
        return identity;
    }

    /// <summary>
    /// Point a repository's remote at an identity, at <c>own</c>, or clear it (§8 <c>git use</c>). Two remotes of one repository at one
    /// effective address must point at one identity: a choice that would differ is refused, naming the other remote.
    /// </summary>
    /// <param name="addresses">The repository's remotes and their effective addresses, as <c>GitRemotes.AddressOf</c> reads them.</param>
    public static void UseRepository(string home, string repository, string remote, string? value, IReadOnlyDictionary<string, string?> addresses)
    {
        if (!addresses.TryGetValue(remote, out var address)) throw new DriverException($"git: {repository} has no remote {remote} (no-remote)");
        var pointer = PointerOf(home, value, address is null ? null : GitAddressHost(address));
        var held = GitIdentityUse.Read(home).Repository.TryGetValue(repository, out var map) ? map : new Dictionary<string, string>();
        var key = GitAddressKeys.Of(address);
        foreach (var (other, otherAddress) in addresses)
        {
            if (other == remote || key is null || !string.Equals(GitAddressKeys.Of(otherAddress), key, StringComparison.OrdinalIgnoreCase)) continue;
            var theirs = held.TryGetValue(other, out var set) ? set : null;
            if (theirs != pointer && theirs is not null)
                throw new DriverException($"git: {other} is at the same address and points elsewhere: point both at one identity (same-address)");
        }

        ChangeDriverConfig(home, root => GitIdentityUse.Set(root, GitIdentityUse.RepositoryKey, repository, remote, pointer));
    }

    /// <summary>Point a workspace's host at an identity, at <c>own</c>, or clear it.</summary>
    public static void UseWorkspace(string home, string workspace, string host, string? value)
    {
        var canonical = HostOf(host);
        var pointer = PointerOf(home, value, canonical);
        ChangeDriverConfig(home, root => GitIdentityUse.Set(root, GitIdentityUse.WorkspaceKey, workspace, canonical, pointer));
    }

    /// <summary>Choose this machine's own sign-ins for a host, or clear it: the machine level takes <c>own</c> alone.</summary>
    public static void UseMachine(string home, string host, bool own)
    {
        var canonical = HostOf(host);
        ChangeDriverConfig(home, root => GitIdentityUse.Set(root, GitIdentityUse.MachineKey, canonical, null, own ? GitIdentityUse.Own : null));
    }

    /// <summary>The canonical host of an address, or null for one with none (a local path).</summary>
    public static string? GitAddressHost(string address) =>
        GitAddressKeys.Of(address) is { } key ? GitHosts.Canonical(key[..key.IndexOf('/')]) : null;

    private static string? PointerOf(string home, string? value, string? host)
    {
        if (value is null) return null;
        if (value == GitIdentityUse.Own) return GitIdentityUse.Own;
        var identity = Find(home, value);
        if (host is not null && identity.Host != host)
            throw new DriverException($"git: {identity.Label} is for {identity.Host}, not {host} (host-mismatch)");
        return identity.Id;
    }

    /// <summary>The identity a person names by id or label.</summary>
    /// <exception cref="DriverException">None is named so.</exception>
    public static GitIdentity Find(string home, string name) =>
        GitIdentities.Named(GitIdentities.Read(home), name) ?? throw new DriverException($"git: no git identity is named {name} (no-identity)");

    private static GitIdentity Replace(string home, GitIdentity identity)
    {
        GitIdentities.Change(home, all => [.. all.Select(each => each.Id == identity.Id ? identity : each)]);
        return identity;
    }

    /// <summary>Change <c>driver.json</c> in place, keeping every key it has no part in, under the home's lease.</summary>
    private static void ChangeDriverConfig(string home, Action<JsonObject> change) => DriverConfigFile.Change(home, change);

    private static string? LastLine(string? words) =>
        words?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

    private static async Task<GitToolRun> RunAsync(string file, IReadOnlyList<string> arguments, string folder)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        Tools.Hand(info);
        using var process = await ProcessStarts.Shared.StartAsync(info, StartClass.Person, CancellationToken.None).ConfigureAwait(false)
            ?? throw new DriverException($"git: {Path.GetFileName(file)} did not start (no-ssh)");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new GitToolRun(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
}
