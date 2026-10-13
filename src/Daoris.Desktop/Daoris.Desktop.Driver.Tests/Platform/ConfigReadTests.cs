using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONFIGREAD1: a <c>driver.json</c> that does not read is said, naming the file and where, and never thrown as the
/// parser's own exception. Every door loads it through <see cref="DriverConfig.Load"/> (CONFIGSEAM1), and each says a
/// <see cref="DriverException"/>'s sentence: the headless host's one catch with exit 2 (REV3), the modules' route as the
/// driver's refusal, and the loop's look with what the loop does about it. The headless host ended on the parser's stack
/// trace until then (HOSTSTART2).
/// </summary>
public sealed class ConfigReadTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-configread1-" + Guid.NewGuid().ToString("N")[..8]);

    public ConfigReadTests() => Directory.CreateDirectory(_home);

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Written(string text)
    {
        var path = Path.Combine(_home, "driver.json");
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The file, the line and the byte in it, counted from one, and the parser's reason without its own counts.</summary>
    [Fact]
    public void A_file_that_does_not_parse_is_said_with_its_path_its_line_and_its_byte()
    {
        var path = Written("""{ "drivable": [""");

        var refused = Assert.Throws<DriverConfigUnreadableException>(() => DriverConfig.Load(path));

        Assert.IsAssignableFrom<DriverException>(refused);
        Assert.Equal(path, refused.Path);
        Assert.Equal((1L, 16L), (refused.Line, refused.Byte));
        Assert.StartsWith($"{path} is not readable JSON at line 1, byte 16 (", refused.Message);
        Assert.EndsWith("). Fix it, or delete it to start from nothing.", refused.Message);
        Assert.DoesNotContain("LineNumber", refused.Message);
        Assert.DoesNotContain("BytePositionInLine", refused.Message);
    }

    /// <summary>A line after the first is counted as an editor counts it, from one.</summary>
    [Fact]
    public void A_fault_on_a_later_line_names_that_line()
    {
        var path = Written("{\n  \"cap\": 2,\n  \"drivable\": [\"a\" \"b\"]\n}\n");

        var refused = Assert.Throws<DriverConfigUnreadableException>(() => DriverConfig.Load(path));

        Assert.Equal((3L, 20L), (refused.Line, refused.Byte));
        Assert.StartsWith($"{path} is not readable JSON at line 3, byte 20 (", refused.Message);
    }

    /// <summary>
    /// JSON that parses and is not the choices' shape: a root that is not an object, and a number the driver keeps whole.
    /// Each threw a runtime exception with no file in it.
    /// </summary>
    [Theory]
    [InlineData("[]", "it holds no JSON object")]
    [InlineData("\"drivable\"", "it holds no JSON object")]
    [InlineData("""{ "cap": 2.5 }""", "`cap` is not a whole number")]
    [InlineData("""{ "pollSeconds": 99999999999 }""", "`pollSeconds` is not a whole number")]
    public void A_file_of_the_wrong_shape_is_said_with_what_is_wrong(string text, string problem)
    {
        var path = Written(text);

        var refused = Assert.Throws<DriverConfigUnreadableException>(() => DriverConfig.Load(path));

        Assert.Equal($"{path} does not read: {problem}. Fix it, or delete it to start from nothing.", refused.Message);
        Assert.Null(refused.Line);
        Assert.Null(refused.Byte);
    }

    /// <summary>A missing file is still the empty config, and a file that reads still reads (D46 §6).</summary>
    [Fact]
    public void A_missing_file_and_a_good_one_are_not_refused()
    {
        Assert.Same(DriverConfig.Empty, DriverConfig.Load(Path.Combine(_home, "driver.json")));
        Assert.Equal(["engine"], DriverConfig.Load(Written("""{ "drivable": ["engine"], "cap": 2 }""")).Drivable);
    }
}
