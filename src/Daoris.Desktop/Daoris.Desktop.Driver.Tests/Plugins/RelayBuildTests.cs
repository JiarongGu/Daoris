using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVENTHOOK1b (the event hook design §4.1, §6.1 step 0; K2.2): a build is its whole bundle. <see cref="RelayBuild.Of"/> hashes one line
/// per file under the folder, its relative path with <c>/</c>, its size and its own hash, in ordinal order, so a changed companion library
/// is another build; a folder that is not there is 64 zeros.
/// </summary>
public sealed class RelayBuildTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "relay-build", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Bundle(string name, string library = "library one")
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(folder, "runtimes", "win"));
        File.WriteAllText(Path.Combine(folder, "daoris-driver.dll"), "the driver");
        File.WriteAllText(Path.Combine(folder, "Daoris.Driver.Lib.dll"), library);
        File.WriteAllText(Path.Combine(folder, "runtimes", "win", "native.dll"), "native");
        return folder;
    }

    private static string Hex(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void A_build_is_the_hash_of_every_file_s_line_in_ordinal_order()
    {
        var folder = Bundle("a");
        var lines = new[]
        {
            $"Daoris.Driver.Lib.dll\t11\t{Hex("library one")}\n",
            $"daoris-driver.dll\t10\t{Hex("the driver")}\n",
            $"runtimes/win/native.dll\t6\t{Hex("native")}\n",
        };

        Assert.Equal(Hex(string.Concat(lines)), RelayBuild.Of(folder));
    }

    [Fact]
    public void The_same_bytes_are_the_same_build_and_a_changed_companion_library_is_another()
    {
        var first = RelayBuild.Of(Bundle("a"));

        Assert.Equal(first, RelayBuild.Of(Bundle("b")));
        Assert.NotEqual(first, RelayBuild.Of(Bundle("c", library: "library two")));
        var added = Bundle("d");
        File.WriteAllText(Path.Combine(added, "extra.json"), "{}");
        Assert.NotEqual(first, RelayBuild.Of(added));
    }

    [Fact]
    public void A_folder_that_is_not_there_is_64_zeros()
    {
        Assert.Equal(RelayBuild.None, RelayBuild.Of(Path.Combine(_root, "absent")));
        Assert.Equal(new string('0', 64), RelayBuild.None);
    }
}
