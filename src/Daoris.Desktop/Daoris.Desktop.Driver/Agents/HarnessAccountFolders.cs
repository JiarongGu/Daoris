namespace Daoris.Driver;

/// <summary>
/// Where a harness's toolchain and accounts live under the Daoris home, and which folders are accounts. <b>The directories that
/// exist are the accounts</b>: there is no register of them to disagree with the disk, the same reason the registry became the
/// authority rather than a view over a scan (D48 §3). Both places of the account homes are read (D172 point 5, HOMELAYOUT2).
/// </summary>
public static class HarnessAccountFolders
{
    /// <summary>Where a managed version of a harness lives. Daoris owns this location, binary and all.</summary>
    public static string ManagedHome(string home, string harness, string version) =>
        DaorisHome.Entry(home, HomeEntry.Toolchain, Name(harness, "agent name"), Name(version, "version"));

    /// <summary>
    /// The executable inside a managed install, or null when nothing is pinned or nothing is
    /// installed at the pin.
    /// </summary>
    /// <remarks>
    /// <b>Two layouts, the vendor's first</b> (AGT2b). A pin from a maker's own channel lands as
    /// <c>&lt;dir&gt;/bin/&lt;binary&gt;</c> — <c>.exe</c> on Windows — and is moved there only once it
    /// verified, so finding it is the proof. Then <b>npm's layout, for what ships only there</b>:
    /// <c>--prefix &lt;dir&gt;</c> puts the shims in <c>&lt;dir&gt;/node_modules/.bin</c>, with a
    /// <c>.cmd</c> beside the shell script on Windows. A pin npm made before AGT2b still resolves. The
    /// CLI's <c>managedBinary</c> is the twin.
    ///
    /// <para>🔴 A pin whose directory is not there answers null, and every caller falls back to
    /// <c>PATH</c> and <b>says so</b>. Silently running a different tool than the one the person
    /// pinned, and reporting success, is the failure this shape exists to prevent.</para>
    /// </remarks>
    public static string? ManagedBinary(
        string home, string harness, string? version, IReadOnlyList<string> binary)
    {
        if (string.IsNullOrWhiteSpace(version) || binary.Count == 0) return null;

        var managed = ManagedHome(home, harness, version);
        var vendor = Path.Combine(managed, "bin", OperatingSystem.IsWindows() ? binary[0] + ".exe" : binary[0]);
        if (File.Exists(vendor)) return vendor;

        var bin = Path.Combine(managed, "node_modules", ".bin");
        foreach (var candidate in new[]
                 {
                     Path.Combine(bin, binary[0] + ".cmd"),
                     Path.Combine(bin, binary[0]),
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Where a named profile's configuration home is. <b>Daoris owns this location and nothing
    /// inside it.</b>
    /// </summary>
    public static string ProfileHome(string home, string harness, string profile) =>
        DaorisHome.Entry(home, HomeEntry.AgentHomes, Name(harness, "agent name"), Name(profile, "profile name"));

    /// <summary>
    /// The home, the agent and the account a profile's folder is, read back from the layout <see cref="ProfileHome"/> makes, at
    /// either place of <see cref="HomeEntry.AgentHomes"/>: <c>&lt;home&gt;/harnesses/&lt;agent&gt;/&lt;account&gt;</c> where an
    /// account was made before HOMELAYOUT3, <c>&lt;Daoris folder&gt;/profiles/agents/&lt;agent&gt;/&lt;account&gt;</c> since. Null
    /// for a folder outside both.
    /// </summary>
    public static (string Home, string Agent, string Account)? AccountAt(string profileHome)
    {
        var account = new DirectoryInfo(Path.GetFullPath(profileHome));
        if (account.Parent is not { Parent: { } holder } agent) return null;
        var home = holder.Name == "harnesses" ? holder.Parent?.FullName
            : holder is { Name: "agents", Parent: { Name: "profiles", Parent: { } folder } } ? folder.FullName
            : null;
        return home is null ? null : (home, agent.Name, account.Name);
    }

    /// <summary>
    /// The profiles that exist for a harness, sorted. A lock's folder or a hidden one is no account
    /// (<see cref="NotAnAccount"/>, HELPSTUCK1): <see cref="Strays"/> names them.
    /// </summary>
    public static IReadOnlyList<string> Profiles(string home, string harness) =>
        [.. Folders(home, harness).Where(name => !NotAnAccount(name))];

    /// <summary>
    /// The folders beside a harness's accounts that are no account (HELPSTUCK1), sorted: never listed, chosen, walked or removed
    /// as one, and never touched here either, since one may hold a sign-in a person made in it while it was listed. The
    /// roster's report names them (<see cref="HarnessReport.Strays"/>), and Ask Daoris's room says them.
    /// </summary>
    public static IReadOnlyList<string> Strays(string home, string harness) =>
        [.. Folders(home, harness).Where(NotAnAccount)];

    /// <summary>
    /// Whether a folder beside the accounts is no account (HELPSTUCK1, twin rule 1): a lock's, its name ending <c>.lock</c> in
    /// any case, since a lockfile library locks a folder by making <c>&lt;folder&gt;.lock</c> beside it, and Claude Code locks
    /// its configuration home so; or a hidden one, its name starting <c>.</c>. Listed as accounts, two such lock folders on the
    /// owner's machine were walked to, signed in to and run in, and a lock of the account each is named after then met a home.
    /// The CLI's <c>notAnAccount</c> is the twin, and the two <c>profiles</c> tables hold the same rows.
    /// </summary>
    public static bool NotAnAccount(string name) =>
        name.StartsWith('.') || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every folder under a harness's account homes, in both places, by name, sorted.</summary>
    private static IEnumerable<string> Folders(string home, string harness) =>
        DaorisHome.Places(home, HomeEntry.AgentHomes, Name(harness, "agent name"))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>Every folder directly under the account homes, in both places, by name, sorted.</summary>
    internal static IEnumerable<string> AgentFolders(string home) =>
        DaorisHome.Places(home, HomeEntry.AgentHomes, "_")
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// The folders under the account homes, in both places, that no agent in <paramref name="agents"/> names (POOLSTRAY1), sorted:
    /// a folder a start whose path carried an unexpanded variable left, say. Never read as an agent, never touched.
    /// </summary>
    public static IReadOnlyList<string> StrayAgents(string home, IReadOnlyCollection<string> agents) =>
        [.. AgentFolders(home).Where(name => !NotAnAccount(name) && !agents.Contains(name, StringComparer.OrdinalIgnoreCase))];

    /// <summary>
    /// The agents Daoris knows on <paramref name="home"/> (POOLSTRAY1): the build's and every installed plugin's
    /// (<see cref="ProviderRegistry.Agents"/>), for a writer or reader handed no set of its own.
    /// </summary>
    public static IReadOnlyList<string> AgentsKnown(string home)
    {
        var built = ProviderRegistry.Built();
        return built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names)).Agents;
    }

    /// <summary>
    /// Each agent's accounts on this machine, for every agent Daoris knows that the wiring names or whose account homes exist
    /// (POOLSTRAY1): a folder under the account homes no known agent names is no agent, and its folders no accounts.
    /// </summary>
    /// <param name="agents">The agents Daoris knows (<see cref="ProviderRegistry.Agents"/>); the home's (<see cref="AgentsKnown"/>) where none is handed.</param>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AccountsOf(
        string home, HarnessSettings settings, IReadOnlyCollection<string>? agents = null)
    {
        var known = new HashSet<string>(agents ?? AgentsKnown(home), StringComparer.OrdinalIgnoreCase);
        var named = new HashSet<string>(AgentFolders(home), StringComparer.OrdinalIgnoreCase);
        foreach (var agent in AccountSet.AgentsOf(settings, new Dictionary<string, IReadOnlyList<string>>())) named.Add(agent);
        var accounts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in named.Where(agent => known.Contains(agent) && !NotAnAccount(agent)))
        {
            try
            {
                accounts[agent] = Profiles(home, agent);
            }
            catch (DriverException)
            {
                // A name no folder may have names no accounts.
            }
        }

