using Shenora;
using Shenora.Core.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The person's door over the bridge (`PersonRoutes.cs`, PERSONDOOR1f and RPC1c, D156 points 2 and 4): the window reads what a
/// terminal asked it to confirm and answers one, and reads the person key the shell holds for its host, which none holds before
/// PERSONDOOR1g. Each route is the window's alone: the pipe's census refuses all three to a terminal (<c>PipeCensusTests</c>).
/// </summary>
public sealed class DriverModulePersonTests : DriverModuleBridge
{
    private (DriverModule Module, InstancePipe Pipe, ServiceProvider Services) Composed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var loop = Loop();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home);
        loop.Pipe = pipe;
        return (new DriverModule(Bus, loop), pipe, provider);
    }

    [Fact]
    public async Task The_window_reads_what_waits_and_confirms_or_refuses_one_once()
    {
        var (module, pipe, services) = Composed();
        using var composed = services;
        using var served = pipe;
        var archive = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_ARCHIVE",
            [new PipeConfirmationField("/ids/0", "string", "s1a2b3c4"), new PipeConfirmationField("/archived", "boolean", "true")],
            new PipeAsker("daoris-driver", 4242))!.Value;
        var delete = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_DELETE",
            [new PipeConfirmationField("/id", "string", "s5e6f7a8")], new PipeAsker(null, null))!.Value;

        var read = await AnswerAsync(module, "PERSON_CONFIRMATIONS");

        Assert.Equal(2, read.GetProperty("seq").GetInt64());
        var shown = read.GetProperty("waiting").EnumerateArray().ToList();
        Assert.Equal(2, shown.Count);
        Assert.Equal(archive.Shown.Id, shown[0].GetProperty("id").GetString());
        Assert.Equal("SESSION_ARCHIVE", shown[0].GetProperty("route").GetString());
        Assert.Equal("DAORIS.DRIVER", shown[0].GetProperty("module").GetString());
        Assert.Equal("/ids/0", shown[0].GetProperty("fields")[0].GetProperty("name").GetString());
        Assert.Equal("boolean", shown[0].GetProperty("fields")[1].GetProperty("type").GetString());
        Assert.Equal("daoris-driver", shown[0].GetProperty("asker").GetProperty("process").GetString());
        Assert.Equal(4242, shown[0].GetProperty("asker").GetProperty("pid").GetInt32());
        Assert.True(shown[0].TryGetProperty("expires", out _));

        // The person's answers: each once, and the terminal's press hears how it ended.
        var confirmed = await AnswerAsync(module, "PERSON_CONFIRM", new { id = archive.Shown.Id, confirm = true });
        Assert.True(confirmed.GetProperty("answered").GetBoolean());
        Assert.True(confirmed.GetProperty("confirmed").GetBoolean());
        Assert.Equal(PipeConfirmationEnd.Confirmed, await archive.Ended);
        var refused = await AnswerAsync(module, "PERSON_CONFIRM", new { id = delete.Shown.Id, confirm = false });
        Assert.False(refused.GetProperty("confirmed").GetBoolean());
        Assert.Equal(PipeConfirmationEnd.Refused, await delete.Ended);

        // One no longer waiting is said as data, by its code, for the card to word: answered elsewhere, expired or taken back.
        var again = await AnswerAsync(module, "PERSON_CONFIRM", new { id = archive.Shown.Id, confirm = true });
        Assert.False(again.GetProperty("answered").GetBoolean());
        Assert.Equal("gone", again.GetProperty("why").GetString());
        Assert.False(string.IsNullOrEmpty(again.GetProperty("message").GetString()));
        Assert.Empty((await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting").EnumerateArray());
    }

    [Fact]
    public async Task A_shell_with_no_pipe_reads_none_waiting_and_answers_none()
    {
        var module = Module();

        var read = await AnswerAsync(module, "PERSON_CONFIRMATIONS");
        Assert.Equal(0, read.GetProperty("seq").GetInt64());
        Assert.Empty(read.GetProperty("waiting").EnumerateArray());
        Assert.Equal("gone", (await AnswerAsync(module, "PERSON_CONFIRM", new { id = "nothing", confirm = true })).GetProperty("why").GetString());
    }

    /// <summary>
    /// The page asks the shell for the person key as it loads, and keeps it in memory (D156 point 2). The shell mints none until
    /// PERSONDOOR1g, so it answers none, and the page sends its writes as it did.
    /// </summary>
    [Fact]
    public async Task The_key_the_shell_holds_for_its_host_is_none_until_the_shell_mints_one()
    {
        var key = await AnswerAsync(Module(), "PERSON_KEY");

        Assert.Equal(System.Text.Json.JsonValueKind.Null, key.GetProperty("key").ValueKind);
    }
}
