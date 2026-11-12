using Daoris.Ports.Processes;

namespace Daoris.Testing.Tests;

public sealed class ScriptedProcessesContractTests : ProcessContract
{
    protected override IMachineAdmission Backend(int places) => new ScriptedProcesses(places);

    [Fact]
    public async Task Each_ask_s_class_is_kept_in_order()
    {
        var processes = new ScriptedProcesses(2);

        await using (await processes.PlaceAsync(AdmissionClasses.Background, CancellationToken.None))
        await using (await processes.PlaceAsync(AdmissionClasses.Person, CancellationToken.None))
        {
            Assert.Equal(0, processes.Free);
        }

        Assert.Equal([AdmissionClasses.Background, AdmissionClasses.Person], processes.Asked);
        Assert.Equal(2, processes.Free);
    }
}

public sealed class ScriptedProcessHostContractTests : ProcessHostContract
{
    private readonly ScriptedProcesses _processes = new ScriptedProcesses(4)
        .On(spec => spec.Program == "says", spec => new ScriptedAnswer([spec.Arguments[0]], 0))
        .On(spec => spec.Program == "exits", spec => new ScriptedAnswer([], int.Parse(spec.Arguments[0])))
        .On(spec => spec.Program == "waits", _ => new ScriptedAnswer([], 0, Waits: true));

    protected override IProcessHost Host => _processes;

    protected override IProcessLiveness Liveness => _processes;

    protected override ProcessSpec Says(string text) => new("says", [text], "contract", AdmissionClasses.Background);

    protected override ProcessSpec Exits(int code) => new("exits", [code.ToString()], "contract", AdmissionClasses.Background);

    protected override ProcessSpec Waits() => new("waits", [], "contract", AdmissionClasses.Background);

    [Fact]
    public async Task A_start_no_row_answers_is_a_failure_naming_it()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _processes.StartAsync(new ProcessSpec("unscripted", ["--flag"], "contract", AdmissionClasses.Person), CancellationToken.None));

        Assert.Contains("unscripted --flag", failure.Message);
        Assert.Equal(4, _processes.Free);
    }
}

public sealed class ScriptedSealContractTests : SealContract
{
    protected override SecretSeal Backend => ScriptedSeal.Port;

    protected override string Foreign => ScriptedSeal.Prefix + "AAAAAAAA";
}
