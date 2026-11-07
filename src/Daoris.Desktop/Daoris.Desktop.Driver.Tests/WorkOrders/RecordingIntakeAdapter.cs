using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A pipe-door harness that takes a settings file, remembering what the intake is handed.</summary>
internal sealed class RecordingIntakeAdapter : ISpawn
{
    private readonly ISpawn _stub = TestDoors.SpawnOf(BuiltDoors.Stub);

    public string Name => "recording-intake";

    public string? HandedText { get; private set; }

    public Provider Door => TestDoors.Door(Name, takesSettings: true, spawn: this);

    public ProcessRecipe Start(SpawnRequest request) => _stub.Start(request);

    public ProcessRecipe Chat(SpawnRequest request) => _stub.Chat(request);

    public void HandSettings(ProcessStartInfo info, string settingsFile) => HandedText = File.ReadAllText(settingsFile);
}
