using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The keep rules as <c>driver.json</c> keeps them (FOUNDATION1 mod-iii): the settings' own bounds, reading and writing, which the
/// kinds' table and the floor's policy answer through, so a bound is said once.
/// </summary>
public sealed class KeepKeyTests
{
    [Fact]
    public void The_kinds_and_the_floor_answer_through_the_key_for_their_bounds()
    {
        Assert.Equal((KeepKey.SetAsideMinDays, KeepKey.SetAsideMaxDays), (KeepKinds.SetAside.MinDays, KeepKinds.SetAside.MaxDays));
        Assert.Equal((KeepKey.DownloadsMinDays, KeepKey.DownloadsMaxDays), (KeepKinds.Downloads.MinDays, KeepKinds.Downloads.MaxDays));
        Assert.Equal((KeepKey.LogsMinDays, KeepKey.LogsMaxDays), (KeepKinds.Logs.MinDays, KeepKinds.Logs.MaxDays));
        Assert.Equal((KeepKey.HistoryMinDays, KeepKey.HistoryMaxDays), (KeepKinds.History.MinDays, KeepKinds.History.MaxDays));
        Assert.Equal((KeepKey.FloorMinGb, KeepKey.FloorMaxGb, KeepKey.FloorMinPercent, KeepKey.FloorMaxPercent),
            (FloorPolicy.MinGb, FloorPolicy.MaxGb, FloorPolicy.MinPercent, FloorPolicy.MaxPercent));
        Assert.Equal(KeepKinds.Table, KeepRules.Settable);
    }

    [Fact]
    public void The_choices_are_read_within_their_bounds_and_kept_as_written()
    {
        const string text = """{"keep":{"setAside":{"days":30,"whole":true},"downloads":{"days":null},"logs":{"days":14},"history":{"days":90},"floor":{"gb":20}}}""";
        using var document = JsonDocument.Parse(text);

        var keep = KeepChoices.Read(document.RootElement);

        Assert.Equal(30, keep.SetAsideDays);
        Assert.True(keep.SetAsideWhole);
        Assert.True(keep.DownloadsNever);
        Assert.Equal(14, keep.LogsDays);
        Assert.Equal(90, keep.HistoryDays);
        Assert.Equal(20, keep.FloorGb);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            keep.Write(writer);
            writer.WriteEndObject();
        }

        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Fact]
    public void A_value_out_of_its_bounds_is_not_read_so_its_kind_keeps_its_default()
    {
        using var document = JsonDocument.Parse("""{"keep":{"setAside":{"days":91},"logs":{"days":6},"history":{"days":29},"floor":{"percent":51}}}""");

        var keep = KeepChoices.Read(document.RootElement);

        Assert.Equal(KeepChoices.None, keep);
        Assert.True(keep.IsDefault("floor"));
    }
}
