using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris agent update</c> and <c>pin</c> as the card shows them (<c>HARNESS_ACTION</c>'s <c>update</c> and <c>pin</c>): judged by what the
/// Agents screen offers and what the route refuses. An Update where the roster's <c>updates</c> names one and the agent is installed; a pin
/// where the agent declares a package or a release channel, of one exact release. A refusal is back to the terminal with no card; another
/// action of the route is shown by its values alone.
/// </summary>
public sealed class AgentUpdateView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "HARNESS_ACTION";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var action = PressPayload.Text(payload, "action");
        if (action is not ("update" or "pin")) return new PressViewed(null, null);

        var name = PressPayload.Text(payload, "door");
        var version = PressPayload.Text(payload, "version");
        var roster = await world.ReadAsync(DriverModule.Name, "HARNESSES", null, cancellationToken).ConfigureAwait(false);
        var door = PressPayload.Rows(roster, "harnesses").FirstOrDefault(row => PressPayload.Field(row, "door") == name);
        if (door.ValueKind != JsonValueKind.Object)
        {
            var names = string.Join(", ", PressPayload.Rows(roster, "harnesses").Select(row => PressPayload.Field(row, "door")));
            return PressViewed.Refused($"there is no agent `{name}` on this machine — one of {names}.");
        }

        var pinned = PressPayload.Field(door, "pinned");
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = name };
        var word = ShellWord.Of(name, "<agent>");
        if (action == "update")
        {
            if (!PressPayload.Flag(door, "present"))
            {
                return PressViewed.Refused($"`{name}` is not installed on this machine, so there is nothing to update — install it under "
                    + $"Agents → the agent's page → Ways in → Install, or with `daoris agent install {word}`.");
            }

            switch (PressPayload.Field(door, "updates"))
            {
                case "pin":
                    facts["pinned"] = pinned;
                    return Shown("agent-update-pin", facts, $"daoris agent update {word}");
                case "tool":
                    return Shown("agent-update-tool", facts, $"daoris agent update {word}");
                default:
                    return PressViewed.Refused(pinned.Length > 0
                        ? $"`{name}` offers no Update: it is pinned at {pinned} and declares no package or release channel to find a newer "
                          + $"version in. Pin another with `daoris agent pin {word} <version>`, or unpin it."
                        : $"`{name}` offers no Update: it declares no updater — it updates itself, or its package manager does.");
            }
        }

        if (version.Length == 0) return PressViewed.Refused("a pin names one exact release, such as 2.1.300.");
        if (!PressPayload.Flag(door, "pinnable"))
        {
            return PressViewed.Refused($"`{name}` declares no package or release channel, so Daoris has no sanctioned way to fetch a version of it. "
                + "Install it with its own tooling and Daoris will find it on PATH.");
        }

        if (!Exact.IsMatch(version))
        {
            return PressViewed.Refused($"`{version}` is not one exact release — a pin names one, like 1.2.3, never a pointer such as latest.");
        }

        facts["version"] = version;
        if (pinned.Length > 0 && pinned != version) facts["pinned"] = pinned;
        return Shown(facts.ContainsKey("pinned") ? "agent-pin-from" : "agent-pin", facts, $"daoris agent pin {word} {ShellWord.Of(version, "<version>")}");
    }

    private static PressViewed Shown(string kind, Dictionary<string, string> facts, string command) =>
        PressViewed.Shown(new PressView(kind, facts, command));

    private static readonly Regex Exact = new(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
}
