using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>What an agent proposes to change about what agents may do (PERM2, D74).</summary>
/// <param name="Action">`add` a rule to a list, `remove` one, or switch a `default` on or off.</param>
/// <param name="Scope">Where it reaches: `machine`, `workspace` or `repository` (D72's three scopes).</param>
/// <param name="Name">The circle or repository, for those two scopes.</param>
/// <param name="List">For `add`: `allow`, `ask` or `deny`.</param>
/// <param name="Rule">For `add` and `remove`: the harness's own rule.</param>
/// <param name="Default">For `default`: the id it ships under.</param>
/// <param name="On">For `default`: whether it goes on.</param>
public sealed record RuleChange(
    string Action, string Scope, string? Name, string? List, string? Rule, string? Default, bool? On);

/// <summary>
/// Where an agent's proposals to change the rules are written (PERM2, D74): one file each, under the
/// Daoris home beside the rules they would change.
/// </summary>
/// <remarks>
/// <para><b>A file under the home, not a row in the store.</b> The rules are machine-local, so anything
/// that would change them is too: a file here is never fed to a remote, never served over HTTP, and
/// read by the three that settle it (the driver's tick, `daoris agent rules`, the screen) without any
/// of them asking the service. One file per proposal, so two sessions proposing at once never write
/// the same file.</para>
///
/// <para><b>The door decides nothing about narrowing.</b> Whether a change narrows depends on the rules
/// as they stand, which only the driver reads: it applies a narrowing at its next tick and holds a
/// widening for the person (D74). This checks the proposal is well formed and records who made it.</para>
///
/// <para><b>THE FILE is the contract</b>, as `permissions.json` is. The driver's `RuleProposals.cs` and
/// the CLI's `ruleproposals.ts` read and settle it and share no code with this; each side's tests hold
/// the same shape.</para>
/// </remarks>
/// <param name="home">The home proposals go under, or null when there is none — every proposal is then refused.</param>
public sealed class RuleProposalBox(string? home)
{
    /// <summary>The home the driver names on the connector it offers — the one its rules live in.</summary>
    public const string HomeVariable = "DAORIS_RULES_HOME";

    /// <summary>The folder under the home that holds one file per proposal.</summary>
    public const string Folder = "proposals";

    /// <summary>The same shape the driver's and the CLI's rules hold — judged again when a proposal is applied.</summary>
    private static readonly Regex Shape = new(@"^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?$", RegexOptions.CultureInvariant);

    public string? Home { get; } = home;

    /// <summary>
    /// The driver's named home first — it is where the rules this would change live — then the
    /// account's (D63), for a connector the driver did not start. Neither is none.
    /// </summary>
    public static RuleProposalBox FromEnvironment(Func<string, string?> environment)
    {
        var named = environment(HomeVariable);
        return new RuleProposalBox(string.IsNullOrWhiteSpace(named) ? DaorisHome.Resolve(environment) : named.Trim());
    }

    public static RuleProposalBox FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

    /// <summary>Why the change is not a proposal the rules could take, or null when it is well formed.</summary>
    public static string? Refusal(RuleChange change)
    {
        switch (change.Action)
        {
            case "add":
                if (change.List is not ("allow" or "ask" or "deny"))
                {
                    return $"a rule goes in `allow`, `ask` or `deny`, not `{change.List}`.";
                }

                if (RuleRefusal(change.Rule) is { } addRefused) return addRefused;
                break;
            case "remove":
                if (RuleRefusal(change.Rule) is { } removeRefused) return removeRefused;
                break;
            case "default":
                if (string.IsNullOrWhiteSpace(change.Default)) return "a default is named by the id it ships under.";
                if (change.On is null) return $"say whether the default `{change.Default}` goes on or off.";
                break;
            default:
                return $"a proposal is to `add`, `remove` or `default`, not `{change.Action}`.";
        }

        return change.Scope switch
        {
            "machine" => null,
            "workspace" or "repository" when string.IsNullOrWhiteSpace(change.Name) =>
                $"a {(change.Scope == "workspace" ? "workspace" : "repository")} scope names the {change.Scope} it reaches.",
            "workspace" or "repository" => null,
            _ => $"a rule reaches the `machine`, a `workspace` or a `repository`, not `{change.Scope}`.",
        };
    }

    /// <summary>
    /// Write one proposal, or refuse it — answered as the sentence an agent reads.
    /// </summary>
    /// <param name="session">The session proposing, when its connector names it.</param>
    /// <param name="ask">The ask it answers, when it is an intake.</param>
    /// <param name="folder">Where the session runs — a machine path, in a file that never leaves the machine.</param>
    public (string? Id, string Message) Propose(
        RuleChange change, string why, string? session, string? ask, string? folder, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(why))
        {
            return (null, "A proposal needs its reason: what you were doing, and what the rule would change. Nothing was proposed.");
        }

        if (Refusal(change) is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");
        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was proposed.");

        var id = Guid.NewGuid().ToString("N")[..8];
        var directory = Path.Combine(Home, Folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{id}.json");
        var beside = path + ".tmp";
        File.WriteAllText(beside, Json(id, change, why.Trim(), session, ask, folder, at), new UTF8Encoding(false));
        File.Move(beside, path);

        return (id,
            $"Proposed `#{id}`: {Describe(change)}. If it narrows what agents may do, the driver applies it at "
            + "its next look; if it widens it, it waits for the person's yes. Either way the record says this "
            + "session proposed it, and why.");
    }

    /// <summary>The change in a line — the rule is the harness's own words, quoted.</summary>
    public static string Describe(RuleChange change)
    {
        var where = change.Scope == "machine" ? "every session on this machine" : $"{change.Scope} `{change.Name}`";
        return change.Action switch
        {
            "add" => $"{change.List} `{change.Rule}` for {where}",
            "remove" => $"remove `{change.Rule}` from {where}",
            _ => $"switch the default `{change.Default}` {(change.On == true ? "on" : "off")}",
        };
    }

    private static string? RuleRefusal(string? rule) =>
        Shape.IsMatch(rule ?? "")
            ? null
            : $"`{rule}` is not a permission rule — one is a tool name with an optional specifier in "
              + "parentheses, like `Bash(npm run test:*)`, `Edit(/src/**)` or `mcp__daoris-knowledge__quest_list`.";

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    // Hand-rolled, for the reason every store here is: nothing may stop working under AOT.
    private static string Json(
        string id, RuleChange change, string why, string? session, string? ask, string? folder, DateTimeOffset at)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("proposed", at.ToString("O"));
            writer.WriteStartObject("by");
            Nullable(writer, "session", session);
            Nullable(writer, "ask", ask);
            Nullable(writer, "folder", folder);
            writer.WriteEndObject();
            writer.WriteStartObject("change");
            writer.WriteString("action", change.Action);
            writer.WriteString("scope", change.Scope);
            Nullable(writer, "name", change.Scope == "machine" ? null : change.Name?.Trim());
            Nullable(writer, "list", change.Action == "add" ? change.List : null);
            Nullable(writer, "rule", change.Action == "default" ? null : change.Rule?.Trim());
            Nullable(writer, "default", change.Action == "default" ? change.Default?.Trim() : null);
            if (change.Action == "default" && change.On is { } on) writer.WriteBoolean("on", on);
            else writer.WriteNull("on");
            writer.WriteEndObject();
            writer.WriteString("why", why);
            writer.WriteString("state", "proposed");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static void Nullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }
}
