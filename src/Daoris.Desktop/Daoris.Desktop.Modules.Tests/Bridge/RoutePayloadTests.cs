using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A route's payload record read off the request as its hand-written reads read it: a required field refused in the kit's codes,
/// a text blank or of another kind read as none, a flag true only when said, a yes or no as said or none, and a whole number or
/// none. A type the reader does not read is refused when the record is first read, so a handler of it fails the module's first use.
/// </summary>
public sealed class RoutePayloadTests
{
    public sealed record Press(string Repository, bool Held, string? Says, bool Never = false, bool? Whole = null, long? Days = null, IReadOnlyList<string>? Paths = null);

    public sealed record Unread(DateTimeOffset At);

    private static IpcRequest Request(string json) => new() { Id = "x", Module = DriverModule.Name, Type = "SET_HOLD", Payload = JsonDocument.Parse(json).RootElement };

    private static readonly Func<IpcRequest, Press> Read = RoutePayload.Reader<Press>();

    [Fact]
    public void Each_field_is_read_by_its_camel_cased_name_as_its_type_says()
    {
        var press = Read(Request("""{ "repository": "engine", "held": true, "says": "Run it.", "never": true, "whole": false, "days": 7, "paths": ["a", 1, "b"] }"""));

        Assert.Equal(new Press("engine", true, "Run it.", true, false, 7, press.Paths), press);
        Assert.Equal(["a", "b"], press.Paths!);
        Assert.Equal(
            [("repository", PayloadRead.Required), ("held", PayloadRead.Required), ("says", PayloadRead.Text), ("never", PayloadRead.Flag),
             ("whole", PayloadRead.Choice), ("days", PayloadRead.Number), ("paths", PayloadRead.Texts)],
            RoutePayload.Fields(typeof(Press)).Select(field => (field.Name, field.Read)));
    }

    [Fact]
    public void An_optional_field_absent_blank_or_of_another_kind_is_none_and_a_flag_is_false()
    {
        var press = Read(Request("""{ "repository": "engine", "held": false, "says": "", "never": "yes", "whole": 1, "days": 1.5 }"""));

        Assert.Equal(new Press("engine", false, null), press);
    }

    [Fact]
    public void A_required_field_missing_or_of_another_kind_is_refused_in_the_kit_s_codes()
    {
        var missing = Assert.Throws<ShenoraException>(() => Read(Request("""{ "held": true }""")));
        var mistyped = Assert.Throws<ShenoraException>(() => Read(Request("""{ "repository": "engine", "held": 3 }""")));

        Assert.Equal(IpcErrorCodes.MissingPayloadValue, missing.Code);
        Assert.Equal(IpcErrorCodes.InvalidPayloadValue, mistyped.Code);
    }

    [Fact]
    public void A_field_of_a_type_no_payload_reads_is_refused_when_the_record_is_first_read() =>
        Assert.Contains("Unread.At", Assert.Throws<InvalidOperationException>(() => RoutePayload.Reader<Unread>()).Message);
}
