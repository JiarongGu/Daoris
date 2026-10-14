using System.Globalization;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// DEDUP9b: a moment is written and read one way in the core, whatever the machine's culture, and an offset
/// written is an offset read (not turned local). Stored moments are kept on machines, so the readings the
/// stores already hold (seconds-only <c>+00:00</c>, seven fraction digits) stay readable.
/// </summary>
public sealed class MomentsTests
{
    private static readonly DateTimeOffset Sample = new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.FromHours(8)).AddTicks(1234567);

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("de-DE")]
    public void Write_and_read_are_the_same_under_any_culture(string culture)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var written = Moments.Write(Sample);
            Assert.Equal("2026-10-14T09:30:15.1234567+08:00", written);
            Assert.Equal(Sample, Moments.Read(written));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void An_offset_is_kept_as_written()
    {
        var read = Moments.Read("2026-10-14T09:30:15.1234567+08:00");
        Assert.Equal(TimeSpan.FromHours(8), read!.Value.Offset);
        Assert.Equal("2026-10-14T09:30:15.1234567+08:00", Moments.Write(read.Value));
    }

    [Fact]
    public void A_seconds_only_moment_reads()
    {
        var read = Moments.Read("2026-10-14T09:30:15+00:00");
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.Zero), read);
    }

    [Fact]
    public void Seven_digits_round_trip()
    {
        var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(7654321);
        Assert.Equal(at, Moments.Read(Moments.Write(at)));
        Assert.Equal("2026-01-02T03:04:05.7654321+00:00", Moments.Write(at));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a word")]
    public void An_unreadable_moment_is_null(string? text) => Assert.Null(Moments.Read(text));

    [Fact]
    public void A_json_field_reads_and_a_missing_or_wrong_one_is_null()
    {
        using var document = JsonDocument.Parse("""{"at":"2026-10-14T09:30:15+00:00","n":3}""");
        var root = document.RootElement;
        Assert.NotNull(Moments.Read(root, "at"));
        Assert.Null(Moments.Read(root, "n"));
        Assert.Null(Moments.Read(root, "absent"));
    }

    [Fact]
    public void Required_throws_on_an_unreadable_moment()
    {
        Assert.Throws<FormatException>(() => Moments.ReadRequired("nope"));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.Zero), Moments.ReadRequired("2026-10-14T09:30:15+00:00"));
    }
}
