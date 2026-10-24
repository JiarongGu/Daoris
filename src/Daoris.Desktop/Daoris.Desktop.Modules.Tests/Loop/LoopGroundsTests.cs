using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The loop's readers of what a press's files say (CONFIRMWAIT1a-i, the confirm-wait design §5.1, §10's plugin row): a plugin's
/// folder by every file under it, so one file changed while the manifest stayed is a moved ground; a file by its bytes; a folder or
/// file that is absent by <c>none</c>; and a held wait no read lists, a help proposal by its record. Every path is under the test's
/// own scratch home.
/// </summary>
public sealed class LoopGroundsTests : DriverModuleBridge
{
    private (LoopGrounds Grounds, ServiceProvider Services, InstancePipe Pipe) Composed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home);
        return (new LoopGrounds(Loop(), pipe), provider, pipe);
    }

    private static JsonElement Payload(object payload) => JsonSerializer.SerializeToElement(payload);

    [Fact]
    public async Task A_plugin_s_folder_is_read_by_every_file_under_it_so_its_code_changing_moves_it_and_its_manifest_alone_does_not_hold_it()
    {
        var (grounds, services, pipe) = Composed();
        using var _ = services;
        using var __ = pipe;
        var folder = Path.Combine(Home, "plugin-source");
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        File.WriteAllText(Path.Combine(folder, "daoris-plugin.json"), """{"id":"p1","version":"1.0.0"}""");
        File.WriteAllText(Path.Combine(folder, "bin", "run.cmd"), "echo one");
        var asked = Payload(new { folder });

        var then = await grounds.FingerprintAsync("plugin-folder", asked, CancellationToken.None);
        var again = await grounds.FingerprintAsync("plugin-folder", asked, CancellationToken.None);
        File.WriteAllText(Path.Combine(folder, "bin", "run.cmd"), "echo two");
        var now = await grounds.FingerprintAsync("plugin-folder", asked, CancellationToken.None);

        Assert.Equal(then, again);
        Assert.NotEqual(then, now);
        Assert.Equal("none", await grounds.FingerprintAsync("plugin-folder", Payload(new { folder = Path.Combine(Home, "nowhere") }), CancellationToken.None));
        // A folder named relatively is no folder the press copies.
        Assert.Equal("none", await grounds.FingerprintAsync("plugin-folder", Payload(new { folder = "plugin-source" }), CancellationToken.None));
    }

    [Fact]
    public async Task A_file_is_read_by_its_bytes_and_a_new_plugin_s_folder_is_none_until_it_is_made()
    {
        var (grounds, services, pipe) = Composed();
        using var _ = services;
        using var __ = pipe;
        var file = Path.Combine(Home, "git.exe");
        File.WriteAllText(file, "one");

        var then = await grounds.FingerprintAsync("file", Payload(new { file }), CancellationToken.None);
        File.WriteAllText(file, "two");

        Assert.NotEqual(then, await grounds.FingerprintAsync("file", Payload(new { file }), CancellationToken.None));
        Assert.Equal("none", await grounds.FingerprintAsync("plugin-new", Payload(new { folder = Home, id = "fresh" }), CancellationToken.None));
        Directory.CreateDirectory(Path.Combine(Home, "fresh"));
        File.WriteAllText(Path.Combine(Home, "fresh", "a.txt"), "a");
        Assert.NotEqual("none", await grounds.FingerprintAsync("plugin-new", Payload(new { folder = Home, id = "fresh" }), CancellationToken.None));
    }

    /// <summary>
    /// CONFIRMWAIT1 (the confirm-wait design §15.1 item 5): a terminal's attachments are read by their bytes, so a file replaced at the
    /// same path moves the press; a path that is no rooted file is bound as it was given.
    /// </summary>
    [Fact]
    public async Task A_terminal_s_attachments_are_read_by_their_bytes()
    {
        var (grounds, services, pipe) = Composed();
        using var _ = services;
        using var __ = pipe;
        var notes = Path.Combine(Home, "notes.md");
        File.WriteAllText(notes, "one");
        var asked = Payload(new { id = "s1", text = "see", said = true, paths = new[] { notes, "relative.md" } });

        var then = await grounds.FingerprintAsync("attachments", asked, CancellationToken.None);
        var again = await grounds.FingerprintAsync("attachments", asked, CancellationToken.None);
        File.WriteAllText(notes, "two");
        var now = await grounds.FingerprintAsync("attachments", asked, CancellationToken.None);

        Assert.Equal(then, again);
        Assert.NotEqual(then, now);
        Assert.NotEqual(
            await grounds.FingerprintAsync("attachments", Payload(new { paths = new[] { "a.md" } }), CancellationToken.None),
            await grounds.FingerprintAsync("attachments", Payload(new { paths = new[] { "b.md" } }), CancellationToken.None));
    }

    [Fact]
    public async Task A_wait_no_read_lists_is_read_from_its_record_and_none_where_it_has_none()
    {
        var (grounds, services, pipe) = Composed();
        using var _ = services;
        using var __ = pipe;

        Assert.Null(await grounds.HeldAsync("help", Payload(new { id = "nobody" }), CancellationToken.None));
        Assert.Null(await grounds.HeldAsync("trust", Payload(new { folder = Home, trustFile = Path.Combine(Home, "t.json") }), CancellationToken.None));
        Assert.Null(await grounds.HeldAsync("workflow-node", Payload(new { run = "r1", node = "n1" }), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => grounds.HeldAsync("nothing", null, CancellationToken.None));
    }
}
