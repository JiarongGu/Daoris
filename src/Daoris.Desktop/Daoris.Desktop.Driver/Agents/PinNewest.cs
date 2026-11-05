namespace Daoris.Driver;

/// <summary>
/// The newest release a pin may move to (USE1a), asked one way for the window's update and the terminal's <c>agent update</c>: the maker's
/// own release channel where the agent declares one, else <c>npm view &lt;package&gt; version</c> through the tools' npm (TOOLS5, D121
/// §2.7), its answer streamed to the person as the pin's <c>npm install</c> is. Anything but one exact version is a refusal that says the
/// pin stays.
/// </summary>
internal static class PinNewest
{
    private static readonly string[] Npm = ["npm"];

    /// <param name="npm">The package manager asked: npm, unless a test holds a stand-in.</param>
    /// <param name="transport">How a channel is reached: the network, unless a test holds its own.</param>
    public static async Task<string> AskAsync(
        HarnessToolchain toolchain, string home, string pinned, IReadOnlyList<string>? npm, Action<string> write,
        CancellationToken ct, Action<HarnessRun>? started = null, HttpMessageHandler? transport = null)
    {
        if (toolchain.Channel is { Length: > 0 } channel)
        {
            // No process asks the pointer, so stopping it is cancelling the request.
            using var asking = CancellationTokenSource.CreateLinkedTokenSource(ct);
            started?.Invoke(new HarnessRun(asking));
            try
            {
                return await ReleaseChannels.LatestAsync(channel, asking.Token, transport).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                throw new DriverException($"{error.Message} The pin stays at {pinned}.");
            }
        }

        IReadOnlyList<string> command;
        try
        {
            command = HarnessActions.ThroughTools(home, npm ?? Npm);
        }
        catch (DriverException error)
        {
            throw new DriverException($"{error.Message} The pin stays at {pinned}.");
        }

        return await OnNpmAsync(toolchain, command, pinned, write, ct, started).ConfigureAwait(false);
    }

    private static async Task<string> OnNpmAsync(
        HarnessToolchain toolchain, IReadOnlyList<string> npm, string pinned, Action<string> write,
        CancellationToken ct, Action<HarnessRun>? started)
    {
        var package = toolchain.Package!;
        var stays = $"Nothing was fetched or pinned, and the pin stays at {pinned}.";
        var heard = new List<string>();

        int code;
        try
        {
            code = await HarnessActions.RunAsync(
                [.. npm, "view", package, "version"], toolchain, profileHome: null,
                line =>
                {
                    // RunAsync echoes the command first; that line is Daoris's, not npm's answer.
                    if (!line.StartsWith("$ ", StringComparison.Ordinal)) heard.Add(line);
                    write(line);
                },
                ct, started).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            throw new DriverException($"{error.Message} {stays}");
        }

        var said = heard.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        var shown = said is null ? "" : $" ({TextBounds.Clip(said, 160)})";
        if (code != 0)
        {
            throw new DriverException($"npm could not say which release of {package} is newest — it exited {code}{shown}. {stays}");
        }

        return HarnessActions.VersionFromNpm(heard)
            ?? throw new DriverException($"npm answered no single version of {package}{shown}. {stays}");
    }
}
