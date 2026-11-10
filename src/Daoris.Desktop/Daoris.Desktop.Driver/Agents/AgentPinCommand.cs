using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris agent pin</c> and <c>update</c>: the managed toolchain from a
/// terminal, the words <c>daoris agent pin|update …</c> hands this program. The machine's own press is <c>HARNESS_ACTION</c>'s; a
/// workspace's pin, and a run with no Daoris, act on the terminal's word,
/// refused from a session's terminal, and hold no lease at its top. A pin from a maker's channel is staged under
/// its process's name while it downloads and published under the home's lease, the one folder move and the pin's write; a pin
/// through npm installs in place under the version's folder at this terminal, which nothing resolves until the pin, published the same
/// way, names it. A pinned <c>update</c> moves the pin to the newest release, published as a pin; an unpinned one runs the
/// agent's own updater at this terminal, which writes the machine and not the home. Its text is kept (<c>fixtures/agents-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's: 0 done, or nothing to move · 1 a session's terminal · 2 a refusal of the words, the channel or npm, an
/// updater or an install that did not end well, and the person's Ctrl+C.
/// </remarks>
internal static class AgentPinCommand
{
    private const string Prefix = "daoris";

    private const string NothingPinned =
        "  Nothing was pinned: a pin naming a version that is not there would run a different tool than the one you asked for.";

    /// <summary>
    /// The press of <c>pin</c> or <c>update</c>: where Daoris runs and the machine's own pin is meant, <c>HARNESS_ACTION</c> starts the
    /// action and says so; else (a workspace's pin, a named wiring home, no Daoris) the verb runs here, at this terminal.
    /// </summary>
    internal static AgentPress Press(IReadOnlyList<string> args, AgentsWorld world)
    {
        var verb = args[0];
        var (name, _, _, _) = AgentsCommand.Agent(args, verb, "<agent>", world, AgentsCommand.WiringPath(world));
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        var payload = new JsonObject { ["action"] = verb, ["door"] = name };
        if (verb == "pin")
        {
            payload["version"] = AgentsCommand.Operand(args, 2)
                ?? throw new AgentWords("`agent pin` needs <agent> <version> — e.g. `daoris agent pin claude-code`");
        }

        return new AgentPress(verb, workspace is null ? AgentRoutes.Harness : null, AgentsCommand.Isolated(args, world.Instance.Environment), payload,
            async ct => verb == "pin" ? await PinAsync(args, world, ct).ConfigureAwait(false) : await UpdateAsync(args, world, ct).ConfigureAwait(false),
            _ => null,
            done =>
            {
                if (done is int exit) return exit;
                world.Instance.Out.WriteLine($"{Prefix}: the {verb} of `{name}` started on this machine; its progress and its end are shown under Agents.");
                return 0;
            });
    }

