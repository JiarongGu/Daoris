using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris git use</c> as the card shows it (<c>GIT_USE</c>): which git identity a repository's remote, a workspace's host or this machine's
/// host signs in as, the person's own sign-ins, or the choice cleared. Refused before any card where a name is not registered here, a
/// host is no host, a repository has no checkout or no such remote here, no such identity exists, the identity is for another host (the
/// remote's own, for a repository), or this machine is asked for anything but its own sign-ins; the person's own
/// sign-ins are a widening, and their card says so. A value is read as the route reads it: exactly, <c>own</c> in lower case alone.
/// </summary>
public sealed class GitUseView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "GIT_USE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scope = PressPayload.Text(payload, "scope");
        var value = Value(payload);
        var host = PressPayload.Text(payload, "host");
        var own = value == GitIdentityUse.Own;
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        string where, words;
        string? remoteHost = null;

        try
        {
            switch (scope)
            {
                case "machine":
                    if (host.Length == 0) return PressViewed.Refused("`--machine` names the host — `daoris git use --machine --host <host> own|--clear`.");
                    if (value is not null && !own) return PressViewed.Refused("this machine chooses the person's own sign-ins or nothing for a host; a host's identity is its default.");
                    where = $"{GitIdentityActs.HostOf(host)} on this machine";
                    words = $"--machine --host {ShellWord.Of(host, "<host>")}";
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
                        words = $"--workspace {ShellWord.Of(scoped.Workspace!, ShellWord.Workspace)} --host {ShellWord.Of(host, "<host>")}";
                    }
                    else
                    {
                        var remote = PressPayload.Text(payload, "remote") is { Length: > 0 } named ? named : "origin";
                        var remotes = await world.RemotesAsync(scoped.Repository!, cancellationToken).ConfigureAwait(false);
                        if (remotes is null) return PressViewed.Refused($"{scoped.Repository} has no checkout here.");
                        if (!remotes.TryGetValue(remote, out var address)) return PressViewed.Refused($"{scoped.Repository} has no remote {remote}.");
                        remoteHost = address is null ? null : GitIdentityActs.GitAddressHost(address);
                        where = $"{remote} of `{scoped.Repository}`";
                        words = $"{ShellWord.Of(scoped.Repository!, ShellWord.Name)}{(remote == "origin" ? "" : $" --remote {ShellWord.Of(remote, "<remote>")}")}";
                    }

                    break;
                }

                default:
                    return PressViewed.Refused($"`{scope}` is not a scope a git identity is chosen for — a repository, a workspace or this machine.");
            }
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
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

        var toward = host.Length > 0 ? GitIdentityActs.HostOf(host) : remoteHost;
        if (toward is not null && !string.Equals(identity.Host, toward, StringComparison.OrdinalIgnoreCase))
        {
            return PressViewed.Refused($"git identity `{identity.Label}` is for {identity.Host}, not {toward}.");
        }

        facts["identity"] = identity.Label;
        return Shown("git-use", facts, $"daoris git use {words} {ShellWord.Of(identity.Label, ShellWord.Workspace)}");
    }

    private static string? Value(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static PressViewed Shown(string kind, Dictionary<string, string> facts, string command) =>
        PressViewed.Shown(new PressView(kind, facts, command));
}
