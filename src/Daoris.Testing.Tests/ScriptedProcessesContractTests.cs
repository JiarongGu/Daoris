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

public sealed class ScriptedSealContractTests : SealContract
{
    protected override SecretSeal Backend => ScriptedSeal.Port;

    protected override string Foreign => ScriptedSeal.Prefix + "AAAAAAAA";
}
