using System.IO.Compression;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A managed version, downloaded, verified, unpacked and laid out (TOOLS4, D121; the tools design §3.6, §3.7, §5):
/// the driver's half of a TWIN with the CLI's <c>toolinstall.ts</c>, whose <c>toolinstall.test.ts</c> holds the same
/// tables, row for row and in the same order. They share no code; a row changed here is changed there, in the same
/// commit.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The CLI reads these theories.</b> <c>toolinstall.test.ts</c>'s <i>the driver's tables are these
/// tables</i> parses each <c>[InlineData]</c> row here and holds it to its own table, cell for cell and in order, so
/// a row changed on one side alone fails <c>npm run verify</c>. Keep each row on one line, its cells literals.</para>
/// <para>Every archive is built in the test from a row's words (<see cref="Archive"/>, the CLI's
/// <c>_archives.ts</c>), and nothing here reaches a network: a download is served by a stand-in that answers as a
/// host would.</para>
/// </remarks>
public sealed class ToolInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-toolinstall-" + Guid.NewGuid().ToString("N")[..8]);

    public ToolInstallTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ── An archive's refusals (§3.6). The CLI's `an archive unpacks, or is refused by its check…` ──────────────

    [Theory]
    [InlineData("a zip that unpacks", "zip", "bin/gh.exe=gh; bin/README=read me; docs/", "", "bin/gh.exe", null)]
    [InlineData("a zip of stored entries", "zip", "bin/gh.exe=gh!stored; docs/", "", "bin/gh.exe", null)]
    [InlineData("a zip written as zip64", "zip", "bin/gh.exe=gh; docs/", "zip64", "bin/gh.exe", null)]
    [InlineData("a zip name from the root", "zip", "bin/gh.exe=gh; /gh.exe=x", "", "bin/gh.exe", "absolute")]
    [InlineData("a zip name on a drive", "zip", "C:/gh.exe=x", "", "bin/gh.exe", "absolute")]
    [InlineData("a zip name that climbs out", "zip", "bin/gh.exe=gh; ../gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip name that climbs out partway", "zip", "bin/../../gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip name that climbs out by a backslash", "zip", "..\\gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip stream name", "zip", "bin/gh.exe:hidden=x", "", "bin/gh.exe", "stream")]
    [InlineData("a zip symbolic link", "zip", "bin/gh.exe=/usr/bin/gh!symlink", "", "bin/gh.exe", "link")]
    [InlineData("a zip entry encrypted", "zip", "bin/gh.exe=gh!encrypted", "", "bin/gh.exe", "encrypted")]
    [InlineData("a zip method other than stored or deflate", "zip", "bin/gh.exe=gh!method12", "", "bin/gh.exe", "method")]
    [InlineData("a zip entry that fails its checksum", "zip", "bin/gh.exe=gh!badcrc", "", "bin/gh.exe", "checksum")]
    [InlineData("a zip with no end record", "zip", "bin/gh.exe=gh", "cut-end", "bin/gh.exe", "truncated")]
    [InlineData("a zip cut in half", "zip", "bin/gh.exe=gh; bin/README=read me", "cut-data", "bin/gh.exe", "truncated")]
    [InlineData("a zip without the executable", "zip", "bin/gh.exe=gh", "", "bin/gh2.exe", "exe")]
    [InlineData("a zip whose executable is a folder", "zip", "bin/gh.exe/", "", "bin/gh.exe", "exe")]
    [InlineData("a tar.gz that unpacks", "tar.gz", "bin/gh=gh; bin/README=read me; docs/", "", "bin/gh", null)]
    [InlineData("a tar.gz whose names are in extended headers", "tar.gz", "bin/gh=gh; docs/", "pax", "bin/gh", null)]
    [InlineData("a tar.gz name from the root", "tar.gz", "/gh=x", "", "bin/gh", "absolute")]
    [InlineData("a tar.gz name that climbs out", "tar.gz", "bin/gh=gh; ../gh=x", "", "bin/gh", "outside")]
    [InlineData("a tar.gz stream name", "tar.gz", "bin/gh:hidden=x", "", "bin/gh", "stream")]
    [InlineData("a tar.gz symbolic link", "tar.gz", "bin/gh=/usr/bin/gh!symlink", "", "bin/gh", "link")]
    [InlineData("a tar.gz hard link", "tar.gz", "bin/gh=bin/other!hardlink", "", "bin/gh", "link")]
    [InlineData("a tar.gz entry neither file nor folder", "tar.gz", "bin/gh=x!fifo", "", "bin/gh", "entry")]
    [InlineData("a tar.gz header that fails its checksum", "tar.gz", "bin/gh=gh!badcrc", "", "bin/gh", "checksum")]
    [InlineData("a tar.gz with no end", "tar.gz", "bin/gh=gh", "cut-end", "bin/gh", "truncated")]
    [InlineData("a tar.gz cut inside an entry", "tar.gz", "bin/gh=gh", "cut-data", "bin/gh", "truncated")]
    [InlineData("a tar.gz whose gzip is cut before its trailer", "tar.gz", "bin/gh=gh", "cut-gzip", "bin/gh", "truncated")]
    [InlineData("a tar.gz without the executable", "tar.gz", "bin/gh=gh", "", "bin/gh2", "exe")]
    [InlineData("an archive kind nobody unpacks", "7z", "bin/gh.exe=gh", "", "bin/gh.exe", "archive")]
    public void An_archive_unpacks_as_the_cli_unpacks_it(string name, string kind, string entries, string shape, string exe, string? check)
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build(kind, entries, shape));
        // Two folders down, so a name that climbs out one or two lands somewhere this can look.
        var into = Path.Combine(_root, "a", "b", "package");

        if (check is null)
        {
            var file = ToolInstall.Unpack(archive, kind, into, exe);
            Assert.Equal(Path.Combine([into, .. exe.Split('/')]), file);
            Assert.Equal("gh", File.ReadAllText(file));
        }
        else
        {
            var refused = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, kind, into, exe));
            Assert.True(check == refused.Check, $"{name}: {refused.Check} — {refused.Message}");
        }

        foreach (var outside in new[]
        {
            Path.Combine(_root, "gh.exe"), Path.Combine(_root, "a", "gh.exe"), Path.Combine(_root, "a", "b", "gh.exe"),
            Path.Combine(_root, "a", "b", "gh"),
        })
        {
            Assert.False(File.Exists(outside), $"{name}: nothing lands outside the folder ({outside})");
        }
    }

    [Fact]
    public void A_zip_unpacks_whole_top_folder_included_and_what_may_run_stays_runnable()
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build("zip", "gh_2.62.0/; gh_2.62.0/bin/gh.exe=gh; gh_2.62.0/LICENSE=MIT", ""));

        var into = Path.Combine(_root, "package");
        var file = ToolInstall.Unpack(archive, "zip", into, "gh_2.62.0/bin/gh.exe");
        Assert.Equal(Path.Combine(into, "gh_2.62.0", "bin", "gh.exe"), file);
        Assert.Equal("MIT", File.ReadAllText(Path.Combine(into, "gh_2.62.0", "LICENSE")));
        if (!OperatingSystem.IsWindows()) Assert.True((File.GetUnixFileMode(file) & UnixFileMode.UserExecute) != 0);
    }

    [Fact]
    public void A_refusal_says_what_it_found_in_a_sentence()
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build("zip", "bin/gh.exe=gh!badcrc", ""));

        var crc = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, "zip", Path.Combine(_root, "package"), "bin/gh.exe"));
        Assert.Matches(@"^the archive's `bin/gh\.exe` fails its own check — 2 bytes with CRC-32 [0-9a-f]{8}, where it states 2 with [0-9a-f]{8}\z", crc.Message);
        var kind = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, "rar", Path.Combine(_root, "package"), "bin/gh.exe"));
        Assert.Contains("`rar` is not an archive this build unpacks — zip or tar.gz", kind.Message);
    }

    /// <summary>
    /// Archives built from a row's words — the CLI's <c>_archives.ts</c>, spelled again with this side's own code.
    /// </summary>
    /// <remarks>
    /// <para><c>entries</c> is items separated by <c>; </c>. An item is a folder, <c>&lt;name&gt;/</c>, or a file,
    /// <c>&lt;name&gt;=&lt;text&gt;</c>, followed by any of these flags, each after a <c>!</c>: <c>stored</c>,
    /// <c>method12</c>, <c>encrypted</c>, <c>badcrc</c>, <c>symlink</c>, <c>hardlink</c>, <c>fifo</c>.</para>
    /// <para><c>shape</c> is <c>""</c>, <c>zip64</c>, <c>pax</c>, <c>cut-end</c>, <c>cut-data</c> or
    /// <c>cut-gzip</c>.</para>
    /// </remarks>
    internal static class Archive
    {
        public sealed record Item(string Name, string? Text, IReadOnlyList<string> Flags);

        public static IReadOnlyList<Item> Parse(string spec) =>
        [
            .. spec.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(item =>
            {
                var parts = item.Split('!');
                var head = parts[0];
                var equals = head.IndexOf('=');
                return equals < 0 ? new Item(head, null, parts[1..]) : new Item(head[..equals], head[(equals + 1)..], parts[1..]);
            }),
        ];

        /// <summary>The archive a row names: <c>zip</c>, <c>tar.gz</c>, or any other kind, built as a zip.</summary>
        public static byte[] Build(string kind, string entries, string shape)
        {
            var items = Parse(entries);
            if (kind == "tar.gz")
            {
                var gz = Gzip(Tar(items, shape));
                return shape == "cut-gzip" ? gz[..^4] : gz;
            }

            var zip = Zip(items, shape == "zip64");
            return shape switch
            {
                "cut-end" => zip[..^22],
                "cut-data" => zip[..(zip.Length / 2)],
                _ => zip,
            };
        }

        private static byte[] Tar(IReadOnlyList<Item> items, string shape)
        {
            var tar = new MemoryStream();
            foreach (var item in items)
            {
                var folder = item.Text is null;
                var type = folder ? '5' : item.Flags.Contains("symlink") ? '2' : item.Flags.Contains("hardlink") ? '1'
                    : item.Flags.Contains("fifo") ? '6' : '0';
                var linked = type is '1' or '2';
                var body = type == '0' ? Encoding.UTF8.GetBytes(item.Text!) : [];
                if (shape == "pax") tar.Write(TarEntry("PaxHeader", Encoding.UTF8.GetBytes(Pax("path", item.Name)), 'x', 0x1A4, ""));
                var entry = TarEntry(shape == "pax" ? "placeholder" : item.Name, body, type, folder ? 0x1ED : 0x1A4, linked ? item.Text! : "");
                if (item.Flags.Contains("badcrc")) Encoding.ASCII.GetBytes("000000\0 ").CopyTo(entry, 148);
                tar.Write(entry);
            }

            if (shape != "cut-end") tar.Write(new byte[1024]);
            var bytes = tar.ToArray();
            return shape == "cut-data" ? bytes[..513] : bytes;
        }

        /// <summary>One ustar entry: its header, then its data padded to the block.</summary>
        private static byte[] TarEntry(string name, byte[] data, char type, int mode, string link)
        {
            var header = new byte[512];
            Put(header, 0, name.Length > 100 ? name[..100] : name);
            Put(header, 100, Octal(mode, 7));
            Put(header, 108, Octal(0, 7));
            Put(header, 116, Octal(0, 7));
            Put(header, 124, Octal(data.Length, 11));
            Put(header, 136, Octal(0, 11));
            Put(header, 148, "        ");
            header[156] = (byte)type;
            Put(header, 157, link);
            Put(header, 257, "ustar\000");
            var sum = header.Sum(value => value);
            Put(header, 148, Convert.ToString(sum, 8).PadLeft(6, '0') + "\0 ");

            var padded = new byte[(data.Length + 511) / 512 * 512];
            data.CopyTo(padded, 0);
            return [.. header, .. padded];
        }

        /// <summary>A pax record: <c>&lt;length&gt; &lt;key&gt;=&lt;value&gt;\n</c>, the length counting itself.</summary>
        private static string Pax(string key, string value)
        {
            var tail = $" {key}={value}\n";
            var length = tail.Length + 1;
            while ($"{length}{tail}".Length != length) length++;
            return $"{length}{tail}";
        }

        private static string Octal(int value, int width) => Convert.ToString(value, 8).PadLeft(width, '0') + "\0";

        private static void Put(byte[] into, int at, string text) => Encoding.UTF8.GetBytes(text).CopyTo(into, at);

        private static byte[] Gzip(byte[] data)
        {
            var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
            return output.ToArray();
        }

        private static byte[] Deflate(byte[] data)
        {
            var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(data);
            return output.ToArray();
        }

        private static byte[] Zip64Extra(params long[] values)
        {
            var extra = new MemoryStream();
            using var writer = new BinaryWriter(extra);
            writer.Write((ushort)0x0001);
            writer.Write((ushort)(8 * values.Length));
            foreach (var value in values) writer.Write((ulong)value);
            writer.Flush();
            return extra.ToArray();
        }

        private static byte[] Zip(IReadOnlyList<Item> items, bool zip64)
        {
            var locals = new MemoryStream();
            var centrals = new MemoryStream();
            using var local = new BinaryWriter(locals);
            using var central = new BinaryWriter(centrals);

            foreach (var item in items)
            {
                var folder = item.Text is null;
                var data = Encoding.UTF8.GetBytes(item.Text ?? "");
                ushort method = folder || item.Flags.Contains("stored") ? (ushort)0 : item.Flags.Contains("method12") ? (ushort)12 : (ushort)8;
                var body = method == 8 ? Deflate(data) : data;
                var crc = ToolInstall.Crc32(data);
                if (item.Flags.Contains("badcrc")) crc ^= 0xffffffff;
                var flags = (ushort)((item.Flags.Contains("encrypted") ? 1 : 0) | 0x800);
                var mode = item.Flags.Contains("symlink") ? 0xA1FF : folder ? 0x41ED : 0x81A4;
                var name = Encoding.UTF8.GetBytes(item.Name);
                ushort version = zip64 ? (ushort)45 : (ushort)20;
                var offset = locals.Length;

                var localExtra = zip64 ? Zip64Extra(data.Length, body.Length) : [];
                local.Write(0x04034b50u);
                local.Write(version);
                local.Write(flags);
                local.Write(method);
                local.Write((ushort)0);
                local.Write((ushort)0x21);
                local.Write(crc);
                local.Write(zip64 ? 0xffffffffu : (uint)body.Length);
                local.Write(zip64 ? 0xffffffffu : (uint)data.Length);
                local.Write((ushort)name.Length);
                local.Write((ushort)localExtra.Length);
                local.Write(name);
                local.Write(localExtra);
                local.Write(body);

                var centralExtra = zip64 ? Zip64Extra(data.Length, body.Length, offset) : [];
                central.Write(0x02014b50u);
                central.Write((ushort)(0x0300 | version));
                central.Write(version);
                central.Write(flags);
                central.Write(method);
                central.Write((ushort)0);
                central.Write((ushort)0x21);
                central.Write(crc);
                central.Write(zip64 ? 0xffffffffu : (uint)body.Length);
                central.Write(zip64 ? 0xffffffffu : (uint)data.Length);
                central.Write((ushort)name.Length);
                central.Write((ushort)centralExtra.Length);
                central.Write((ushort)0);
                central.Write((ushort)0);
                central.Write((ushort)0);
                central.Write((uint)mode << 16);
                central.Write(zip64 ? 0xffffffffu : (uint)offset);
                central.Write(name);
                central.Write(centralExtra);
            }

            local.Flush();
            central.Flush();
            var directory = centrals.ToArray();
            var at = locals.Length;
            var all = new MemoryStream();
            using var tail = new BinaryWriter(all);
            tail.Write(locals.ToArray());
            tail.Write(directory);
            if (zip64)
            {
                tail.Write(0x06064b50u);
                tail.Write(44ul);
                tail.Write((ushort)45);
                tail.Write((ushort)45);
                tail.Write(0u);
                tail.Write(0u);
                tail.Write((ulong)items.Count);
                tail.Write((ulong)items.Count);
                tail.Write((ulong)directory.Length);
                tail.Write((ulong)at);
                tail.Write(0x07064b50u);
                tail.Write(0u);
                tail.Write((ulong)(at + directory.Length));
                tail.Write(1u);
            }

            tail.Write(0x06054b50u);
            tail.Write((ushort)0);
            tail.Write((ushort)0);
            tail.Write(zip64 ? (ushort)0xffff : (ushort)items.Count);
            tail.Write(zip64 ? (ushort)0xffff : (ushort)items.Count);
            tail.Write(zip64 ? 0xffffffffu : (uint)directory.Length);
            tail.Write(zip64 ? 0xffffffffu : (uint)at);
            tail.Write((ushort)0);
            tail.Flush();
            return all.ToArray();
        }
    }
}
