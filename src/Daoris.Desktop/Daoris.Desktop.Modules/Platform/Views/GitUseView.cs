using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris git use</c> as the card shows it (<c>GIT_USE</c>): which git identity a repository's remote, a workspace's host or this machine's
/// host signs in as, the person's own sign-ins, or the choice cleared. Refused before any card where a name is not registered here, no
/// such identity exists, the identity is for another host, or this machine is asked for anything but its own sign-ins; the person's own
/// sign-ins are a widening, and their card says so.
/// </summary>
public sealed class GitUseView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "GIT_USE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scope = PressPayload.Text(payload, "scope");
        var value = PressPayload.Text(payload, "value") is { Length: > 0 } given ? given : null;
        var host = PressPayload.Text(payload, "host");
        var own = string.Equals(value, GitIdentityUse.Own, StringComparison.OrdinalIgnoreCase);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        string where, words;

        switch (scope)
        {
            case "machine":
                if (host.Length == 0) return PressViewed.Refused("`--machine` names the host — `daoris git use --machine --host <host> own|--clear`.");
                if (value is not null && !own) return PressViewed.Refused("this machine chooses the person's own sign-ins or nothing for a host; a host's identity is its default.");
                where = $"{GitIdentityActs.HostOf(host)} on this machine";
                words = $"--machine --host {host}";
                break;
            case "workspace" or "repository":
            {
                var scoped = await PlaceScope.ReadAsync(world, payload, "a git identity", true, cancellationToken).ConfigureAwait(false);
                if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);
                if (scoped.Circle != (scope == "workspace")) return PressViewed.Refused("a git identity is chosen for a repository's remote or a workspace's host — name exactly one.");
                if (scoped.Circle)
                {
                    if (host.Length == 0) return PressViewed.Refused("a workspace chooses a git identity for a host: add `--host <host>`.");
                    where = $"{GitIdentityActs.HostOf(host)} in workspace `{scoped.Workspace}`";
                    words = $"--workspace {ShellWord.Of(scoped.Workspace!, ShellWord.Workspace)} --host {host}";
                }
                else
                {
                    var remote = PressPayload.Text(payload, "remote") is { Length: > 0 } named ? named : "origin";
                    where = $"{remote} of `{scoped.Repository}`";
                    words = $"{ShellWord.Of(scoped.Repository!, ShellWord.Name)}{(remote == "origin" ? "" : $" --remote {remote}")}";
                }

                break;
            }

            default:
                return PressViewed.Refused($"`{scope}` is not a scope a git identity is chosen for — a repository, a workspace or this machine.");
        }

        facts["where"] = where;
        if (value is null) return Shown("git-use-clear", facts, $"daoris git use {words} --clear");
        if (own) return Shown("git-use-own", facts, $"daoris git use {words} {GitIdentityUse.Own}");

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        var identities = machine.Facts.GitIdentities;
        if (identities.FirstOrDefault(each => string.Equals(each.Label, value, StringComparison.OrdinalIgnoreCase) || each.Id == value) is not { } identity)
        {
            return PressViewed.Refused($"there is no git identity `{value}` on this machine — one of {string.Join(", ", identities.Select(each => $"`{each.Label}`"))}.");
        }

        if (host.Length > 0 && !string.Equals(identity.Host, GitIdentityActs.HostOf(host), StringComparison.OrdinalIgnoreCase))
        {
            return PressViewed.Refused($"git identity `{identity.Label}` is for {identity.Host}, not {GitIdentityActs.HostOf(host)}.");
        }

        facts["identity"] = identity.Label;
        return Shown("git-use", facts, $"daoris git use {words} {ShellWord.Of(identity.Label, ShellWord.Workspace)}");
    }

    private static PressViewed Shown(string kind, Dictionary<string, string> facts, string command) =>
        PressViewed.Shown(new PressView(kind, facts, command));
}
