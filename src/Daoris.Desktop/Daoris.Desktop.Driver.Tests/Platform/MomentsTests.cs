using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one way the driver writes and reads a moment (DEDUP2a): written round-trip in the invariant culture, read in it
/// with the offset kept, and never in the machine's culture, whose calendar (Buddhist, Hijri) would write and read
/// other digits.
/// </summary>
public sealed class MomentsTests
{
    private static readonly DateTimeOffset Sample = new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.FromHours(2)) + TimeSpan.FromTicks(1234567);

    public static TheoryData<string> Cultures => new() { "ar-SA", "th-TH", "de-DE" };

    private static void Under(string culture, Action action)
    {
        var before = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
            action();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = before;
        }
    }

    [Theory, MemberData(nameof(Cultures))]
    public void Write_is_the_round_trip_form_in_the_invariant_culture(string culture) => Under(culture, () =>
    {
        Assert.Equal("2026-10-14T09:30:15.1234567+02:00", Moments.Write(Sample));
        Assert.Equal(Sample.ToString("O", CultureInfo.InvariantCulture), Moments.Write(Sample));
        Assert.Equal("2026-10-14T09:30:15.1234567+02:00", Moments.Write((DateTimeOffset?)Sample));
        Assert.Null(Moments.Write((DateTimeOffset?)null));
    });

    [Theory, MemberData(nameof(Cultures))]
    public void Read_keeps_the_offset_as_written(string culture) => Under(culture, () =>
    {
        var read = Moments.Read("2026-10-14T09:30:15.1234567+02:00");

        Assert.Equal(Sample, read);
        Assert.Equal(TimeSpan.FromHours(2), read!.Value.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), Moments.Read("2026-10-14T09:30:15-05:00")!.Value.Offset);
    });

    [Theory, MemberData(nameof(Cultures))]
    public void Read_takes_a_seconds_only_moment_and_a_seven_digit_one(string culture) => Under(culture, () =>
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.Zero), Moments.Read("2026-10-14T09:30:15+00:00"));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 9, 30, 15, TimeSpan.Zero), Moments.Read("2026-10-14T09:30:15Z"));
        Assert.Equal(Sample.ToUniversalTime(), Moments.Read("2026-10-14T07:30:15.1234567+00:00"));
    });

    [Theory, MemberData(nameof(Cultures))]
    public void Read_answers_null_for_an_unreadable_moment(string culture) => Under(culture, () =>
    {
        Assert.Null(Moments.Read(null));
        Assert.Null(Moments.Read(""));
        Assert.Null(Moments.Read("not a moment"));
    });

    [Theory, MemberData(nameof(Cultures))]
    public void Read_takes_a_json_field_and_a_node(string culture) => Under(culture, () =>
    {
        using var document = JsonDocument.Parse("""{"at":"2026-10-14T09:30:15.1234567+02:00","bad":"x","n":3}""");
        var element = document.RootElement;

        Assert.Equal(Sample, Moments.Read(element, "at"));
        Assert.Null(Moments.Read(element, "bad"));
        Assert.Null(Moments.Read(element, "n"));
        Assert.Null(Moments.Read(element, "missing"));

        var node = JsonNode.Parse("""{"at":"2026-10-14T09:30:15.1234567+02:00","n":3}""")!.AsObject();
        Assert.Equal(Sample, Moments.Read(node["at"]));
        Assert.Null(Moments.Read(node["n"]));
        Assert.Null(Moments.Read(node["missing"]));
    });

    [Theory, MemberData(nameof(Cultures))]
    public void ReadStrict_takes_exactly_the_four_iso_formats_as_utc(string culture) => Under(culture, () =>
    {
        var expected = new DateTimeOffset(2026, 10, 14, 7, 30, 15, TimeSpan.Zero);

        Assert.Equal(expected, Moments.ReadStrict("2026-10-14T07:30:15Z"));
        Assert.Equal(expected + TimeSpan.FromTicks(1234567), Moments.ReadStrict("2026-10-14T07:30:15.1234567Z"));
        Assert.Equal(expected, Moments.ReadStrict("2026-10-14T09:30:15+02:00"));
        Assert.Equal(expected + TimeSpan.FromTicks(1234567), Moments.ReadStrict("2026-10-14T09:30:15.1234567+02:00"));
        Assert.Equal(TimeSpan.Zero, Moments.ReadStrict("2026-10-14T09:30:15+02:00")!.Value.Offset);
    });

    [Theory, MemberData(nameof(Cultures))]
    public void ReadStrict_refuses_what_a_lenient_parse_would_take(string culture) => Under(culture, () =>
    {
        Assert.Null(Moments.ReadStrict(null));
        Assert.Null(Moments.ReadStrict(""));
        Assert.Null(Moments.ReadStrict("Oct 3"));
        Assert.Null(Moments.ReadStrict("2026-10-14"));
        Assert.Null(Moments.ReadStrict("2026-10-14 07:30:15"));
    });
}