        return accounts;
    }

    /// <summary>Every folder under a harness's account homes, in both places, by name, sorted: the stray ones counted for a log line.</summary>
    internal static int FolderCount(string home, string harness) => Folders(home, harness).Count();

    /// <summary>
    /// The id an account made by signing in, or by a key, gets (D66 §3, ACCT2): a fresh one, never reused
    /// (<see cref="AccountNames.NewId"/>), where it was the first free <c>account-N</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A neutral id, never who signed in.</b> It is needed before the sign-in starts, when nobody knows whose it is;
    /// and renaming the directory afterwards would move a home a harness may have keyed its credential to. What a person
    /// reads is the account's name, offered at the sign-in's end as who signed in and kept only where the person keeps it
    /// (<see cref="AccountNames"/>). Twin rule 5: the CLI's <c>login --new</c> and <c>key</c> draw the same way.
    /// </remarks>
    public static string NextAccount(string home, string harness) => AccountNames.NewId(home, harness);

    /// <summary>
    /// Delete an account: its directory, credentials included (D66 §3). True when there was one.
    /// </summary>
    /// <remarks>
    /// <para>The name is judged first (<see cref="ProfileHome"/>), so a name pointing anywhere else is
    /// refused before anything is touched — the tool's own configuration home is not under here and
    /// can never be named.</para>
    ///
    /// <para>🔴 <b>Read-only files are cleared first.</b> A harness that clones into its home leaves
    /// read-only git objects there, and a recursive delete refuses them on Windows — an account
    /// removed on screen would stay on disk because of an attribute. Links are not followed: a
    /// directory link inside is removed as a link, and what it points at is not Daoris's.</para>
    /// </remarks>
    public static bool RemoveProfile(string home, string harness, string profile)
    {
        var directory = ProfileHome(home, harness, profile);
        if (!Directory.Exists(directory))
        {
            // A key whose directory someone removed by hand still goes: nothing is left behind here. Nor its name (ACCT2).
            HarnessKeys.Remove(home, harness, profile);
            AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
            AccountNames.Forget(home, harness, profile);
            return false;
        }

        var walk = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };
        foreach (var file in Directory.EnumerateFiles(directory, "*", walk))
        {
            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(directory, recursive: true);
        // An account that was a key goes with its key (AGT3): a removed account keeps nothing here. Nor its cool-off
        // (TOOL4e): an account made later by `profile add` under its name starts afresh. Nor its name (ACCT2).
        HarnessKeys.Remove(home, harness, profile);
        AccountCooling.End(home, harness, profile, DateTimeOffset.UtcNow);
        AccountNames.Forget(home, harness, profile);
        return true;
    }

    /// <summary>
    /// A name that will become a directory. Refused rather than normalised: a name carrying a
    /// separator or a traversal is a request to own a location somewhere else, and quietly rewriting
    /// it would put a profile where nobody would look for it.
    /// </summary>
    private static string Name(string value, string what)
    {
        var trimmed = value?.Trim() ?? "";
        var bad = trimmed.Length == 0
            || trimmed is "." or ".."
            || trimmed.Contains('/') || trimmed.Contains('\\')
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

        return bad
            ? throw new DriverException(
                $"`{value}` is not a usable {what} — letters, digits, dashes and dots. It becomes a "
                + "directory Daoris owns the location of, so it may not point anywhere else.")
            : trimmed;
    }
}
