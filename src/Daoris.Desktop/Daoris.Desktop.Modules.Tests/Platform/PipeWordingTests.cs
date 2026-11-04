using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The pipe's wording: a person's route's marks say which of its values the card's sentence names and by what role, by the
/// catalogue key its act is worded by (the web's names check holds every person's route worded in each language). The named
/// values travel on the pipe's telling as <c>named</c>, a field added beside <c>fields</c>.
/// </summary>
public sealed class PipeWordingTests : Bridge
{
    [Fact]
    public void A_drivers_route_is_worded_by_its_route_and_another_modules_by_its_module_too()
    {
        Assert.Equal("person.route.SESSION_INPUT", PipeWording.Key(DriverModule.Name, "SESSION_INPUT"));
        Assert.Equal("person.route.REMOTES.SET", PipeWording.Key("DAORIS.REMOTES", "SET"));
    }

    /// <summary>
    /// The worked example (design §3.2): <c>sessions say</c>'s press names the session, the words, the level, each file by its path
    /// whole, and <c>said</c> as a form, since it chooses whether the verb's own path reads the files from this machine.
    /// </summary>
    [Fact]
    public void Session_input_names_the_session_the_words_the_level_the_files_and_said_as_a_form()
    {
        var row = PipeWording.For(DriverModule.Name, "SESSION_INPUT")!;

        Assert.Equal(
            [("id", PipeRole.Session), ("text", PipeRole.Words), ("level", PipeRole.Level), ("paths", PipeRole.Files), ("said", PipeRole.Form)],
            row.Select(word => (word.Field, word.Role)));
    }

    [Fact]
    public void The_named_values_are_the_values_its_row_names_by_their_pointers_and_the_rest_are_named_by_none()
    {
        var (fields, _) = PipeConfirmations.FieldsOf(JsonDocument.Parse(
            """{ "id": "3f2a91c4", "text": "Run the tests.", "level": "urgent", "said": true, "paths": ["a/b.ts", "c/b.ts"], "key": "k1" }""").RootElement);

        var named = PipeWording.Named(DriverModule.Name, "SESSION_INPUT", fields!);

        Assert.Equal(
            [
                ("session", "/id", "3f2a91c4"), ("words", "/text", "Run the tests."), ("level", "/level", "urgent"),
                ("files", "/paths/0", "a/b.ts"), ("files", "/paths/1", "c/b.ts"), ("form", "/said", "true"),
            ],
            named.Select(each => (each.Role, each.Name, each.Value)));
        // A value no role names (`key`) is named by none, so the card lists it under the sentence.
        Assert.DoesNotContain(named, each => each.Name == "/key");
        // A route that is not the person's has no row, and names nothing.
        Assert.Empty(PipeWording.Named(DriverModule.Name, "STATE", fields!));
    }

    [Fact]
    public void A_press_tells_the_values_its_sentence_names_beside_every_value_it_carries()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var (fields, _) = PipeConfirmations.FieldsOf(JsonDocument.Parse("""{ "id": "3f2a91c4", "text": "Hello." }""").RootElement);

        var asked = confirmations.Ask(DriverModule.Name, "SESSION_INPUT", fields!, new PipeAsker("bash", 4120))!.Value;

        Assert.Equal(2, asked.Shown.Fields.Count);
        Assert.Equal([("session", "/id"), ("words", "/text")], asked.Shown.Named!.Select(each => (each.Role, each.Name)));
        Assert.Equal(asked.Shown.Named, confirmations.Now().Waiting.Single().Named);
    }
}
