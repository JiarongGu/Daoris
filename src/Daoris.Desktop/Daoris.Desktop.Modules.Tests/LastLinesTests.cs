namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HOSTSTART1: a host that exits at start said why on its standard error, and the window showed only that it exited.
/// The supervisor now reads that stream for as long as the host runs and keeps its last few lines, bounded, for the
/// sentence the window shows. Over a reader of text here, so this is the fast half; <see cref="HostSupervisorTests"/>
/// holds the same over a real stand-in host.
/// </summary>
public sealed class LastLinesTests
{
    private static async Task<string?> SaidAsync(string printed)
    {
        var lines = LastLines.Drain(new StringReader(printed));
        await lines.Ended.WaitAsync(TimeSpan.FromSeconds(10));
        return lines.Said;
    }

    [Fact]
    public async Task A_sentence_printed_before_the_end_is_said_as_it_was_printed()
    {
        Assert.Equal(
            "DAORIS_MODE 'sharde' is not a mode: `local` or `shared`.",
            await SaidAsync("DAORIS_MODE 'sharde' is not a mode: `local` or `shared`.\r\n"));
    }

    [Fact]
    public async Task A_last_line_with_no_line_end_is_said_too()
    {
        Assert.Equal("first\nlast", await SaidAsync("first\nlast"));
    }

    [Fact]
    public async Task Nothing_printed_says_nothing()
    {
        Assert.Null(await SaidAsync(""));
        Assert.Null(await SaidAsync("\n  \r\n\t\n"));
    }

    /// <summary>A few lines, the newest: what was printed last is what came nearest the exit.</summary>
    [Fact]
    public async Task Only_the_newest_few_lines_are_kept()
    {
        var printed = string.Concat(Enumerable.Range(1, 40).Select(n => $"line {n}\n"));

        var said = await SaidAsync(printed);

        var kept = said!.Split('\n');
        Assert.Equal(LastLines.MostLines, kept.Length);
        Assert.Equal($"line {40 - LastLines.MostLines + 1}", kept[0]);
        Assert.Equal("line 40", kept[^1]);
    }

    /// <summary>A few hundred characters in all, the newest lines kept whole while they fit.</summary>
    [Fact]
    public async Task The_lines_kept_are_bounded_in_characters_newest_first()
    {
        var line = new string('x', LastLines.MostCharacters / 3);
        var printed = $"oldest {line}\nolder {line}\nnewer {line}\nnewest {line}\n";

        var said = await SaidAsync(printed);

        Assert.True(said!.Replace("\n", "").Length <= LastLines.MostCharacters, said);
        Assert.EndsWith($"newest {line}", said);
        Assert.Contains($"newer {line}", said);
        Assert.DoesNotContain("oldest", said);
    }

    /// <summary>A line longer than the bound keeps its start, where a sentence says what it is about, and says it was cut.</summary>
    [Fact]
    public async Task A_line_longer_than_the_bound_keeps_its_start_and_says_it_was_cut()
    {
        var said = await SaidAsync("Failed: " + new string('y', 4 * LastLines.MostCharacters) + "\n");

        Assert.Equal(LastLines.MostCharacters, said!.Length);
        Assert.StartsWith("Failed: yyy", said);
        Assert.EndsWith("…", said);
    }

    /// <summary>
    /// The runtime prints an exception that ended a process as its head, its inner exceptions, then its frames, and the
    /// frames come last: kept as printed, the newest lines would be frames and never the reason.
    /// </summary>
    [Fact]
    public async Task An_exception_s_frames_are_not_kept_and_its_reasons_are()
    {
        const string printed =
            "Unhandled exception. System.IO.IOException: Failed to bind to address http://127.0.0.1:5177: address already in use.\n"
            + " ---> Microsoft.AspNetCore.Connections.AddressInUseException: Only one usage of each socket address is normally permitted.\n"
            + "   at Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketTransportOptions.CreateDefaultBoundListenSocket(EndPoint endpoint)\n"
            + "   --- End of inner exception stack trace ---\n"
            + "   at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.AddressBinder.BindEndpointAsync(ListenOptions endpoint, AddressBindContext context, CancellationToken cancellationToken)\n"
            + "--- End of stack trace from previous location ---\n"
            + "   at Program.<Main>$(String[] args) in Program.cs:line 1788\n"
            + "   at Program.<Main>(String[] args)\n";

        var said = await SaidAsync(printed);

        Assert.Equal(
            "Unhandled exception. System.IO.IOException: Failed to bind to address http://127.0.0.1:5177: address already in use.\n"
            + " ---> Microsoft.AspNetCore.Connections.AddressInUseException: Only one usage of each socket address is normally permitted.",
            said);
    }

    /// <summary>
    /// Shown as printed, and never as markup: a tag or an ampersand is text. A control character prints nothing and is
    /// dropped, since a NUL in a window's label ends its text there.
    /// </summary>
    [Fact]
    public async Task A_line_is_kept_as_printed_markup_and_all_with_its_control_characters_dropped()
    {
        Assert.Equal(
            "<b>bold</b> & <a href=\"x\">a link</a>\tand on",
            await SaidAsync("<b>bold</b> & <a href=\"x\">a link</a>\tand\0 on\u0007\r\n"));
    }

    /// <summary>A host that prints without end, on one line, is read to its end in bounded memory and still said.</summary>
    [Fact]
    public async Task A_megabyte_on_one_line_is_read_to_its_end_and_said_bounded()
    {
        var said = await SaidAsync(new string('z', 1024 * 1024) + "\nthe end\n");

        Assert.EndsWith("the end", said);
        Assert.True(said!.Length <= LastLines.MostCharacters + 1, $"{said.Length} characters");
    }

    /// <summary>
    /// The host the supervisor starts has its standard error read, in UTF-8 as every stream the desktop redirects
    /// (the first deployment's 4c), and its output left alone.
    /// </summary>
    [Fact]
    public void The_host_it_starts_has_its_standard_error_read_as_utf8()
    {
        var start = HostSupervisor.StartInfo(new Daoris.Driver.HostLocation(
            Path.Combine("install", "app", "daoris-knowledge-http", "host"), Path.Combine("install", "app")));

        Assert.True(start.RedirectStandardError);
        Assert.Equal("utf-8", start.StandardErrorEncoding?.WebName);
        Assert.Empty(start.StandardErrorEncoding!.GetPreamble());
        Assert.False(start.RedirectStandardOutput);
    }
}
