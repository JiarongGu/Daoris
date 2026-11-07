using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

using Daoris.Driver;

/// <summary>
/// The person's word is <i>role</i>: *actor* survives only in code names and stored shapes. The room as the helper reads it,
/// whole under each machine, and the sentences the staffing and brainstorm refusals say never use it.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RoomSaysNoActorTests
{
    private static readonly Regex Word = new(@"\bactors?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void The_room_in_every_golden_says_no_actor()
    {
        var golden = Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "Knowledge", "golden", "help-room");
        var saying = Directory.EnumerateFiles(golden).Where(file => Word.IsMatch(File.ReadAllText(file))).Select(Path.GetFileName).ToList();
        Assert.Empty(saying);
    }

    [Fact]
    public void A_refused_key_names_a_role()
    {
        Assert.False(Word.IsMatch(ActorKey.Judge("builder", [])!));
    }
}
