using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a build says of a home it starts on: the agent accounts it lists, how many sessions' records and runs' bindings it meets, and
/// each thing it cannot read or finds amiss (<see cref="Problems"/>, each a sentence): a session's record, a run's binding or a
/// workflow's file that does not read, a run that binds a workflow the home does not hold, a path no entry names.
/// </summary>
public sealed record HomeProofReport(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Accounts, int Sessions, int Runs, IReadOnlyList<string> Problems);

/// <summary>The reading of a home a build owes (<see cref="HomeProof.Read"/>): what it names lies above Install, so Workflows installs it.</summary>
internal interface IHomeProofReading
{
    /// <summary>The build's reading of <paramref name="home"/>; it changes nothing under it.</summary>
    HomeProofReport Read(string home);
}

/// <summary>
/// <c>daoris home proof</c>'s report, the one a build asks of itself and a running build asks of the staged one
/// (<see cref="HomeProbe"/>): the answers a build must still give of the home it is about to meet, which an update that read a place
/// the home no longer held once did not (FIX-LOG 2026-11-20, three accounts hidden). The report's words are here; the reading is Workflows'.
/// </summary>
public static class HomeProof
{
    private static IHomeProofReading? _reading;

    internal static IHomeProofReading Reading
    {
        get => _reading ?? throw new InvalidOperationException("no reading of a home is installed: Workflows installs it when the driver loads");
        set => _reading = value;
    }

    /// <summary>The build's reading of <paramref name="home"/>.</summary>
    public static HomeProofReport Read(string home) => Reading.Read(home);

    /// <summary>The report as one line, for a verb to print and <see cref="Parse"/> to read back.</summary>
    public static string Say(HomeProofReport report) => JsonSerializer.Serialize(report, Options);

    /// <summary>The report a verb printed, found in its output; null where there is none.</summary>
    public static HomeProofReport? Parse(string output)
    {
        foreach (var line in output.Split('\n').Select(each => each.Trim()).Where(each => each.StartsWith('{')).Reverse())
        {
            try
            {
                return JsonSerializer.Deserialize<HomeProofReport>(line, Options);
            }
            catch (JsonException)
            {
                // Not the report: a line of something else that begins so.
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
