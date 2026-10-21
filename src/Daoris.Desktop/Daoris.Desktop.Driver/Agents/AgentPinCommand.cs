namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver agent pin</c> and <c>update</c> (PUSH1f2e, D194, the alone-path design §1.4): the managed toolchain (TOOL2, D57) from a
/// terminal, the words <c>daoris agent pin|update …</c> hands this program. No route takes either (§2): each acts on the terminal's word,
/// refused from a session's terminal (D194 point 5), and holds no lease at its top. A pin from a maker's channel (AGT2b) is staged under
/// its process's name while it downloads and published under the home's lease, the one folder move and the pin's write (§1.4); a pin
/// through npm installs in place under the version's folder at this terminal, which nothing resolves until the pin, published the same
/// way, names it. A pinned <c>update</c> moves the pin to the newest release (USE1a), published as a pin; an unpinned one runs the
/// agent's own updater at this terminal, which writes the machine and not the home. Today's text is kept (<c>fixtures/agents-text.json</c>).
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
    /// <c>pin &lt;agent&gt; &lt;version&gt; [--workspace &lt;name&gt;]</c> (TOOL2, D57): that version installed where Daoris keeps it, then
    /// pinned, the machine's or one workspace's. From the maker's own channel, verified, where the agent declares one; else npm's own
    /// <c>--prefix</c>. A version the channel cannot verify is refused before a byte is fetched, and never handed to npm instead.
    /// </summary>
    internal static async Task<int> PinAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await AgentLongCommand.RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        var path = AgentsCommand.WiringPath(world);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "pin", "<agent>", world, path);
        if (toolchain.Package is not { Length: > 0 } && toolchain.Channel is not { Length: > 0 })
        {
            throw new AgentWords(
                $"`{name}` declares no package, so Daoris has no sanctioned way to fetch a version of it. Install it with its own tooling "
                + "and Daoris will find it on PATH.");
        }

        var version = AgentsCommand.Operand(args, 2)
            ?? throw new AgentWords("`agent pin` needs <agent> <version> — e.g. `daoris agent pin claude-code`");
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        if (toolchain.Channel is { Length: > 0 } channel)
        {
            try
            {
                // Refused HERE, before a single byte is fetched (AGT2b).
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
        AgentsWorld world, string path, string name, HarnessToolchain toolchain, string version, string? workspace, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        var where = HarnessSettings.ManagedHome(home, name, version);
        ChannelStaged? staged = null;

        if (toolchain.Channel is { Length: > 0 } channel)
        {
            if (HarnessSettings.ManagedBinary(home, name, version, toolchain.Binary) is { } present)
            {
                // An install already in place is the proof it verified — the channel moves it there only after every check.
                Say($"{Prefix}: `{name}` {version} is already installed where Daoris keeps it — nothing was downloaded.");
                Say($"  {present}");
            }
            else
            {
                Say($"{Prefix}: installing {toolchain.Product ?? name} {version} from {toolchain.Maker ?? "its maker"}'s own release channel, "
                    + "into a directory Daoris owns.");
                Say($"  {where}");
                try
                {
                    staged = await ReleaseChannels.StageAsync(channel, where, version, line => Say($"  {line}"), ct, world.Transport)
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
            }
        }
        else
        {
            Say($"{Prefix}: installing `{toolchain.Package}@{version}` into a directory Daoris owns.");
            Say($"  {where}");
            IReadOnlyList<string> command;
            try
            {
                // The npm the tools resolve (TOOLS5, D121 §2.7): its prefix is named, so what it installs runs on the tools' node.
                command = HarnessActions.ThroughTools(home, [.. world.Npm, "install", "--prefix", where, $"{toolchain.Package}@{version}"]);
            }
            catch (DriverException refused)
            {
                throw new AgentWords(refused.Message);
            }

            var installed = await AgentLongCommand.TerminalAsync(world, command, toolchain, "npm", ct).ConfigureAwait(false);
            if (installed != 0)
            {
                if (!ct.IsCancellationRequested) Say(NothingPinned);
                return installed;
            }
        }

        return await PublishAsync(world, path, name, version, workspace, staged, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The pin published under the home's lease (the alone-path design §1.4), waited for and never refused: a channel's staging made the
    /// version by one move, then the pin written. A Ctrl+C while it waits removes the staging and writes nothing.
    /// </summary>
    private static async Task<int> PublishAsync(
        AgentsWorld world, string path, string name, string version, string? workspace, ChannelStaged? staged, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        var circle = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        try
        {
            using (await HomeLease.ForPublicationAsync(home, "daoris-driver agent pin",
                holder => error.WriteLine($"{Prefix}: waiting for {holder.Named} to let this home go; the pin is written then, and Ctrl+C stops."),
                ct, world.Waiting, world.Now).ConfigureAwait(false))
            {
                if (staged is not null)
                {
                    ReleaseChannels.Publish(staged, line => Say($"  {line}"));
                    staged = null;
                }

                HarnessSettings.Pin(path, home, name, version, circle);
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
            // The file's edit lock, held by another writer for the whole of its wait: nothing was written.
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
    /// <c>update &lt;agent&gt; [--workspace &lt;name&gt;]</c> (USE1a): update does what it says. A pinned agent with a package or a channel
    /// moves its pin to the newest release, since running its updater would change a different copy than the one sessions run; unpinned,
    /// the agent's own updater runs at this terminal, as it always did.
    /// </summary>
    internal static async Task<int> UpdateAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await AgentLongCommand.RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "update", "<agent>", world, path);
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        var circle = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        var settings = HarnessSettings.Load(path);
        if (settings.Problem is { } problem) throw new AgentWords(problem);
        var pins = circle is null ? settings.Versions : settings.WorkspaceVersions.GetValueOrDefault(circle);
        var pin = pins?.GetValueOrDefault(name)?.Trim() is { Length: > 0 } held ? held : null;

        if (pin is not null && (toolchain.Package is { Length: > 0 } || toolchain.Channel is { Length: > 0 }))
        {
            return await MovePinAsync(world, path, name, toolchain, pin, circle, ct).ConfigureAwait(false);
        }

        if (circle is not null)
        {
            throw new AgentWords(
                $"the `{circle}` workspace pins no version of `{name}`, so there is no pin to move — `daoris agent pin {name} <version> "
                + $"--workspace {ShellWord.Of(circle, "<workspace>")}` sets one.");
        }

        if (toolchain.UpdateArguments is not { Count: > 0 } update)
        {
            throw new AgentWords($"`{name}` declares no updater — it updates itself, or its package manager does.");
        }

        Say($"{Prefix}: updating `{name}`. Never mid-session and never unasked: a tool that changed");
        Say("  under a running loop is a moving target nobody diffed.");
        return await AgentLongCommand.TerminalAsync(world, [.. toolchain.Binary, .. update], toolchain, $"`{name}`'s updater", ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Move a pin to the newest release (USE1a): resolve the newest to one exact version, then pin it exactly as <c>pin</c> does. The pin
    /// never names a pointer, and never moves backwards.
    /// </summary>
    private static async Task<int> MovePinAsync(
        AgentsWorld world, string path, string name, HarnessToolchain toolchain, string pin, string? circle, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var error = world.Instance.Error;
        var home = HarnessSettings.HomeOf(path);
        var scope = circle is not null ? $" for the `{circle}` workspace" : "";
        string newest;
        try
        {
            if (toolchain.Channel is { Length: > 0 } channel)
            {
                Say($"{Prefix}: `{name}` is pinned at {pin}{scope}. Asking {toolchain.Maker ?? "its maker"}'s own release channel which "
                    + "release is newest.");
                try
                {
                    newest = await ReleaseChannels.LatestAsync(channel, ct, world.Transport).ConfigureAwait(false);
                }
                catch (DriverException refused)
                {
                    throw new AgentWords($"{refused.Message} The pin stays at {pin}.");
                }
            }
            else
            {
                Say($"{Prefix}: `{name}` is pinned at {pin}{scope}. Asking npm which release of `{toolchain.Package}` is newest.");
                newest = await NewestOnNpmAsync(world, home, toolchain, pin, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — nothing was fetched, and the pin stays at {pin}.").ConfigureAwait(false);
            return 2;
        }

        if (newest == pin && HarnessSettings.ManagedBinary(home, name, pin, toolchain.Binary) is not null)
        {
            Say($"{Prefix}: `{name}` {pin} is already the newest release — nothing was fetched, and the pin stays.");
            return 0;
        }

        if (HarnessActions.CompareReleases(newest, pin) < 0)
        {
            Say($"{Prefix}: `{name}` is pinned at {pin}, newer than the newest release ({newest}) — nothing was");
            Say("  fetched, and the pin stays. Update never moves a pin backwards.");
            return 0;
        }

        Say(newest == pin
            ? $"{Prefix}: `{name}` {pin} is the newest release and is not installed here — installing it."
            : $"{Prefix}: `{name}` {pin} → {newest}");
        return await InstallAndPinAsync(world, path, name, toolchain, newest, circle, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The newest version npm publishes of a package, asked with <c>npm view &lt;package&gt; version</c> through the tools' npm, as the pin
    /// it would make runs it. Anything but one version is a refusal that says so.
    /// </summary>
    private static async Task<string> NewestOnNpmAsync(
        AgentsWorld world, string home, HarnessToolchain toolchain, string pin, CancellationToken ct)
    {
        var package = toolchain.Package!;
        var stays = $"Nothing was fetched or pinned, and the pin stays at {pin}.";
        IReadOnlyList<string> command;
        try
        {
            command = HarnessActions.ThroughTools(home, [.. world.Npm, "view", package, "version"]);
        }
        catch (DriverException refused)
        {
            throw new AgentWords($"{refused.Message} {stays}");
        }

        world.Instance.Out.WriteLine($"  $ {string.Join(' ', command)}");
        var heard = new List<string>();
        int code;
        try
        {
            // Its answer is read, not shown: the line RunAsync echoes first is Daoris's, not npm's.
            code = await HarnessActions.RunAsync(command, toolchain, profileHome: null,
                line => { if (!line.StartsWith("$ ", StringComparison.Ordinal)) heard.Add(line); }, ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            throw new AgentWords($"`{command[0]}` could not be run — {refused.Message.TrimEnd('.')}. {stays}");
        }

        var said = heard.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        if (code != 0)
        {
            throw new AgentWords(
                $"npm could not say which release of `{package}` is newest — it exited {code}{(said is null ? "" : $" ({TextBounds.Clip(said, 160)})")}. {stays}");
        }

        return HarnessActions.VersionFromNpm(heard)
            ?? throw new AgentWords(
                $"npm answered no single version of `{package}`{(said is null ? "" : $" (it said `{TextBounds.Clip(said, 160)}`)")}. {stays}");
    }
}
