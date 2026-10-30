using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTLYNTAI1s-i, the real-process half of <see cref="NativePrompt"/>: a driven Claude Code session's prompt written on its input as
/// BOM-less UTF-8 in one call and the input closed, so a stand-in that reads its input to the end hears every byte of a Chinese prompt
/// and one with an astral character; a start that redirects no input, and the protocol door's, are left as they are; a harness that
/// ended before the write is its own answer, never the write's failure; and the same bytes arrive through the built launcher the
/// harness is started by (the <see cref="AgentStartsProcessTests"/> pattern). The harness is <c>node</c> running a script of this test's.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class NativePromptProcessTests : IDisposable
{
    private const string Prompt = "报告 😀 Serve the report\n端口 8080 \U0001F600";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-native-prompt-" + Guid.NewGuid().ToString("N")[..8]);

    public NativePromptProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionTarget Target(string root) => new(
        QuestId: "abc123", Title: "Fix the flaky gate", Body: "It fails one run in five.", Asker: "Platform", Repository: "Game",
        Root: root, ServiceUrl: "http://localhost:5177");

    /// <summary>A harness that reads its input to the end and says the bytes it read, as hex, then how many it read.</summary>
    private string Reader()
    {
        var script = Path.Combine(_root, "reader.mjs");
        File.WriteAllText(script, """
            const chunks = [];
            for await (const chunk of process.stdin) chunks.push(chunk);
            const raw = Buffer.concat(chunks);
            console.log(raw.toString('hex'));
            console.log(raw.length);
            """);
        return script;
    }

    private static string Hex(string text) => Convert.ToHexString(new UTF8Encoding(false).GetBytes(text)).ToLowerInvariant();

    /// <summary>The real adapter's resume start, built as the driver builds it, over <paramref name="script"/>.</summary>
    private ProcessStartInfo Resume(string script) =>
        new ClaudeCodeAdapter().PrepareResume(Target(_root), ["node", script], "0b5e7c1a", Prompt)!;

    [Fact]
    public async Task The_prompt_arrives_whole_as_bom_less_utf8_and_the_input_is_closed()
    {
        var info = Resume(Reader());
        Assert.DoesNotContain(Prompt, info.ArgumentList);
        using var process = Process.Start(info)!;
        var read = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        await NativePrompt.WriteAsync(process, new ClaudeCodeAdapter(), Prompt, CancellationToken.None);

        // The reader ends only once its input is closed: an unclosed input fails this by its bound.
        var said = (await read.WaitAsync(Poll.Patience)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(Hex(Prompt), said[0]);
        Assert.False(said[0].StartsWith("efbbbf", StringComparison.Ordinal));
        Assert.Equal(new UTF8Encoding(false).GetByteCount(Prompt).ToString(System.Globalization.CultureInfo.InvariantCulture), said[1]);
    }

    /// <summary>A start that redirects no input (the stub's) is left as it is: there is no input to write, and nothing is thrown.</summary>
    [Fact]
    public async Task A_start_that_redirects_no_input_is_left_alone()
    {
        var stub = AdapterSet.Built().Resolve("stub");
        // It never reads its input, which is the host's own: the point is that nothing is written to one that is not redirected.
        var script = Path.Combine(_root, "idle.mjs");
        File.WriteAllText(script, "console.log('ok');\n");
        var info = stub.Prepare(Target(_root), ["node", script]);
        Assert.False(info.RedirectStandardInput);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        var read = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        await NativePrompt.WriteAsync(process, stub, Prompt, CancellationToken.None);

        await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal("ok", (await read).Trim());
    }

    /// <summary>The protocol door writes its own frames on its input, so the prompt is never written there.</summary>
    [Fact]
    public async Task The_protocol_doors_input_is_never_written_a_prompt()
    {
        var acp = AdapterSet.Built().Resolve("acp-stub");
        var info = acp.Prepare(Target(_root), ["node", Reader()]);
        Assert.True(info.RedirectStandardInput);
        using var process = Process.Start(info)!;
        var read = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        await NativePrompt.WriteAsync(process, acp, Prompt, CancellationToken.None);
        process.StandardInput.Close();

        await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal("0", (await read).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1]);
    }

    /// <summary>A harness that ended before the write has said why in its own exit: the write's refusal is not raised in its place.</summary>
    [Fact]
    public async Task A_harness_that_ended_before_the_write_is_its_own_answer()
    {
        var script = Path.Combine(_root, "gone.mjs");
        File.WriteAllText(script, "process.exit(3);\n");
        using var process = Process.Start(Resume(script))!;
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(Poll.Patience);

        await NativePrompt.WriteAsync(process, new ClaudeCodeAdapter(), new string('x', 1 << 20), CancellationToken.None);

        Assert.Equal(3, process.ExitCode);
    }

    /// <summary>
    /// The same bytes through the launcher a harness is started by (TERMTRUST1a): the stdio it was handed is the harness's, so a
    /// non-ASCII prompt written on the launcher's input is read intact on the harness's.
    /// </summary>
    [Fact]
    public async Task The_prompt_passes_the_launcher_to_the_harness_byte_for_byte()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = AgentStartsProcessTests.Installed(_root);
        using var jobs = new AgentJobs(1_900_000_000 + Random.Shared.Next(100_000_000));

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(
                Resume(Reader()), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
        }

        using (process)
        {
            Assert.Equal(PieceLauncher.Program, Path.GetFileName(process.StartInfo.FileName));
            var read = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();

            await NativePrompt.WriteAsync(process, new ClaudeCodeAdapter(), Prompt, CancellationToken.None);

            var said = (await read.WaitAsync(Poll.Patience)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
            Assert.Equal(Hex(Prompt), said[0]);
        }
    }
}
