using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The provider sheet: each door's sheet read from
/// its registration, each row's source and fallback, a measurement that applies only at its configuration, and the measurements table
/// held to the evidence documents it names.
/// </summary>
public sealed class AgentCapabilitiesTests
{
    private static readonly ProviderRegistry Registry = ProviderRegistry.Built();

    private static AgentCapabilities SheetOf(string door) => Registry.Facts.Of(door)!.Door(door)!;

    [Fact]
    public void Every_door_s_sheet_lists_every_capability_in_order_and_its_door_as_declared()
    {
        foreach (var door in Registry.Doors)
        {
            var sheet = SheetOf(door.Value);
            Assert.Equal(SheetCapability.All, sheet.Rows.Select(row => row.Capability));
            Assert.Equal(SheetRow.Declared, sheet.Row(SheetCapability.Door).Source);
            Assert.All(sheet.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Fallback), $"{door.Value}'s {row.Capability}"));
            Assert.All(sheet.Rows.Where(row => row.Source == SheetRow.Absent), row => Assert.Null(row.Value));
        }
    }

    [Fact]
    public void A_door_s_word_is_its_transport_s()
    {
        Assert.Equal("protocol", SheetOf("claude-code-acp").Transport);
        Assert.Equal("protocol", SheetOf("claude-code-acp").Row(SheetCapability.Door).Value);
        Assert.Equal("native", SheetOf("claude-code").Transport);
        Assert.Equal("native", SheetOf("claude-code").Row(SheetCapability.Door).Value);
    }

    [Fact]
    public void A_measured_row_names_its_evidence_and_configuration_and_another_door_of_the_agent_does_not_borrow_it()
    {
        var steering = SheetOf("claude-code-acp").Row(SheetCapability.Steering);
        Assert.Equal(SheetRow.Measured, steering.Source);
        Assert.Equal(SheetRow.Yes, steering.Value);
        Assert.Equal("docs/2026-10-03-steer-evidence.md", steering.Evidence);
        Assert.Contains("adapter 0.84.0", steering.Applies, StringComparison.Ordinal);

        // The native door of the same agent measured nothing: silence is never support.
        var native = SheetOf("claude-code").Row(SheetCapability.Steering);
        Assert.Equal(SheetRow.Absent, native.Source);
        Assert.False(SheetOf("claude-code").Does(SheetCapability.Steering));
        Assert.Equal(SheetRow.Absent, SheetOf("codex-acp").Row(SheetCapability.Confinement).Source);
    }

    [Fact]
    public void An_opening_s_answer_outranks_a_declaration_and_never_a_measurement()
    {
        var provider = Registry.Provider(Registry.ProviderOf(new DoorId("claude-code-acp")));
        var door = Registry.Door("claude-code-acp");
        var negotiated = new Dictionary<string, bool> { [SheetCapability.Resume] = false, [SheetCapability.Steering] = false };

        var sheet = Sheet.Of(provider, door, negotiated);

        Assert.Equal(SheetRow.Negotiated, sheet.Row(SheetCapability.Resume).Source);
        Assert.Equal(SheetRow.No, sheet.Row(SheetCapability.Resume).Value);
        Assert.Equal(SheetRow.Measured, sheet.Row(SheetCapability.Steering).Source);

        // With the measurement taken off the door, the same steering reads as the opening answered it.
        var unmeasured = door with { Abilities = door.Abilities with { Steering = Known<bool>.Absent } };
        Assert.Equal(SheetRow.Negotiated, Sheet.Of(provider, unmeasured, negotiated).Row(SheetCapability.Steering).Source);
    }

    [Fact]
    public void Each_agent_is_answered_once_by_any_of_its_names_its_named_door_leading()
    {
        var claude = Registry.Facts.Of("claude-code-acp")!;
        Assert.Equal("claude-code", claude.Id);
        Assert.Equal("claude-code", claude.Lead);
        Assert.Equal(["claude-code", "claude-code-acp"], claude.Doors.Select(door => door.Door));
        Assert.True(claude.Acts.SignsIn);
        Assert.True(claude.Acts.Installs);

        // A holder with no door of its id: its one door leads, and its account acts are the agent's.
        var codex = Registry.Facts.Of("codex-acp")!;
        Assert.Equal("codex", codex.Id);
        Assert.Equal("codex-acp", codex.Lead);
        Assert.True(codex.Acts.SignsIn);
        Assert.True(codex.Acts.DeviceCode);
        Assert.Same(codex, Registry.Facts.Of("codex"));

        Assert.Null(Registry.Facts.Of("acme"));
        Assert.Equal(Registry.Providers.Select(provider => provider.Id.Value), Registry.Facts.Agents.Select(agent => agent.Id));
    }

    [Fact]
    public void A_measured_ability_is_a_known_value_on_its_door_with_its_evidence_and_the_versions_it_was_taken_at()
    {
        var abilities = Registry.Door("claude-code-acp").Abilities;
        foreach (var measured in new[] { abilities.WordsAtNextStep, abilities.Steering })
        {
            Assert.Equal(KnownBy.Measured, measured.By);
            Assert.True(measured.Required);
            Assert.True(File.Exists(Path.Combine(GitSeed.RepoRoot(), measured.Evidence)), $"{measured.Evidence} is not in the repository");
            Assert.Equal(new MeasuredAt("2.1.284", "0.84.0"), measured.At);
        }
    }

    [Fact]
    public void The_sheet_and_the_door_s_abilities_read_one_source_so_they_cannot_disagree()
    {
        foreach (var id in Registry.Doors)
        {
            var can = Registry.Door(id.Value).Can;
            var sheet = SheetOf(id.Value);
            Assert.Equal(can.WordsAtNextStep, sheet.Does(SheetCapability.WordsAtNextStep));
            Assert.Equal(can.Resumes, sheet.Does(SheetCapability.Resume));
            Assert.Equal(can.ServersOnWire, sheet.Does(SheetCapability.ServersOnWire));
            Assert.Equal(can.Interactive, sheet.Does(SheetCapability.Conversation));
            Assert.Equal(can.TakesSettings, sheet.Does(SheetCapability.TakesRules));
        }
    }

    [Fact]
    public void A_value_the_transport_supplies_says_it_is_a_default_and_not_measured()
    {
        var reading = AbilityReading.Of(Registry.Door("alt-acp-stub"), AbilityName.WordsAtNextStep);
        Assert.True(reading.IsKnown);
        Assert.Equal(AbilityReading.TransportDefault, reading.Evidence);
        Assert.Equal(KnownBy.Declared, reading.By);
    }

    [Fact]
    public void Codex_and_dsh_state_the_next_step_words_as_unmeasured_where_the_transport_gives_them()
    {
        foreach (var id in new[] { "codex-acp", "dsh" })
        {
            var stated = Registry.Door(id).Abilities.WordsAtNextStep;
            Assert.Equal(KnownBy.Declared, stated.By);
            Assert.Contains("not measured", stated.Evidence, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The service's call keys' twin: each agent the service reads a call's lineage for, in the fixture its
    /// <c>CallLineageTests</c> holds its one ordered list to, is an agent this registry answers.
    /// </summary>
    [Fact]
    public void The_service_s_call_keys_name_registered_agents()
    {
        var fixture = Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "provider-census.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fixture));
        var agents = document.RootElement.GetProperty("service").GetProperty("callKeys").EnumerateArray().Select(row => row[0].GetString()!).ToList();

        Assert.NotEmpty(agents);
        Assert.All(agents, agent => Assert.Equal(agent, Registry.Facts.Of(agent)?.Id));
    }
}
