using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A harness action while it runs (2026-09-23): the prompt it prints, the answer it is sent, the
/// stop. Written from the real login flow measured with no console — <c>claude auth login</c>
/// prints its sign-in link wrapped in a hyperlink escape and then <i>Paste code here if prompted
/// &gt;</i> with no newline, and waits on stdin. A pump that delivered whole lines only, from a
/// process whose stdin nobody held, was a login that could never finish.
/// </summary>
public sealed class HarnessRunTests
{
    private static readonly HarnessToolchain Node = new(Binary: ["node"], VersionArguments: ["--version"]);

    private static Task Until(Func<bool> condition) => Poll.Until(condition, within: TimeSpan.FromSeconds(15));

    [Fact]
    public async Task A_prompt_with_no_newline_is_delivered_and_the_answer_sent_reaches_the_process()
    {
        var lines = new List<string>();
        HarnessRun? run = null;
        const string script =
            "console.log('Opening browser to sign in…');"
            + "process.stdout.write('Paste code here if prompted >');"
            + "process.stdin.once('data', d => { console.log('got ' + d.toString().trim()); process.exit(0); });";

        var exit = HarnessActions.RunAsync(["node", "-e", script], Node, null, lines.Add, CancellationToken.None, r => run = r);

        // The prompt has no end, and it arrives anyway — as its own line, once the stream is quiet.
        await Until(() => lines.Contains("Paste code here if prompted >"));
        Assert.NotNull(run);
        // UTF-8 in, UTF-8 out — the ellipsis survives, which under the console's codepage it did not.
        Assert.Contains("Opening browser to sign in…", lines);

        run!.Send("abc-123");

        Assert.Equal(0, await exit);
        Assert.Contains("got abc-123", lines);
    }

    [Fact]
    public async Task Cancel_ends_a_process_that_would_wait_for_ever()
    {
        HarnessRun? run = null;
        var exit = HarnessActions.RunAsync(
            ["node", "-e", "setInterval(() => {}, 1000)"], Node, null, _ => { }, CancellationToken.None, r => run = r);

        await Until(() => run is not null);
        run!.Cancel();

        Assert.NotEqual(0, await exit);
        run.Cancel(); // ended is fine; ended twice is fine
    }

    /// <summary>The terminal's own instructions are not the harness's words.</summary>
    [Fact]
    public void A_hyperlink_escape_leaves_its_label_and_a_colour_leaves_its_text()
    {
        const string wrapped =
            "If the browser didn't open, visit: \x1b]8;;https://x.test/a?b=1\x1b\\https://x.test/a?b=1\x1b]8;;\x1b\\";

        Assert.Equal("If the browser didn't open, visit: https://x.test/a?b=1", HarnessActions.Clean(wrapped));
        Assert.Equal("ok", HarnessActions.Clean("\x1b[32mok\x1b[0m"));
        Assert.Equal("plain", HarnessActions.Clean("plain"));
    }

    /// <summary>
    /// 🔴 REV3: every declared installer is `npm …`, and on Windows `npm` is `npm.cmd`. A bare name with
    /// no shell appends only `.exe`, so *Install* — and a pin of anything that ships only on npm — never
    /// started from the desktop, and the page read the generic "something refused". The CLI's twin was
    /// fixed 2026-09-22; this is the driver's.
    /// </summary>
    [Fact]
    public async Task A_bare_command_that_is_a_windows_shim_starts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "daoris-shim-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "rev3-shim.cmd"), "@echo shim %*\r\n");
        var saved = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", folder + Path.PathSeparator + saved);
        try
        {
            var lines = new List<string>();
            var exit = await HarnessActions.RunAsync(
                ["rev3-shim", "install", "-g", "@acme/agent@1.2.3"], Node, null, lines.Add, CancellationToken.None);

            Assert.Equal(0, exit);
            Assert.Contains(lines, line => line.StartsWith("shim install -g @acme/agent@1.2.3", StringComparison.Ordinal));

            // An argument the shim's shell would reinterpret is refused, never passed through.
            var refused = await Assert.ThrowsAsync<DriverException>(() => HarnessActions.RunAsync(
                ["rev3-shim", "install", "@acme/agent@1&calc"], Node, null, _ => { }, CancellationToken.None));
            Assert.Contains("cannot be passed to a Windows command shim safely", refused.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", saved);
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A command that is nowhere is the driver's sentence, never the generic refusal the page falls back to.</summary>
    [Fact]
    public async Task A_command_that_is_nowhere_is_refused_in_a_sentence()
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() => HarnessActions.RunAsync(
            ["daoris-rev3-no-such-command"], Node, null, _ => { }, CancellationToken.None));
        Assert.Contains("daoris-rev3-no-such-command", refused.Message);
    }

    /// <summary>A blank line is a line — the shape of the output is the harness's.</summary>
    [Fact]
    public async Task Whole_lines_arrive_as_they_did_blank_ones_included()
    {
        var lines = new List<string>();
        var exit = HarnessActions.RunAsync(
            ["node", "-e", "console.log('one\\n\\nthree')"], Node, null, lines.Add, CancellationToken.None);

        Assert.Equal(0, await exit);
        Assert.Equal(["$ node -e console.log('one\\n\\nthree')", "one", "", "three"], lines);
    }
}
