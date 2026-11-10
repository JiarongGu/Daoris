using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A widening's or an answer's press: sent to the running Daoris, whose window confirms it and
/// runs <paramref name="route"/> as the page presses it, its answer said by <paramref name="words"/>; where no window runs,
/// <paramref name="alone"/> does it here. The exit code is the verb's. The host's is <see cref="InstanceDoor.PressAsync"/>.
/// </summary>
public delegate Task<int> GitPress(InstanceRoute route, object payload, Func<Task<int>> alone, Func<JsonElement, int> words);

/// <summary>What <see cref="GitIdentityCommand"/> is handed from the world: the home, the registry's checkouts, and the running window.</summary>
/// <param name="Home">The home the verbs act on.</param>
/// <param name="Checkouts">The registry's checkouts (root, repository, workspace), asked only by <c>git use</c> of a repository.</param>
/// <param name="Press">A widening's press, through the running window where one runs.</param>
/// <param name="Environment">The variables, read for a session's marker.</param>
/// <param name="Token">Reads a pasted token from the terminal's standard input, asked only by <c>--token-stdin</c>; never an argument.</param>
public sealed record GitIdentityWorld(
    string Home,
    Func<Task<IReadOnlyList<(string Root, string Repository, string Workspace)>>> Checkouts,
    GitPress Press,
    Func<string, string?> Environment,
    Func<Task<string?>>? Token = null);

/// <summary>
/// The terminal's git identities: <c>git identity</c>, <c>git default</c> and
/// <c>git use</c>, each change one typed operation of <see cref="GitIdentityActs"/>, one text per verb, a refusal one line
/// (<c>git: {sentence} ({code})</c>). Exit codes are the family's: 0 done · 1 refused · 2 could not (the usage, a file that does not
/// read).
/// </summary>
/// <remarks>
/// <para><b>A person's own acts.</b> From a session's environment (<c>DAORIS_SESSION_ID</c>) every change is refused, as a person's press is
/// there: an agent cannot set up, point or trust anything here.</para>
/// <para><b>The widenings are confirmed in the window first</b>: an import (it reads a file of the person's), trusting
/// a host's key, choosing <c>own</c>, turning <c>read-only</c> off and removing an identity are pressed through the running Daoris as
/// Settings → Git presses them (<c>GIT_IDENTITY</c>, <c>GIT_USE</c>), so its window confirms each before it runs there; where no window
/// runs, the verb does it here. Nothing a widening touches is done in this process while a window runs.</para>
/// </remarks>
public static class GitIdentityCommand
{
    public const string Usage =
        """
        usage: daoris git identity list [--json]
               daoris git identity add-ssh <host> --label <label> [--name <name> --email <email>] [--import <file>] [--rsa]
               daoris git identity add-https <host> --label <label> --user <user> --token-stdin [--name <name> --email <email>]
               daoris git identity token <identity> [--kind api|https] --token-stdin | --clear
               daoris git identity public-key <identity>
               daoris git identity test <identity> [--trust-host <fingerprint>] [--repo <address>]
               daoris git identity rename <identity> <label>
               daoris git identity commits-as <identity> --name <name> --email <email> | --clear
               daoris git identity read-only <identity> on|off
               daoris git identity remove <identity>
               daoris git default <host> <identity>|--clear
               daoris git use <repository> [--remote <name>] <identity>|own|--clear
               daoris git use --workspace <workspace> --host <host> <identity>|own|--clear
               daoris git use --machine --host <host> own|--clear
        """;

    /// <summary>The family's first words this command answers (<c>git branches</c> is <see cref="GitBranchesCommand"/>'s).</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] is "identity" or "default" or "use";

    /// <summary>Whether the words change the home, so the verb holds its lease (<see cref="VerbLeases"/>): all but a read.</summary>
    public static bool Changes(IReadOnlyList<string> args) =>
        Answers(args) && !(args[0] == "identity" && args.Count > 1 && args[1] is "list" or "public-key") && !args.Any(arg => arg is "--help" or "-h");

    /// <summary>The line a press the window confirmed and ran is said with.</summary>
    public const string Confirmed = "git: confirmed in Daoris's window and done there.";

