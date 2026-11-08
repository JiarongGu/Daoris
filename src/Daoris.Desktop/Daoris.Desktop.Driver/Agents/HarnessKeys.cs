using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The keys of accounts that are API keys — <c>keys.json</c> under the home, keyed by
/// agent and then account.
/// </summary>
/// <remarks>
/// <para><b>Beside the account, never inside it</b>: the account's directory is the tool's.
/// Encrypted at rest for the Windows user (<see cref="KeySeal"/>); tracked by nothing; no HTTP surface. A person
/// sees a key only as its <see cref="Handle"/>.</para>
/// </remarks>
public static class HarnessKeys
{
    /// <summary>The key's last four characters — every Anthropic key begins the same way.</summary>
    public static string Handle(string key) => key.Length > 4 ? $"…{key[^4..]}" : "…";

    /// <summary>
    /// Make an account that is this key: a fresh id (<see cref="AccountNames.NewId"/>), its directory, and the key
    /// kept beside it. A blank key is refused before anything is made.
    /// </summary>
    public static string Add(string home, string harness, string key)
    {
        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            throw new DriverException("that is not an API key — it is blank, or has spaces in it.");
        }

        var account = HarnessSettings.NextAccount(home, harness);

        // The key first, as the CLI's `addKeyAccount`: a `keys.json` that does not read refuses here, before an account
        // directory exists. Written over, it would lose every other account's key.
        var keys = Read(home);
        var read = Written(keys);
        if (!keys.TryGetValue(harness, out var held)) keys[harness] = held = new(StringComparer.Ordinal);
        held[account] = KeySeal.Sealed(trimmed);
        Write(home, read, keys);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, harness, account));

        // Marked as a sign-in is: a name a removed account had carries no word that it is signed out.
        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(home, harness, account), DateTimeOffset.UtcNow);
        HarnessSettings.Placed(HarnessSettings.ResolvePath(home), harness, account);

        // A new key into an account ends a cool-off its name still carried: the spend behind it never came from this key.
        // Never the reason a key is not kept.
        try
        {
            AccountCooling.End(home, harness, account, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is kept; the cool-off ends at its reset instead.
        }

        return account;
    }

    /// <summary>
    /// Write a new key into an account that is already a key: the repair for a key its provider refused. The
    /// account keeps its id, so its name, lists, defaults and order stand; its last reading and its cool-off, what
    /// marked it refused on disk, are cleared and it is marked signed in as <see cref="Add"/> does. The roster's in-memory
    /// refusal is the caller's (<c>Harnesses.Removed</c> and <c>LookedAgain</c>). The CLI's <c>replaceKeyAccount</c> is the twin.
    /// An account not on this machine, one that is not a key, and a blank or spaced key are refused before anything is written.
    /// </summary>
    public static string Replace(string home, string harness, string account, string key, bool keyOnly = false)
    {
        var named = HarnessSettings.Profiles(home, harness).FirstOrDefault(each => string.Equals(each, account, StringComparison.Ordinal))
            ?? throw new DriverException(
                $"`{harness}` has no account `{account}` on this machine — accounts that exist: "
                + (HarnessSettings.Profiles(home, harness) is { Count: > 0 } accounts ? string.Join(", ", accounts) : "(none)"));
        var keys = Read(home);
        var read = Written(keys);
        // An agent held by a key alone has no sign-in to be: an account of it without a key is one waiting for it.
        var held = keys.GetValueOrDefault(harness);
        if (held is null || !held.ContainsKey(named))
        {
            if (!keyOnly)
            {
                throw new DriverException(
                    $"`{named}` is not an API key account — it is a sign-in, so sign in again with `daoris agent login {harness}`.");
            }

            if (held is null) keys[harness] = held = new(StringComparer.Ordinal);
        }

        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            throw new DriverException("that is not an API key — it is blank, or has spaces in it.");
        }

        held[named] = KeySeal.Sealed(trimmed);
        Write(home, read, keys);

        // What marked it refused is cleared, none the reason the key is not kept.
        try
        {
            AccountReads.Forget(home, harness, named);
            AccountCooling.End(home, harness, named, DateTimeOffset.UtcNow);
            ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(home, harness, named), DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is kept; the next press reads the account again.
        }

        return named;
    }

    /// <summary>The key an account is, or null for a sign-in.</summary>
    public static string? Of(string home, string harness, string profile) =>
        Read(home).TryGetValue(harness, out var held) && held.TryGetValue(profile, out var kept) ? KeySeal.Open(kept) : null;

    /// <summary>
    /// Take an account's key away and keep the account: its last reading, which was of that key, is forgotten, so it reads no key
    /// until a new one is set. Nothing to take is an answer.
    /// </summary>
    public static void Withdraw(string home, string harness, string profile)
    {
        Remove(home, harness, profile);
        try
        {
            AccountReads.Forget(home, harness, profile);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is gone; the next press reads the account again.
        }
    }

    /// <summary>Forget an account's key. Nothing to forget is an answer.</summary>
    public static void Remove(string home, string harness, string profile)
    {
        var keys = Read(home);
        var read = Written(keys);
        if (!keys.TryGetValue(harness, out var held) || !held.Remove(profile)) return;
        if (held.Count == 0) keys.Remove(harness);
        Write(home, read, keys);
    }

    private static Dictionary<string, Dictionary<string, string>> Read(string home)
    {
        var keys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var path = DaorisHome.Entry(home, HomeEntry.Keys);
        if (!File.Exists(path)) return keys;

        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return keys;
            foreach (var agent in document.RootElement.EnumerateObject())
            {
                if (agent.Value.ValueKind != JsonValueKind.Object) continue;
                var held = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in agent.Value.EnumerateObject())
                {
                    // A value that is not text is no key, and is kept as written: read as one, it would throw.
                    if (entry.Value.ValueKind == JsonValueKind.String && entry.Value.GetString() is { Length: > 0 } key) held[entry.Name] = key;
                }

                keys[agent.Name] = held;
            }
        }
        catch (JsonException)
        {
            // An unreadable file holds no key Daoris can hand anyone; the account then asks for one.
        }

        return keys;
    }

    /// <summary>
    /// The edit from <paramref name="read"/>, the keys as read and written, made onto the file as it stands
    /// (<see cref="JsonKeeping.Save"/>): a key this build has no field for stays where it is, as written, at the top and inside an agent's
    /// keys, and a file that does not read is refused, never written over. Rebuilt from the keys it read, an edit over a file that did
    /// not read would write every other account's key away. The CLI's <c>writeKeys</c> does the same.
    /// </summary>
    private static void Write(string home, string read, Dictionary<string, Dictionary<string, string>> keys)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Keys);
        // Every key a write leaves is a sealed one: a key kept before the seal is sealed by the first write after it.
        var sealedKeys = keys.ToDictionary(
            agent => agent.Key,
            agent => agent.Value.ToDictionary(account => account.Key, account => KeySeal.Sealed(account.Value), StringComparer.Ordinal),
            StringComparer.Ordinal);
        JsonKeeping.Save(path, File.Exists(path), read, Written(sealedKeys));
    }

    /// <summary>The keys' own shape: agent then account, each sorted.</summary>
    private static string Written(Dictionary<string, Dictionary<string, string>> keys) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        foreach (var (agent, held) in keys.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(agent);
            foreach (var (account, key) in held.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteString(account, key);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    });
}