    /// <summary>
    /// <c>pin &lt;agent&gt; &lt;version&gt; [--workspace &lt;name&gt;]</c>: that version installed where Daoris keeps it, then
    /// pinned, the machine's or one workspace's. From the maker's own channel, verified, where the agent declares one; else npm's own
    /// <c>--prefix</c>. A version the channel cannot verify is refused before a byte is fetched, and never handed to npm instead.
    /// </summary>
    internal static async Task<int> PinAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await AgentLongCommand.RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        var path = AgentsCommand.WiringPath(world);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "pin", "<agent>", world, path);
        if (toolchain.Program.Package is not { Length: > 0 } && toolchain.Program.Channel is not { Length: > 0 })
        {
            throw new AgentWords(
                $"`{name}` declares no package, so Daoris has no sanctioned way to fetch a version of it. Install it with its own tooling "
                + "and Daoris will find it on PATH.");
        }

        var version = AgentsCommand.Operand(args, 2)
            ?? throw new AgentWords("`agent pin` needs <agent> <version> — e.g. `daoris agent pin claude-code`");
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        if (toolchain.Program.Channel is { Length: > 0 } channel)
        {
            try
            {
                // Refused HERE, before a single byte is fetched.
                ReleaseChannels.RefuseVersion(channel, version);
            }
            catch (DriverException refused)
            {
                throw new AgentWords(refused.Message);
            }
        }

        return await InstallAndPinAsync(world, path, name, toolchain, version, workspace, ct).ConfigureAwait(false);
    }

    /// <summary>One version installed, from the channel or through npm, then pinned; the pin is written only once the version is there.</summary>
    private static async Task<int> InstallAndPinAsync(
        AgentsWorld world, string path, string name, AccountAgent toolchain, string version, string? workspace, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        ChannelStaged? staged = null;

        PinPlan plan;
        try
        {
            // The one decision both surfaces ask (PinInstall): a channel's version is refused unverifiable before a byte is fetched, a
            // package's npm is the tools', its prefix named so what it installs runs on the tools' node.
            plan = PinInstall.Plan(toolchain, home, name, version, world.Npm);
        }
        catch (DriverException refused) when (toolchain.Program.Channel is { Length: > 0 })
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            await error.WriteLineAsync(NothingPinned).ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }

        switch (plan.Way)
        {
            case PinWay.Present:
                // An install already in place is the proof it verified — the channel moves it there only after every check.
                Say($"{Prefix}: `{name}` {version} is already installed where Daoris keeps it — nothing was downloaded.");
                Say($"  {plan.Present}");
                break;
            case PinWay.Channel:
                Say($"{Prefix}: installing {toolchain.Product ?? name} {version} from {toolchain.Maker ?? "its maker"}'s own release channel, "
                    + "into a directory Daoris owns.");
                Say($"  {plan.Where}");
                try
                {
                    staged = await ReleaseChannels.StageAsync(plan.Channel!, plan.Where, version, line => Say($"  {line}"), ct, world.Transport)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — nothing was installed, and nothing was pinned.").ConfigureAwait(false);
                    return 2;
                }
                catch (DriverException refused)
                {
                    await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
                    await error.WriteLineAsync(NothingPinned).ConfigureAwait(false);
                    return 2;
                }

                break;
            default:
                Say($"{Prefix}: installing `{toolchain.Program.Package}@{version}` into a directory Daoris owns.");
                Say($"  {plan.Where}");
                var installed = await AgentLongCommand.TerminalAsync(world, plan.Command!, toolchain, "npm", ct).ConfigureAwait(false);
                if (installed != 0)
                {
                    if (!ct.IsCancellationRequested) Say(NothingPinned);
                    return installed;
                }

                break;
        }

        return await PublishAsync(world, path, name, version, workspace, staged, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The pin published under the home's lease, waited for and never refused: a channel's staging made the
    /// version by one move, then the pin written. A Ctrl+C while it waits removes the staging and writes nothing.
    /// </summary>
    private static async Task<int> PublishAsync(
        AgentsWorld world, string path, string name, string version, string? workspace, ChannelStaged? staged, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        var named = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        try
        {
            using (await HomeLease.ForPublicationAsync(home, "daoris agent pin",
                holder => error.WriteLine($"{Prefix}: waiting for {holder.Named} to let this home go; the pin is written then, and Ctrl+C stops."),
                ct, world.Waiting, world.Now).ConfigureAwait(false))
            {
                if (staged is not null)
                {
                    ReleaseChannels.Publish(staged, line => Say($"  {line}"));
                    staged = null;
                }

                HarnessSettings.Pin(path, home, name, version, named, AgentsCommand.Agents(world, path));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync(staged is null
                ? $"{Prefix}: stopped at your Ctrl+C — `{name}` {version} is installed where Daoris keeps it, and nothing was pinned."
                : $"{Prefix}: stopped at your Ctrl+C — the verified download was removed, and nothing was pinned.").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
            return 2;
        }
        catch (IOException held)
        {
            // The file's edit lock, held by another writer for the whole of its wait: nothing is written.
            await error.WriteLineAsync($"{Prefix}: {held.Message}").ConfigureAwait(false);
            return 2;
        }
        finally
        {
            if (staged is not null) ReleaseChannels.Discard(staged);
        }

        Say(workspace is not null
            ? $"{Prefix}: `{name}` runs at {version} for the `{workspace}` workspace on this machine."
            : $"{Prefix}: `{name}` runs at {version} on this machine.");
        Say("  Sessions spawn this binary rather than whatever is on PATH. `daoris agent unpin`");
        Say("  puts it back, and the version is on every session record either way.");
        return 0;
    }

    /// <summary>
    /// <c>update &lt;agent&gt; [--workspace &lt;name&gt;]</c>: update does what it says. A pinned agent with a package or a channel
    /// moves its pin to the newest release, since running its updater would change a different copy than the one sessions run; unpinned,
    /// the agent's own updater runs at this terminal.
    /// </summary>
    internal static async Task<int> UpdateAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await AgentLongCommand.RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "update", "<agent>", world, path);
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        var named = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        var settings = HarnessSettings.Load(path);
        if (settings.Problem is { } problem) throw new AgentWords(problem);
        var pins = named is null ? settings.Versions : settings.WorkspaceVersions.GetValueOrDefault(named);
        var pin = pins?.GetValueOrDefault(name)?.Trim() is { Length: > 0 } held ? held : null;

        if (pin is not null && (toolchain.Program.Package is { Length: > 0 } || toolchain.Program.Channel is { Length: > 0 }))
        {
            return await MovePinAsync(world, path, name, toolchain, pin, named, ct).ConfigureAwait(false);
        }

        if (named is not null)
        {
            throw new AgentWords(
                $"the `{named}` workspace pins no version of `{name}`, so there is no pin to move — `daoris agent pin {name} <version> "
                + $"--workspace {ShellWord.Of(named, "<workspace>")}` sets one.");
        }

        if (toolchain.Program.UpdateArguments is not { Count: > 0 })
        {
            throw new AgentWords($"`{name}` declares no updater — it updates itself, or its package manager does.");
        }

        Say($"{Prefix}: updating `{name}`. Never mid-session and never unasked: a tool that changed");
        Say("  under a running loop is a moving target nobody diffed.");
        return await AgentLongCommand.TerminalAsync(
            world, UpdateWords(toolchain.Program, AgentsCommand.ConfiguredCommand(HarnessSettings.HomeOf(path), name)), toolchain, $"`{name}`'s updater", ct)
            .ConfigureAwait(false);
    }

    internal static IReadOnlyList<string> UpdateWords(ToolSpec program, IReadOnlyList<string>? configured) =>
        [.. program.Command(configured), .. program.UpdateArguments!];

    /// <summary>
    /// Move a pin to the newest release: resolve the newest to one exact version, then pin it exactly as <c>pin</c> does. The pin
    /// never names a pointer, and never moves backwards.
    /// </summary>
    private static async Task<int> MovePinAsync(
        AgentsWorld world, string path, string name, AccountAgent toolchain, string pin, string? named, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        var scope = named is not null ? $" for the `{named}` workspace" : "";
        Say(toolchain.Program.Channel is { Length: > 0 }
            ? $"{Prefix}: `{name}` is pinned at {pin}{scope}. Asking {toolchain.Maker ?? "its maker"}'s own release channel which release is newest."
            : $"{Prefix}: `{name}` is pinned at {pin}{scope}. Asking npm which release of `{toolchain.Program.Package}` is newest.");
        string newest;
        try
        {
            // The window's one way (PinNewest): the npm handed, through the tools; its answer shown; the refusal in the window's words.
            newest = await PinNewest.AskAsync(toolchain, home, pin, world.Npm, line => Say($"  {line}"), ct, transport: world.Transport)
                .ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — nothing was fetched, and the pin stays at {pin}.").ConfigureAwait(false);
            return 2;
        }

        var move = PinMove.Decide(newest, pin, HarnessSettings.ManagedBinary(home, name, pin, toolchain.Program.Binary) is not null);
        if (move == PinMoveKind.Stays)
        {
            Say($"{Prefix}: `{name}` {pin} is already the newest release — nothing was fetched, and the pin stays.");
            return 0;
        }

        if (move == PinMoveKind.Behind)
        {
            Say($"{Prefix}: `{name}` is pinned at {pin}, newer than the newest release ({newest}) — nothing was");
            Say("  fetched, and the pin stays. Update never moves a pin backwards.");
            return 0;
        }

        Say(move == PinMoveKind.Reinstall
            ? $"{Prefix}: `{name}` {pin} is the newest release and is not installed here — installing it."
            : $"{Prefix}: `{name}` {pin} → {newest}");
        return await InstallAndPinAsync(world, path, name, toolchain, newest, named, ct).ConfigureAwait(false);
    }

}