    /// <summary>Run the words; the exit code is the family's.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitIdentityWorld world, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        if (!Answers(args))
        {
            await error.WriteLineAsync($"git: `{(args.Count == 0 ? "" : args[0])}` is not a git verb.\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        IReadOnlyDictionary<string, string?> flags;
        IReadOnlyList<string> words;
        try
        {
            flags = Flags(args, out words);
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync(Line(refused)).ConfigureAwait(false);
            return 1;
        }

        async Task<int> Alone()
        {
            if (Changes(args) && world.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"git: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            return await ActAsync(words, flags, world, output, error, ct).ConfigureAwait(false);
        }
        if (!Widens(words, flags)) return await Alone().ConfigureAwait(false);
        if (Pressed(words, flags) is not { } press) return await Alone().ConfigureAwait(false);
        return await world.Press(press.Route, press.Payload, Alone, _ =>
        {
            output.WriteLine(Confirmed);
            return 0;
        }).ConfigureAwait(false);
    }

    /// <summary>A refusal in its one line, prefixed once.</summary>
    private static string Line(DriverException refused) => refused.Message.StartsWith("git: ", StringComparison.Ordinal) ? refused.Message : $"git: {refused.Message}";

    /// <summary>
    /// The route and payload a widening is pressed with, as Settings → Git presses it (<c>GIT_IDENTITY</c>, <c>GIT_USE</c>); null for words
    /// that name none, which are done here and refused there as they always were. An imported key's file is named by its full path.
    /// </summary>
    public static (InstanceRoute Route, object Payload)? Pressed(IReadOnlyList<string> words, IReadOnlyDictionary<string, string?> flags)
    {
        static void Put(Dictionary<string, object?> payload, string key, string? value)
        {
            if (value is not null) payload[key] = value;
        }

        switch (words)
        {
            case ["identity", "add-ssh", var host] when Flag(flags, "--import") is { } file && Flag(flags, "--label") is { } label:
            {
                var payload = new Dictionary<string, object?> { ["action"] = "add-ssh", ["host"] = host, ["label"] = label };
                Put(payload, "name", Flag(flags, "--name"));
                Put(payload, "email", Flag(flags, "--email"));
                payload["import"] = Path.GetFullPath(file);
                if (flags.ContainsKey("--rsa")) payload["rsa"] = true;
                return (PersonPresses.GitIdentity, payload);
            }

            case ["identity", "test", var name] when Flag(flags, "--trust-host") is { } fingerprint:
                return (PersonPresses.GitIdentity, new Dictionary<string, object?> { ["action"] = "test", ["identity"] = name, ["trust"] = fingerprint });
            case ["identity", "read-only", var name, "off"]:
                return (PersonPresses.GitIdentity, new Dictionary<string, object?> { ["action"] = "read-only", ["identity"] = name, ["on"] = false });
            case ["identity", "remove", var name]:
                return (PersonPresses.GitIdentity, new Dictionary<string, object?> { ["action"] = "remove", ["identity"] = name });
            case ["use", ..]:
            {
                var clear = flags.ContainsKey("--clear");
                var payload = new Dictionary<string, object?>();
                if (flags.ContainsKey("--machine")) payload["scope"] = "machine";
                else if (Flag(flags, "--workspace") is { } workspace) (payload["scope"], payload["workspace"]) = ("workspace", workspace);
                else if (words.Count == (clear ? 2 : 3)) (payload["scope"], payload["repository"], payload["remote"]) = ("repository", words[1], Flag(flags, "--remote") ?? "origin");
                else return null;
                if (payload["scope"] is "machine" or "workspace")
                {
                    if (Flag(flags, "--host") is not { } host) return null;
                    payload["host"] = host;
                }

                if (!clear)
                {
                    if (words.Count != (payload["scope"] is "repository" ? 3 : 2)) return null;
                    payload["value"] = words[^1];
                }

                return (PersonPresses.GitUse, payload);
            }

            default:
                return null;
        }
    }

    /// <summary>The words done in this process: a read, a change that is no widening, or a widening where no window runs.</summary>
    private static async Task<int> ActAsync(
        IReadOnlyList<string> words, IReadOnlyDictionary<string, string?> flags, GitIdentityWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        try
        {
            return await (words switch
            {
                ["identity", "list"] => List(world.Home, flags.ContainsKey("--json"), output),
                ["identity", "add-ssh", var host] => AddAsync(world.Home, host, flags, output, ct),
                ["identity", "add-https", var host] => AddHttpsAsync(world, host, flags, output, ct),
                ["identity", "public-key", var name] => Say(output, GitKeys.PublicKey(world.Home, GitIdentityActs.Find(world.Home, name).Id)
                    ?? throw new DriverException($"git: {name} has no public key on this machine (no-key)")),
                ["identity", "test", var name] => TestAsync(world.Home, name, Flag(flags, "--trust-host"), Flag(flags, "--repo"), output, ct),
                ["identity", "rename", var name, var label] => Said(output, $"git: {GitIdentityActs.Rename(world.Home, name, label).Label} renamed."),
                ["identity", "commits-as", var name] => Said(output, CommitsAs(world.Home, name, flags)),
                ["identity", "read-only", var name, "on" or "off"] => Said(output,
                    $"git: {GitIdentityActs.ReadOnly(world.Home, name, words[3] == "on").Label} is {(words[3] == "on" ? "read only on its host: Daoris never tries a write with it" : "no longer marked read only")}."),
                ["identity", "remove", var name] => Say(output, Removed(world.Home, name)),
                ["identity", "token", var name] => TokenVerbAsync(world, name, flags, output),
                ["default", var host] when flags.ContainsKey("--clear") => Said(output, $"git: {GitIdentityActs.HostOf(host)} has no default identity now.", GitIdentityActs.Default(world.Home, host, null)),
                ["default", var host, var name] => Said(output, $"git: {GitIdentityActs.Default(world.Home, host, name)!.Label} is {GitIdentityActs.HostOf(host)}'s default."),
                ["use", ..] => UseAsync(world, words, flags, output, ct),
                _ => Refuse(error, $"git: those words are not a git verb.\n{Usage}", 2),
            }).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync(Line(refused)).ConfigureAwait(false);
            return refused.Message.Contains("(identities-unread)", StringComparison.Ordinal) || refused.Message.Contains("(config-unread)", StringComparison.Ordinal) ? 2 : 1;
        }
    }

    /// <summary>
    /// Whether the words are pressed through the window: an import, a host's key trusted, <c>read-only</c> turned off, an
    /// identity removed (the pointers at it fall to a looser step), and every <c>use</c>, whose card names the choice
    /// (<c>own</c> widens what Daoris's git may use).
    /// </summary>
    public static bool Widens(IReadOnlyList<string> words, IReadOnlyDictionary<string, string?> flags) =>
        words switch
        {
            ["identity", "add-ssh", ..] => flags.ContainsKey("--import"),
            ["identity", "test", ..] => flags.ContainsKey("--trust-host"),
            ["identity", "read-only", _, "off"] => true,
            ["identity", "remove", ..] => true,
            ["use", ..] => true,
            _ => false,
        };

    /// <summary>The words split from their flags: <c>--json</c>, <c>--rsa</c>, <c>--clear</c> and <c>--machine</c> alone, every other one with its value.</summary>
    /// <exception cref="DriverException">A flag with no value.</exception>
    public static IReadOnlyDictionary<string, string?> Flags(IReadOnlyList<string> args, out IReadOnlyList<string> words)
    {
        var flags = new Dictionary<string, string?>(StringComparer.Ordinal);
        var kept = new List<string>();
        for (var at = 0; at < args.Count; at++)
        {
            var arg = args[at];
            if (arg is "--json" or "--rsa" or "--clear" or "--machine" or "--token-stdin") flags[arg] = null;
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (at + 1 >= args.Count || args[at + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new DriverException($"git: `{arg}` takes a value (usage)");
                flags[arg] = args[++at];
            }
            else kept.Add(arg);
        }

        words = kept;
        return flags;
    }

    private static string? Flag(IReadOnlyDictionary<string, string?> flags, string name) => flags.TryGetValue(name, out var value) ? value : null;

    private static async Task<int> AddAsync(string home, string host, IReadOnlyDictionary<string, string?> flags, TextWriter output, CancellationToken ct)
    {
        var label = Flag(flags, "--label") ?? throw new DriverException("git: name the identity with --label (label)");
        var identity = await GitIdentityActs.AddSshAsync(home, host, label, Flag(flags, "--name"), Flag(flags, "--email"), Flag(flags, "--import"),
            flags.ContainsKey("--rsa"), ct).ConfigureAwait(false);
        var page = GitHosts.Known.TryGetValue(identity.Host, out var known) ? known.KeyPage : null;
        await output.WriteLineAsync($"git: {identity.Label} added for {identity.Host}: SSH {identity.KeyType} {identity.Fingerprint}"
            + (identity.Default ? $", {identity.Host}'s default." : ".")).ConfigureAwait(false);
        await output.WriteLineAsync($"Add this public key where {identity.Host} takes SSH keys{(page is null ? "" : $" ({page})")}, then test it:").ConfigureAwait(false);
        await output.WriteLineAsync(GitKeys.PublicKey(home, identity.Id)).ConfigureAwait(false);
        await output.WriteLineAsync($"  daoris git identity test {identity.Id}").ConfigureAwait(false);
        return 0;
    }

    /// <summary>The token on standard input, read for a verb that sets one; never an argument.</summary>
    private static async Task<string> TokenAsync(GitIdentityWorld world)
    {
        var token = world.Token is null ? null : (await world.Token().ConfigureAwait(false))?.Trim();
        return string.IsNullOrEmpty(token) ? throw new DriverException("git: --token-stdin reads the token from standard input, and none came (token)") : token;
    }

    private static async Task<int> AddHttpsAsync(GitIdentityWorld world, string host, IReadOnlyDictionary<string, string?> flags, TextWriter output, CancellationToken ct)
    {
        var label = Flag(flags, "--label") ?? throw new DriverException("git: name the identity with --label (label)");
        if (!flags.ContainsKey("--token-stdin")) throw new DriverException("git: an HTTPS identity needs its token: pass --token-stdin and give it on standard input (token)");
        var identity = GitIdentityHttps.AddHttps(world.Home, host, label, Flag(flags, "--user"), Flag(flags, "--name"), Flag(flags, "--email"),
            await TokenAsync(world).ConfigureAwait(false));
        await output.WriteLineAsync($"git: {identity.Label} added for {identity.Host}: HTTPS as {identity.User}"
            + (identity.Default ? $", {identity.Host}'s default." : ".")).ConfigureAwait(false);
        await output.WriteLineAsync($"Its token is kept in Daoris's tokens folder, limited to your account. Test it: daoris git identity test {identity.Id} --repo https://{identity.Host}/<owner>/<repository>").ConfigureAwait(false);
        return 0;
    }

    /// <summary>Set (<c>--token-stdin</c>) or clear (<c>--clear</c>) an identity's token; the value is never said back.</summary>
    private static async Task<int> TokenVerbAsync(GitIdentityWorld world, string name, IReadOnlyDictionary<string, string?> flags, TextWriter output)
    {
        var kind = Flag(flags, "--kind") ?? GitTokens.Api;
        if (flags.ContainsKey("--clear"))
        {
            var cleared = GitIdentityTokens.Clear(world.Home, name, kind);
            return await Said(output, $"git: {cleared.Label}'s {kind} token is cleared.").ConfigureAwait(false);
        }

        if (!flags.ContainsKey("--token-stdin")) throw new DriverException("git: pass --token-stdin and give the token on standard input, or --clear (usage)");
        var identity = GitIdentityTokens.Set(world.Home, name, kind, await TokenAsync(world).ConfigureAwait(false));
        return await Said(output, $"git: {identity.Label}'s {kind} token is set.").ConfigureAwait(false);
    }

    private static async Task<int> TestAsync(string home, string name, string? trust, string? repo, TextWriter output, CancellationToken ct)
    {
        var tested = GitIdentityActs.Find(home, name);
        var answer = tested.Way == GitIdentity.Https
            ? await GitIdentityHttps.TestHttpsAsync(home, name, repo ?? throw new DriverException($"git: test an HTTPS identity against a repository on {tested.Host}: --repo <address> (repo)"), ct).ConfigureAwait(false)
            : await GitIdentityActs.TestAsync(home, name, trust, ct).ConfigureAwait(false);
        var identity = GitIdentityActs.Find(home, name);
        switch (answer.Result)
        {
            case "reached":
                await output.WriteLineAsync($"git: {identity.Label} reached {identity.Host}" + (answer.User is null ? "." : $" as {answer.User}.")).ConfigureAwait(false);
                return 0;
            case "host-unknown" or "host-changed":
                await output.WriteLineAsync(answer.Result == "host-unknown"
                    ? $"git: {identity.Host} is not yet trusted. Its keys:"
                    : $"git: {identity.Host}'s key changed since you trusted it. Do not trust it unless you know why. Its keys now:").ConfigureAwait(false);
                foreach (var key in answer.HostKeys)
                {
                    var mark = key.Published switch { true => "matches the key the host publishes", false => "NOT a key the host publishes", null => "the host publishes none Daoris knows" };
                    await output.WriteLineAsync($"  {key.Key.Split(' ')[0]} {key.Fingerprint}  {mark}").ConfigureAwait(false);
                }

                await output.WriteLineAsync($"Compare, then trust one: daoris git identity test {identity.Id} --trust-host <fingerprint>").ConfigureAwait(false);
                return 1;
            default:
                await output.WriteLineAsync($"git: {identity.Label}'s test: {answer.Result}" + (answer.Words is null ? "." : $" ({answer.Words})")).ConfigureAwait(false);
                return 1;
        }
    }

    private static string CommitsAs(string home, string name, IReadOnlyDictionary<string, string?> flags)
    {
        if (flags.ContainsKey("--clear")) return $"git: {GitIdentityActs.CommitsAs(home, name, null, null).Label} commits as git's own answer.";
        var identity = GitIdentityActs.CommitsAs(home, name, Flag(flags, "--name"), Flag(flags, "--email"));
        return $"git: {identity.Label} commits as {identity.CommitsName} <{identity.CommitsEmail}> where a repository names no author.";
    }

    private static string Removed(string home, string name)
    {
        var identity = GitIdentityActs.Find(home, name);
        var pointing = GitIdentityActs.Remove(home, name);
        var gone = identity.Way == GitIdentity.Https ? "its token deleted" : "its key and tokens deleted";
        return pointing.Count == 0
            ? $"git: {identity.Label} removed, and {gone}."
            : $"git: {identity.Label} removed, and {gone}; these fall to the next step: {string.Join(", ", pointing)}.";
    }

    private static async Task<int> UseAsync(GitIdentityWorld world, IReadOnlyList<string> words, IReadOnlyDictionary<string, string?> flags, TextWriter output, CancellationToken ct)
    {
        string? Value(int at) => flags.ContainsKey("--clear") ? null
            : words.Count > at ? words[at] : throw new DriverException("git: name an identity, own, or --clear (usage)");

        if (flags.ContainsKey("--machine"))
        {
            var host = Flag(flags, "--host") ?? throw new DriverException("git: name the host with --host (usage)");
            var value = Value(1);
            if (value is not (null or GitIdentityUse.Own)) throw new DriverException("git: this machine chooses own or --clear for a host; a host's identity is its default (usage)");
            GitIdentityActs.UseMachine(world.Home, host, value is not null);
            await output.WriteLineAsync(value is null ? $"git: this machine no longer uses your own git sign-ins for {GitIdentityActs.HostOf(host)}."
                : $"git: this machine uses your own git sign-ins for {GitIdentityActs.HostOf(host)} where nothing nearer chooses.").ConfigureAwait(false);
            return 0;
        }

        if (Flag(flags, "--workspace") is { } workspace)
        {
            var host = Flag(flags, "--host") ?? throw new DriverException("git: name the host with --host (usage)");
            GitIdentityActs.UseWorkspace(world.Home, workspace, host, Value(1));
            await output.WriteLineAsync($"git: workspace {workspace}'s {GitIdentityActs.HostOf(host)} set.").ConfigureAwait(false);
            return 0;
        }

        if (words.Count < 2) throw new DriverException("git: name the repository (usage)");
        var repository = words[1];
        var remote = Flag(flags, "--remote") ?? "origin";
        var checkouts = await world.Checkouts().ConfigureAwait(false);
        var checkout = checkouts.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        if (checkout.Root is null || !Directory.Exists(checkout.Root)) throw new DriverException($"git: {repository} has no checkout here (no-checkout)");
        GitFolders.Remember(world.Home, checkouts);

        var addresses = new Dictionary<string, string?>(StringComparer.Ordinal);
        var (code, said, _) = await WorkingTree.GitAsync(checkout.Root, ["remote"], ct).ConfigureAwait(false);
        if (code == 0)
        {
            foreach (var name in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                addresses[name] = await GitRemotes.AddressOf(checkout.Root, name, ct: ct).ConfigureAwait(false);
        }

        GitIdentityActs.UseRepository(world.Home, checkout.Repository, remote, Value(2), addresses);
        await output.WriteLineAsync($"git: {checkout.Repository}'s {remote} set.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// The identities as the terminal lists them: each its label, host, way, key, test and default, and the pointers; or the file's
    /// shape with <c>--json</c>, which holds no secret by construction.
    /// </summary>
    private static async Task<int> List(string home, bool json, TextWriter output)
    {
        var identities = GitIdentities.Read(home);
        if (GitIdentities.Problem(home) is { } problem) throw new DriverException($"git: {problem} (identities-unread)");
        var pointers = GitIdentityUse.Read(home);
        if (json)
        {
            var list = new JsonArray();
            foreach (var each in identities)
            {
                list.Add(new JsonObject
                {
                    ["id"] = each.Id, ["label"] = each.Label, ["host"] = each.Host, ["way"] = each.Way, ["user"] = each.User,
                    ["keyType"] = each.KeyType, ["fingerprint"] = each.Fingerprint, ["readOnly"] = each.ReadOnly, ["default"] = each.Default,
                    ["test"] = each.Test?.Result, ["pointedAtBy"] = new JsonArray([.. GitIdentityUse.PointingAt(pointers, each.Id).Select(scope => (JsonNode?)scope)]),
                });
            }

            await output.WriteAsync(JsonFile.Serialize(new JsonObject { ["identities"] = list })).ConfigureAwait(false);
            return 0;
        }

        if (identities.Count == 0)
        {
            await output.WriteLineAsync("git: no git identities yet. Daoris's git reaches a private repository only as an identity you set up: daoris git identity add-ssh <host> --label <label>").ConfigureAwait(false);
            return 0;
        }

        foreach (var each in identities)
        {
            await output.WriteLineAsync($"{each.Label}  {each.Host} · {each.Way.ToUpperInvariant()}{(each.KeyType is null ? "" : $" {each.KeyType} {each.Fingerprint}")}"
                + (each.Default ? $" · default for {each.Host}" : "") + (each.ReadOnly ? " · read only on the host" : "") + $"  ({each.Id})").ConfigureAwait(false);
            var test = each.Test is { } tested ? $"{tested.Result}{(tested.User is null ? "" : $" as {tested.User}")} {tested.At:yyyy-MM-dd HH:mm}Z" : "not tested";
            await output.WriteLineAsync($"  {test}" + (each.CommitsName is null ? "" : $" · commits as {each.CommitsName} <{each.CommitsEmail}>")
                + (GitIdentityUse.PointingAt(pointers, each.Id) is { Count: > 0 } pointing ? $" · {string.Join(", ", pointing)}" : "")).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> Say(TextWriter output, string line)
    {
        await output.WriteLineAsync(line).ConfigureAwait(false);
        return 0;
    }

    private static Task<int> Said(TextWriter output, string line, object? _ = null) => Say(output, line);

    private static async Task<int> Refuse(TextWriter error, string line, int code)
    {
        await error.WriteLineAsync(line).ConfigureAwait(false);
        return code;
    }
}
